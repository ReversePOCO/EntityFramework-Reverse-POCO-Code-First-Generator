using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.LanguageMapping;
using Efrpg.Licensing;
using Efrpg.Readers;
using Efrpg.Templates;

// ReSharper disable UseStringInterpolation

namespace Efrpg.Generators
{
    public abstract class Generator
    {
        public bool InitialisationOk { get; private set; }

        protected EfrpgResult _result;
        public readonly IDbContextFilterList FilterList;

        private Dictionary<string, string> _dbTypeToPropertyType;
        private List<string> _spatialTypes;
        private List<string> _precisionTypes;
        private List<string> _precisionAndScaleTypes;

        private static readonly Regex ReservedColumnNames = new Regex("^(event|Equals|GetHashCode|GetType|ToString)$", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> SqlServerStoredProcedureParameterDbType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { string.Empty, "VarChar" },
            { "hierarchyid", "VarChar" },
            { "bigint", "BigInt" },
            { "binary", "Binary" },
            { "bit", "Bit" },
            { "char", "Char" },
            { "datetime", "DateTime" },
            { "decimal", "Decimal" },
            { "numeric", "Decimal" },
            { "float", "Float" },
            { "image", "Image" },
            { "int", "Int" },
            { "money", "Money" },
            { "nchar", "NChar" },
            { "ntext", "NText" },
            { "nvarchar", "NVarChar" },
            { "real", "Real" },
            { "uniqueidentifier", "UniqueIdentifier" },
            { "smalldatetime", "SmallDateTime" },
            { "smallint", "SmallInt" },
            { "smallmoney", "SmallMoney" },
            { "text", "Text" },
            { "timestamp", "Timestamp" },
            { "tinyint", "TinyInt" },
            { "varbinary", "VarBinary" },
            { "varchar", "VarChar" },
            { "variant", "Variant" },
            { "xml", "Xml" },
            { "udt", "Udt" },
            { "table type", "Structured" },
            { "structured", "Structured" },
            { "date", "Date" },
            { "time", "Time" },
            { "datetime2", "DateTime2" },
            { "datetimeoffset", "DateTimeOffset" },
            { "json", "NVarChar" },
            { "vector", "VarBinary" }
        };

        protected abstract bool AllowFkToNonPrimaryKey();
        protected abstract bool FkMustHaveSameNumberOfColumnsAsPrimaryKey();
        protected abstract void SetupEntity(Column c);
        protected abstract void SetupConfig(Column c);
        public abstract string PrimaryKeyModelBuilder(Table table);
        public abstract List<string> IndexModelBuilder(Table t);
        public abstract string IndexModelBuilder(Column c);
        protected abstract string GetHasMethod(Relationship relationship, IList<Column> fkCols, IList<Column> pkCols, bool isNotEnforced, bool fkHasUniqueConstraint);
        protected abstract string GetWithMethod(Relationship relationship, IList<Column> fkCols, string fkPropName, string manyToManyMapping, string mapKey,
            bool includeReverseNavigation, string hasMethod, string pkTableNameHumanCase, string fkTableNameHumanCase, string primaryKeyColumns, bool fkHasUniqueConstraint);
        protected abstract string GetCascadeOnDelete(bool cascadeOnDelete);
        protected abstract string GetForeignKeyConstraintName(string foreignKeyConstraintName);

        private LicenceType _licenceType;
        private bool _hasTrialLicence;
        private FileHeaderFooter _fileHeaderFooter;
        private readonly StringBuilder _preHeaderInfo;
        private readonly string _codeGeneratedAttribute;
        private readonly FileManagementService _fileManagementService;
        private const int Indent = 4;

        protected Generator(FileManagementService fileManagementService)
        {
            _fileManagementService = fileManagementService;
            InitialisationOk = false;
            _result = null;
            _fileHeaderFooter = null;
            _preHeaderInfo = new StringBuilder(1024);
            _codeGeneratedAttribute = string.Format("[GeneratedCode(\"EF.Reverse.POCO.Generator\", \"{0}\")]", EfrpgVersion.Version());
            FilterList = new DbContextFilterList();
        }

        public void Init(EfrpgResult result, string singleDbContextSubNamespace)
        {
            if (Settings.ConnectionString.Contains("**TODO**"))
            {
                _fileManagementService.Error("// Please update your .tt file's `Settings.ConnectionString` string to the database you want to reverse engineer and save your .tt file.");
                return;
            }

            var licence = ReadAndValidateLicence();
            if (licence == null)
                return;

            BuildPreHeaderInfo(licence);

            _result = result;
            if (_result == null)
            {
                _fileManagementService.Error("// The efrpg tool returned no schema, so nothing can be generated.");
                return;
            }

            var typeMapper = DatabaseToPropertyTypeFactory.Create();
            _dbTypeToPropertyType = typeMapper.GetMapping();
            _spatialTypes = typeMapper.SpatialTypes();
            _precisionTypes = typeMapper.PrecisionTypes();
            _precisionAndScaleTypes = typeMapper.PrecisionAndScaleTypes();

            if (!string.IsNullOrEmpty(_result.DefaultSchema))
                Settings.DefaultSchema = _result.DefaultSchema;

            if (Settings.IncludeConnectionSettingComments)
                _preHeaderInfo.Append(DatabaseDetails());

            if (Settings.UseDataAnnotations)
            {
                Settings.AdditionalNamespaces.Add("System.ComponentModel.DataAnnotations");
                Settings.AdditionalNamespaces.Add("System.ComponentModel.DataAnnotations.Schema");
            }

            _licenceType = licence.LicenceType;
            _hasTrialLicence = licence.LicenceType == LicenceType.Trial;
            InitialisationOk = FilterList.ReadDbContextSettings(_result, singleDbContextSubNamespace);
            _fileManagementService.Init(FilterList.GetFilters());
        }

        public string GetPreHeaderInfo()
        {
            return _preHeaderInfo.ToString();
        }

        private Licence ReadAndValidateLicence()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var file = Path.Combine(path, "ReversePOCO.txt");
            const string obtainAt = "// Please obtain your licence file at www.ReversePOCO.co.uk, and place it in your documents folder shown above.";

            if (!File.Exists(file))
            {
                _fileManagementService.Error(string.Format("// Licence file {0} not found.", file));
                _fileManagementService.Error(obtainAt);
                return TrialLicenceFallback();
            }

            var validator = new LicenceValidator();
            if (!validator.Validate(File.ReadAllText(file)))
            {
                _fileManagementService.Error(validator.Expired
                    ? string.Format("// Your licence file {0} has expired.", file)
                    : string.Format("// Your licence file {0} is not valid.", file));

                _fileManagementService.Error(obtainAt);
                return TrialLicenceFallback();
            }

            return validator.Licence; // Thank you for having a valid licence and supporting this product :-)
        }

        private Licence TrialLicenceFallback()
        {
            _fileManagementService.Error("// Defaulting to Trial version.");
            return new Licence(string.Empty, string.Empty, LicenceType.Trial, "1", DateTime.MaxValue);
        }

        public string DatabaseDetails()
        {
            return _result?.DatabaseDetails ?? string.Empty;
        }

        public string GetPropertyType(string dbType)
        {
            if (_dbTypeToPropertyType != null && _dbTypeToPropertyType.TryGetValue(dbType, out var propertyType))
                return propertyType;
            return _dbTypeToPropertyType != null && _dbTypeToPropertyType.ContainsKey(string.Empty)
                ? _dbTypeToPropertyType[string.Empty]
                : string.Empty;
        }

        public bool IsSpatialType(string type) => type != null && _spatialTypes != null && _spatialTypes.Contains(type);
        public bool IsPrecisionType(string type) => type != null && _precisionTypes != null && _precisionTypes.Contains(type);
        public bool IsPrecisionAndScaleType(string type) => type != null && _precisionAndScaleTypes != null && _precisionAndScaleTypes.Contains(type);

        public Column CreateColumn(RawTable rt, Table table, IDbContextFilter filter)
        {
            if (!string.IsNullOrEmpty(rt.SynonymTriggerName) && string.IsNullOrEmpty(table.TriggerName))
                table.TriggerName = rt.SynonymTriggerName;

            var col = new Column
            {
                Scale = rt.Scale,
                PropertyType = GetPropertyType(rt.TypeName),
                SqlPropertyType = rt.TypeName,
                IsNullable = rt.IsNullable,
                MaxLength = rt.MaxLength,
                DateTimePrecision = rt.DateTimePrecision,
                Precision = rt.Precision,
                IsIdentity = rt.IsIdentity,
                IsComputed = rt.IsComputed,
                IsRowGuid = rt.IsRowGuid,
                GeneratedAlwaysType = (ColumnGeneratedAlwaysType) rt.GeneratedAlwaysType,
                IsStoreGenerated = rt.IsStoreGenerated,
                PrimaryKeyOrdinal = rt.PrimaryKeyOrdinal,
                IsPrimaryKey = rt.PrimaryKey,
                IsForeignKey = rt.IsForeignKey,
                IsSpatial = rt.TypeName == "geography" || rt.TypeName == "geometry",
                Ordinal = rt.Ordinal,
                DbName = rt.ColumnName,
                Default = rt.Default,
                ParentTable = table,
                ExistsInBaseClass = false
            };

            if (col.IsPrimaryKey)
                col.IsNullable = false;

            if (col.MaxLength == -1 && (col.SqlPropertyType.EndsWith("varchar", StringComparison.InvariantCultureIgnoreCase) ||
                                        col.SqlPropertyType.EndsWith("varbinary", StringComparison.InvariantCultureIgnoreCase)))
            {
                col.SqlPropertyType += "(max)";
            }

            // SQL Server 2025 vector type - dimension is stored as (byte_size - 8) / 4
            if (col.SqlPropertyType.Equals("vector", StringComparison.InvariantCultureIgnoreCase) && col.MaxLength > 0)
            {
                var vectorDimension = (col.MaxLength - 8) / 4;
                col.SqlPropertyType = string.Format("vector({0})", vectorDimension);
            }

            if (col.IsPrimaryKey && !col.IsIdentity && col.IsStoreGenerated && rt.TypeName == "uniqueidentifier")
            {
                col.IsStoreGenerated = false;
                col.IsIdentity = true;
            }

            if (!col.IsPrimaryKey && filter.IsExcluded(col))
                col.Hidden = true;

            col.IsFixedLength = (rt.TypeName == "char" || rt.TypeName == "nchar");
            col.IsUnicode = !(rt.TypeName == "char" || rt.TypeName == "varchar" || rt.TypeName == "text");
            col.IsMaxLength = (rt.TypeName == "ntext");

            col.IsRowVersion = col.IsStoreGenerated && rt.TypeName == "timestamp";
            if (col.IsRowVersion)
                col.MaxLength = 8;

            if (rt.TypeName == "hierarchyid")
                col.MaxLength = 0;

            col.CleanUpDefault();
            col.NameHumanCase = NamingHelper.CleanUp(col.DbName);
            if (string.IsNullOrWhiteSpace(col.NameHumanCase))
            {
                col.NameHumanCase = "Unknown";
                col.Hidden = true;
            }
            col.NameHumanCase = ReservedColumnNames.Replace(col.NameHumanCase, "_$1");

            if (NamingHelper.ReservedKeywords.Contains(col.NameHumanCase))
                col.NameHumanCase = "@" + col.NameHumanCase;

            col.DisplayName = Column.ToDisplayName(col.DbName);

            var titleCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(col.NameHumanCase) : col.NameHumanCase).Replace(" ", string.Empty);
            if (titleCase != string.Empty)
                col.NameHumanCase = titleCase;

