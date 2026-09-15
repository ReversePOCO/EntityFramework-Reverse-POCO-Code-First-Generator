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
            if (Settings.MergeMultipleStoredProcModelsIfAllSame && ReturnModels.Count < 2)
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
            var willIncludeProcResult = includeProcResult && ReturnModels.Count > 0 && ReturnModels.First().Count > 0;
            var sb = new StringBuilder(255);
            var data = Parameters.Where(x => x.Mode != StoredProcedureParameterMode.Out).OrderBy(x => x.Ordinal).ToList();

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
        // database default is NULL, or it is INOUT and so comes back NULL whenever the database says so.
        // NullableReverseNavigationProperties only controls reverse nav props, not SP parameter types.
        private static bool IsNullableInSignature(StoredProcedureParameter p)
        {
            if (!Column.StoredProcedureNotNullable.Contains(p.PropertyType.ToLower()))
                return true;

            if (!Settings.AllowNullStrings)
                return false;

            return p.Mode == StoredProcedureParameterMode.InOut || (p.HasDefault && p.DefaultValue == null);
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
                if (parameter.Mode == StoredProcedureParameterMode.InOut ||
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

        public string WriteNetCoreTableValuedFunctionsSqlAtParams()
        {
            var sb = new StringBuilder(255);
            var count = Parameters.Count;
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

                sb.AppendLine(
                    string.Format("        var {0} = new {1}", WriteStoredProcSqlParameterName(p), Settings.SqlParameter())
                    + string.Format(" {{ ParameterName = \"{0}\", ", p.Name)
                    + (isSpatialEf6 ? string.Format("UdtTypeName = \"{0}\"", spatialUdtName) : string.Format("SqlDbType = SqlDbType.{0}", p.SqlDbType))
                    + ", Direction = ParameterDirection."
                    + (p.Mode == StoredProcedureParameterMode.In ? "Input" : p.Mode == StoredProcedureParameterMode.InOut ? "InputOutput" : "Output")
                    + (p.Mode != StoredProcedureParameterMode.Out
                        ? ", Value = " + (isSpatialEf6
                            ? string.Format("{0} == null ? (object)DBNull.Value : Microsoft.SqlServer.Types.{1}.Parse({0}.AsText())", p.NameHumanCase, spatialSqlType)
                            : p.NameHumanCase + getValueOrDefault)
                        : string.Empty)
                    + (p.MaxLength != 0 ? ", Size = " + p.MaxLength : string.Empty)
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

            if (includeProcResult && ReturnModels.Count < 2)
            {
                sb.Append("        var procResultParam = new ");
                sb.Append(Settings.SqlParameter());
                sb.AppendLine(" { ParameterName = \"@procResult\", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Output };");
            }

            return sb.ToString();
        }

        public string WriteTableValuedFunctionDeclareSqlParameter()
        {
            var sb = new StringBuilder(1024);
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
            {
                sb.AppendLine(string.Format(
                    "        var {0}Param = new ObjectParameter(\"{1}\", typeof({2})) {{ Value = (object){3} }};",
                    p.NameHumanCase,
                    p.Name.Substring(1),
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
            var parameters = Parameters.OrderBy(x => x.Ordinal).ToList();
            var hasParam = parameters.Any();

            if (!IsEfCore8Plus && includeCancellationToken)
                sb.Append(", cancellationToken");

            if (hasParam || includeProcResultParam)
                sb.Append(", ");

            if (IsEfCore8Plus && (hasParam || includeProcResultParam))
                sb.Append(" new[] {");

            var coalesceNulls = !appendParam && Settings.NeedsNullForgiving();
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
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
            if (Parameters.Count == 0)
                return "new ObjectParameter[] { }";

            var sb = new StringBuilder(255);
            foreach (var p in Parameters.OrderBy(x => x.Ordinal))
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

        private string ConvertDataColumnType(DataColumn col)
        {
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
