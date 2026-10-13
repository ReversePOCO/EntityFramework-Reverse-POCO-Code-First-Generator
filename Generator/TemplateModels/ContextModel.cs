using System.Collections.Generic;
using Efrpg.Generators;
using Efrpg.Readers;

namespace Efrpg.TemplateModels
{
    public class ContextModel
    {
        public string DbContextClassModifiers { get; set; }
        public string DbContextName { get; set; }
        public string DbContextBaseClass { get; set; }
        public bool AddParameterlessConstructorToDbContext { get; set; }
        public bool HasDefaultConstructorArgument { get; set; }
        public string DefaultConstructorArgument { get; set; }
        public string ConfigurationClassName { get; set; }
        public string contextInterface { get; set; }
        public string setInitializer { get; set; }
        public bool DbContextClassIsPartial { get; set; }
        public List<TableTemplateData> tables { get; set; }
        public bool hasTables { get; set; }
        public List<string> indexes { get; set; }
        public bool hasIndexes { get; set; }
        public List<StoredProcTemplateData> storedProcs { get; set; }
        public bool hasStoredProcs { get; set; }
        public List<string> tableValuedFunctionComplexTypes { get; set; }
        public bool hasTableValuedFunctionComplexTypes { get; set; }
        public List<string> AdditionalContextInterfaceItems { get; set; }
        public bool addSaveChanges { get; set; }
        public List<TableValuedFunctionsTemplateData> tableValuedFunctions { get; set; }
        public List<ScalarValuedFunctionsTemplateData> scalarValuedFunctions { get; set; }
        public List<RawSequence> Sequences { get; set; }
        public bool hasSequences { get; set; }
        public bool hasTableValuedFunctions { get; set; }
        public bool hasScalarValuedFunctions { get; set; }
        public string ConnectionString { get; set; }
        public string ConnectionStringName { get; set; }
        public string ConnectionStringActions { get; set; }
        public bool IncludeObjectContextConstructor { get; set; }
        public string QueryString { get; set; }
        public string FromSql { get; set; }
        public string ExecuteSqlCommand { get; set; }
        public string StoredProcModelBuilderCommand { get; set; }
        public string StoredProcModelBuilderPostCommand { get; set; }
        public bool OnConfigurationUsesConfiguration { get; set; }
        public bool OnConfigurationUsesConnectionString { get; set; }
        public string DefaultSchema { get; set; }
        public string UseDatabaseProvider { get; set; }
        public bool NeedsServerVersion { get; set; }
        public string SqlParameter { get; set; }
        public string SqlParameterValue { get; set; }
        public bool UseLazyLoadingProxies { get; set; }
        public bool hasTriggers { get; set; }
        public List<Trigger> Triggers { get; set; }
        public bool hasMemoryOptimisedTables { get; set; }
        public List<string> MemoryOptimisedTables { get; set; }
    }
}