            if (col.NameHumanCase == table.NameHumanCase)
                col.NameHumanCase += "_";

            if (char.IsDigit(col.NameHumanCase[0]))
                col.NameHumanCase = "_" + col.NameHumanCase;

            table.HasNullableColumns = col.IsColumnNullable();

            if (string.IsNullOrEmpty(col.PropertyType))
                return null;

            return col;
        }

        public void LoadEnums()
        {
            if (_result == null || !Settings.ElementsToGenerate.HasFlag(Elements.Enum))
                return;

            try
            {
                foreach (var filterKeyValuePair in FilterList.GetFilters())
                {
                    var filter = filterKeyValuePair.Value;
                    foreach (var table in filter.Tables)
                    {
                        filter.AddEnum(table);
                    }
                }

                // Read enumeration values from the database. The AddEnum callbacks above may have
                // added entries to the enumeration lists, so this must happen after them. The efrpg
                // tool is re-invoked here with the now-resolved enumeration specs (see ReadEnumsViaTool).
                if (Settings.Enumerations != null && Settings.Enumerations.Count > 0)
                {
                    var enumerations = ReadEnumsViaTool(Settings.Enumerations);
                    if (enumerations.Count > 0)
                    {
                        foreach (var filterKeyValuePair in FilterList.GetFilters())
                            filterKeyValuePair.Value.Enums.AddRange(enumerations);
                    }
                }

                foreach (var filterKeyValuePair in FilterList.GetFilters())
                {
                    var filter = filterKeyValuePair.Value;
                    foreach (var enumeration in filter.Enums)
                    {
                        filter.UpdateEnum(enumeration);
                        foreach (var enumerationMember in enumeration.Items)
                        {
                            filter.UpdateEnumMember(enumerationMember);
                        }
                    }
                }

                // Remove tables marked as RemoveTable
                // This means it was only required to generate an enum and can now be removed
                foreach (var filter in FilterList.GetFilters().Select(filterKeyValuePair => filterKeyValuePair.Value))
                {
                    filter.Tables.RemoveAll(x => x.RemoveTable);
                }
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Format("// Failed to read enumeration tables in LoadEnums() - {0}", error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Empty);
            }
        }

        // Re-invokes the efrpg tool to read the actual enum values for the resolved enumeration specs.
        private List<Enumeration> ReadEnumsViaTool(List<EnumerationSettings> specs)
        {
            if (specs == null || specs.Count == 0)
                return new List<Enumeration>();

            var specsXml    = EnumXml.WriteSpecs(specs);
            var specsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(specsXml));
            var xml         = EfrpgToolRunner.Run("--enums-base64 " + specsBase64);
            var rawEnums    = EnumXml.ReadData(xml);
            return EnumBuilder.Build(specs, rawEnums);
        }

        public void LoadSequences()
        {
            if (_result == null || !Settings.ElementsToGenerate.HasFlag(Elements.Context))
                return;

            try
            {
                var sequences = _result.Sequences;
                if (sequences == null || sequences.Count <= 0)
                    return; // No sequences in database

                foreach (var filterKeyValuePair in FilterList.GetFilters())
                {
                    // Only add sequences where the table is used by this filter
                    foreach (var seq in sequences
                                 .Where(seq => seq.TableMapping
                                     .Any(tableMapping => filterKeyValuePair.Value.Tables
                                         .Any(x =>
                                             x.Schema.DbName.Equals(tableMapping.TableSchema, StringComparison.InvariantCultureIgnoreCase) &&
                                             x.DbName.Equals(tableMapping.TableName, StringComparison.InvariantCultureIgnoreCase)))))
                    {
                        filterKeyValuePair.Value.Sequences.Add(seq);
                    }
                }
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Format("// Failed to read sequences in LoadSequences() - {0}", error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Empty);
            }
        }

        public void ReadDatabase()
        {
            Settings.CheckSettings();
            LoadTables();
            LoadStoredProcs();
            LoadEnums();
            LoadSequences();
        }

        public void LoadTables()
        {
            if (_result == null ||
                !(Settings.ElementsToGenerate.HasFlag(Elements.Poco) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Context) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.Interface) ||
                  Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration)))
                return;

            try
            {
                var rawTables = _result.Tables;
                var rawIndexes = _result.Indexes;
                var rawForeignKeys = _result.ForeignKeys;

                AddTablesToFilters(rawTables);

                // Always read extended properties so they are available in the UpdateColumn delegate
                // (e.g. "if (column.ExtendedProperty == \"HIDE\") column.Hidden = true;") and for
                // ApplyJsonPropertyNameAttribute, regardless of whether comments are being generated.
                AddExtendedPropertiesToFilters(_result.ExtendedProperties);

                RunUpdateDelegates();

                IdentifyUniqueForeignKeys(rawIndexes, rawForeignKeys);
                AddIndexesToFilters(rawIndexes);
                SetPrimaryKeys();
                AddForeignKeysToFilters(rawForeignKeys);

                if (Settings.IsEfCore8Plus())
                {
                    AddMemoryOptimisedTablesToFilters(_result.MemoryOptimisedTables);
                    AddTriggersToFilters(_result.Triggers);
                }

                SetupEntityAndConfig(); // Must be done last
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Format("// Failed to read database schema in LoadTables() - {0}", error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Empty);
            }
        }

        public static void IdentifyUniqueForeignKeys(List<RawIndex> rawIndexes, List<RawForeignKey> rawForeignKeys)
        {
            if (rawIndexes == null || rawForeignKeys == null || !rawIndexes.Any() || !rawForeignKeys.Any())
                return;

            var uniqueForeignKeys = from i in rawIndexes.Where(x => x.IsUniqueConstraint)
                                    join fk1 in rawForeignKeys
                                        on new { X1 = i.Schema, X2 = i.TableName } equals new { X1 = fk1.PkSchema, X2 = fk1.PkTableName }
                                    select fk1;

            foreach (var fk in uniqueForeignKeys)
                fk.HasUniqueConstraint = true;
        }

        // Create tables from raw data for each of the DbContextFilters

        private void AddTablesToFilters(List<RawTable> rawTables)
        {
            if (rawTables == null || !rawTables.Any())
                return;

            var tablesNames = rawTables
                .Select(x => new { x.SchemaName, x.TableName, x.IsView })
                .Distinct()
                .OrderBy(x => x.SchemaName)
                .ThenBy(x => x.TableName)
                .ToList();

            var deleteFilteredOutFiles = Settings.GenerateSeparateFiles;

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;

                foreach (var tn in tablesNames)
                {
                    var exclude = tn.IsView && !filter.IncludeViews;
                    if (exclude && !Settings.GenerateSeparateFiles)
                        continue;

                    // Check if schema is excluded
                    var schema = new Schema(tn.SchemaName);
                    if (filter.IsExcluded(schema))
                    {
                        exclude = true;
                        if (!deleteFilteredOutFiles)
                            continue;
                    }

                    // Check if table is excluded
                    var table = new Table(filter, schema, tn.TableName, tn.IsView);
                    if (filter.IsExcluded(table))
                    {
                        exclude = true;
                        if (!deleteFilteredOutFiles)
                            continue;
                    }

                    // Handle table names with underscores - singularise just the last word
                    var tableName = NamingHelper.CleanUp(filter.TableRename(tn.TableName, tn.SchemaName, tn.IsView));
                    var singularCleanTableName = Inflector.MakeSingular(tableName);
                    table.NameHumanCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(singularCleanTableName) : singularCleanTableName).Replace(" ", "").Replace("$", "").Replace(".", "");

                    if (Settings.PrependSchemaName && string.Compare(table.Schema.DbName, Settings.DefaultSchema, StringComparison.OrdinalIgnoreCase) != 0 && Settings.PrependSchemaNameForTable(table))
                        table.NameHumanCase = table.Schema.DbName + "_" + table.NameHumanCase;

                    if (filter.IsExcluded(table)) // Retest exclusion after table rename
                    {
                        exclude = true;
                        if (!deleteFilteredOutFiles)
                            continue;
                    }

                    if (exclude)
                    {
                        FileManagementService.DeleteFile(table.NameHumanCaseWithSuffix() + FileManagementService.Extension); // Poco
                        FileManagementService.DeleteFile(table.NameHumanCaseWithSuffix() + Settings.ConfigurationClassName + FileManagementService.Extension); // Poco config
                        continue;
                    }

                    // Check for table or C# name clashes
                    var originalTable = filter.Tables.Find(x => x.NameHumanCase == table.NameHumanCase);
                    if (NamingHelper.ReservedKeywords.Contains(table.NameHumanCase) ||
                        originalTable != null)
                    {
                        if (originalTable == null)
                        {
                            table.NameHumanCase += "1";
                        }
                        else
                        {
                            originalTable.PluralNameOverride = originalTable.NameHumanCase;

                            var newTableName = (Settings.UsePascalCase ? Inflector.ToTitleCase(tableName) : tableName).Replace(" ", "").Replace("$", "").Replace(".", "");
                            if (NamingHelper.ReservedKeywords.Contains(newTableName) ||
                                filter.Tables.Find(x => x.NameHumanCase == newTableName) != null)
                            {
                                table.NameHumanCase += "1";
                            }
                            else
                            {
                                table.NameHumanCase = newTableName;
                                table.PluralNameOverride = newTableName;
                            }
                        }
                    }

                    // Create columns for table
                    foreach (var rawTable in rawTables
                        .Where(x => x.SchemaName == tn.SchemaName && x.TableName == tn.TableName)
                        .OrderBy(x => x.Ordinal))
                    {
                        table.IsSynonym = table.IsSynonym || rawTable.IsSynonym;
                        var column = CreateColumn(rawTable, table, filter);
                        if (column != null)
                            table.Columns.Add(column);
                    }

                    // Check for property name clashes in columns
                    foreach (var c in table.Columns.Where(c => table.Columns.FindAll(x => x.NameHumanCase == c.NameHumanCase).Count > 1))
                    {
                        var n = 1;
                        var original = c.NameHumanCase;
                        c.NameHumanCase = original + n++;

                        // Check if the above resolved the name clash, if not, use next value
                        while (c.ParentTable.Columns.Count(c2 => c2.NameHumanCase == c.NameHumanCase) > 1)
                            c.NameHumanCase = original + n++;
                    }

                    filter.Tables.Add(table);
                }
            }
        }

        private void RunUpdateDelegates()
        {
            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;
                foreach (var table in filter.Tables)
                {
                    if (table.IsView)
                        filter.ViewProcessing(table);

                    filter.UpdateTable(table);

                    foreach (var column in table.Columns)
                        filter.UpdateColumn(column, table);

                    table.Suffix = Settings.TableSuffix;
                }
            }
        }

        private void AddIndexesToFilters(List<RawIndex> rawIndexes)
        {
            if (rawIndexes == null || !rawIndexes.Any())
                return;

            var indexTables = rawIndexes
                .Select(x => new { x.Schema, x.TableName })
                .Distinct()
                .OrderBy(x => x.Schema)
                .ThenBy(x => x.TableName)
                .ToList();

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;

                Table t = null;
                foreach (var indexTable in indexTables)
                {
                    // Lookup table
                    if (t == null || t.DbName != indexTable.TableName || t.Schema.DbName != indexTable.Schema)
                        t = filter.Tables.GetTable(indexTable.TableName, indexTable.Schema);

                    if (t == null)
                        continue;

                    // Find indexes for table
                    t.Indexes = rawIndexes.Where(x => x.Schema == indexTable.Schema && x.TableName == indexTable.TableName)
                            .OrderBy(o => o.ColumnCount)
                            .ThenBy(o => o.KeyOrdinal)
                            .ToList();

                    // Set index on column
                    foreach (var index in t.Indexes)
                    {
                        var col = t.Columns.Find(x => x.DbName == index.ColumnName);
                        if (col == null)
                            continue;

                        col.Indexes.Add(index);

                        col.IsPrimaryKey = col.IsPrimaryKey || index.IsPrimaryKey;
                        col.IsUniqueConstraint = col.IsUniqueConstraint || (index.IsUniqueConstraint && index.ColumnCount == 1);
                        col.IsUnique = col.IsUnique || (index.IsUnique && index.ColumnCount == 1);
                    }

                    // Check if table has any primary keys
                    if (t.PrimaryKeys.Any())
                        continue; // Already has a primary key, ignore this unique index / constraint

                    // Find unique indexes for table
                    var uniqueIndexKeys = t.Indexes
                        .Where(x => x.IsUnique || x.IsPrimaryKey || x.IsUniqueConstraint)
                        .OrderBy(o => o.ColumnCount)
                        .ThenBy(o => o.KeyOrdinal);

                    // Process only the first index with the lowest unique column count
                    string indexName = null;
                    foreach (var key in uniqueIndexKeys)
                    {
                        if (indexName == null)
                            indexName = key.IndexName;

                        if (indexName != key.IndexName)
                            break; // First unique index with lowest column count has been processed, exit.

                        var col = t.Columns.Find(x => x.DbName == key.ColumnName);
                        if (col != null && !col.IsNullable && !col.Hidden && !col.IsPrimaryKey)
                        {
                            col.IsPrimaryKey = true;
                            col.IsUniqueConstraint = true;
                            col.IsUnique = true;
                            col.UniqueIndexName = indexName;
                        }
                    }
                }
            }
        }

        private void AddForeignKeysToFilters(List<RawForeignKey> rawForeignKeys)
        {
            if (rawForeignKeys == null || !rawForeignKeys.Any())
                return;

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;
                var fks = new List<RawForeignKey>();
                fks.AddRange(rawForeignKeys/*.OrderBy(x => x.SortOrder).ThenBy(x => x.FkTableName).ThenBy(x => x.PkTableName)*/);

                if (!fks.Any())
                    continue;

                var foreignKeys = new List<ForeignKey>();
                foreach (var rawForeignKey in fks)
                {
                    var fkTableNameFiltered = filter.TableRename(rawForeignKey.FkTableName, rawForeignKey.FkSchema, false);
                    var pkTableNameFiltered = filter.TableRename(rawForeignKey.PkTableName, rawForeignKey.PkSchema, false);

                    var fk = new ForeignKey(rawForeignKey, fkTableNameFiltered, pkTableNameFiltered);

                    var filteredFk = filter.ForeignKeyFilter(fk);
                    if (filteredFk != null)
                    {
                        if (Settings.ForeignKeyFilterFunc != null)
                            filteredFk = Settings.ForeignKeyFilterFunc(filteredFk);

                        if (filteredFk != null)
                            foreignKeys.Add(filteredFk);
                    }
                }

                IdentifyForeignKeys(foreignKeys, filter.Tables);
                Settings.AddExtraForeignKeys?.Invoke(filter, this, foreignKeys, filter.Tables);

                // Work out if there are any foreign key relationship naming clashes
                ProcessForeignKeys(foreignKeys, true, filter);

                // Mappings tables can only be true for Ef6 and EFCore 5 onwards
                if (Settings.UseMappingTables && !(Settings.IsEf6() || Settings.IsEfCore8Plus()))
                    Settings.UseMappingTables = false;

                if (Settings.UseMappingTables)
                    filter.Tables.IdentifyMappingTables(foreignKeys, true, _result?.IncludeSchema ?? true);

                // Now we know our foreign key relationships and have worked out if there are any name clashes,
                // re-map again with intelligently named relationships.
                filter.Tables.ResetNavigationProperties();

                ProcessForeignKeys(foreignKeys, false, filter);
                if (Settings.UseMappingTables)
                    filter.Tables.IdentifyMappingTables(foreignKeys, false, _result?.IncludeSchema ?? true);
            }
        }

        /*private void SortForeignKeys(List<RawForeignKey> rawForeignKeys)
        {
            foreach (var fk in rawForeignKeys)
            {
                fk.SortOrder = 10;
                
                var fkColumn = fk.FkColumn.ToLowerInvariant();
                var pkTable = fk.PkTableName.ToLowerInvariant();
                
                // Matches exactly
                if (fkColumn == pkTable)
                {
                    fk.SortOrder = 1;
                    continue;
                }

                // Matches without 'id'
                if (fkColumn.EndsWith("id") && fkColumn.Remove(fkColumn.Length - 2, 2) == pkTable)
                {
                    fk.SortOrder = 2;
                    continue;
                }

                // Matches if trimmed
                if (fkColumn.Length > pkTable.Length && fkColumn.Substring(0, pkTable.Length) == pkTable)
                {
                    fk.SortOrder = 3;
                    continue;
                }
            }
        }*/

        private void AddTriggersToFilters(List<RawTrigger> triggers)
        {
            if (triggers == null || !triggers.Any())
                return;

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;

                Table t = null;
                foreach (var trigger in triggers)
                {
                    // Lookup table
                    if (t == null || t.DbName != trigger.TableName || t.Schema.DbName != trigger.SchemaName)
                        t = filter.Tables.GetTable(trigger.TableName, trigger.SchemaName);

                    if (t == null)
                        continue;

                    // Only store the one trigger name as EFCore 7+ does not care what its name is. It only cares that there is one present.
                    t.TriggerName = trigger.TriggerName;
                }
            }
        }

        private void AddMemoryOptimisedTablesToFilters(List<RawMemoryOptimisedTable> memoryOptimisedTables)
        {
            if (memoryOptimisedTables == null || !memoryOptimisedTables.Any())
                return;

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;

                Table t = null;
                foreach (var trigger in memoryOptimisedTables)
                {
                    // Lookup table
                    if (t == null || t.DbName != trigger.TableName || t.Schema.DbName != trigger.SchemaName)
                        t = filter.Tables.GetTable(trigger.TableName, trigger.SchemaName);

                    if (t == null)
                        continue;

                    t.IsMemoryOptimised = true;
                }
            }
        }

        private void AddExtendedPropertiesToFilters(List<RawExtendedProperty> extendedProperties)
        {
            if (extendedProperties == null || !extendedProperties.Any())
                return;

            var commentsInSummaryBlock = Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock;
            var multiLine = new Regex("[\r\n]+", RegexOptions.Compiled);
            var whiteSpace = new Regex("\\s+", RegexOptions.Compiled);

            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;

                Table t = null;
                foreach (var extendedProperty in extendedProperties)
                {
                    // Lookup table
                    if (t == null || t.DbName != extendedProperty.TableName || t.Schema.DbName != extendedProperty.SchemaName)
                        t = filter.Tables.GetTable(extendedProperty.TableName, extendedProperty.SchemaName);

                    if (t == null)
                        continue;

                    if (extendedProperty.TableLevelExtendedComment)
                    {
                        // Table level extended comment
                        t.ExtendedProperty.Add(multiLine.Replace(extendedProperty.ExtendedProperty, "\r\n    /// "));
                        if (!extendedProperty.PropertyName.StartsWith("JsonProperty", StringComparison.InvariantCultureIgnoreCase))
                            t.Description = extendedProperty.ExtendedProperty;
                        continue;
                    }

                    // Column level extended comment
                    var col = t.Columns.Find(x => x.DbName == extendedProperty.ColumnName);
                    if (col == null)
                        continue;

                    // Store in ExtendedProperties dictionary by property name
                    if (!string.IsNullOrEmpty(extendedProperty.PropertyName))
                    {
                        col.ExtendedProperties[extendedProperty.PropertyName] = extendedProperty.ExtendedProperty;
                    }

                    // Keep existing behavior for backward compatibility
                    if (!extendedProperty.PropertyName.StartsWith("JsonProperty", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (commentsInSummaryBlock)
                            col.ExtendedProperty = multiLine.Replace(extendedProperty.ExtendedProperty, "\r\n        /// ");
                        else
                            col.ExtendedProperty = whiteSpace.Replace(multiLine.Replace(extendedProperty.ExtendedProperty, " "), " ");

                        col.Description = extendedProperty.ExtendedProperty;
                    }
                }
            }
        }

        private void SetPrimaryKeys()
        {
            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;
                foreach (var table in filter.Tables)
                {
                    table.SetPrimaryKeys();
                }

                if (_hasTrialLicence)
                    filter.Tables.TrimForTrialLicence();
            }
        }

        private void SetupEntityAndConfig()
        {
            foreach (var filterKeyValuePair in FilterList.GetFilters())
            {
                var filter = filterKeyValuePair.Value;
                foreach (var table in filter.Tables)
                {
                    table.Columns.ForEach(SetupEntityAndConfig);
                }
            }
        }

        public void LoadStoredProcs()
        {
            if (_result == null || !_result.CanReadStoredProcedures)
                return;

            try
            {
                var deleteFilteredOutFiles = Settings.GenerateSeparateFiles;

                var spFilters = FilterList
                    .GetFilters()
                    .Where(x => x.Value.IncludeStoredProcedures || x.Value.IncludeTableValuedFunctions || x.Value.IncludeScalarValuedFunctions)
                    .ToList();

                if (!spFilters.Any())
                    return;

                var rawStoredProcs = _result.StoredProcedures;

                // Only call stored procedures to obtain the return models that are not filtered out
                // We don't want to do this for every db context we are generating as that is inefficient
                var procs = rawStoredProcs
                    .Select(sp => new { sp.Schema, sp.Name, sp.IsTableValuedFunction, sp.IsScalarValuedFunction, sp.IsStoredProcedure })
                    .Distinct()
                    .OrderBy(x => x.Schema)
                    .ThenBy(x => x.Name);

                var storedProcs = new List<StoredProcedure>();
                foreach (var proc in procs)
                {
                    var sp = new StoredProcedure
                    {
                        DbName = proc.Name,
                        NameHumanCase = (Settings.UsePascalCase ? Inflector.ToTitleCase(proc.Name) : proc.Name).Replace(" ", "").Replace("$", ""),
                        Schema = new Schema(proc.Schema),
                        IsTableValuedFunction = proc.IsTableValuedFunction,
                        IsScalarValuedFunction = proc.IsScalarValuedFunction,
                        IsStoredProcedure = proc.IsStoredProcedure
                    };
                    sp.NameHumanCase = NamingHelper.CleanUp(sp.NameHumanCase);
                    if (Settings.PrependSchemaName && (string.Compare(proc.Schema, Settings.DefaultSchema, StringComparison.OrdinalIgnoreCase) != 0) && Settings.PrependSchemaNameForStoredProcedure(sp))
                        sp.NameHumanCase = proc.Schema + "_" + sp.NameHumanCase;

                    sp.Parameters.AddRange(rawStoredProcs
                        .Where(x => x.Parameter != null &&
                                    x.Schema == proc.Schema &&
                                    x.Name == proc.Name)
                        .Select(x => x.Parameter));

                    NormaliseStoredProcedureParameters(sp);
                    ApplyStoredProcedureReturnModels(sp, rawStoredProcs
                        .FirstOrDefault(x =>
                            x.Schema == proc.Schema &&
                            x.Name == proc.Name &&
                            (x.ReturnModelsRead || !string.IsNullOrEmpty(x.ReturnModelError) || x.ReturnModels.Any())));

                    sp.HasSpatialParameter = sp.Parameters.Any(x => x.IsSpatial);

                    if (Settings.DisableGeographyTypes && sp.HasSpatialParameter)
                        continue; // Ignore stored procedure due to spatial parameter

                    // Check to see if this stored proc is to be kept by any of the filters
                    if (spFilters.All(x => x.Value.IsExcluded(sp)))
                    {
                        if (deleteFilteredOutFiles)
                            FileManagementService.DeleteFile(sp.WriteStoredProcReturnModelName(spFilters[0].Value) + FileManagementService.Extension);

                        continue; // All Db Context exclude this stored proc, ignore it as nobody wants it
                    }

                    // Two routines can clean up to the same C# name - resolve_to_same_name and Resolve_ToSameName
                    // both become ResolveToSameName - which generates two methods with identical signatures. Tables
                    // and columns already disambiguate with a numeric suffix; routines now do the same. procs is
                    // ordered by schema then name, so which one keeps the plain name does not depend on the order
                    // the catalogue happened to return.
                    sp.NameHumanCase = NamingHelper.MakeUnique(sp.NameHumanCase,
                                                               storedProcs.Select(x => x.NameHumanCase).ToList());

                    storedProcs.Add(sp);
                }

                if (!storedProcs.Any())
                    return; // No stored procs to read the return model for, so exit

                // Stored proc return models were populated by the dotnet tool (efrpg) via EfrpgResult.
                // No additional DB call is needed from the T4 host.

                // Check if any of the stored proc return models have spatial types
                /*foreach (var sp in storedProcs)
                {
                    if(sp.ReturnModels.Any())
                    {
                        // todo
                    }
                }*/

                // Force generation of Async stored procs
                /*foreach (var sp in storedProcs.Where(x => !x.IsTableValuedFunction && !x.ReturnModels.Any()))
                {
                    sp.ReturnModels.Add(new List<DataColumn> { new DataColumn("result", typeof(int)) });
                }*/

                // Remove stored procs where the return model type contains spaces and cannot be mapped
                // Also need to remove any TVF functions with parameters that are non scalar types, such as DataTable
                var validStoredProcs = new List<StoredProcedure>();
                foreach (var sp in storedProcs)
                {
                    if (!sp.ReturnModels.Any())
                    {
                        validStoredProcs.Add(sp);
                        continue;
                    }


                    if (sp.IsTableValuedFunction && sp.Parameters.Any(c => c.PropertyType == "DataTable"))
                        continue; // Invalid, ignore

                    validStoredProcs.Add(sp); // Valid, keep this stored proc
                }

                // Update the list of stored procs for each of the db context filters that want them
                foreach (var filterKeyValuePair in spFilters)
                {
                    var filter = filterKeyValuePair.Value;
                    foreach (var sp in validStoredProcs)
                    {
                        if (!filter.IsExcluded(sp))
                        {
                            if (_hasTrialLicence)
                            {
                                const int n = 1 + 2 + 3 + 4;
                                if (filter.StoredProcs.Count < n)
                                    filter.StoredProcs.Add(sp);
                            }
                            else
                                filter.StoredProcs.Add(sp);
                        }
                        else
                        {
                            if (deleteFilteredOutFiles)
                                FileManagementService.DeleteFile(sp.WriteStoredProcReturnModelName(filter) + FileManagementService.Extension);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Format("// Failed to read database schema for stored procedures - {0}", error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Empty);
            }
        }

        private void NormaliseStoredProcedureParameters(StoredProcedure sp)
        {
            if (sp == null || sp.Parameters == null)
                return;

            foreach (var parameter in sp.Parameters)
            {
                NormaliseStoredProcedureParameterName(parameter);
                NormaliseStoredProcedureParameterTypes(parameter);

                // The readers that used to live in this project stripped remaining special characters (underscores
                // included) from the parameter names of everything they read return objects for - stored procedures
                // and TVFs, but never scalar functions. Scalar function parameters therefore keep their underscores
                // (udf_net_sale(list_price)) while stored procedure parameters lose them (firstval). The readers now
                // live in the separate efrpg tool repository; this behaviour is reproduced here because generated
                // signatures depend on it, so it must not be "tidied" to be consistent.
                if (!sp.IsScalarValuedFunction)
                    parameter.NameHumanCase = Regex.Replace(parameter.NameHumanCase, @"[^A-Za-z0-9@\s]*", string.Empty);
            }
        }

        // Reproduces exactly what the readers did before they moved to the separate efrpg tool repository: CleanUp,
        // then title-case only when UsePascalCase, then the reserved-keyword escape. No further character stripping -
        // that would eat underscores (list_price -> listprice) and non-Latin letters, changing generated signatures.
        // CleanUp already handles illegal characters.
        private static void NormaliseStoredProcedureParameterName(StoredProcedureParameter parameter)
        {
            var clean = NamingHelper.CleanUp((parameter.Name ?? string.Empty).Replace("@", string.Empty));
            if (string.IsNullOrWhiteSpace(clean))
                clean = "p" + parameter.Ordinal;

            parameter.NameHumanCase = Inflector.MakeInitialLower((Settings.UsePascalCase ? Inflector.ToTitleCase(clean) : clean).Replace(" ", string.Empty));

            if (NamingHelper.ReservedKeywords.Contains(parameter.NameHumanCase))
                parameter.NameHumanCase = "@" + parameter.NameHumanCase;
        }

        private void NormaliseStoredProcedureParameterTypes(StoredProcedureParameter parameter)
        {
            var dataType = NormaliseDbType(parameter.DataType);
            if (!string.IsNullOrEmpty(dataType))
            {
                parameter.SqlDbType = GetStoredProcedureParameterDbType(dataType);
                parameter.PropertyType = GetPropertyType(dataType);
                parameter.IsSpatial = IsSpatialType(dataType);
            }

            var returnDataType = NormaliseDbType(parameter.ReturnDataType);
            if (!string.IsNullOrEmpty(returnDataType))
            {
                parameter.ReturnSqlDbType = GetStoredProcedureParameterDbType(returnDataType);
                parameter.ReturnPropertyType = GetPropertyType(returnDataType);
            }
        }

        private static string NormaliseDbType(string dbType)
        {
            return (dbType ?? string.Empty).Trim().ToLowerInvariant();
        }

        private string GetStoredProcedureParameterDbType(string sqlType)
        {
            var normalised = NormaliseDbType(sqlType);
            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    string parameterDbType;
                    if (SqlServerStoredProcedureParameterDbType.TryGetValue(normalised, out parameterDbType))
                        return parameterDbType;
                    return SqlServerStoredProcedureParameterDbType[string.Empty];

                default:
                    return normalised;
            }
        }

        private static void ApplyStoredProcedureReturnModels(StoredProcedure sp, RawStoredProcedure rawStoredProcedure)
        {
            if (sp == null || rawStoredProcedure == null)
                return;

            sp.ReturnModels.Clear();
            foreach (var returnModel in rawStoredProcedure.ReturnModels)
                sp.ReturnModels.Add(returnModel.Select(CreateDataColumn).ToList());

            if (!string.IsNullOrEmpty(rawStoredProcedure.ReturnModelError))
            {
                Settings.ReadStoredProcReturnObjectException(new Exception(rawStoredProcedure.ReturnModelError), sp);
                return;
            }

            if (rawStoredProcedure.ReturnModelsRead)
            {
                sp.MergeModelsIfAllSame();
                Settings.ReadStoredProcReturnObjectCompleted(sp);
            }
        }

        private static DataColumn CreateDataColumn(RawStoredProcedureReturnColumn rawColumn)
        {
            var column = new DataColumn(rawColumn.ColumnName, ResolveDataColumnType(rawColumn))
            {
                AllowDBNull = rawColumn.AllowDBNull
            };

            try
            {
                column.Unique = rawColumn.Unique;
            }
            catch
            {
                // Some provider schemas report uniqueness in combinations DataColumn cannot represent standalone.
            }

            if (rawColumn.Precision > 0 || rawColumn.Scale > 0)
            {
                column.ExtendedProperties["Precision"] = rawColumn.Precision;
                column.ExtendedProperties["Scale"] = rawColumn.Scale;
            }

            if (!string.IsNullOrEmpty(rawColumn.DataTypeFullName))
                column.ExtendedProperties["DataTypeFullName"] = rawColumn.DataTypeFullName;
            if (!string.IsNullOrEmpty(rawColumn.DataTypeName))
                column.ExtendedProperties["DataTypeName"] = rawColumn.DataTypeName;
            if (!string.IsNullOrEmpty(rawColumn.DataTypeNamespace))
                column.ExtendedProperties["DataTypeNamespace"] = rawColumn.DataTypeNamespace;

            return column;
        }

        private static Type ResolveDataColumnType(RawStoredProcedureReturnColumn rawColumn)
        {
            switch (rawColumn.DataTypeFullName)
            {
                case "System.Boolean": return typeof(bool);
                case "System.Byte": return typeof(byte);
                case "System.SByte": return typeof(sbyte);
                case "System.Int16": return typeof(short);
                case "System.UInt16": return typeof(ushort);
                case "System.Int32": return typeof(int);
                case "System.UInt32": return typeof(uint);
                case "System.Int64": return typeof(long);
                case "System.UInt64": return typeof(ulong);
                case "System.Single": return typeof(float);
                case "System.Double": return typeof(double);
                case "System.Decimal": return typeof(decimal);
                case "System.String": return typeof(string);
                case "System.DateTime": return typeof(DateTime);
                case "System.DateTimeOffset": return typeof(DateTimeOffset);
                case "System.TimeSpan": return typeof(TimeSpan);
                case "System.Guid": return typeof(Guid);
                case "System.Byte[]": return typeof(byte[]);
                case "System.Object": return typeof(object);
            }

            var type = !string.IsNullOrEmpty(rawColumn.DataTypeFullName)
                ? Type.GetType(rawColumn.DataTypeFullName, false)
                : null;

            if (type == null && !string.IsNullOrEmpty(rawColumn.DataTypeAssemblyQualifiedName))
                type = Type.GetType(rawColumn.DataTypeAssemblyQualifiedName, false);

            return type ?? typeof(object);
        }

        /// <summary>AddRelationship overload for single-column foreign-keys.</summary>
        public void AddRelationship(IDbContextFilter filter, List<ForeignKey> fkList, Tables tablesAndViews, string name, string pkSchema, string pkTable, string pkColumn, string fkSchema, string fkTable, string fkColumn, string parentName, string childName, bool isUnique)
        {
            AddRelationship(filter, fkList, tablesAndViews, name, pkSchema, pkTable, new[] { pkColumn }, fkSchema, fkTable, new[] { fkColumn }, parentName, childName, isUnique);
        }

        public void AddRelationship(IDbContextFilter filter, List<ForeignKey> fkList, Tables tablesAndViews, string relationshipName,
            string pkSchema, string pkTableName, string[] pkColumns,
            string fkSchema, string fkTableName, string[] fkColumns,
            string parentName, string childName, bool isUnique)
        {
            // Argument validation:
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (fkList == null) throw new ArgumentNullException(nameof(fkList));
            if (tablesAndViews == null) throw new ArgumentNullException(nameof(tablesAndViews));
            if (string.IsNullOrEmpty(relationshipName)) throw new ArgumentNullException(nameof(relationshipName));
            if (string.IsNullOrEmpty(pkSchema)) throw new ArgumentNullException(nameof(pkSchema));
            if (string.IsNullOrEmpty(pkTableName)) throw new ArgumentNullException(nameof(pkTableName));
            if (pkColumns == null) throw new ArgumentNullException(nameof(pkColumns));
            if (pkColumns.Length == 0 || pkColumns.Any(string.IsNullOrEmpty)) throw new ArgumentException("Invalid primary-key columns: No primary-key column names are specified, or at least one primary-key column name is empty.", nameof(pkColumns));
            if (string.IsNullOrEmpty(fkSchema)) throw new ArgumentNullException(nameof(fkSchema));
            if (string.IsNullOrEmpty(fkTableName)) throw new ArgumentNullException(nameof(fkTableName));
            if (fkColumns == null) throw new ArgumentNullException(nameof(fkColumns));
            if (fkColumns.Length != pkColumns.Length || fkColumns.Any(string.IsNullOrEmpty)) throw new ArgumentException("Invalid foreign-key columns:Foreign-key column list has a different number of columns than the primary-key column list, or at least one foreign-key column name is empty.", nameof(pkColumns));

            //////////////////

            var pkTable = tablesAndViews.GetTable(pkTableName, pkSchema);
            if (pkTable == null)
                throw new ArgumentException("Couldn't find table " + pkSchema + "." + pkTableName);

            var fkTable = tablesAndViews.GetTable(fkTableName, fkSchema);
            if (fkTable == null)
                throw new ArgumentException("Couldn't find table " + fkSchema + "." + fkTableName);

            // Ensure all columns exist:
            foreach (var pkCol in pkColumns)
            {
                if (pkTable.Columns.SingleOrDefault(c => c.DbName == pkCol) == null)
                    throw new ArgumentException("The relationship primary-key column \"" + pkCol + "\" does not exist in table or view " + pkSchema + "." + pkTableName);
            }
            foreach (var fkCol in fkColumns)
            {
                if (fkTable.Columns.SingleOrDefault(c => c.DbName == fkCol) == null)
                    throw new ArgumentException("The relationship foreign-key column \"" + fkCol + "\" does not exist in table or view " + fkSchema + "." + fkTableName);
            }

            for (var i = 0; i < pkColumns.Length; i++)
            {
                var pkc = pkColumns[i];
                var fkc = fkColumns[i];

                var fkTableNameFiltered = filter.TableRename(fkTableName, fkSchema, fkTable.IsView);
                var pkTableNameFiltered = filter.TableRename(pkTableName, pkSchema, pkTable.IsView);

                var fk = new ForeignKey(
                    fkTable.DbName,
                    fkSchema,
                    pkTable.DbName,
                    pkSchema,
                    fkc,
                    pkc,
                    "AddRelationship: " + relationshipName,
                    fkTableNameFiltered,
                    pkTableNameFiltered,
                    int.MaxValue,
                    false,
                    false,
                    parentName,
                    childName,
                    isUnique
                )
                { IncludeReverseNavigation = true };

                fkList.Add(fk);
                fkTable.HasForeignKey = true;
            }
        }

        private void ProcessForeignKeys(List<ForeignKey> fkList, bool checkForFkNameClashes, IDbContextFilter filter)
        {
            var constraints = fkList.Select(x => x.FkSchema + "." + x.ConstraintName).Distinct();
            foreach (var constraint in constraints)
            {
                var foreignKeys = fkList
                    .Where(x => string.Format("{0}.{1}", x.FkSchema, x.ConstraintName).Equals(constraint, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                var foreignKey = foreignKeys.First();
                var fkTable = filter.Tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                if (fkTable == null || fkTable.IsMapping || !fkTable.HasForeignKey)
                    continue;

                var pkTable = filter.Tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                if (pkTable == null || pkTable.IsMapping)
                    continue;

                var fkCols = foreignKeys.Select(x => new ColumnAndForeignKey
                {
                    ForeignKey = x,
                    Column = fkTable.Columns.Find(n => string.Equals(n.DbName, x.FkColumn, StringComparison.InvariantCultureIgnoreCase))
                })
                    .Where(x => x.Column != null)
                    .OrderBy(o => o.ForeignKey.Ordinal)
                    .ToList();

                if (!fkCols.Any())
                    continue;

                if (FkMustHaveSameNumberOfColumnsAsPrimaryKey() || AllowFkToNonPrimaryKey())
                {
                    // Check FK has same number of columns as the primary key it points to
                    var pks = pkTable.PrimaryKeys.OrderBy(x => x.PropertyType).ThenBy(y => y.DbName).ToArray();
                    var cols = fkCols.Select(x => x.Column).OrderBy(x => x.PropertyType).ThenBy(y => y.DbName).ToArray();

                    if (FkMustHaveSameNumberOfColumnsAsPrimaryKey() && pks.Length != cols.Length)
                    {
                        // Also check unique constraints
                        if (!AllowFkToNonPrimaryKey())
                            continue;

                        if (!fkCols.All(x => x.ForeignKey.HasUniqueConstraint))
                            continue;
                    }

                    if (!AllowFkToNonPrimaryKey() && pks.Where((pk, n) => pk.PropertyType != cols[n].PropertyType).Any())
                        continue;
                }

                var pkCols = foreignKeys.Select(x => new ColumnAndForeignKey
                {
                    ForeignKey = x,
                    Column = pkTable.Columns.Find(n => string.Equals(n.DbName, x.PkColumn, StringComparison.InvariantCultureIgnoreCase))
                })
                    .Where(x => x.Column != null)
                    .OrderBy(o => o.ForeignKey.Ordinal)
                    .ToList();

                if (!pkCols.Any())
                    continue;

                var allPkColsArePrimaryKeys = pkCols.All(c => c.Column.IsPrimaryKey);
                if (!AllowFkToNonPrimaryKey() && !allPkColsArePrimaryKeys)
                    continue; // Cannot have a FK to a non-primary key

                var relationship = CalcRelationship(pkTable, fkTable, fkCols, pkCols);
                if (relationship == Relationship.DoNotUse)
                    continue;

                var pkTableHumanCaseWithSuffix = pkTable.NameHumanCaseWithSuffix();//foreignKey.PkTableHumanCase(pkTable.Suffix);
                var pkTableHumanCase = foreignKey.PkTableHumanCase(null);
                var fkHasUniqueConstraint = pkCols.All(x => x.ForeignKey.HasUniqueConstraint) && relationship == Relationship.OneToOne;

                if (fkHasUniqueConstraint && pkCols.Any(x => x.Column.IsNullable))
                    continue; // This would force the column to be not null

                var flipRelationship = FlipRelationship(relationship);
                var fkMakePropNameSingular = relationship == Relationship.OneToOne;
                var pkPropName = fkTable.GetUniqueForeignKeyName(true, pkTableHumanCase, foreignKey, checkForFkNameClashes, true, relationship);
                var fkPropName = pkTable.GetUniqueForeignKeyName(false, fkTable.NameHumanCase, foreignKey, checkForFkNameClashes, fkMakePropNameSingular, flipRelationship);

                // Determine if the foreign key navigation property needs null-forgiving operator or nullable marker
                // If all FK columns are nullable, the navigation property can be marked nullable (requires AllowNullStrings).
                // NullableReverseNavigationProperties only controls one-to-one reverse nav props, not FK nav props.
                var allFkColumnsAreNullable = fkCols.All(x => x.Column.IsNullable);
                var needsNullForgiving = Settings.NeedsNullForgiving();
                var nullableMarker = Settings.AllowNullStrings && allFkColumnsAreNullable ? "?" : string.Empty;
                var nullForgivingOperator = needsNullForgiving && !allFkColumnsAreNullable ? " = null!;" : string.Empty;

                var fkd = new PropertyAndComments
                {
                    AdditionalDataAnnotations = filter.ForeignKeyAnnotationsProcessing(fkTable, pkTable, pkPropName, fkPropName),

                    PropertyName = pkPropName,

                    Definition = string.Format("public {0}{1}{2} {3} {4}{5}{6}",
                        Table.GetLazyLoadingMarker(),
                        pkTableHumanCaseWithSuffix,
                        nullableMarker,
                        pkPropName,
                        "{ get; set; }",
                        nullForgivingOperator,
                        Settings.IncludeComments != CommentsStyle.None ? " // " + foreignKey.ConstraintName : string.Empty),

                    Comments = string.Format("Parent {0} pointed by [{1}].({2}) ({3})",
                        pkTableHumanCase,
                        fkTable.DbName,
                        string.Join(", ", fkCols.Select(x => "[" + x.Column.NameHumanCase + "]").Distinct().ToArray()),
                        foreignKey.ConstraintName)
                };

                var firstFkCol = fkCols.First();
                firstFkCol.Column.EntityFk.Add(fkd);

                string manyToManyMapping, mapKey;
                if (foreignKeys.Count > 1)
                {
                    manyToManyMapping = string.Format("c => new {{ {0} }}", string.Join(", ", fkCols.Select(x => "c." + x.Column.NameHumanCase).Distinct().ToArray()));
                    mapKey = string.Format("{0}", string.Join(",", fkCols.Select(x => "\"" + x.Column.DbName + "\"").Distinct().ToArray()));
                }
                else
                {
                    manyToManyMapping = string.Format("c => c.{0}", firstFkCol.Column.NameHumanCase);
                    mapKey = string.Format("\"{0}\"", firstFkCol.Column.DbName);
                }

                var primaryKeyColumns = string.Empty;
                if (!allPkColsArePrimaryKeys)
                {
                    if (pkCols.Count > 1)
                        primaryKeyColumns = string.Format("p => new {{ {0} }}", string.Join(", ", pkCols.Select(x => "p." + x.Column.NameHumanCase).Distinct().ToArray()));
                    else
                        primaryKeyColumns = string.Format("p => p.{0}", pkCols.First().Column.NameHumanCase);
                }

                var fkCols2 = fkCols.Select(c => c.Column).ToList();
                var pkCols2 = pkCols.Select(c => c.Column).ToList();

                var rel = GetRelationship(relationship, fkCols2, pkCols2, pkPropName, fkPropName, manyToManyMapping, mapKey, foreignKey.CascadeOnDelete, foreignKey.IncludeReverseNavigation, foreignKey.IsNotEnforced, foreignKey.ConstraintName, pkTableHumanCase, fkTable.NameHumanCaseWithSuffix(), primaryKeyColumns, fkHasUniqueConstraint);
                var com = Settings.IncludeComments != CommentsStyle.None && string.IsNullOrEmpty(GetForeignKeyConstraintName("x")) ? " // " + foreignKey.ConstraintName : string.Empty;
                firstFkCol.Column.ConfigFk.Add(string.Format("{0};{1}", rel, com));

                if (foreignKey.IncludeReverseNavigation)
                    pkTable.AddReverseNavigation(relationship, fkTable, fkPropName, string.Format("{0}.{1}", fkTable.DbName, foreignKey.ConstraintName), foreignKeys);
            }
        }

        private void IdentifyForeignKeys(List<ForeignKey> fkList, Tables tables)
        {
            foreach (var foreignKey in fkList)
            {
                var fkTable = tables.GetTable(foreignKey.FkTableName, foreignKey.FkSchema);
                if (fkTable == null)
                    continue; // Could be filtered out

                var pkTable = tables.GetTable(foreignKey.PkTableName, foreignKey.PkSchema);
                if (pkTable == null)
                    continue; // Could be filtered out

                var fkCol = fkTable.Columns.Find(n => string.Equals(n.DbName, foreignKey.FkColumn, StringComparison.InvariantCultureIgnoreCase));
                if (fkCol == null)
                    continue; // Could not find fk column

                var pkCol = pkTable.Columns.Find(n => string.Equals(n.DbName, foreignKey.PkColumn, StringComparison.InvariantCultureIgnoreCase));
                if (pkCol == null)
                    continue; // Could not find pk column

                fkTable.HasForeignKey = true;
            }
        }

        private string GetRelationship(Relationship relationship, IList<Column> fkCols, IList<Column> pkCols, string pkPropName, string fkPropName,
            string manyToManyMapping, string mapKey, bool cascadeOnDelete, bool includeReverseNavigation, bool isNotEnforced, string foreignKeyConstraintName,
            string pkTableNameHumanCase, string fkTableNameHumanCase, string primaryKeyColumns, bool fkHasUniqueConstraint)
        {
            var hasMethod = GetHasMethod(relationship, fkCols, pkCols, isNotEnforced, fkHasUniqueConstraint);
            if (hasMethod == null)
                return string.Empty; // Relationship not supported

            var withMethod = GetWithMethod(relationship, fkCols, fkPropName, manyToManyMapping, mapKey, includeReverseNavigation, hasMethod, pkTableNameHumanCase, fkTableNameHumanCase, primaryKeyColumns, fkHasUniqueConstraint);

            return string.Format("{0}(a => a.{1}){2}{3}{4}",
                hasMethod,
                pkPropName,
                withMethod,
                GetCascadeOnDelete(cascadeOnDelete),
                GetForeignKeyConstraintName(foreignKeyConstraintName));
        }

        // Calculates the relationship between a child table and it's parent table.
        public static Relationship CalcRelationship(Table parentTable, Table childTable, List<ColumnAndForeignKey> fkCols, List<ColumnAndForeignKey> pkCols)
        {
            var childTableCols = fkCols.Select(c => c.Column).ToList();
            var parentTableCols = pkCols.Select(c => c.Column).ToList();

            if (childTableCols.Count == 1 && parentTableCols.Count == 1)
                return CalcRelationshipSingle(parentTable, childTable, childTableCols.First(), parentTableCols.First());

            // This relationship has multiple composite keys

            // childTable FK columns are exactly the primary key (they are part of primary key, and no other columns are primary keys)
            // TODO: we could also check if they are a unique index
            var childTableColumnsAllPrimaryKeys = (childTableCols.Count == childTableCols.Count(x => x.IsPrimaryKey)) &&
                                                  (childTableCols.Count == childTable.PrimaryKeys.Count());

            // parentTable columns are exactly the primary key (they are part of primary key, and no other columns are primary keys)
            // TODO: we could also check if they are a unique index
            var parentTableColumnsAllPrimaryKeys = (parentTableCols.Count == parentTableCols.Count(x => x.IsPrimaryKey)) &&
                                                   (parentTableCols.Count == parentTable.PrimaryKeys.Count());

            // childTable FK columns are not only FK but also the whole PK (not only part of PK); parentTable columns are the whole PK (not only part of PK) - so it's 1:1
            if (childTableColumnsAllPrimaryKeys && parentTableColumnsAllPrimaryKeys)
                return Relationship.OneToOne;

            // Check if covered by a unique constraint
            if (fkCols.All(x => x.ForeignKey.HasUniqueConstraint))
                return Relationship.OneToOne;

            // Check if covered by a unique index on PK table
            if (parentTableCols.All(x => x.IsUnique))
                return Relationship.OneToOne;

            return Relationship.ManyToOne;
        }

        // Calculates the relationship between a child table and it's parent table.
        public static Relationship CalcRelationshipSingle(Table parentTable, Table childTable, Column childTableCol, Column parentTableCol)
        {
            if (!childTableCol.IsPrimaryKey && !childTableCol.IsUniqueConstraint)
                return Relationship.ManyToOne;

            if (!parentTableCol.IsPrimaryKey && !parentTableCol.IsUniqueConstraint)
                return Relationship.ManyToOne;

            if (childTable.PrimaryKeys.Count() != 1)
                return Relationship.ManyToOne;

            if (parentTable.PrimaryKeys.Count() != 1)
                return Relationship.ManyToOne;

            return Relationship.OneToOne;
        }

        public static Relationship FlipRelationship(Relationship relationship)
        {
            switch (relationship)
            {
                case Relationship.OneToOne: return Relationship.OneToOne;
                case Relationship.OneToMany: return Relationship.ManyToOne;
                case Relationship.ManyToOne: return Relationship.OneToMany;
                case Relationship.ManyToMany: return Relationship.ManyToMany;
                case Relationship.DoNotUse: return Relationship.DoNotUse;

                default:
                    throw new ArgumentOutOfRangeException(nameof(relationship), relationship, null);
            }
        }

        public void SetupEntityAndConfig(Column c)
        {
            SetupEntity(c);
            SetupConfig(c);
        }

        public void GenerateCode()
        {
            try
            {
                CreateOutputFolders();
                foreach (var filter in FilterList.GetFilters())
                {
                    _fileManagementService.UseFileManager(filter.Key);
                    GenerateCode(filter.Value);
                }
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Format("// Failed to generate the code in GenerateCode() - {0}", error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error("// -----------------------------------------------------------------------------------------");
                _fileManagementService.Error(string.Empty);
            }
        }

        private void CreateOutputFolders()
        {
            if (!Settings.GenerateSeparateFiles)
                return;

            CreateOutputFolder(Settings.ContextFolder);
            CreateOutputFolder(Settings.InterfaceFolder);
            CreateOutputFolder(Settings.PocoFolder);
            CreateOutputFolder(Settings.PocoConfigurationFolder);
        }

        private void CreateOutputFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            var fullPath = Path.Combine(Settings.Root, folder);
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception x)
            {
                var error = FormatError(x);
                _fileManagementService.Error(string.Empty);
                _fileManagementService.Error(string.Format("// Unable to create folder: {0} Error: {1}", fullPath, error));
                _fileManagementService.Error("/*" + x.StackTrace + "*/");
                _fileManagementService.Error(string.Empty);
            }
        }

        private void GenerateCode(IDbContextFilter filter)
        {
            var codeGenerator = new CodeGenerator(this, filter);

            const string contextInterface = "contextInterface:";
            const string contextFactory = "contextFactory:";
            const string contextClass = "contextClass:";
            const string contextFakeClass = "contextFakeClass:";
            const string contextFakeDbSet = "contextFakeDbSet:";
            const string pocoClass = "pocoClass:";
            const string pocoConfiguration = "pocoConfiguration:";
            const string spReturnModels = "spReturnModel:";
            const string enumType = "enumType:";
            const string ownedEntityClass = "ownedEntityClass:";

            var codeOutputList = new CodeOutputList();
            codeOutputList.Add(contextInterface, codeGenerator.GenerateInterface());
            codeOutputList.Add(contextFactory, codeGenerator.GenerateFactory());
            codeOutputList.Add(contextClass, codeGenerator.GenerateContext());
            codeOutputList.Add(contextFakeClass, codeGenerator.GenerateFakeContext());
            codeOutputList.Add(contextFakeDbSet, codeGenerator.GenerateFakeDbSet());

            var IsEfCore8Plus = Settings.IsEfCore8Plus();

            foreach (var table in filter.Tables
                .Where(t => !t.IsMapping)
                .OrderBy(x => x.NameHumanCase))
            {
                // Write poco class, even if it has no primary key, for completeness.
                codeOutputList.Add(pocoClass + table.NameHumanCase, codeGenerator.GeneratePoco(table));

                // Only write the config if it has a primary key
                if (table.HasPrimaryKey || (table.IsView && IsEfCore8Plus))
                    codeOutputList.Add(pocoConfiguration + table.NameHumanCase, codeGenerator.GeneratePocoConfiguration(table));
            }

            foreach (var sp in filter.StoredProcs
                .Where(x => x.ReturnModels.Count > 0 &&
                            x.ReturnModels.Any(returnColumns => returnColumns.Any()) &&
                            !Settings.StoredProcedureReturnTypes.ContainsKey(x.NameHumanCase) &&
                            !Settings.StoredProcedureReturnTypes.ContainsKey(x.DbName))
                .OrderBy(x => x.NameHumanCase))
            {
                var key = spReturnModels + sp.WriteStoredProcReturnModelName(filter);
                codeOutputList.Add(key, codeGenerator.GenerateStoredProcReturnModel(sp));
            }

            foreach (var enumeration in filter.Enums)
            {
                codeOutputList.Add(enumType + enumeration.EnumName, codeGenerator.GenerateEnum(enumeration));
            }

            // Collect unique owned entity types across all tables and generate one class per type
            var ownedEntityTypes = new Dictionary<string, List<OwnedEntity>>(StringComparer.Ordinal);
            foreach (var table in filter.Tables.Where(t => !t.IsMapping))
            {
                foreach (var oe in table.OwnedEntities)
                {
                    if (!ownedEntityTypes.TryGetValue(oe.PropertyType, out var list))
                    {
                        list = new List<OwnedEntity>();
                        ownedEntityTypes[oe.PropertyType] = list;
                    }
                    list.Add(oe);
                }
            }
            foreach (var kvp in ownedEntityTypes.OrderBy(x => x.Key))
                codeOutputList.Add(ownedEntityClass + kvp.Key, codeGenerator.GenerateOwnedEntityClass(kvp.Key, kvp.Value));

            _fileHeaderFooter = new FileHeaderFooter(filter.SubNamespace);
            if (!Settings.GenerateSeparateFiles)
            {
                var preHeader = _preHeaderInfo.ToString();
                if (!string.IsNullOrWhiteSpace(preHeader))
                    _fileManagementService.WriteLine(preHeader.Trim());

                var header = _fileHeaderFooter.Header;
                if (!string.IsNullOrWhiteSpace(header))
                    _fileManagementService.WriteLine(header.Trim());

                var usings = codeGenerator.GenerateUsings(codeOutputList.GetUsings());
                if (!string.IsNullOrWhiteSpace(usings))
                {
                    _fileManagementService.WriteLine("");
                    _fileManagementService.WriteLine(usings.Trim());
                }

                var ns = _fileHeaderFooter.Namespace;
                if (!string.IsNullOrWhiteSpace(ns))
                {
                    _fileManagementService.WriteLine("");
                    _fileManagementService.WriteLine(ns.Trim());
                }
            }

            // Write the pre header info with the database context and it's interface
            if (codeOutputList.Files.ContainsKey(contextInterface)) WriteCodeOutput(codeGenerator, codeOutputList.Files[contextInterface], true, firstInGroup: true);
            if (codeOutputList.Files.ContainsKey(contextClass)) WriteCodeOutput(codeGenerator, codeOutputList.Files[contextClass], true, firstInGroup: true);
            if (codeOutputList.Files.ContainsKey(contextFactory)) WriteCodeOutput(codeGenerator, codeOutputList.Files[contextFactory], true);
            if (codeOutputList.Files.ContainsKey(contextFakeClass)) WriteCodeOutput(codeGenerator, codeOutputList.Files[contextFakeClass], true, firstInGroup: true);
            if (codeOutputList.Files.ContainsKey(contextFakeDbSet)) WriteCodeOutput(codeGenerator, codeOutputList.Files[contextFakeDbSet], true);

            WriteCodeOutputForGroup(codeGenerator, "POCO classes", true,
                codeOutputList.Files
                    .Where(x => x.Key.StartsWith(pocoClass))
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToList());

            WriteCodeOutputForGroup(codeGenerator, "POCO Configuration", true,
                codeOutputList.Files
                    .Where(x => x.Key.StartsWith(pocoConfiguration))
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToList());

            WriteCodeOutputForGroup(codeGenerator, "Enumerations", true,
                codeOutputList.Files
                    .Where(x => x.Key.StartsWith(enumType))
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToList());

            WriteCodeOutputForGroup(codeGenerator, "Owned entity classes", true,
                codeOutputList.Files
                    .Where(x => x.Key.StartsWith(ownedEntityClass))
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToList());

            WriteCodeOutputForGroup(codeGenerator, "Stored procedure return models", true,
                codeOutputList.Files
                    .Where(x => x.Key.StartsWith(spReturnModels))
                    .OrderBy(x => x.Key)
                    .Select(x => x.Value)
                    .ToList());

            if (!Settings.GenerateSeparateFiles)
                _fileManagementService.WriteLine(_fileHeaderFooter.Footer);
        }

        private void WriteCodeOutputForGroup(CodeGenerator codeGenerator, string regionNameForGroup, bool writePreHeaderInfo, List<CodeOutput> list)
        {
            var count = 0;
            var max = list.Count;
            foreach (var co in list)
            {
                ++count;
                WriteCodeOutput(codeGenerator, co, writePreHeaderInfo, regionNameForGroup, count == 1, count == max);
            }
        }

        private void WriteCodeOutput(CodeGenerator codeGenerator, CodeOutput code, bool writePreHeaderInfo, string regionNameForGroup = null, bool firstInGroup = false, bool lastInGroup = false)
        {
            if (Settings.GenerateSeparateFiles)
            {
                _fileManagementService.EndBlock();
                _fileManagementService.StartNewFile(code.Filename);

                // If generating separate files, check if the db context is the same name as the tt filename.
                // If it is the same, force writing to outer.
                if (Path.GetFileNameWithoutExtension(code.Filename).Equals(Settings.TemplateFile, StringComparison.CurrentCultureIgnoreCase))
                    _fileManagementService.ForceWriteToOuter = true;

                if (writePreHeaderInfo)
                {
                    var preHeader = _preHeaderInfo.ToString();
                    if (!string.IsNullOrWhiteSpace(preHeader))
                        _fileManagementService.WriteLine(preHeader.Trim());
                }

                var header = _fileHeaderFooter.Header;
                if (!string.IsNullOrWhiteSpace(header))
                    _fileManagementService.WriteLine(header.Trim());

                var fileUsings = code.GetUsings();
                if (Settings.UseFolderNameInNamespace)
                {
                    var ownNs = _fileHeaderFooter.GetNamespaceName(code.SubFolder);
                    fileUsings = fileUsings.Where(u => u != ownNs).ToList();
                }
                var usings = codeGenerator.GenerateUsings(fileUsings);
                if (!string.IsNullOrWhiteSpace(usings))
                {
                    _fileManagementService.WriteLine("");
                    _fileManagementService.WriteLine(usings.Trim());
                }

                var ns = Settings.UseFolderNameInNamespace
                    ? _fileHeaderFooter.GetNamespaceBlock(code.SubFolder)
                    : _fileHeaderFooter.Namespace;
                if (!string.IsNullOrWhiteSpace(ns))
                {
                    _fileManagementService.WriteLine("");
                    _fileManagementService.WriteLine(ns.Trim());
                }
            }

            WriteLines(IndentCode(code, regionNameForGroup, firstInGroup, lastInGroup));

            if (Settings.GenerateSeparateFiles)
                _fileManagementService.WriteLine(_fileHeaderFooter.Footer);

            _fileManagementService.ForceWriteToOuter = false;
        }

        /// <summary>
        ///     The comment banner written above the first class in each group of generated code, without indentation. Empty
        ///     when the licence needs no banner.
        /// </summary>
        public static List<string> LicenceBanner(LicenceType licenceType)
        {
            const string rule = "// ****************************************************************************************************";

            switch (licenceType)
            {
                case LicenceType.Commercial:
                    return new List<string>();

                case LicenceType.Academic:
                    return new List<string> { rule, "// Academic licence: for classroom learning and academic purposes only. Not for commercial use.", rule };

                case LicenceType.Trial:
                    return new List<string> { rule, "// This is not a commercial licence, therefore only a few tables/views/stored procedures are generated.", rule };

                default:
                    throw new ArgumentOutOfRangeException(nameof(licenceType), licenceType, null);
            }
        }

        private List<string> IndentCode(CodeOutput output, string regionNameForGroup, bool firstInGroup, bool lastInGroup)
        {
            if (output == null)
                return null;

            var indentNum = (Settings.UseNamespace && !Settings.UseFileScopedNamespaces) ? 1 : 0;
            var useRegion = !Settings.GenerateSeparateFiles && !string.IsNullOrEmpty(output.Region);

            var lines = new List<string>();
            if (Settings.UseRegions && useRegion)
            {
                lines.Add(IndentedStringBuilder(indentNum, "#region " + output.Region));
                lines.Add(null); // Add blank line after the region
            }
            else
            {
                if (Settings.UseRegions && !Settings.GenerateSeparateFiles && firstInGroup && !string.IsNullOrEmpty(regionNameForGroup))
                {
                    lines.Add(IndentedStringBuilder(indentNum, "#region " + regionNameForGroup));
                    lines.Add(null); // Add blank line after the region group
                }
            }

            var banner = firstInGroup ? LicenceBanner(_licenceType) : new List<string>();
            if (banner.Count > 0)
            {
                lines.AddRange(IndentedStringBuilder(indentNum, banner));
                lines.Add(null);
            }

            if (Settings.IncludeCodeGeneratedAttribute)
                lines.Add(IndentedStringBuilder(indentNum, _codeGeneratedAttribute));

            lines.AddRange(IndentedStringBuilder(indentNum, output.Code));

            if (useRegion || (!Settings.GenerateSeparateFiles && lastInGroup && !string.IsNullOrEmpty(regionNameForGroup)))
            {
                if (Settings.UseRegions)
                {
                    lines.Add(null); // Include blank line after #endregion
                    lines.Add(IndentedStringBuilder(indentNum, "#endregion"));
                    lines.Add(null); // Include blank line after #endregion
                }
                else
                {
                    lines.Add(null); // Include blank line

                }
            }

            return lines;
        }

        protected string IndentedStringBuilder(int indentNum, string line)
        {
            var indent = new string(' ', indentNum * Indent);
            return string.IsNullOrWhiteSpace(line) ? string.Empty : string.Format("{0}{1}", indent, line);
        }

        protected IEnumerable<string> IndentedStringBuilder(int indentNum, List<string> lines)
        {
            var indent = new string(' ', indentNum * Indent);

            return
            (
                from line in lines
                select string.IsNullOrWhiteSpace(line) ? string.Empty : string.Format("{0}{1}", indent, line)
            );
        }

        private void WriteLines(List<string> lines)
        {
            foreach (var line in lines)
                _fileManagementService.WriteLine(line);
        }

        private void BuildPreHeaderInfo(Licence licence)
        {
            if (Settings.ShowLicenseInfo)
            {
                _preHeaderInfo.Append("// This code was generated by EntityFramework Reverse POCO Generator");
                if (Settings.IncludeGeneratorVersionInCode)
                {
                    _preHeaderInfo.Append(" ");
                    _preHeaderInfo.Append(EfrpgVersion.Version());
                }

                _preHeaderInfo.AppendLine(" (http://www.reversepoco.co.uk/).");

                _preHeaderInfo.AppendLine("// Created by Simon Hughes (https://about.me/simon.hughes).");
                _preHeaderInfo.AppendLine("//");
                _preHeaderInfo.AppendLine(string.Format("// {0}{1}", LicenceConstants.RegisteredTo, licence.RegisteredTo));
                _preHeaderInfo.AppendLine(string.Format("// {0}{1}", LicenceConstants.Company, licence.Company));
                _preHeaderInfo.AppendLine(string.Format("// {0}{1}", LicenceConstants.LicenceType, licence.GetLicenceType()));
                _preHeaderInfo.AppendLine(string.Format("// {0}{1}", LicenceConstants.NumLicences, licence.NumLicences));
                _preHeaderInfo.AppendLine(string.Format("// {0}{1}", LicenceConstants.ValidUntil,
                    licence.ValidUntil.ToString(LicenceConstants.ExpiryFormat).ToUpperInvariant()));
                _preHeaderInfo.AppendLine("//");
            }

            if (Settings.IncludeConnectionSettingComments)
            {
                _preHeaderInfo.AppendLine("// The following connection settings were used to generate this file:");

                if (!string.IsNullOrEmpty(Settings.ConnectionStringName))
                    _preHeaderInfo.AppendLine(string.Format("//     Connection String Name: \"{0}\"", Settings.ConnectionStringName));

                _preHeaderInfo.AppendLine(string.Format("//     Connection String:      \"{0}\"", ZapPassword(Settings.ConnectionString)));

                _preHeaderInfo.AppendLine("//");
            }
        }

        private string ZapPassword(string conn)
        {
            var rx = new Regex("password=[^\";]*", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return rx.Replace(conn, "password=**zapped**;");
        }

        private string FormatError(Exception ex)
        {
            return ex.Message.Replace("\r\n", "\n").Replace("\n", " ");
        }
    }
}
