using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.TemplateModels;
using Efrpg.Templates;

namespace Efrpg.Generators
{
    public class CodeGenerator
    {
        private readonly Generator _generator;
        private readonly IDbContextFilter _filter;
        private readonly List<TableTemplateData> _tables;
        private readonly List<StoredProcTemplateData> _storedProcs;
        private readonly List<string> _globalUsings;
        private readonly Template _template;
        private readonly List<TableValuedFunctionsTemplateData> _tableValuedFunctions;
        private readonly List<ScalarValuedFunctionsTemplateData> _scalarValuedFunctions;
        private readonly List<string> _tableValuedFunctionComplexTypes;

        private readonly bool _hasTables, _hasStoredProcs, _hasTableValuedFunctions, _hasScalarValuedFunctions, _hasTableValuedFunctionComplexTypes, _hasEnums;

        public CodeGenerator(Generator generator, IDbContextFilter filter)
        {
#pragma warning disable IDE0016 // Use 'throw' expression
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (filter == null) throw new ArgumentNullException(nameof(filter));
#pragma warning restore IDE0016 // Use 'throw' expression

            var isEfCore = !Settings.IsEf6();
            var IsEfCore8Plus = Settings.IsEfCore8Plus();

            _generator = generator;
            _filter = filter;

            _tables = filter.Tables
                .Where(t => !t.IsMapping && (t.HasPrimaryKey || (t.IsView && IsEfCore8Plus)))
                .OrderBy(x => x.NameHumanCase)
                .Select(tbl => new TableTemplateData(tbl))
                .ToList();

            if (filter.IncludeStoredProcedures)
            {
                // Every EXEC below is T-SQL. Any other provider gets its own call statement in all three places.
                var returnsProcResult = StoredProcedure.ReturnsProcResult();
                _storedProcs = filter.StoredProcs
                    .Where(s => s.IsStoredProcedure)
                    .OrderBy(x => x.NameHumanCase)
                    .Select(sp => new StoredProcTemplateData(
                        sp.ReturnModels.Count == 0,
                        sp.ReturnModels.Count > 0,
                        sp.ReturnModels.Count == 1,
                        sp.ReturnModels.Count > 1,
                        sp.WriteStoredProcReturnType(_filter),
                        sp.WriteStoredProcReturnModelName(filter),
                        sp.WriteStoredProcFunctionName(filter),
                        sp.WriteStoredProcFunctionParams(false, false, false),
                        sp.WriteStoredProcFunctionParams(false, true, false),
                        sp.WriteStoredProcFunctionParams(true, false, false),
                        sp.WriteStoredProcFunctionParams(true, true, false),
                        sp.WriteStoredProcFunctionParams(false, false, true),
                        sp.WriteStoredProcFunctionParams(false, true, true),
                        sp.WriteStoredProcFunctionParams(true, false, true),
                        sp.WriteStoredProcFunctionParams(true, true, true),
                        !sp.StoredProcCanExecuteAsync(),
                        sp.WriteStoredProcFunctionOverloadCall(),
                        sp.WriteStoredProcFunctionSetSqlParameters(false),
                        sp.WriteStoredProcFunctionSetSqlParameters(true),
                        !returnsProcResult
                            ? sp.WriteStoredProcFunctionCallSql()
                            : sp.ReturnModels.Count == 1
                            ? // exec
                            string.Format("EXEC @procResult = [{0}].[{1}] {2}", sp.Schema.DbName, sp.DbName, sp.WriteStoredProcFunctionSqlAtParams()).Trim()
                            : string.Format("[{0}].[{1}]", sp.Schema.DbName, sp.DbName),
                        !returnsProcResult
                            ? sp.WriteStoredProcFunctionCallSql()
                            : sp.ReturnModels.Count == 0
                            ? // Async exec
                            string.Format("EXEC @procResult = [{0}].[{1}] {2}", sp.Schema.DbName, sp.DbName, sp.WriteStoredProcFunctionSqlAtParams()).Trim()
                            : sp.ReturnModels.Count == 1
                                ?
                                string.Format("EXEC [{0}].[{1}] {2}", sp.Schema.DbName, sp.DbName, sp.WriteStoredProcFunctionSqlAtParams()).Trim()
                                : string.Format("[{0}].[{1}]", sp.Schema.DbName, sp.DbName),
                        sp.WriteStoredProcReturnModelName(_filter),
                        sp.WriteStoredProcFunctionSqlParameterAnonymousArray(true, true, false),
                        sp.WriteStoredProcFunctionSqlParameterAnonymousArray(false, true, false),
                        sp.WriteStoredProcFunctionSqlParameterAnonymousArray(true, true, true, IsEfCore8Plus),
                        sp.WriteStoredProcFunctionSqlParameterAnonymousArray(false, true, true, IsEfCore8Plus),
                        sp.WriteStoredProcFunctionDeclareSqlParameter(true),
                        sp.WriteStoredProcFunctionDeclareSqlParameter(false),
                        sp.Parameters.OrderBy(x => x.Ordinal).Select(sp.WriteStoredProcSqlParameterName).ToList(),
                        sp.ReturnModels.Count,
                        !returnsProcResult
                            ? sp.WriteStoredProcFunctionCallSql()
                            : string.Format("EXEC @procResult = [{0}].[{1}] {2}", sp.Schema.DbName, sp.DbName, sp.WriteStoredProcFunctionSqlAtParams()),
                        !string.IsNullOrEmpty(sp.Error),
                        sp.Error ?? string.Empty
                    )
                    {
                        HasProcResult = returnsProcResult,
                        ExecutesAsCommand = sp.ExecutesAsStoredProcedureCommand(),
                        CommandText = sp.WriteStoredProcFunctionQualifiedName(),
                        CommandParameters = "new[] { " +
                                            string.Join(", ", sp.Parameters.OrderBy(x => x.Ordinal).Select(sp.WriteStoredProcSqlParameterName)) + " }"
                    })
                    .ToList();
            }
            else
                _storedProcs = new List<StoredProcTemplateData>();

            // When not using data annotations, populate fluent HasColumnName mappings for EF Core
            if (!Settings.UseDataAnnotations && isEfCore && _storedProcs.Count > 0)
            {
                var builderCmd = IsEfCore8Plus ? "Entity" : "Query";
                var spList = filter.StoredProcs.Where(s => s.IsStoredProcedure).OrderBy(x => x.NameHumanCase).ToList();
                for (var i = 0; i < _storedProcs.Count && i < spList.Count; i++)
                    _storedProcs[i].ColumnMappings = spList[i].GetReturnColumnMappings(builderCmd, spList[i].WriteStoredProcReturnModelName(_filter));
            }

            if (filter.IncludeTableValuedFunctions)
            {
                _tableValuedFunctions = filter.StoredProcs
                    .Where(s => s.IsTableValuedFunction)
                    .OrderBy(x => x.NameHumanCase)
                    .Select(tvf => new TableValuedFunctionsTemplateData(
                        tvf.ReturnModels.Count == 1 && tvf.ReturnModels[0].Count == 1,
                        tvf.ReturnModels.Count == 1 && tvf.ReturnModels[0].Count == 1 ? tvf.ReturnModels[0][0].ColumnName : null,
                        tvf.WriteStoredProcFunctionName(_filter),
                        tvf.WriteStoredProcReturnModelName(_filter),
                        tvf.WriteStoredProcFunctionParams(false, true),
                        tvf.WriteStoredProcFunctionParams(false, false),
                        tvf.DbName,
                        tvf.Schema.DbName,
                        isEfCore ? tvf.WriteStoredProcFunctionDeclareSqlParameter(false) : tvf.WriteTableValuedFunctionDeclareSqlParameter(),
                        isEfCore
                            ? tvf.WriteStoredProcFunctionSqlParameterAnonymousArray(false, false)
                            : tvf.WriteTableValuedFunctionSqlParameterAnonymousArray(),
                        isEfCore ? tvf.WriteNetCoreTableValuedFunctionsSqlAtParams() : tvf.WriteStoredProcFunctionSqlAtParams(),
                        IsEfCore8Plus ? "FromSqlRaw" : "FromSql",
                        IsEfCore8Plus ? "Set" : "Query",
                        IsEfCore8Plus ? "Entity" : "Query",
                        IsEfCore8Plus ? ".HasNoKey()" : string.Empty,
                        !Settings.StoredProcedureReturnTypes.ContainsKey(tvf.NameHumanCase) && !Settings.StoredProcedureReturnTypes.ContainsKey(tvf.DbName)
                    )
                    {
                        QualifiedName = tvf.WriteStoredProcFunctionQualifiedName()
                    })
                    .ToList();

                _tableValuedFunctionComplexTypes = filter.StoredProcs
                    .Where(s => s.IsTableValuedFunction &&
                                !Settings.StoredProcedureReturnTypes.ContainsKey(s.NameHumanCase) &&
                                !Settings.StoredProcedureReturnTypes.ContainsKey(s.DbName))
                    .OrderBy(x => x.NameHumanCase)
                    .Select(x => x.WriteStoredProcReturnModelName(_filter))
                    .ToList();

                // When not using data annotations, populate fluent HasColumnName mappings for EF Core TVFs
                if (!Settings.UseDataAnnotations && isEfCore && _tableValuedFunctions.Count > 0)
                {
                    var builderCmd = IsEfCore8Plus ? "Entity" : "Query";
                    var tvfList = filter.StoredProcs.Where(s => s.IsTableValuedFunction).OrderBy(x => x.NameHumanCase).ToList();
                    for (var i = 0; i < _tableValuedFunctions.Count && i < tvfList.Count; i++)
                        _tableValuedFunctions[i].ColumnMappings = tvfList[i].GetReturnColumnMappings(builderCmd, tvfList[i].WriteStoredProcReturnModelName(_filter));
                }
            }
            else
            {
                _tableValuedFunctions = new List<TableValuedFunctionsTemplateData>();
                _tableValuedFunctionComplexTypes = new List<string>();
            }

            if (filter.IncludeScalarValuedFunctions)
            {
                _scalarValuedFunctions = filter.StoredProcs
                    .Where(s => s.IsScalarValuedFunction &&
                                s.Parameters.Any(x => x.Mode == StoredProcedureParameterMode.Out))
                    .OrderBy(x => x.NameHumanCase)
                    .Select(svf => new ScalarValuedFunctionsTemplateData(
                        svf.WriteStoredProcFunctionName(_filter),
                        svf.Parameters.Where(x => x.Mode == StoredProcedureParameterMode.Out).OrderBy(x => x.Ordinal).FirstOrDefault()?.PropertyType,
                        svf.WriteStoredProcFunctionParams(false, true),
                        svf.WriteStoredProcFunctionParams(false, false),
                        svf.DbName,
                        svf.Schema.DbName
                    )
                    {
                        DbFunctionArguments = svf.WriteDbFunctionArguments()
                    })
                    .ToList();
            }
            else
                _scalarValuedFunctions = new List<ScalarValuedFunctionsTemplateData>();

            var returnModelsUsed = new List<string>();
            foreach (var sp in _storedProcs)
            {
                if (returnModelsUsed.Contains(sp.ReturnModelName))
                    sp.CreateDbSetForReturnModel = false;
                else
                    returnModelsUsed.Add(sp.ReturnModelName);
            }

            _hasTables = _tables.Any();
            _hasStoredProcs = _storedProcs.Any();
            _hasTableValuedFunctions = _tableValuedFunctions.Any();
            _hasScalarValuedFunctions = _scalarValuedFunctions.Any();
            _hasTableValuedFunctionComplexTypes = _tableValuedFunctionComplexTypes.Any();
            _hasEnums = filter.Enums.Any();

            _globalUsings = new List<string>();
            _template = TemplateFactory.Create();
            CalcGlobalUsings();
        }

