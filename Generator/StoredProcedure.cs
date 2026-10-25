using Efrpg.Filtering;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Efrpg
{
    public class StoredProcedure : EntityName
    {
        public Schema Schema;
        public List<StoredProcedureParameter> Parameters;
        public List<List<DataColumn>> ReturnModels; // A list of return models, containing a list of return columns
        public bool IsTableValuedFunction;
        public bool IsScalarValuedFunction;
        public bool IsStoredProcedure;
        public bool HasSpatialParameter;
        public bool HasSpatialReturnModel;
        public string Error; // Set when an exception occurs reading the return model (e.g. SP uses temp tables)

        // DataColumn.DataType must be a real System.Type, and the enum only exists in the generated code, so the
        // enum type name travels in ExtendedProperties instead. ApplyEnumDefinitions sets it; a Database.tt can also
        // set it by hand in ReadStoredProcReturnObjectCompleted.
        public const string EnumTypeExtendedProperty = "EnumType";

        // The C# types an enum may be backed by. A definition matching a column of any other type is ignored:
        // EF could never materialise a varchar into an enum, so retyping it would only move the failure to runtime.
        private static readonly HashSet<string> EnumUnderlyingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong"
        };

        public StoredProcedure()
        {
            Parameters = new List<StoredProcedureParameter>();
            ReturnModels = new List<List<DataColumn>>();
        }

        public static bool IsNullable(DataColumn col)
        {
            if (col.DataType.Namespace == null || !col.AllowDBNull)
                return false;

            var typeName = col.DataType.Name.ToLower();

            // Mirrors Column.NotNullable for table columns: when AllowNullStrings is enabled, DB-nullable string and
            // byte[] return columns become string? / byte[]?. EF Core 8+ infers result-set column nullability from the
            // C# annotation, so a plain 'string' under NRT is read without a NULL check and throws
            // SqlNullValueException when the stored procedure returns NULL (issue #885).
            if (Settings.AllowNullStrings && (typeName == "string" || typeName == "byte[]"))
                return true;

            return !(
                       Column.StoredProcedureNotNullable.Contains(typeName) ||
                       Column.StoredProcedureNotNullable.Contains(col.DataType.Namespace.ToLower() + "." + typeName)
                     );
        }

        public static string WrapTypeIfNullable(string propertyType, DataColumn col)
        {
            if (!IsNullable(col))
                return propertyType;

            // Reference types can only take the '?' annotation; Nullable<T> requires a value type.
            if (IsReferenceType(propertyType))
                return propertyType + "?";

            return string.Format(Settings.NullableShortHand ? "{0}?" : "Nullable<{0}>", propertyType);
        }

        public void MergeModelsIfAllSame()
        {
            if (!Settings.MergeMultipleStoredProcModelsIfAllSame || ReturnModels.Count < 2)
                return;

            if (ReturnModels.Select(x => x.Count).Distinct().Count() != 1)
                return; // Column count varies between models

            var modelsCount = ReturnModels.Count;
            var colCount = ReturnModels[0].Count;
            for (var i = 1; i < modelsCount; ++i)
            {
                for (var n = 0; n < colCount; n++)
                {
                    var col = ReturnModels[0][n];
                    var other = ReturnModels[i][n];

                    if (col.ColumnName != other.ColumnName || col.DataType.FullName != other.DataType.FullName)
                        return; // Columns differ
                }
            }

            ReturnModels.RemoveRange(1, modelsCount - 1);
        }

        public string WriteStoredProcFunctionName(IDbContextFilter filter)
        {
            var name = filter.StoredProcedureRename(this);
            return !string.IsNullOrEmpty(name) ? name : NameHumanCase;
        }

        public bool StoredProcHasOutParams()
        {
            return Parameters.Any(x => x.Mode != StoredProcedureParameterMode.In);
        }

        public bool StoredProcCanExecuteAsync()
        {
            return !StoredProcHasOutParams() && !IsTableValuedFunction && ReturnModels.Count <= 1;
        }

        public string WriteStoredProcFunctionParams(bool includeProcResult, bool forInterface, bool includeCancellationToken = false)
        {
            var willIncludeProcResult = includeProcResult && ReturnModels.Count > 0 && (ReturnModels.First().Count > 0 || HasReturnTypeMapping());
            var sb = new StringBuilder(255);
            var data = Parameters.Where(x => x.Mode != StoredProcedureParameterMode.Out || IsProcedureOutParameter(x)).OrderBy(x => x.Ordinal).ToList();

            // When any parameter carries DB default info, use it to determine optional params.
            // Otherwise fall back to the type-based tail heuristic for backward compatibility.
            var useDbDefaults = data.Any(p => p.HasDefault);
            int firstOptionalIndex;
            if (useDbDefaults && !forInterface && !willIncludeProcResult)
            {
                // Find the longest contiguous tail sequence of In-mode parameters that all have DB defaults.
                firstOptionalIndex = data.Count;
                for (var i = data.Count - 1; i >= 0; i--)
                {
                    if (data[i].Mode == StoredProcedureParameterMode.In && data[i].HasDefault)
                        firstOptionalIndex = i;
                    else
                        break;
                }
            }
            else
            {
                var minNullable = WhichTailEndParametersCanBeNullable(data, willIncludeProcResult, forInterface);
                firstOptionalIndex = minNullable == 0 ? data.Count : minNullable - 1;
            }

            var count = 0;
            foreach (var p in data)
            {
                var isInParam = p.Mode == StoredProcedureParameterMode.In;
                var isOptional = isInParam && count >= firstOptionalIndex;
                var isReferenceType = Column.StoredProcedureNotNullable.Contains(p.PropertyType.ToLower());
                var makeNullable = IsNullableInSignature(p);

                string defaultSuffix;
                if (!isOptional)
                    defaultSuffix = string.Empty;
                else if (useDbDefaults)
                    defaultSuffix = " = " + ToCSharpDefault(p);
                else if (!isReferenceType)
                    defaultSuffix = " = null";
                else
                    defaultSuffix = string.Empty; // reference types without DB info stay required

                sb.AppendFormat("{0}{1}{2} {3}{4}, ",
                    p.Mode == StoredProcedureParameterMode.In ? string.Empty : p.Mode == StoredProcedureParameterMode.InOut ? "ref " : "out ",
                    p.PropertyType,
                    makeNullable ? "?" : string.Empty,
                    p.NameHumanCase,
                    defaultSuffix);

                ++count;
            }

            if (willIncludeProcResult)
                sb.Append("out int procResult, ");

            if (includeCancellationToken)
                sb.Append("CancellationToken cancellationToken = default(CancellationToken), ");

            if (includeCancellationToken || willIncludeProcResult || sb.Length > 2)
                sb.Length -= 2;

            return sb.ToString();
        }

        // Whether the generated method declares this parameter with '?'. It depends on the parameter alone, so every
        // overload, the interface and the fake context agree; otherwise one passes a string? to another's string.
        // A reference type is only annotated under AllowNullStrings, and only when NULL is a legitimate value: its
        // database default is NULL, or it is INOUT or OUT and so comes back NULL whenever the database says so.
        // NullableReverseNavigationProperties only controls reverse nav props, not SP parameter types.
        private static bool IsNullableInSignature(StoredProcedureParameter p)
        {
            if (!Column.StoredProcedureNotNullable.Contains(p.PropertyType.ToLower()))
                return true;

            if (!Settings.AllowNullStrings)
                return false;

            return p.Mode != StoredProcedureParameterMode.In || (p.HasDefault && p.DefaultValue == null);
        }

        private static bool IsSqlServer()
        {
            return Settings.DatabaseType == DatabaseType.SqlServer;
        }

        // Only SQL Server procedures have a return status, which is what procResult carries. Elsewhere the generated
        // method still returns int, so one interface or fake fits every provider, and returns 0.
        public static bool ReturnsProcResult()
        {
            return IsSqlServer();
        }

        // SQL Server reports a procedure's OUTPUT parameter as INOUT, so there OUT only ever marks a function's return
        // value, which is never an argument. Every other database reports a procedure's OUT parameter as OUT, and the
        // caller must receive it.
        private bool IsProcedureOutParameter(StoredProcedureParameter p)
        {
            return IsStoredProcedure && p.Mode == StoredProcedureParameterMode.Out && !IsSqlServer();
        }

        // A PostgreSQL function's OUT parameter is a column of the rows it returns, not something the caller passes.
        private bool IsResultColumn(StoredProcedureParameter p)
        {
            return IsTableValuedFunction && p.Mode == StoredProcedureParameterMode.Out && !IsSqlServer();
        }

        private List<StoredProcedureParameter> CallArguments()
        {
            return Parameters.Where(p => !IsResultColumn(p)).OrderBy(x => x.Ordinal).ToList();
        }

        // Converts a StoredProcedureParameter's stored DefaultValue to a valid C# literal.
        // Uses a whitelist: only types whose values are valid C# compile-time constants get
        // a non-null default; everything else falls back to null.
        //
        // Valid compile-time constants in C#: numeric primitives, bool, string, enum, null.
        // NOT valid: DateTime, DateTimeOffset, Guid, TimeSpan, DateOnly, TimeOnly, byte[],
        //            object, spatial types, or SQL expressions (getdate(), newid(), etc.).
        private static string ToCSharpDefault(StoredProcedureParameter p)
        {
            if (p.DefaultValue == null)
                return "null";

            var raw = p.DefaultValue.Trim();
            var propertyType = p.PropertyType?.ToLower() ?? string.Empty;

            // SQL string literal (single-quoted): only usable when the C# type is string
            if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
            {
                if (propertyType == "string")
                {
                    var str = raw.Substring(1, raw.Length - 2).Replace("''", "'");
                    return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                }
                return "null"; // DateTime?, Guid?, etc. stored as SQL string literals → null
            }

            // Unicode string prefix N'...' — not a valid C# literal for any type
            if (raw.Length >= 3 && (raw[0] == 'N' || raw[0] == 'n') &&
                raw[1] == '\'' && raw[raw.Length - 1] == '\'')
            {
                if (propertyType == "string")
                {
                    var str = raw.Substring(2, raw.Length - 3).Replace("''", "'");
                    return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                }
                return "null";
            }

            // bool: SQL bit (0/1) or PostgreSQL true/false keywords
            if (propertyType == "bool?" || propertyType == "bool")
            {
                if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return "false";
                if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return "true";
                return "null";
            }

            // decimal / float: only emit if the raw value is actually numeric
            if (propertyType == "decimal?" || propertyType == "decimal")
                return IsSqlNumericLiteral(raw) ? raw + "m" : "null";
            if (propertyType == "float?" || propertyType == "float")
                return IsSqlNumericLiteral(raw) ? raw + "f" : "null";

            // Remaining integer/floating-point value types: only emit a raw numeric literal.
            // Anything else (getdate(), newid(), expressions, etc.) falls back to null.
            if (IsCSharpNumericValueType(propertyType))
                return IsSqlNumericLiteral(raw) ? raw : "null";

            // All other types (DateTime, DateTimeOffset, Guid, TimeSpan, DateOnly, TimeOnly,
            // string with non-string default, byte[], object, spatial types, etc.) → null.
            return "null";
        }

        // Returns true for C# integer/floating-point value types that accept a numeric literal
        // as a compile-time default parameter value.
        private static bool IsCSharpNumericValueType(string lowerPropertyType)
        {
            switch (lowerPropertyType)
            {
                case "byte":
                case "byte?":
                case "sbyte":
                case "sbyte?":
                case "short":
                case "short?":
                case "int":
                case "int?":
                case "long":
                case "long?":
                case "ushort":
                case "ushort?":
                case "uint":
                case "uint?":
                case "ulong":
                case "ulong?":
                case "double":
                case "double?":
                    return true;
                default:
                    return false;
            }
        }

        // Returns true when raw looks like a plain SQL numeric literal (optional sign, digits,
        // optional single decimal point). Rejects SQL expressions, identifiers, etc.
        private static bool IsSqlNumericLiteral(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            var i = 0;
            if (raw[i] == '-' || raw[i] == '+') i++;
            if (i >= raw.Length) return false;
            var hasDot = false;
            for (; i < raw.Length; i++)
            {
                if (raw[i] == '.') { if (hasDot) return false; hasDot = true; }
                else if (!char.IsDigit(raw[i])) return false;
            }
            return true;
        }

        // Returns the minimum nullable parameter, counting from 1. 0 Means no column is nullable.
        public int WhichTailEndParametersCanBeNullable(List<StoredProcedureParameter> data, bool includeProcResult, bool forInterface)
        {
            if (forInterface || includeProcResult || !data.Any())
                return 0;

            var dataCount = data.Count;
            var parameterNumber = dataCount;
            foreach (var parameter in data.OrderByDescending(x => x.Ordinal))
            {
                // A ref or out argument cannot follow an optional one, so the optional tail stops at the first of them.
                if (parameter.Mode != StoredProcedureParameterMode.In ||
                    Column.StoredProcedureNotNullable.Contains(parameter.PropertyType.ToLower()))
                {
                    return parameterNumber == dataCount ? 0 : parameterNumber + 1;
                }

                --parameterNumber;
            }

            return 1;
        }

        public string WriteStoredProcFunctionOverloadCall()
        {
            var sb = new StringBuilder(255);
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
            {
                sb.AppendFormat("{0}{1}, ",
                    p.Mode == StoredProcedureParameterMode.In ? string.Empty : p.Mode == StoredProcedureParameterMode.InOut ? "ref " : "out ",
                    p.NameHumanCase);
            }

            sb.Append("out procResult");
            return sb.ToString();
        }

        public string WriteStoredProcFunctionSqlAtParams()
        {
            // Outside SQL Server this only ever lands in an EF6 Entity SQL function call, whose parameters are written
            // @name and which has no OUTPUT keyword.
            if (!IsSqlServer())
                return string.Join(", ", CallArguments().Select(p => "@" + p.Name.TrimStart('@')));

            var sb = new StringBuilder(255);
            var n = 1;
            var count = Parameters.Count;
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
            {
                sb.AppendFormat("{0}{1}{2}",
                    p.Name,
                    p.Mode == StoredProcedureParameterMode.In ? string.Empty : " OUTPUT",
                    n++ < count ? ", " : string.Empty);
            }

            return sb.ToString();
        }

        // The statement that calls this procedure outside SQL Server, written into a C# string literal. Each shape was
        // run against the provider's driver through EF Core's ExecuteSqlRaw before it was used here.
        public string WriteStoredProcFunctionCallSql()
        {
            var name = WriteStoredProcFunctionQualifiedName();
            var parameters = Parameters.OrderBy(x => x.Ordinal).ToList();
            switch (Settings.DatabaseType)
            {
                case DatabaseType.Oracle:
                    return string.Format("BEGIN {0}({1}); END;", name, string.Join(", ", parameters.Select(p => ":" + p.Name)));

                case DatabaseType.PostgreSQL:
                    // Npgsql refuses an out-only parameter named in the SQL, and fills it from the row CALL returns.
                    return string.Format("CALL {0}({1})", name,
                        string.Join(", ", parameters.Select(p => p.Mode == StoredProcedureParameterMode.Out ? "NULL" : "@" + p.Name)));

                case DatabaseType.MySql:
                    return string.Format("CALL {0}({1})", name, string.Join(", ", parameters.Select(p => "@" + p.Name)));

                default:
                    return string.Format("EXEC @procResult = [{0}].[{1}] {2}", Schema.DbName, DbName, WriteStoredProcFunctionSqlAtParams());
            }
        }

        // MySqlConnector accepts no direction but Input on a text command, so a CALL cannot return an OUT or INOUT
        // parameter. Such a procedure runs as a CommandType.StoredProcedure command on EF's own connection instead.
        public bool ExecutesAsStoredProcedureCommand()
        {
            return Settings.DatabaseType == DatabaseType.MySql && Parameters.Any(p => p.Mode != StoredProcedureParameterMode.In);
        }

        // The schema-qualified name in the database's own quoting. The tool reports an Oracle package member as
        // PACKAGE.MEMBER, so there each part is quoted on its own. Double quotes are escaped because the result is
        // written inside a C# string literal.
        public string WriteStoredProcFunctionQualifiedName()
        {
            var parts = new List<string> { Schema.DbName };
            parts.AddRange(Settings.DatabaseType == DatabaseType.Oracle ? DbName.Split('.') : new[] { DbName });

            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return string.Join(".", parts.Select(x => "[" + x + "]"));

                case DatabaseType.MySql:
                    return string.Join(".", parts.Select(x => "`" + x + "`"));

                default:
                    return string.Join(".", parts.Select(x => "\\\"" + x + "\\\""));
            }
        }

        // The arguments of this scalar function's [DbFunction] attribute. EF quotes a DbFunction name as a single
        // identifier, so an Oracle package member cannot be called PACKAGE.MEMBER there. Mapped as built in, its quoted
        // three-part name goes into the SQL exactly as written.
        public string WriteDbFunctionArguments()
        {
            if (Settings.DatabaseType == DatabaseType.Oracle && DbName.Contains('.'))
                return string.Format("\"{0}\", IsBuiltIn = true", WriteStoredProcFunctionQualifiedName());

            return string.Format("\"{0}\", \"{1}\"", DbName, Schema.DbName);
        }

        public string WriteNetCoreTableValuedFunctionsSqlAtParams()
        {
            var sb = new StringBuilder(255);
            var count = CallArguments().Count;
            for (var n = 0; n < count; n++)
            {
                sb.AppendFormat("{{{0}}}{1}",
                    n,
                    (n + 1) < count ? ", " : string.Empty);
            }

            return sb.ToString();
        }

        public string WriteStoredProcSqlParameterName(StoredProcedureParameter p)
        {
            return p.NameHumanCase + "Param";
        }

        public string WriteStoredProcFunctionDeclareSqlParameter(bool includeProcResult)
        {
            var sb = new StringBuilder(1024);
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
            {
                var lowerPropertyType = p.PropertyType.ToLower();
                var isNullable = !Column.StoredProcedureNotNullable.Contains(lowerPropertyType);
                var getValueOrDefault = isNullable ? ".GetValueOrDefault()" : string.Empty;
                var isDbGeography = lowerPropertyType == "dbgeography";
                var isDbGeometry = lowerPropertyType == "dbgeometry";
                var isSpatialEf6 = isDbGeography || isDbGeometry;
                var spatialSqlType = isDbGeography ? "SqlGeography" : "SqlGeometry";
                var spatialUdtName = isDbGeography ? "geography" : "geometry";

                var typeAssignment = isSpatialEf6 ? string.Format("UdtTypeName = \"{0}\"", spatialUdtName) : WriteParameterTypeAssignment(p);

                sb.AppendLine(
                    string.Format("        var {0} = new {1}", WriteStoredProcSqlParameterName(p), Settings.SqlParameter())
                    + string.Format(" {{ ParameterName = \"{0}\", ", p.Name)
                    + (typeAssignment.Length > 0 ? typeAssignment + ", " : string.Empty)
                    + "Direction = ParameterDirection."
                    + (p.Mode == StoredProcedureParameterMode.In ? "Input" : p.Mode == StoredProcedureParameterMode.InOut ? "InputOutput" : "Output")
                    + (p.Mode != StoredProcedureParameterMode.Out
                        ? ", Value = " + (isSpatialEf6
                            ? string.Format("{0} == null ? (object)DBNull.Value : Microsoft.SqlServer.Types.{1}.Parse({0}.AsText())", p.NameHumanCase, spatialSqlType)
                            : p.NameHumanCase + getValueOrDefault)
                        : string.Empty)
                    + (p.MaxLength != 0 ? ", Size = " + p.MaxLength : string.Empty)
                    + (NeedsPlSqlBufferSize(p) ? ", Size = " + PlSqlMaxBufferSize : string.Empty)
                    + ((p.Precision > 0 || p.Scale > 0) ? ", Precision = " + p.Precision + ", Scale = " + p.Scale : string.Empty)
                    + (p.PropertyType.ToLower().Contains("datatable") ? ", TypeName = \"" + p.UserDefinedTypeName + "\"" : string.Empty)
                    + " };");

                if (p.Mode != StoredProcedureParameterMode.Out)
                {
                    sb.AppendFormat(
                        isNullable
                            ? "        if (!{0}.HasValue){1}            {0}Param.Value = DBNull.Value;{1}{1}"
                            : "        if ({0}Param.Value == null){1}            {0}Param.Value = DBNull.Value;{1}{1}",
                        p.NameHumanCase, Environment.NewLine);
                }
            }

            if (includeProcResult && ReturnModels.Count < 2 && ReturnsProcResult())
            {
                sb.Append("        var procResultParam = new ");
                sb.Append(Settings.SqlParameter());
                sb.AppendLine(" { ParameterName = \"@procResult\", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Output };");
            }

            // With no procResult line to close them, the declarations end on a blank line of their own, so the call is
            // set apart the same way whatever kind of parameter came last.
            var blankLine = Environment.NewLine + Environment.NewLine;
            if (!ReturnsProcResult() && sb.Length > 0 && !sb.ToString().EndsWith(blankLine))
                sb.AppendLine();

            return sb.ToString();
        }

        // DbType values that every ADO.NET provider here maps the same way. Oracle needs one on an OUT parameter: with
        // OracleDbType set instead, ODP.NET hands back OracleDecimal, which the generated (decimal) cast cannot unbox.
        private static readonly Dictionary<string, string> ProviderNeutralDbTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "bool",           "Boolean" },
            { "byte",           "Byte" },
            { "sbyte",          "SByte" },
            { "short",          "Int16" },
            { "ushort",         "UInt16" },
            { "int",            "Int32" },
            { "uint",           "UInt32" },
            { "long",           "Int64" },
            { "ulong",          "UInt64" },
            { "decimal",        "Decimal" },
            { "double",         "Double" },
            { "float",          "Single" },
            { "string",         "String" },
            { "DateTime",       "DateTime" },
            { "DateTimeOffset", "DateTimeOffset" },
            { "Guid",           "Guid" },
            { "byte[]",         "Binary" }
        };

        // PL/SQL's largest VARCHAR2 and RAW. ALL_ARGUMENTS records no length for a procedure's parameter, and ODP.NET
        // raises ORA-06502 when an OUT string has no buffer to land in.
        private const int PlSqlMaxBufferSize = 32767;

        private static bool NeedsPlSqlBufferSize(StoredProcedureParameter p)
        {
            var propertyType = (p.PropertyType ?? string.Empty).ToLowerInvariant();
            return Settings.DatabaseType == DatabaseType.Oracle && p.MaxLength == 0 && p.Mode != StoredProcedureParameterMode.In &&
                   (propertyType == "string" || propertyType == "byte[]");
        }

        private static string WriteParameterTypeAssignment(StoredProcedureParameter p)
        {
            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return string.Format("SqlDbType = SqlDbType.{0}", p.SqlDbType);

                // Npgsql infers the type from the value, and fills OUT and INOUT parameters from the row CALL returns.
                // DbType.DateTime would also force timestamp and reject a UTC value bound for timestamptz.
                case DatabaseType.PostgreSQL:
                    return string.Empty;

                case DatabaseType.Oracle when string.Equals(p.DataType, "ref cursor", StringComparison.OrdinalIgnoreCase):
                    return "OracleDbType = OracleDbType.RefCursor";
            }

            return ProviderNeutralDbTypes.TryGetValue((p.PropertyType ?? string.Empty).TrimEnd('?'), out var dbType)
                ? "DbType = DbType." + dbType
                : string.Empty;
        }

        public string WriteTableValuedFunctionDeclareSqlParameter()
        {
            var sb = new StringBuilder(1024);
            foreach (var p in CallArguments())
            {
                sb.AppendLine(string.Format(
                    "        var {0}Param = new ObjectParameter(\"{1}\", typeof({2})) {{ Value = (object){3} }};",
                    p.NameHumanCase,
                    p.Name.TrimStart('@'),
                    p.PropertyType,
                    p.NameHumanCase +
                    (p.Mode == StoredProcedureParameterMode.In &&
                     Column.StoredProcedureNotNullable.Contains(p.PropertyType.ToLowerInvariant())
                        ? string.Empty
                        : " ?? DBNull.Value")));
            }

            return sb.ToString();
        }

        public string WriteStoredProcFunctionSqlParameterAnonymousArray(bool includeProcResultParam, bool appendParam, bool includeCancellationToken = false, bool IsEfCore8Plus = false)
        {
            var sb = new StringBuilder(255);
            var parameters = CallArguments();
            var hasParam = parameters.Any();
            includeProcResultParam = includeProcResultParam && ReturnsProcResult();

            if (!IsEfCore8Plus && includeCancellationToken)
                sb.Append(", cancellationToken");

            if (hasParam || includeProcResultParam)
                sb.Append(", ");

            if (IsEfCore8Plus && (hasParam || includeProcResultParam))
                sb.Append(" new[] {");

            var coalesceNulls = !appendParam && Settings.NeedsNullForgiving();
            foreach (var p in parameters)
            {
                var paramName = string.Format("{0}{1}", p.NameHumanCase, appendParam ? "Param" : string.Empty);
                // Without the Param suffix these are a TVF's raw arguments to FromSqlRaw's params object[], whose
                // elements are non-nullable under NRT. EF Core maps DBNull.Value to the same NULL parameter as null.
                if (coalesceNulls && IsNullableInSignature(p))
                    paramName = string.Format("(object?){0} ?? DBNull.Value", paramName);
                sb.Append(string.Format("{0}, ", paramName));
                hasParam = true;
            }

            if (includeProcResultParam)
                sb.Append("procResultParam");
            else if (hasParam)
                sb.Remove(sb.Length - 2, 2);

            if (IsEfCore8Plus && (hasParam || includeProcResultParam))
                sb.Append("}");
            if (IsEfCore8Plus && includeCancellationToken)
                sb.Append(", cancellationToken");

            return sb.ToString();
        }

        public string WriteTableValuedFunctionSqlParameterAnonymousArray()
        {
            var parameters = CallArguments();
            if (parameters.Count == 0)
                return "new ObjectParameter[] { }";

            var sb = new StringBuilder(255);
            foreach (var p in parameters)
            {
                sb.Append(string.Format("{0}Param, ", p.NameHumanCase));
            }

            return sb.ToString().Substring(0, sb.Length - 2);
        }

        public string WriteStoredProcFunctionSetSqlParameters(bool isFake)
        {
            var sb = new StringBuilder(255);
            foreach (var p in Parameters
                .Where(x => x.Mode != StoredProcedureParameterMode.In)
                .OrderBy(x => x.Ordinal))
            {
                var Default = string.Format("default({0})", p.PropertyType);
                var notNullable = Column.StoredProcedureNotNullable.Contains(p.PropertyType.ToLower());

                if (isFake)
                {
                    sb.AppendLine(string.Format("        {0} = {1};", p.NameHumanCase, Default));
                }
                else
                {
                    sb.AppendLine(string.Format("        if (IsSqlParameterNull({0}Param))", p.NameHumanCase));
                    sb.AppendLine(string.Format("            {0} = {1};", p.NameHumanCase, notNullable ? Default : "null"));
                    sb.AppendLine("        else");
                    sb.AppendLine(string.Format("            {0} = ({1}) {2}Param.Value;", p.NameHumanCase, p.PropertyType, p.NameHumanCase));
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        public bool HasReturnTypeMapping()
        {
            return Settings.StoredProcedureReturnTypes.ContainsKey(NameHumanCase) || Settings.StoredProcedureReturnTypes.ContainsKey(DbName);
        }

        /// <summary>
        ///     Gives a mapped procedure whose result set could not be read a result set, so it returns the mapped type
        ///     rather than int (#709). Discovery comes back empty for temp tables, dynamic SQL, and always on MySQL,
        ///     Oracle and SQLite, which is exactly when a mapping is needed. The set has no columns: the type declares them.
        /// </summary>
        public void UseReturnTypeMappingWhenNoResultSetWasRead()
        {
            if (IsStoredProcedure && ReturnModels.Count == 0 && HasReturnTypeMapping())
                ReturnModels.Add(new List<DataColumn>());
        }

        public string WriteStoredProcReturnModelName(IDbContextFilter filter)
        {
            if (Settings.StoredProcedureReturnTypes.ContainsKey(NameHumanCase))
                return Settings.StoredProcedureReturnTypes[NameHumanCase];
            if (Settings.StoredProcedureReturnTypes.ContainsKey(DbName))
                return Settings.StoredProcedureReturnTypes[DbName];

            var name = string.Format("{0}ReturnModel", NameHumanCase);
            var customName = filter.StoredProcedureReturnModelRename(name, this);
            if (!string.IsNullOrEmpty(customName))
                name = customName;

            return name;
        }

        // Returns true if the string is already a valid C# identifier (no sanitization needed).
        private static bool IsValidCSharpIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsDigit(name[0]))
                return false;
            foreach (var c in name)
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            return true;
        }

        // Returns a valid C# identifier for a DB return-column name.
        // Preserves the original name when it is already a valid identifier (restoring pre-fix
        // behaviour for normal columns). Only applies full sanitization when the name contains
        // characters that would produce an invalid identifier (e.g. spaces, hyphens).
        public static string SanitizeReturnColumnName(string rawColumnName)
        {
            // Apply JSON_/XML_ GUID replacement first (same as original code)
            var columnName = Regex.Replace(rawColumnName, "^(?<prefix>JSON|XML)_([0-9A-F]{8}-([0-9A-F]{4}-){3}[0-9A-F]{12})", "${prefix}_Value", RegexOptions.IgnoreCase);

            // If it is already a valid C# identifier, only handle reserved keywords
            if (IsValidCSharpIdentifier(columnName))
            {
                if (NamingHelper.ReservedKeywords.Contains(columnName))
                    columnName = "@" + columnName;
                return columnName;
            }

            // Column name is not a valid C# identifier (e.g. contains spaces or special chars).
            // Apply the same sanitization pipeline used for table/view columns.
            var cleaned = NamingHelper.CleanUp(columnName);
            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = "Unknown";
            columnName = (Settings.UsePascalCase ? Inflector.ToTitleCase(cleaned) : cleaned).Replace(" ", string.Empty);
            if (string.IsNullOrWhiteSpace(columnName))
                columnName = "Unknown";
            if (NamingHelper.ReservedKeywords.Contains(columnName))
                columnName = "@" + columnName;
            return columnName;
        }

        // Returns fluent HasColumnName statements for any return model columns whose C# property
        // name differs from the DB column name. Used when UseDataAnnotations=false (EF Core only).
        public List<string> GetReturnColumnMappings(string builderCmd, string returnModelName)
        {
            var mappings = new List<string>();
            if (!ReturnModels.Any()) return mappings;
            foreach (var col in ReturnModels.First())
            {
                var rawName = col.ColumnName;
                var sanitized = SanitizeReturnColumnName(rawName);
                var effectiveName = sanitized.TrimStart('@');
                if (effectiveName != rawName)
                    mappings.Add(string.Format("modelBuilder.{0}<{1}>().Property(e => e.{2}).HasColumnName(\"{3}\");", builderCmd, returnModelName, sanitized, rawName));

                // When the generated file is NRT-enabled but this DB-nullable reference-type property is still emitted
                // without a '?' (AllowNullStrings=false with NullableReverseNavigationProperties=true, or a type such
                // as object/spatial that never takes the annotation), EF Core 8+ would infer the column as required
                // and throw SqlNullValueException on NULLs. Make the DB nullability explicit, exactly as table
                // columns do via IsRequired(false). (issue #885)
                if (!Settings.IsEf6() && Settings.NeedsNullForgiving() &&
                    col.AllowDBNull && !IsNullable(col) && IsReferenceType(ConvertDataColumnType(col)))
                {
                    mappings.Add(string.Format("modelBuilder.{0}<{1}>().Property(e => e.{2}).IsRequired(false);", builderCmd, returnModelName, sanitized));
                }

                // HasPrecision for fluent API mode (EF Core only; [Precision] handles data annotations mode)
                if (!Settings.IsEf6() && !Settings.UseDataAnnotations &&
                    col.DataType == typeof(decimal) &&
                    col.ExtendedProperties.Contains("Precision") && col.ExtendedProperties.Contains("Scale"))
                {
                    var p = (byte)col.ExtendedProperties["Precision"];
                    var s = (byte)col.ExtendedProperties["Scale"];
                    if (p > 0 || s > 0)
                        mappings.Add(string.Format("modelBuilder.{0}<{1}>().Property(e => e.{2}).HasPrecision({3}, {4});", builderCmd, returnModelName, sanitized, p, s));
                }
            }
            return mappings;
        }

        public string WriteStoredProcReturnColumn(DataColumn col)
        {
            var rawColumnName = col.ColumnName;
            var columnName = SanitizeReturnColumnName(rawColumnName);

            var propertyType = WrapTypeIfNullable(ConvertDataColumnType(col), col);

            // Add null-forgiving operator for non-nullable reference types when NRT is enabled
            var nullForgivingOperator = string.Empty;
            if (Settings.NeedsNullForgiving() && !IsNullable(col))
            {
                var baseType = ConvertDataColumnType(col);
                if (IsReferenceType(baseType))
                    nullForgivingOperator = " = null!;";
            }

            var attributeLines = new System.Text.StringBuilder();

            // When UseDataAnnotations=true and the C# name differs from the DB column name,
            // add [Column("original")] so EF Core can map the result set column to this property.
            // When UseDataAnnotations=false, HasColumnName is emitted in OnModelCreating instead.
            var effectiveName = columnName.TrimStart('@');
            if (Settings.UseDataAnnotations && effectiveName != rawColumnName)
                attributeLines.AppendFormat("[Column(\"{0}\")]{1}    ", rawColumnName, Environment.NewLine);

            // [Precision(p, s)] for decimal/numeric columns on EF Core with data annotations.
            // Precision/scale are set by EnrichFirstResultSetWithPrecision when SQL Server 2012+ is detected.
            if (!Settings.IsEf6() && Settings.UseDataAnnotations &&
                col.DataType == typeof(decimal) &&
                col.ExtendedProperties.Contains("Precision") && col.ExtendedProperties.Contains("Scale"))
            {
                var p = (byte)col.ExtendedProperties["Precision"];
                var s = (byte)col.ExtendedProperties["Scale"];
                if (p > 0 || s > 0)
                    attributeLines.AppendFormat("[Precision({0}, {1})]{2}    ", p, s, Environment.NewLine);
            }

            return string.Format("{0}public {1} {2} {{ get; set; }}{3}",
                attributeLines,
                propertyType,
                columnName,
                nullForgivingOperator);
        }

        private static bool IsReferenceType(string propertyType)
        {
            var lowerType = propertyType.ToLower();

            // List of known reference types
            return lowerType == "string" ||
                   lowerType == "byte[]" ||
                   lowerType == "datatable" ||
                   lowerType == "system.data.datatable" ||
                   lowerType == "object" ||
                   lowerType == "microsoft.sqlserver.types.sqlgeography" ||
                   lowerType == "microsoft.sqlserver.types.sqlgeometry" ||
                   lowerType == "sqlgeography" ||
                   lowerType == "sqlgeometry" ||
                   lowerType == "system.data.entity.spatial.dbgeography" ||
                   lowerType == "system.data.entity.spatial.dbgeometry" ||
                   lowerType == "dbgeography" ||
                   lowerType == "dbgeometry" ||
                   lowerType == "system.data.entity.hierarchy.hierarchyid" ||
                   lowerType == "hierarchyid" ||
                   lowerType == "hierarchyid?" ||
                   lowerType == "nettopologysuite.geometries.point" ||
                   lowerType == "nettopologysuite.geometries.geometry";
        }

        /// <summary>
        ///     Retypes return columns as enums using the same Settings.AddEnumDefinitions list that types table columns
        ///     (issue #888). A return model has no table to match against, so a definition applies when its Table is "*"
        ///     or names this routine, its Schema matches, and its Column matches the result column by database name or
        ///     sanitised C# name. Table-scoped definitions never reach here, so a status column shared by a table and a
        ///     procedure is typed by one wildcard entry rather than two.
        /// </summary>
        public void ApplyEnumDefinitions(List<EnumDefinition> enumDefinitions)
        {
            if (enumDefinitions == null || !enumDefinitions.Any())
                return;

            foreach (var col in ReturnModels.SelectMany(model => model))
            {
                if (col.ExtendedProperties.Contains(EnumTypeExtendedProperty))
                    continue; // Set by hand in ReadStoredProcReturnObjectCompleted; that wins

                if (!EnumUnderlyingTypes.Contains(ConvertDataColumnType(col)))
                    continue;

                var sanitisedName = SanitizeReturnColumnName(col.ColumnName).TrimStart('@');
                var enumDefinition = enumDefinitions.FirstOrDefault(e =>
                    e.Schema.Equals(Schema.DbName, StringComparison.InvariantCultureIgnoreCase) &&
                    (e.Table == "*" || e.Table.Equals(DbName, StringComparison.InvariantCultureIgnoreCase) || e.Table.Equals(NameHumanCase, StringComparison.InvariantCultureIgnoreCase)) &&
                    (e.Column.Equals(col.ColumnName, StringComparison.InvariantCultureIgnoreCase) || e.Column.Equals(sanitisedName, StringComparison.InvariantCultureIgnoreCase)));

                if (enumDefinition != null)
                    col.ExtendedProperties[EnumTypeExtendedProperty] = enumDefinition.EnumType;
            }
        }

        private string ConvertDataColumnType(DataColumn col)
        {
            if (col.ExtendedProperties.Contains(EnumTypeExtendedProperty))
                return (string) col.ExtendedProperties[EnumTypeExtendedProperty];

            if (col.DataType == typeof(object) && col.ExtendedProperties.Contains("DataTypeFullName"))
            {
                var fullName = col.ExtendedProperties["DataTypeFullName"] as string;
                var typeName = col.ExtendedProperties["DataTypeName"] as string;
                var typeNamespace = col.ExtendedProperties["DataTypeNamespace"] as string;
                var providerType = ConvertProviderDataColumnType(fullName, typeName, typeNamespace);
                if (!string.IsNullOrEmpty(providerType))
                    return providerType;
            }

            return ConvertDataColumnType(col.DataType);
        }

        private string ConvertProviderDataColumnType(string fullName, string typeName, string typeNamespace)
        {
            var isEfCore8Plus = Settings.IsEfCore8Plus();
            var lowerFullName = (fullName ?? string.Empty).ToLower();
            var lowerTypeName = (typeName ?? string.Empty).ToLower();

            if (lowerTypeName == "sqlhierarchyid" || lowerFullName.EndsWith(".sqlhierarchyid"))
                return isEfCore8Plus ? "HierarchyId" : "Microsoft.SqlServer.Types.SqlHierarchyId";

            if (isEfCore8Plus)
            {
                if (lowerTypeName == "sqlgeography" || lowerFullName.EndsWith(".sqlgeography"))
                    return "NetTopologySuite.Geometries.Point";
                if (lowerTypeName == "sqlgeometry" || lowerFullName.EndsWith(".sqlgeometry"))
                    return "NetTopologySuite.Geometries.Geometry";
            }

            if (!string.IsNullOrEmpty(fullName))
                return fullName;

            if (!string.IsNullOrEmpty(typeNamespace) && !string.IsNullOrEmpty(typeName))
                return typeNamespace + "." + typeName;

            return typeName;
        }

        private string ConvertDataColumnType(Type type)
        {
            var isEfCore8Plus = Settings.IsEfCore8Plus();

            if (type.Name.Equals("SqlHierarchyId"))
                return isEfCore8Plus ? "HierarchyId" : "Microsoft.SqlServer.Types.SqlHierarchyId";

            var typeNamespace = type.Namespace + ".";
            if (type.Namespace?.ToLower() == "system")
                typeNamespace = string.Empty;

            var typeName = type.Name;
            var isArray = typeName.EndsWith("[]");
            if (isArray)
                typeName = typeName.Replace("[]", string.Empty);

            switch (typeName.ToLower())
            {
                case "int16":
                    typeName = "short";
                    break;
                case "int32":
                    typeName = "int";
                    break;
                case "int64":
                    typeName = "long";
                    break;
                case "uint16":
                    typeName = "ushort";
                    break;
                case "uint32":
                    typeName = "uint";
                    break;
                case "uint64":
                    typeName = "ulong";
                    break;
                case "string":
                    typeName = "string";
                    break;
                case "decimal":
                    typeName = "decimal";
                    break;
                case "double":
                    typeName = "double";
                    break;
                case "float":
                    typeName = "float";
                    break;
                case "byte":
                    typeName = "byte";
                    break;
                case "boolean":
                    typeName = "bool";
                    break;
            }

            if (isEfCore8Plus)
            {
                switch (typeName.ToLower())
                {
                    case "microsoft.sqlserver.types.sqlgeography":
                    case "sqlgeography":
                        typeNamespace = "NetTopologySuite.Geometries.";
                        typeName = "Point";
                        break;

                    case "microsoft.sqlserver.types.sqlgeometry":
                    case "sqlgeometry":
                        typeNamespace = "NetTopologySuite.Geometries.";
                        typeName = "Geometry";
                        break;
                }
            }

            if (isArray)
                typeName += "[]";

            return typeNamespace + typeName;
        }

        public string WriteStoredProcReturnType(IDbContextFilter filter)
        {
            var returnModelCount = ReturnModels.Count;
            if (returnModelCount == 0)
                return "int";

            var spReturnClassName = WriteStoredProcReturnModelName(filter);
            return returnModelCount == 1
                ? string.Format("List<{0}>", spReturnClassName)
                : spReturnClassName;
        }
    }
}