        private void CalcGlobalUsings()
        {
            _globalUsings.AddRange(Settings.AdditionalNamespaces.Where(x => !string.IsNullOrEmpty(x)).Distinct());

            if ((Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration) ||
                 Settings.ElementsToGenerate.HasFlag(Elements.Context) ||
                 Settings.ElementsToGenerate.HasFlag(Elements.Interface)) &&
                (!Settings.ElementsToGenerate.HasFlag(Elements.Poco) && !string.IsNullOrWhiteSpace(Settings.PocoNamespace)))
                _globalUsings.Add(Settings.PocoNamespace);

            if (Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration) &&
                (!Settings.ElementsToGenerate.HasFlag(Elements.Context) && !string.IsNullOrWhiteSpace(Settings.ContextNamespace)))
                _globalUsings.Add(Settings.ContextNamespace);

            if (Settings.ElementsToGenerate.HasFlag(Elements.Context) &&
                (!Settings.ElementsToGenerate.HasFlag(Elements.Interface) && !string.IsNullOrWhiteSpace(Settings.InterfaceNamespace)))
                _globalUsings.Add(Settings.InterfaceNamespace);

            if (Settings.ElementsToGenerate.HasFlag(Elements.Context) &&
                (!Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration) && !string.IsNullOrWhiteSpace(Settings.PocoConfigurationNamespace)))
                _globalUsings.Add(Settings.PocoConfigurationNamespace);

            if (Settings.UseFolderNameInNamespace && Settings.UseNamespace && Settings.GenerateSeparateFiles)
            {
                // When folder names are appended to namespaces, each file needs using statements
                // for all the other folder-derived namespaces so cross-folder references compile.
                var baseNs = (Settings.Namespace + _filter.SubNamespace).Trim().Replace(' ', '_');
                AddFolderNamespaceUsing(baseNs, Settings.ContextFolder);
                AddFolderNamespaceUsing(baseNs, Settings.InterfaceFolder);
                AddFolderNamespaceUsing(baseNs, Settings.PocoFolder);
                AddFolderNamespaceUsing(baseNs, Settings.PocoConfigurationFolder);
            }
        }

        private void AddFolderNamespaceUsing(string baseNs, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;
            var folderNs = folder.Trim('\\', '/').Replace('\\', '.').Replace('/', '.');
            if (string.IsNullOrWhiteSpace(folderNs))
                return;
            var ns = baseNs + "." + folderNs;
            if (!_globalUsings.Contains(ns))
                _globalUsings.Add(ns);
        }

        private bool CanWriteInterface()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Interface) &&
                   !string.IsNullOrWhiteSpace(Settings.DbContextInterfaceName) &&
                   (_hasTables || _hasStoredProcs || _hasTableValuedFunctions || _hasScalarValuedFunctions);
        }

        private bool CanWriteFactory()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Context) &&
                   Settings.AddIDbContextFactory &&
                   (_hasTables || _hasStoredProcs || _hasTableValuedFunctions || _hasScalarValuedFunctions);
        }

        private bool CanWriteContext()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Context) &&
                   (_hasTables || _hasStoredProcs || _hasTableValuedFunctions || _hasScalarValuedFunctions);
        }

        private bool CanWriteFakeContext()
        {
            return Settings.AddUnitTestingDbContext &&
                   Settings.ElementsToGenerate.HasFlag(Elements.Context) &&
                   (_hasTables || _hasStoredProcs || _hasTableValuedFunctions || _hasScalarValuedFunctions);
        }

        private bool CanWritePoco()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Poco) && _hasTables;
        }

        private bool CanWritePocoConfiguration()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.PocoConfiguration) && _hasTables;
        }

        private bool CanWriteStoredProcReturnModel()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Poco) && (_hasStoredProcs || _hasTableValuedFunctions);
        }

        private bool CanWriteEnums()
        {
            return Settings.ElementsToGenerate.HasFlag(Elements.Enum) && _hasEnums;
        }

        private bool CanWriteOwnedEntityClasses()
        {
            return !Settings.IsEf6() &&
                   Settings.ElementsToGenerate.HasFlag(Elements.Poco);
        }

        public string GenerateUsings(List<string> usings)
        {
            return !usings.Any() ? null : Template.Transform(_template.Usings(), usings).Trim();
        }

        // Several EF Core templates carry hard-coded nullable reference annotations: the interface and
        // fake context/DbSet (Find/FindAsync, ToString), the DbContext (IConfiguration?) and the
        // DbContext factory (DbContextOptions<>?). The compiler ignores the project-wide nullable
        // setting for auto-generated files, so without an explicit '#nullable' directive those
        // annotations raise CS8669. Enable only the annotations context (not warnings) so the '?' is
        // legal without forcing CS8618 on the DbContext's non-initialised DbSet<T> properties. Bracket
        // only when the file header doesn't already carry '#nullable enable' (NeedsNullForgiving() == false).
        //
        // The block must close with '#nullable disable', not '#nullable restore': restore returns to the
        // *project* nullable setting, so in a consumer project with <Nullable>enable</Nullable> everything
        // after this block in the combined single-file output (POCOs, stored procedure return models)
        // would silently become NRT-enabled. EF Core 8+ then infers 'string' result columns as required
        // and throws SqlNullValueException when the database returns NULL (issue #885). '#nullable disable'
        // returns to the auto-generated file's baseline (disabled), keeping the rest of the file oblivious.
        private static void AddCodeWithNullableContext(CodeOutput co, string template, object data)
        {
            var bracket = !Settings.NeedsNullForgiving() && Settings.IsEfCore8Plus();
            if (bracket)
                co.AddCode("#nullable enable annotations");
            co.AddCode(Template.Transform(template, data));
            if (bracket)
                co.AddCode("#nullable disable");
        }

        public CodeOutput GenerateInterface()
        {
            var filename = Settings.DbContextInterfaceName + FileManagementService.Extension;
            if (!CanWriteInterface())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var data = new InterfaceModel
            {
                interfaceModifier = Settings.DbContextInterfaceModifiers ?? "public partial",
                DbContextInterfaceName = Settings.DbContextInterfaceName,
                DbContextInterfaceBaseClasses = Settings.DbContextInterfaceBaseClasses,
                DbContextName = Settings.DbContextName,
                tables = _tables.Where(x => x.DbSetModifier == "public").ToList(),
                AdditionalContextInterfaceItems = Settings.AdditionalContextInterfaceItems.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList(),
                addSaveChanges = !Settings.UseInheritedBaseInterfaceFunctions,
                storedProcs = _storedProcs,
                hasStoredProcs = _hasStoredProcs,
                tableValuedFunctions = _tableValuedFunctions,
                scalarValuedFunctions = _scalarValuedFunctions,
                hasTableValuedFunctions = _hasTableValuedFunctions && _filter.IncludeTableValuedFunctions,
                hasScalarValuedFunctions = _hasScalarValuedFunctions && _filter.IncludeScalarValuedFunctions,
            };

            var co = new CodeOutput(string.Empty, filename, "Database context interface", Settings.InterfaceFolder, _globalUsings);
            co.AddUsings(_template.DatabaseContextInterfaceUsings(data));
            AddCodeWithNullableContext(co, _template.DatabaseContextInterface(), data);

            return co;
        }

        public CodeOutput GenerateContext()
        {
            var filename = Settings.DbContextName + FileManagementService.Extension;
            if (!CanWriteContext())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var indexes = new List<string>();
            var hasSpatialTypes = false;
            var hasHierarchyIdType = false;
            foreach (var table in _tables)
            {
                var columnsQuery = Settings.OrderProperties == OrderProperties.Ordinal ? table.Table.Columns.OrderBy(x => x.Ordinal) : table.Table.Columns.OrderBy(x => x.NameHumanCase);
                
                var columns = columnsQuery
                    .Where(x => !x.Hidden && !string.IsNullOrEmpty(x.Config))
                    .ToList();

                if (!Settings.DisableGeographyTypes && !hasSpatialTypes)
                    hasSpatialTypes = columns.Any(x => x.IsSpatial);

                if (!hasHierarchyIdType && columns.Any(x => x.SqlPropertyType.Equals("hierarchyid", StringComparison.InvariantCultureIgnoreCase)))
                    hasHierarchyIdType = true;

                indexes.AddRange(columns
                    .Select(_generator.IndexModelBuilder)
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            var IsEfCore8Plus = Settings.IsEfCore8Plus();

            var data = new ContextModel
            {
                DbContextClassModifiers = Settings.DbContextClassModifiers,
                DbContextName = Settings.DbContextName,
                DbContextBaseClass = Settings.DbContextBaseClass,
                AddParameterlessConstructorToDbContext = Settings.AddParameterlessConstructorToDbContext,
                HasDefaultConstructorArgument = !string.IsNullOrEmpty(Settings.DefaultConstructorArgument),
                DefaultConstructorArgument = Settings.DefaultConstructorArgument,
                ConfigurationClassName = Settings.ConfigurationClassName,
                ConnectionString = Settings.ConnectionString,
                ConnectionStringName = Settings.ConnectionStringName,
                ConnectionStringActions = GetConnectionStringActions(hasSpatialTypes, hasHierarchyIdType),
                contextInterface = string.IsNullOrWhiteSpace(Settings.DbContextInterfaceName) ? "" : ", " + Settings.DbContextInterfaceName,
                setInitializer = string.Format("<{0}>(null);", Settings.DbContextName),
                DbContextClassIsPartial = Settings.DbContextClassIsPartial(),
                tables = _tables,
                hasTables = _hasTables,
                indexes = indexes,
                storedProcs = _storedProcs,
                hasStoredProcs = _hasStoredProcs,
                tableValuedFunctionComplexTypes = _tableValuedFunctionComplexTypes,
                hasTableValuedFunctionComplexTypes = _hasTableValuedFunctionComplexTypes,
                AdditionalContextInterfaceItems = Settings.AdditionalContextInterfaceItems.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList(),
                addSaveChanges = !Settings.UseInheritedBaseInterfaceFunctions,
                tableValuedFunctions = _tableValuedFunctions,
                scalarValuedFunctions = _scalarValuedFunctions,
                Sequences = _filter.Sequences,
                hasTableValuedFunctions = _hasTableValuedFunctions && _filter.IncludeTableValuedFunctions,
                hasScalarValuedFunctions = _hasScalarValuedFunctions && _filter.IncludeScalarValuedFunctions,
                IncludeObjectContextConstructor = !Settings.DbContextBaseClass.Contains("IdentityDbContext"),
                QueryString = IsEfCore8Plus ? "Set" : "Query",
                FromSql = IsEfCore8Plus ? "FromSqlRaw" : "FromSql",
                ExecuteSqlCommand = IsEfCore8Plus ? "ExecuteSqlRaw" : "ExecuteSqlCommand",
                StoredProcModelBuilderCommand = IsEfCore8Plus ? "Entity" : "Query",
                StoredProcModelBuilderPostCommand = IsEfCore8Plus ? ".HasNoKey()" : string.Empty,
                OnConfigurationUsesConfiguration = Settings.OnConfiguration == OnConfiguration.Configuration,
                OnConfigurationUsesConnectionString = Settings.OnConfiguration == OnConfiguration.ConnectionString,
                DefaultSchema = Settings.DefaultSchema,
                UseDatabaseProvider = Settings.DatabaseProvider(),
                NeedsServerVersion = Settings.DatabaseType == DatabaseType.MySql, // Pomelo's UseMySql has no overload without one
                UseLazyLoadingProxies = Settings.UseLazyLoading && Settings.IsEfCore8Plus(),
                SqlParameter = Settings.SqlParameter(),
                SqlParameterValue = Settings.SqlParameterValue(),
                Triggers = _tables.Where(x => !string.IsNullOrEmpty(x.Table.TriggerName) || x.Table.Columns.Any(c => c.IsComputed))
                                                                .Select(x => new Trigger { TableName = x.Table.NameHumanCaseWithSuffix(), TriggerName = x.Table.TriggerName ?? "HasComputedColumn" }).ToList(),
                MemoryOptimisedTables = _tables.Where(x => x.Table.IsMemoryOptimised).Select(x => x.Table.NameHumanCaseWithSuffix()).ToList()
            };

            data.hasIndexes = data.indexes.Any();
            data.hasTriggers = data.Triggers.Any();
            data.hasSequences = data.Sequences.Any();
            data.hasMemoryOptimisedTables = data.MemoryOptimisedTables.Any();

            var co = new CodeOutput(string.Empty, filename, "Database context", Settings.ContextFolder, _globalUsings);
            co.AddUsings(_template.DatabaseContextUsings(data));
            AddCodeWithNullableContext(co, _template.DatabaseContext(), data);

            return co;
        }

        // The namespaces PostgreSQL's own column types live in. In one generated file the context's usings happened to
        // cover NpgsqlTypes and System.Collections, but nothing imported PhysicalAddress's namespace, and a POCO in a
        // file of its own had none of them.
        private static List<string> ColumnTypeNamespaces(Table table)
        {
            var namespaces = new List<string>();
            foreach (var propertyType in table.Columns.Where(x => !x.Hidden).Select(x => x.PropertyType ?? string.Empty))
            {
                if (propertyType.StartsWith("BitArray"))
                    namespaces.Add("System.Collections");
                else if (propertyType.StartsWith("PhysicalAddress"))
                    namespaces.Add("System.Net.NetworkInformation");
                else if (propertyType.StartsWith("Npgsql"))
                    namespaces.Add("NpgsqlTypes");
            }

            return namespaces.Distinct().ToList();
        }

        private static string GetConnectionStringActions(bool hasSpatialTypes, bool hasHierarchyIdType)
        {
            if (Settings.IsEf6())
                return string.Empty;

            var body = string.Empty;

            if (hasSpatialTypes && hasHierarchyIdType)
                body = ".UseNetTopologySuite().UseHierarchyId()";
            else if (hasSpatialTypes)
                body = ".UseNetTopologySuite()";
            else if (hasHierarchyIdType)
                body = ".UseHierarchyId()";

            body += Settings.ConnectionStringActions ?? string.Empty;

            return string.IsNullOrEmpty(body) ? string.Empty : ", x => x" + body;
        }

        private static string GetPropertyInitialiser(Column col)
        {
            // If using property initialisers and there's a default value, use it
            if (Settings.UsePropertyInitialisers && !string.IsNullOrWhiteSpace(col.Default))
                return string.Format(" = {0};", col.Default);

            // For non-nullable reference types, add null-forgiving operator to satisfy nullable reference types
            // Only apply this when nullable reference types are enabled
            if (!Settings.NeedsNullForgiving())
                return string.Empty;

            // Check if this is a reference type
            var isReferenceType = IsReferenceType(col.PropertyType);

            // If it's a reference type and the database column is NOT nullable, add = null!;
            // Partial property declarations cannot have initializers — the implementation part handles initialization.
            if (isReferenceType && !col.IsNullable && !col.IsPartial && string.IsNullOrWhiteSpace(col.Default))
                return " = null!;";

            return string.Empty;
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
                   lowerType == "nettopologysuite.geometries.point" ||
                   lowerType == "nettopologysuite.geometries.geometry";
        }

        public CodeOutput GenerateFakeContext()
        {
            var filename = "Fake" + Settings.DbContextName + FileManagementService.Extension;
            if (!CanWriteFakeContext())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var data = new FakeContextModel
            {
                DbContextClassModifiers = Settings.DbContextClassModifiers,
                DbContextName = Settings.DbContextName,
                DbContextBaseClass = Settings.DbContextBaseClass,
                contextInterface = string.IsNullOrWhiteSpace(Settings.DbContextInterfaceName) ? "" : " : " + Settings.DbContextInterfaceName,
                DbContextClassIsPartial = Settings.DbContextClassIsPartial(),
                tables = _tables,
                storedProcs = _storedProcs,
                hasStoredProcs = _hasStoredProcs,
                tableValuedFunctions = _tableValuedFunctions,
                scalarValuedFunctions = _scalarValuedFunctions,
                hasTableValuedFunctions = _hasTableValuedFunctions && _filter.IncludeTableValuedFunctions,
                hasScalarValuedFunctions = _hasScalarValuedFunctions && _filter.IncludeScalarValuedFunctions,
            };

            var co = new CodeOutput(string.Empty, filename, "Fake Database context", Settings.ContextFolder, _globalUsings);
            co.AddUsings(_template.FakeDatabaseContextUsings(data, _filter));
            if (Settings.FakeDbContextInDebugOnlyMode)
                co.AddCode("#if DEBUG");
            AddCodeWithNullableContext(co, _template.FakeDatabaseContext(), data);
            if (Settings.FakeDbContextInDebugOnlyMode)
                co.AddCode("#endif");

            return co;
        }

        public CodeOutput GenerateFakeDbSet()
        {
            var filename = "FakeDbSet" + FileManagementService.Extension;
            if (!CanWriteFakeContext())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var providerAssemblyName = Settings.DatabaseProviderAssemblyName();

            var data = new FakeDbSetModel
            {
                DbContextClassModifiers    = Settings.DbContextClassModifiers,
                DbContextClassIsPartial    = Settings.DbContextClassIsPartial(),
                DatabaseProviderNameLiteral = providerAssemblyName == null
                    ? "null"
                    : "\"" + providerAssemblyName + "\"",
            };

            var co = new CodeOutput(string.Empty, filename, "Fake DbSet", Settings.ContextFolder, _globalUsings);
            co.AddUsings(_template.FakeDbSetUsings(data));
            if (Settings.FakeDbContextInDebugOnlyMode)
                co.AddCode("#if DEBUG");
            AddCodeWithNullableContext(co, _template.FakeDbSet(), data);
            if (Settings.FakeDbContextInDebugOnlyMode)
                co.AddCode("#endif");

            return co;
        }

        public CodeOutput GenerateFactory()
        {
            var filename = Settings.DbContextName + "Factory" + FileManagementService.Extension;
            if (!CanWriteFactory())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var data = new FactoryModel
            {
                classModifier = Settings.DbContextClassModifiers,
                contextName = Settings.DbContextName
            };

            var co = new CodeOutput(string.Empty, filename, "Database context factory", Settings.ContextFolder, _globalUsings);
            co.AddUsings(_template.DatabaseContextFactoryUsings(data));
            AddCodeWithNullableContext(co, _template.DatabaseContextFactory(), data);
            return co;
        }

        public CodeOutput GeneratePoco(Table table)
        {
            var filename = table.NameHumanCaseWithSuffix() + FileManagementService.Extension;
            if (!CanWritePoco())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var IsEfCore8Plus = Settings.IsEfCore8Plus();

            var columnsQuery = Settings.OrderProperties == OrderProperties.Ordinal ? table.Columns.OrderBy(x => x.Ordinal) : table.Columns.OrderBy(x => x.NameHumanCase);

            var data = new PocoModel
            {
                UseHasNoKey = IsEfCore8Plus && table.IsView && !table.HasPrimaryKey,
                HasNoPrimaryKey = !table.HasPrimaryKey,
                Name = table.DbName,
                NameHumanCaseWithSuffix = table.NameHumanCaseWithSuffix(),
                ClassModifier = columnsQuery
                    .Where(x => !x.Hidden && !x.ExistsInBaseClass)
                    .Any(x => x.IsPartial) ? "public partial" : Settings.EntityClassesModifiers,
                ClassComment = table.WriteComments(),
                ExtendedComments = table.WriteExtendedComments(),
                ClassAttributes = table.WriteClassAttributes(),
                BaseClasses = table.BaseClasses,
                InsideClassBody = Settings.WriteInsideClassBody(table),
                HasHierarchyId = table.Columns.Any(x => x.PropertyType.EndsWith("hierarchyid", StringComparison.InvariantCultureIgnoreCase)),
                HasSqlVector = table.Columns.Any(x => x.PropertyType.StartsWith("SqlVector", StringComparison.InvariantCultureIgnoreCase)) && Settings.IsEfCore10Plus(),
                ColumnTypeNamespaces = ColumnTypeNamespaces(table),
                Columns = columnsQuery
                    .Where(x => !x.Hidden && !x.ExistsInBaseClass)
                    .Select((col, index) => new PocoColumnModel
                    {
                        AddNewLineBefore = index > 0 && (((Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock || Settings.IncludeComments == CommentsStyle.InSummaryBlock) && !string.IsNullOrEmpty(col.SummaryComments)) || (col.Attributes != null && col.Attributes.Any())),
                        HasSummaryComments = (Settings.IncludeExtendedPropertyComments == CommentsStyle.InSummaryBlock || Settings.IncludeComments == CommentsStyle.InSummaryBlock) && !string.IsNullOrEmpty(col.SummaryComments),
                        SummaryComments = !string.IsNullOrEmpty(col.SummaryComments) ? SecurityElement.Escape(col.SummaryComments) : null,
                        Attributes = col.Attributes,
                        OverrideModifier = col.OverrideModifier,
                        IncludeFieldNameConstants = Settings.IncludeFieldNameConstants,
                        WrapIfNullable = col.WrapIfNullable(),
                        NameHumanCase = col.NameHumanCase,
                        PrivateSetterForComputedColumns = Settings.UsePrivateSetterForComputedColumns && col.IsComputed ? "private " : string.Empty,
                        PropertyInitialisers = GetPropertyInitialiser(col),
                        InlineComments = col.InlineComments,
                        IsPartial = col.IsPartial
                    })
                    .ToList(),
                HasReverseNavigation = table.ReverseNavigationProperty.Count > 0,
                ReverseNavigationProperty = table.ReverseNavigationProperty
                    .OrderBy(x => Settings.OrderProperties == OrderProperties.Ordinal ? x.Definition : x.PropertyName)
                    .Select(x => new PocoReverseNavigationPropertyModel
                    {
                        ReverseNavHasComment = Settings.IncludeComments != CommentsStyle.None && !string.IsNullOrEmpty(x.Comments),
                        ReverseNavComment = Settings.IncludeComments != CommentsStyle.None ? x.Comments : string.Empty,
                        AdditionalReverseNavigationsDataAnnotations = Settings.AdditionalReverseNavigationsDataAnnotations,
                        AdditionalDataAnnotations = x.AdditionalDataAnnotations,
                        Definition = x.Definition
                    })
                    .ToList(),
                HasForeignKey = table.HasForeignKey,
                ForeignKeyTitleComment = Settings.IncludeComments != CommentsStyle.None && table.Columns.SelectMany(x => x.EntityFk).Any() ? "    // Foreign keys" + Environment.NewLine : string.Empty,
                ForeignKeys = table.Columns
                    .SelectMany(x => x.EntityFk)
                    .OrderBy(x => Settings.OrderProperties == OrderProperties.Ordinal ? x.Definition : x.PropertyName)
                    .Select(x => new PocoForeignKeyModel
                    {
                        HasFkComment = Settings.IncludeComments != CommentsStyle.None && !string.IsNullOrEmpty(x.Comments),
                        FkComment = x.Comments,
                        AdditionalForeignKeysDataAnnotations = Settings.AdditionalForeignKeysDataAnnotations,
                        AdditionalDataAnnotations = x.AdditionalDataAnnotations,
                        Definition = x.Definition
                    })
                    .ToList(),
                CreateConstructor = !Settings.UsePropertyInitialisers &&
                                    (
                                        table.Columns.Any(c => c.Default != string.Empty && !c.Hidden) ||
                                        table.ReverseNavigationCtor.Any() ||
                                        Settings.EntityClassesArePartial()
                                    ),
                ColumnsWithDefaults = columnsQuery
                    .Where(c => c.Default != string.Empty && !c.Hidden && Settings.IncludeColumnsWithDefaults)
                    .Select(x => new PocoColumnsWithDefaultsModel { NameHumanCase = x.NameHumanCase, Default = x.Default })
                    .ToList(),
                ReverseNavigationCtor = table.ReverseNavigationCtor,
                EntityClassesArePartial = Settings.EntityClassesArePartial(),
                HasSpatial = table.Columns.Any(x => x.IsSpatial),
                HasOwnedEntities = table.OwnedEntities.Any(),
                OwnedEntities = table.OwnedEntities
                    .Select(oe => new PocoOwnedEntityModel
                    {
                        PropertyType        = oe.PropertyType,
                        PropertyName        = oe.PropertyName,
                        PropertyInitialiser = Settings.NeedsNullForgiving() ? " = null!;" : string.Empty
                    })
                    .ToList()
            };

            var co = new CodeOutput(table.DbName, filename, null, Settings.PocoFolder, _globalUsings);
            co.AddUsings(_template.PocoUsings(data));
            co.AddUsings(table.AdditionalNamespaces);
            co.AddCode(Template.Transform(_template.Poco(), data));
            return co;
        }

        public CodeOutput GeneratePocoConfiguration(Table table)
        {
            var filename = table.NameHumanCaseWithSuffix() + Settings.ConfigurationClassName + FileManagementService.Extension;
            if (!CanWritePocoConfiguration())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var columnsQuery = Settings.OrderProperties == OrderProperties.Ordinal ? table.Columns.OrderBy(x => x.Ordinal) : table.Columns.OrderBy(x => x.NameHumanCase);

            var columns = columnsQuery
                .Where(x => !x.Hidden && !string.IsNullOrEmpty(x.Config))
                .ToList();

            var IsEfCore8Plus = Settings.IsEfCore8Plus();

            var foreignKeys = columns.SelectMany(x => x.ConfigFk).OrderBy(o => o).ToList();
            var primaryKey = _generator.PrimaryKeyModelBuilder(table);

            var indexes = _generator.IndexModelBuilder(table);
            var hasIndexes = indexes != null && indexes.Any();

            var data = new PocoConfigurationModel
            {
                UseHasNoKey = IsEfCore8Plus && table.IsView && !table.HasPrimaryKey,
                Name = table.DbName,
                ToTableOrView = (IsEfCore8Plus && table.IsView && !table.HasPrimaryKey) ? "ToView" : "ToTable",
                ConfigurationClassName = table.NameHumanCaseWithSuffix() + Settings.ConfigurationClassName,
                NameHumanCaseWithSuffix = table.NameHumanCaseWithSuffix(),
                Schema = table.Schema.DbName,
                PrimaryKeyNameHumanCase = primaryKey ?? table.PrimaryKeyNameHumanCase(),
                NotUsingDataAnnotations = !Settings.UseDataAnnotations,
                HasSchema = !string.IsNullOrEmpty(table.Schema.DbName),
                ClassModifier = Settings.ConfigurationClassesModifiers,
                ClassComment = table.WriteComments(),
                Columns = columns.Select(x => x.Config).ToList(),
                HasReverseNavigation = table.ReverseNavigationProperty.Count > 0,
                UsesDictionary = table.UsesDictionary,
                HasSpatial = table.Columns.Any(x => x.IsSpatial),
                ReverseNavigationProperty = table.ReverseNavigationProperty
                    .OrderBy(x => Settings.OrderProperties == OrderProperties.Ordinal ? x.Definition : x.PropertyName)
                    .Select(x => new PocoReverseNavigationPropertyModel
                    {
                        ReverseNavHasComment = Settings.IncludeComments != CommentsStyle.None && !string.IsNullOrEmpty(x.Comments),
                        ReverseNavComment = Settings.IncludeComments != CommentsStyle.None ? x.Comments : string.Empty,
                        AdditionalReverseNavigationsDataAnnotations = Settings.AdditionalReverseNavigationsDataAnnotations,
                        AdditionalDataAnnotations = x.AdditionalDataAnnotations,
                        Definition = x.Definition
                    })
                    .ToList(),

                HasForeignKey = foreignKeys.Any(),
                ForeignKeys = foreignKeys,
                MappingConfiguration = table.MappingConfiguration,
                ConfigurationClassesArePartial = Settings.ConfigurationClassesArePartial(),
                Indexes = indexes,
                HasIndexes = hasIndexes,
                // Written inside ToTable(..., t => t.HasComment(...)): EF Core 10 made the entity-level HasComment
                // obsolete, and the TableBuilder form has no view equivalent, so a view's description is not emitted.
                HasTableComment = !Settings.UseDataAnnotations &&
                                  !Settings.IsEf6() &&
                                  !table.IsView &&
                                  Settings.IncludeExtendedPropertyComments != CommentsStyle.None &&
                                  !string.IsNullOrEmpty(table.Description),
                TableComment = table.Description?.Replace("\"", "\"\"")
            };

            // Build builder.OwnsOne(...) blocks for any owned entity mappings
            var ownedEntityConfigs = new List<string>();
            if (table.OwnedEntities.Any())
            {
                var orderByOrdinal = Settings.OrderProperties == OrderProperties.Ordinal;
                foreach (var oe in table.OwnedEntities)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("builder.OwnsOne(x => x." + oe.PropertyName + ", b =>");
                    sb.AppendLine("        {");

                    var oeCols = orderByOrdinal
                        ? oe.Columns.OrderBy(c => c.Ordinal).ToList()
                        : oe.Columns.OrderBy(c => c.OwnedEntityPropertyName).ToList();

                    foreach (var col in oeCols)
                    {
                        if (!string.IsNullOrEmpty(col.OwnedEntityConfig))
                            sb.AppendLine("            " + col.OwnedEntityConfig);
                    }

                    sb.Append("        });");
                    ownedEntityConfigs.Add(sb.ToString());
                }
            }

            data.HasOwnedEntityConfigs = ownedEntityConfigs.Any();
            data.OwnedEntityConfigs    = ownedEntityConfigs;

            var co = new CodeOutput(table.DbName, filename, null, Settings.PocoConfigurationFolder, _globalUsings);
            co.AddUsings(_template.PocoConfigurationUsings(data));
            co.AddUsings(table.AdditionalNamespaces);
            co.AddCode(Template.Transform(_template.PocoConfiguration(), data));
            return co;
        }

        public CodeOutput GenerateStoredProcReturnModel(StoredProcedure sp)
        {
            var filename = sp.WriteStoredProcReturnModelName(_filter) + FileManagementService.Extension;
            if (!CanWriteStoredProcReturnModel())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var multipleModelReturnColumns = new List<MultipleModelReturnColumns>();
            var model = 0;
            foreach (var returnModel in sp.ReturnModels)
            {
                multipleModelReturnColumns.Add(new MultipleModelReturnColumns(++model, returnModel.Select(sp.WriteStoredProcReturnColumn).ToList()));
            }

            var useProperties = Settings.UsePropertiesForStoredProcResultSets;
            var needsNullForgiving = Settings.NeedsNullForgiving();
            
            var data = new StoredProcReturnModel
            {
                ResultClassModifiers = Settings.ResultClassModifiers,
                WriteStoredProcReturnModelName = sp.WriteStoredProcReturnModelName(_filter),
                PropertyGetSet = useProperties 
                    ? " { get; set; }"              // Properties: always include { get; set; }
                    : (needsNullForgiving ? "" : ";"),  // Fields: skip ; if NRT enabled (will be added with = null!;)
                NullForgivingOperator = needsNullForgiving
                    ? " = null!;"                       // Add = null!; when NRT is enabled
                    : string.Empty,                     // Nothing extra needed; PropertyGetSet already terminates the declaration
                SingleModel = sp.ReturnModels.Count == 1,
                SingleModelReturnColumns = sp.ReturnModels
                    .First()
                    .Select(sp.WriteStoredProcReturnColumn)
                    .ToList(),
                MultipleModelReturnColumns = multipleModelReturnColumns
            };

            var co = new CodeOutput(sp.DbName, filename, null, Settings.PocoFolder, _globalUsings);
            co.AddUsings(_template.StoredProcReturnModelUsings());
            co.AddCode(Template.Transform(_template.StoredProcReturnModels(), data));
            return co;
        }

        public CodeOutput GenerateEnum(Enumeration enumeration)
        {
            var filename = enumeration.EnumName + FileManagementService.Extension;
            if (!CanWriteEnums())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var co = new CodeOutput(enumeration.EnumName, filename, null, Settings.PocoFolder, null);
            co.AddUsings(_template.EnumUsings());
            if (enumeration.Items.Any(i => i.Attributes.Any(a => a.StartsWith("[Description"))))
                co.AddUsings(new List<string> { "System.ComponentModel" });
            co.AddCode(Template.Transform(_template.Enums(), enumeration));
            return co;
        }

        /// <summary>
        ///     The one file every enum is written to when <see cref="Settings.EnumsFileName" /> is set. It goes in the
        ///     folder the per-enum files would have used and carries every using any of them needs; the enums themselves
        ///     are written into it one by one, so each keeps its own attributes.
        /// </summary>
        public CodeOutput EnumsFile(List<CodeOutput> enums)
        {
            // Tolerate "Enums.cs" as well as "Enums" rather than writing Enums.cs.cs.
            var name = Settings.EnumsFileName.Trim();
            if (name.EndsWith(FileManagementService.Extension, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - FileManagementService.Extension.Length);

            var usings = enums.SelectMany(x => x.GetUsings()).ToList();
            return new CodeOutput(name, name + FileManagementService.Extension, null, Settings.PocoFolder, usings);
        }

        public CodeOutput GenerateOwnedEntityClass(string typeName, IList<OwnedEntity> instances)
        {
            var filename = typeName + FileManagementService.Extension;
            if (!CanWriteOwnedEntityClasses())
            {
                FileManagementService.DeleteFile(filename);
                return null;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var props = new List<OwnedEntityPropertyItem>();
            foreach (var oe in instances)
            {
                foreach (var col in oe.Columns)
                {
                    var propName = col.OwnedEntityPropertyName;
                    if (string.IsNullOrEmpty(propName) || !seen.Add(propName))
                        continue;
                    props.Add(new OwnedEntityPropertyItem
                    {
                        WrappedType = col.WrapIfNullable(),
                        PropertyName = propName,
                        PropertyInitialiser = GetPropertyInitialiser(col)
                    });
                }
            }

            var data = new OwnedEntityClassModel
            {
                ClassModifier = Settings.EntityClassesModifiers,
                ClassName = typeName,
                Properties = props
            };

            var folder = string.IsNullOrEmpty(Settings.OwnedEntityFolder) ? Settings.PocoFolder : Settings.OwnedEntityFolder;
            var co = new CodeOutput(typeName, filename, null, folder, _globalUsings);
            co.AddUsings(_template.OwnedEntityClassUsings(data));
            co.AddCode(Template.Transform(_template.OwnedEntityClass(), data));
            return co;
        }
    }
}