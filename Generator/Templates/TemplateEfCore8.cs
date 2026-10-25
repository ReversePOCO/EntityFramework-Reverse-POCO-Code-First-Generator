using System.Collections.Generic;
using System.Linq;
using Efrpg.Filtering;
using Efrpg.TemplateModels;

namespace Efrpg.Templates
{
    /// <summary>
    /// {{Mustache}} template documentation available at https://github.com/jehugaleahsa/mustache-sharp
    /// This template is also used for EfCore 9 and 10.
    /// </summary>
    public class TemplateEfCore8 : Template
    {
        public override string Usings()
        {
            return @"
{{#each this}}
using {{this}};{{#newline}}
{{/each}}";
        }

        public override List<string> DatabaseContextInterfaceUsings(InterfaceModel data)
        {
            var usings = new List<string>
            {
                "System",
                "System.Data",
                "System.Threading.Tasks",
                "System.Threading"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.tables.Any() || data.hasStoredProcs)
            {
                usings.Add("Microsoft.EntityFrameworkCore");
                usings.Add("Microsoft.EntityFrameworkCore.Infrastructure");
                usings.Add("System.Linq");
            }

            if (data.hasStoredProcs)
                usings.Add("System.Collections.Generic");

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Collections.Generic");
                usings.Add("Microsoft.EntityFrameworkCore.ChangeTracking");
                usings.Add("System.Linq");
                usings.Add("System.Linq.Expressions");
            }

            return usings;
        }

        public override string DatabaseContextInterface()
        {
            return @"
{{interfaceModifier}} interface {{DbContextInterfaceName}} : {{DbContextInterfaceBaseClasses}}{{#newline}}
{{{#newline}}

{{#each tables}}
    DbSet<{{DbSetName}}> {{PluralTableName}} { get; set; }{{Comment}}{{#newline}}
{{/each}}

{{#if AdditionalContextInterfaceItems}}
{{#newline}}
    // Additional interface items{{#newline}}
{{/if}}
{{#each AdditionalContextInterfaceItems}}
    {{this}}{{#newline}}
{{/each}}


{{#if addSaveChanges}}
{{#newline}}
    int SaveChanges();{{#newline}}
    int SaveChanges(bool acceptAllChangesOnSuccess);{{#newline}}
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));{{#newline}}
    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken));{{#newline}}
    DatabaseFacade Database { get; }{{#newline}}
    DbSet<TEntity> Set<TEntity>() where TEntity : class;{{#newline}}
    string? ToString();{{#newline}}{{#newline}}

    EntityEntry Add(object entity);{{#newline}}
    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;{{#newline}}
    Task AddRangeAsync(params object[] entities);{{#newline}}
    Task AddRangeAsync(IEnumerable<object> entities, CancellationToken cancellationToken = default);{{#newline}}
    ValueTask<EntityEntry<TEntity>> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class;{{#newline}}
    ValueTask<EntityEntry> AddAsync(object entity, CancellationToken cancellationToken = default);{{#newline}}
    void AddRange(IEnumerable<object> entities);{{#newline}}
    void AddRange(params object[] entities);{{#newline}}{{#newline}}

    EntityEntry Attach(object entity);{{#newline}}
    EntityEntry<TEntity> Attach<TEntity>(TEntity entity) where TEntity : class;{{#newline}}
    void AttachRange(IEnumerable<object> entities);{{#newline}}
    void AttachRange(params object[] entities);{{#newline}}{{#newline}}

    EntityEntry Entry(object entity);{{#newline}}
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;{{#newline}}{{#newline}}

    TEntity? Find<TEntity>(params object?[]? keyValues) where TEntity : class;{{#newline}}
    ValueTask<TEntity?> FindAsync<TEntity>(object?[]? keyValues, CancellationToken cancellationToken) where TEntity : class;{{#newline}}
    ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues) where TEntity : class;{{#newline}}
    ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken);{{#newline}}
    ValueTask<object?> FindAsync(Type entityType, params object?[]? keyValues);{{#newline}}
    object? Find(Type entityType, params object?[]? keyValues);{{#newline}}{{#newline}}

    EntityEntry Remove(object entity);{{#newline}}
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;{{#newline}}
    void RemoveRange(IEnumerable<object> entities);{{#newline}}
    void RemoveRange(params object[] entities);{{#newline}}{{#newline}}

    EntityEntry Update(object entity);{{#newline}}
    EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;{{#newline}}
    void UpdateRange(IEnumerable<object> entities);{{#newline}}
    void UpdateRange(params object[] entities);{{#newline}}{{#newline}}

    IQueryable<TResult> FromExpression<TResult> (Expression<Func<IQueryable<TResult>>> expression);{{#newline}}
{{/if}}


{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}
{{#if HasReturnModels}}

{{#if MultipleReturnModels}}
    // {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseTrue}}); Cannot be created as EF Core does not yet support stored procedures with multiple result sets.{{#newline}}
{{#else}}
    {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseTrue}});{{#newline}}
{{/if}}
{{#if SingleReturnModel}}
    {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsTrueTrue}});{{#newline}}
{{/if}}
{{#else}}
{{#if HasError}}
    // Unable to determine return model for '{{FunctionName}}'. Error: {{Error}}{{#newline}}
{{/if}}
    int {{FunctionName}}({{WriteStoredProcFunctionParamsTrueTrue}});{{#newline}}
{{/if}}

{{#if MultipleReturnModels}}
    // Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseTrue}}); Cannot be created as EF Core does not yet support stored procedures with multiple result sets.{{#newline}}
{{#else}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}
    Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseTrueToken}});{{#newline}}
{{/if}}
{{/if}}
{{#newline}}
{{/each}}
{{/if}}

{{#if hasTableValuedFunctions}}
{{#newline}}
    // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
    IQueryable<{{ReturnClassName}}> {{ExecName}}({{WriteStoredProcFunctionParamsFalseTrue}}); // {{Schema}}.{{Name}}{{#newline}}
{{/each}}
{{/if}}

{{#if hasScalarValuedFunctions}}
{{#newline}}
    // Scalar Valued Functions{{#newline}}
{{#each scalarValuedFunctions}}
    {{ReturnType}} {{ExecName}}({{WriteStoredProcFunctionParamsFalseTrue}}); // {{Schema}}.{{Name}}{{#newline}}
{{/each}}
{{/if}}

}";
        }

        public override List<string> DatabaseContextUsings(ContextModel data)
        {
            var usings = new List<string>
            {
                "System",
                "System.Data",
                "System.Data.SqlTypes",
                "Microsoft.EntityFrameworkCore",
                "System.Threading.Tasks",
                "System.Threading"
            };

            switch (Settings.DatabaseType)
            {
                case DatabaseType.SqlServer:
                    usings.Add("Microsoft.Data.SqlClient");
                    break;
                case DatabaseType.SQLite:
                    usings.Add("Microsoft.Data.Sqlite");
                    break;
                case DatabaseType.PostgreSQL:
                    usings.Add("Npgsql");
                    usings.Add("NpgsqlTypes");
                    break;
                case DatabaseType.MySql:
                    usings.Add("MySqlConnector");
                    if (data.hasStoredProcs)
                        usings.Add("Microsoft.EntityFrameworkCore.Storage"); // GetDbTransaction(), for a procedure with OUT parameters
                    break;
                case DatabaseType.Oracle:
                    usings.Add("Oracle.ManagedDataAccess.Client");
                    break;
            }

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.tables.Any() || data.hasStoredProcs)
            {
                usings.Add("System.Linq");
            }

            if (data.hasStoredProcs)
                usings.Add("System.Collections.Generic");

            if (Settings.OnConfiguration == OnConfiguration.Configuration)
                usings.Add("Microsoft.Extensions.Configuration");

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Collections.Generic");
                usings.Add("Microsoft.EntityFrameworkCore.ChangeTracking");
            }

            return usings;
        }

        public override string DatabaseContext()
        {
            return @"
{{DbContextClassModifiers}} class {{DbContextName}} : {{DbContextBaseClass}}{{contextInterface}}{{#newline}}
{{{#newline}}
{{#if OnConfigurationUsesConfiguration}}
    private readonly IConfiguration? _configuration;{{#newline}}{{#newline}}
{{/if}}

{{#if AddParameterlessConstructorToDbContext}}
    public {{DbContextName}}(){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}
{{/if}}

    public {{DbContextName}}(DbContextOptions<{{DbContextName}}> options){{#newline}}
        : base(options){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    protected {{DbContextName}}(DbContextOptions options){{#newline}}
        : base(options){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

{{#if OnConfigurationUsesConfiguration}}
    public {{DbContextName}}(IConfiguration configuration){{#newline}}
    {{{#newline}}
        _configuration = configuration;{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}
{{/if}}

{{#each tables}}
    {{DbSetModifier}} DbSet<{{DbSetName}}> {{PluralTableName}} { get; set; }{{Comment}}{{#newline}}
{{/each}}
{{#newline}}

{{#if OnConfigurationUsesConfiguration}}
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder){{#newline}}
    {{{#newline}}
        if (!optionsBuilder.IsConfigured && _configuration != null){{#newline}}
        {{{#newline}}
            optionsBuilder.{{UseDatabaseProvider}}(_configuration.GetConnectionString(@""{{ConnectionStringName}}""){{#if NeedsServerVersion}}, ServerVersion.AutoDetect(_configuration.GetConnectionString(@""{{ConnectionStringName}}"")){{/if}}{{ConnectionStringActions}});{{#newline}}
{{#if UseLazyLoadingProxies}}
            optionsBuilder.UseLazyLoadingProxies();{{#newline}}
{{/if}}
        }{{#newline}}
    }{{#newline}}{{#newline}}
{{/if}}

{{#if OnConfigurationUsesConnectionString}}
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder){{#newline}}
    {{{#newline}}
        if (!optionsBuilder.IsConfigured){{#newline}}
        {{{#newline}}
            optionsBuilder.{{UseDatabaseProvider}}(@""{{ConnectionString}}""{{#if NeedsServerVersion}}, ServerVersion.AutoDetect(@""{{ConnectionString}}""){{/if}}{{ConnectionStringActions}});{{#newline}}
{{#if UseLazyLoadingProxies}}
            optionsBuilder.UseLazyLoadingProxies();{{#newline}}
{{/if}}
        }{{#newline}}
    }{{#newline}}{{#newline}}
{{/if}}


    public bool IsSqlParameterNull({{SqlParameter}} param){{#newline}}
    {{{#newline}}
        var sqlValue = param.{{SqlParameterValue}};{{#newline}}
        var nullableValue = sqlValue as INullable;{{#newline}}
        if (nullableValue != null){{#newline}}
            return nullableValue.IsNull;{{#newline}}
        return (sqlValue == null || sqlValue == DBNull.Value);{{#newline}}
    }{{#newline}}{{#newline}}


    protected override void OnModelCreating(ModelBuilder modelBuilder){{#newline}}
    {{{#newline}}
        base.OnModelCreating(modelBuilder);{{#newline}}

{{#if hasSequences}}
{{#newline}}
{{#each Sequences}}
        modelBuilder.HasSequence<{{DataType}}>(""{{Name}}"", ""{{Schema}}"").StartsAt({{StartValue}}).IncrementsBy({{IncrementValue}}).IsCyclic({{IsCycleEnabled}})
{{#if hasMinValue}}
.HasMin({{MinValue}})
{{/if}}
{{#if hasMaxValue}}
.HasMax({{MaxValue}})
{{/if}}
;{{#newline}}
{{/each}}
{{/if}}

{{#if hasTables}}
{{#newline}}
{{#each tables}}
        modelBuilder.ApplyConfiguration(new {{DbSetConfigName}}());{{#newline}}
{{/each}}
{{/if}}

{{#if hasMemoryOptimisedTables}}
{{#newline}}
{{#each MemoryOptimisedTables}}
        modelBuilder.Entity<{{this}}>().ToTable(t => t.IsMemoryOptimized());{{#newline}}
{{/each}}
{{/if}}

{{#if hasTriggers}}
{{#newline}}
{{#each Triggers}}
        modelBuilder.Entity<{{TableName}}>().ToTable(tb => tb.HasTrigger(""{{TriggerName}}""));{{#newline}}
{{/each}}
{{/if}}

{{#if hasStoredProcs}}
{{#newline}}
{{#each storedProcs}}
{{#if SingleReturnModel}}
{{#if IncludeModelBuilder}}
        modelBuilder.{{StoredProcModelBuilderCommand}}<{{ReturnModelName}}>(){{StoredProcModelBuilderPostCommand}};{{#newline}}
{{#if HasColumnMappings}}
{{#each ColumnMappings}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}
{{/if}}
{{/if}}
{{/each}}
{{/if}}

{{#if hasTableValuedFunctions}}
{{#newline}}
        // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
{{#if IncludeModelBuilder}}
        modelBuilder.{{ModelBuilderCommand}}<{{ReturnClassName}}>(){{ModelBuilderPostCommand}};{{#newline}}
{{#if HasColumnMappings}}
{{#each ColumnMappings}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}
{{/if}}
{{/each}}
{{/if}}

{{#if DbContextClassIsPartial}}
{{#newline}}
        OnModelCreatingPartial(modelBuilder);{{#newline}}
{{/if}}

    }{{#newline}}
{{#newline}}



{{#if DbContextClassIsPartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
    partial void DisposePartial(bool disposing);{{#newline}}
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);{{#newline}}
    static partial void OnCreateModelPartial(ModelBuilder modelBuilder, string schema);{{#newline}}
{{/if}}


{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}
{{#if HasReturnModels}}

{{#if MultipleReturnModels}}
    // public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseFalse}}) Cannot be created as EF Core does not yet support stored procedures with multiple result sets.{{#newline}}
{{#else}}
    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        int procResult;{{#newline}}
        return {{FunctionName}}({{WriteStoredProcFunctionOverloadCall}});{{#newline}}
    }{{#newline}}{{#newline}}
{{/if}}

{{#if SingleReturnModel}}
    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
        const string sqlCommand = ""{{Exec}}"";{{#newline}}
        var procResultData = {{QueryString}}<{{ReturnModelName}}>(){{#newline}}
            .{{FromSql}}(sqlCommand{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrue}}){{#newline}}
            .ToList();{{#newline}}{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{#if HasProcResult}}
        procResult = (int) procResultParam.Value;{{#newline}}
{{#else}}
        procResult = 0;{{#newline}}
{{/if}}
        return procResultData;{{#newline}}
    }{{#newline}}
{{/if}}

{{#else}}
{{#if HasError}}
    // Unable to determine return model for '{{FunctionName}}'. Error: {{Error}}{{#newline}}
{{/if}}
    public int {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
{{#if HasProcResult}}
{{#newline}}
{{/if}}
{{#if ExecutesAsCommand}}
        using (var command = Database.GetDbConnection().CreateCommand()){{#newline}}
        {{{#newline}}
            command.CommandText = ""{{CommandText}}"";{{#newline}}
            command.CommandType = CommandType.StoredProcedure;{{#newline}}
            command.Transaction = Database.CurrentTransaction?.GetDbTransaction();{{#newline}}
            command.Parameters.AddRange({{CommandParameters}});{{#newline}}
            Database.OpenConnection();{{#newline}}
            try{{#newline}}
            {{{#newline}}
                command.ExecuteNonQuery();{{#newline}}
            }{{#newline}}
            finally{{#newline}}
            {{{#newline}}
                Database.CloseConnection();{{#newline}}
            }{{#newline}}
        }{{#newline}}
{{#else}}
        Database.{{ExecuteSqlCommand}}(""{{ExecWithNoReturnModel}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrue}});{{#newline}}
{{/if}}
{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{#if HasProcResult}}
        return (int)procResultParam.Value;{{#newline}}
{{#else}}
        return 0;{{#newline}}
{{/if}}
    }{{#newline}}
{{/if}}
{{#newline}}

{{#if MultipleReturnModels}}
    // public async Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseFalse}}) Cannot be created as EF Core does not yet support stored procedures with multiple result sets.{{#newline}}
{{#newline}}
{{#else}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}
    public async Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseFalseToken}}){{#newline}}
    {{{#newline}}
{{#if HasNoReturnModels}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
{{#if HasProcResult}}
{{#newline}}
{{/if}}
        await Database.ExecuteSqlRawAsync(""{{AsyncExec}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrueToken}});{{#newline}}
{{#newline}}
{{#if HasProcResult}}
        return (int)procResultParam.Value;{{#newline}}
{{#else}}
        return 0;{{#newline}}
{{/if}}
{{#else}}
{{WriteStoredProcFunctionDeclareSqlParameterFalse}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
        const string sqlCommand = ""{{AsyncExec}}"";{{#newline}}
        var procResultData = await {{QueryString}}<{{ReturnModelName}}>(){{#newline}}
            .{{FromSql}}(sqlCommand{{WriteStoredProcFunctionSqlParameterAnonymousArrayFalse}}){{#newline}}
            .ToListAsync(cancellationToken);{{#newline}}{{#newline}}

        return procResultData;{{#newline}}
{{/if}}
    }{{#newline}}
{{/if}}
{{#newline}}
{{/if}}
{{/each}}
{{/if}}

{{#if hasTableValuedFunctions}}
{{#newline}}
    // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
{{#newline}}
    // {{Schema}}.{{Name}}{{#newline}}
    public IQueryable<{{ReturnClassName}}> {{ExecName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        return {{QueryString}}<{{ReturnClassName}}>(){{#newline}}
            .{{FromSql}}(""SELECT * FROM {{QualifiedName}}({{WriteStoredProcFunctionSqlAtParams}})""{{WriteTableValuedFunctionSqlParameterAnonymousArray}}){{#newline}}
            .AsNoTracking();{{#newline}}
    }{{#newline}}
{{/each}}
{{/if}}

{{#if hasScalarValuedFunctions}}
{{#newline}}
    // Scalar Valued Functions{{#newline}}
{{#each scalarValuedFunctions}}
{{#newline}}
    [DbFunction({{DbFunctionArguments}})]{{#newline}}
    public {{ReturnType}} {{ExecName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        throw new Exception(""Don't call this directly. Use LINQ to call the scalar valued function as part of your query"");{{#newline}}
    }{{#newline}}
{{/each}}
{{/if}}
}";
        }

        public override List<string> DatabaseContextFactoryUsings(FactoryModel data)
        {
            var usings = new List<string>
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Design"
            };
            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");
            return usings;
        }

        public override string DatabaseContextFactory()
        {
            return @"
{{classModifier}} class {{contextName}}Factory : IDesignTimeDbContextFactory<{{contextName}}>{{#newline}}
{{{#newline}}
    private readonly DbContextOptions<{{contextName}}>? Options;{{#newline}}
{{#newline}}
    public {{contextName}}Factory(){{#newline}}
    {{{#newline}}
        Options = null;{{#newline}}
    }{{#newline}}
{{#newline}}
    public {{contextName}}Factory(DbContextOptions<{{contextName}}> options){{#newline}}
    {{{#newline}}
        Options = options;{{#newline}}
    }{{#newline}}
{{#newline}}
    public {{contextName}} CreateDbContext(string[] args){{#newline}}
    {{{#newline}}
        return new {{contextName}}();{{#newline}}
    }{{#newline}}
{{#newline}}
    public {{contextName}} CreateDbContext(){{#newline}}
    {{{#newline}}
        return Options != null ? new {{contextName}}(Options) : new {{contextName}}();{{#newline}}
    }{{#newline}}
{{#newline}}
    public {{contextName}} CreateDbContext(DbContextOptions<{{contextName}}> options){{#newline}}
    {{{#newline}}
        return options == null{{#newline}}
            ? new {{contextName}}(){{#newline}}
            : new {{contextName}}(options);{{#newline}}
    }{{#newline}}
}";
        }

        public override List<string> FakeDatabaseContextUsings(FakeContextModel data, IDbContextFilter filter)
        {
            var usings = new List<string>
            {
                "System",
                "System.Collections.Concurrent", // Set<TEntity>() caches the DbSet property it reflected over
                "System.Data",
                "System.Linq",                   // Set<TEntity>() uses FirstOrDefault()
                "System.Reflection",             // Set<TEntity>() reflects over this class' DbSet properties
                "System.Threading.Tasks",
                "System.Threading",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Infrastructure"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.hasStoredProcs)
                usings.Add("System.Collections.Generic");

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Collections.Generic");
                usings.Add("Microsoft.EntityFrameworkCore.ChangeTracking");
                usings.Add("System.Linq");
                usings.Add("System.Linq.Expressions");
            }

            if (Settings.DatabaseType == DatabaseType.PostgreSQL)
            {
                usings.Add("Npgsql");
                usings.Add("NpgsqlTypes");
            }

            return usings;
        }

        public override string FakeDatabaseContext()
        {
            return @"
{{DbContextClassModifiers}} class Fake{{DbContextName}}{{contextInterface}}{{#newline}}
{{{#newline}}

{{#each tables}}
    {{DbSetModifier}} DbSet<{{DbSetName}}> {{PluralTableName}} { get; set; } = null!;{{Comment}}{{#newline}}
{{/each}}
{{#newline}}

    public Fake{{DbContextName}}(){{#newline}}
    {{{#newline}}
        _shim     = new FakeDbContextShim();{{#newline}}
        _database = new FakeDatabaseFacade(_shim);{{#newline}}
{{#newline}}

{{#each tables}}
        {{PluralTableName}} = new FakeDbSet<{{DbSetName}}>({{DbSetPrimaryKeys}});{{#newline}}
{{/each}}
{{#newline}}

{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}

{{#newline}}
    public int SaveChangesCount { get; private set; }{{#newline}}
    public virtual int SaveChanges(){{#newline}}
    {{{#newline}}
        ++SaveChangesCount;{{#newline}}
        return 1;{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual int SaveChanges(bool acceptAllChangesOnSuccess){{#newline}}
    {{{#newline}}
        return SaveChanges();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<int>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(SaveChanges());{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<int>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(SaveChanges(acceptAllChangesOnSuccess));{{#newline}}
    }{{#newline}}{{#newline}}


{{#if DbContextClassIsPartial}}
    partial void InitializePartial();{{#newline}}
{{#newline}}
{{/if}}

    protected virtual void Dispose(bool disposing){{#newline}}
    {{{#newline}}
        if (disposing){{#newline}}
            _shim.Dispose();{{#newline}}
    }{{#newline}}{{#newline}}

    public void Dispose(){{#newline}}
    {{{#newline}}
        Dispose(true);{{#newline}}
        GC.SuppressFinalize(this);{{#newline}}
    }{{#newline}}{{#newline}}

    private readonly FakeDbContextShim _shim;{{#newline}}
    private DatabaseFacade _database;{{#newline}}
    public DatabaseFacade Database { get { return _database; } }{{#newline}}{{#newline}}

    // Keyed on the runtime type as well as the entity type, because this cache is static and is therefore shared{{#newline}}
    // with any class deriving from this one, which may declare DbSet properties this class does not.{{#newline}}
    private static readonly ConcurrentDictionary<(Type ContextType, Type EntityType), PropertyInfo?> _dbSetProperties ={{#newline}}
        new ConcurrentDictionary<(Type, Type), PropertyInfo?>();{{#newline}}{{#newline}}

    public DbSet<TEntity> Set<TEntity>() where TEntity : class{{#newline}}
    {{{#newline}}
        var property = _dbSetProperties.GetOrAdd({{#newline}}
            (GetType(), typeof(TEntity)),{{#newline}}
            key => key.ContextType{{#newline}}
                .GetProperties(BindingFlags.Public | BindingFlags.Instance){{#newline}}
                .FirstOrDefault(x => x.PropertyType == typeof(DbSet<>).MakeGenericType(key.EntityType)));{{#newline}}
{{#newline}}
        if (property == null){{#newline}}
            throw new InvalidOperationException(""Cannot find a DbSet<"" + typeof(TEntity).Name + ""> on "" + GetType().Name + "". The entity type is not part of this context."");{{#newline}}
{{#newline}}
        return (DbSet<TEntity>) property.GetValue(this)!;{{#newline}}
    }{{#newline}}{{#newline}}

    public override string? ToString(){{#newline}}
    {{{#newline}}
        return GetType().Name;{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry Add(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual Task AddRangeAsync(params object[] entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual async Task AddRangeAsync(IEnumerable<object> entities, CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        await Task.CompletedTask;{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual async ValueTask<EntityEntry<TEntity>> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default) where TEntity : class{{#newline}}
    {{{#newline}}
        await Task.CompletedTask;{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual async ValueTask<EntityEntry> AddAsync(object entity, CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        await Task.CompletedTask;{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void AddRange(IEnumerable<object> entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void AddRange(params object[] entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry Attach(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry<TEntity> Attach<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void AttachRange(IEnumerable<object> entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void AttachRange(params object[] entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry Entry(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual TEntity? Find<TEntity>(params object?[]? keyValues) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual ValueTask<TEntity?> FindAsync<TEntity>(object?[]? keyValues, CancellationToken cancellationToken) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual ValueTask<TEntity?> FindAsync<TEntity>(params object?[]? keyValues) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual ValueTask<object?> FindAsync(Type entityType, params object?[]? keyValues){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual object? Find(Type entityType, params object?[]? keyValues){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry Remove(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void RemoveRange(IEnumerable<object> entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void RemoveRange(params object[] entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry Update(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void UpdateRange(IEnumerable<object> entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual void UpdateRange(params object[] entities){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

    public virtual IQueryable<TResult> FromExpression<TResult> (Expression<Func<IQueryable<TResult>>> expression){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}{{#newline}}

{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}

{{#if HasReturnModels}}
{{#newline}}
{{#if CreateDbSetForReturnModel}}
    public DbSet<{{ReturnModelName}}> {{ReturnModelName}} { get; set; } = null!;{{#newline}}
{{/if}}
    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        int procResult;{{#newline}}
        return {{FunctionName}}({{WriteStoredProcFunctionOverloadCall}});{{#newline}}
    }{{#newline}}{{#newline}}

    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersTrue}}
        procResult = 0;{{#newline}}
        return new {{ReturnType}}();{{#newline}}
    }{{#newline}}

{{#newline}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}
    public Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseFalseToken}}){{#newline}}
    {{{#newline}}
        int procResult;{{#newline}}
        return Task.FromResult({{FunctionName}}({{WriteStoredProcFunctionOverloadCall}}));{{#newline}}
    }{{#newline}}
{{/if}}

{{#else}}
{{#newline}}
{{#if HasError}}
    // Unable to determine return model for '{{FunctionName}}'. Error: {{Error}}{{#newline}}
{{/if}}
    public int {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersTrue}}
        return 0;{{#newline}}
    }{{#newline}}
{{#newline}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}
    public Task<int> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseFalseToken}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersTrue}}
        return Task.FromResult(0);{{#newline}}
    }{{#newline}}
{{/if}}
{{/if}}
{{/each}}
{{/if}}



{{#if hasTableValuedFunctions}}
{{#newline}}
    // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
{{#newline}}
    // {{Schema}}.{{Name}}{{#newline}}
    public IQueryable<{{ReturnClassName}}> {{ExecName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        return new List<{{ReturnClassName}}>().AsQueryable();{{#newline}}
    }{{#newline}}
{{/each}}
{{/if}}

{{#if hasScalarValuedFunctions}}
{{#newline}}
    // Scalar Valued Functions{{#newline}}
{{#each scalarValuedFunctions}}
{{#newline}}
    // {{Schema}}.{{Name}}{{#newline}}
    public {{ReturnType}} {{ExecName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
        return default({{ReturnType}});{{#newline}}
    }{{#newline}}
{{/each}}
{{/if}}
}";
        }

        public override List<string> FakeDbSetUsings(FakeDbSetModel data)
        {
            var usings = new List<string>
            {
                "System",
                "System.Collections",
                "System.ComponentModel",
                "System.Linq",
                "System.Linq.Expressions",
                "System.Reflection",
                "System.Collections.ObjectModel",
                "System.Collections.Generic",
                "System.Threading",
                "System.Threading.Tasks",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Query",
                "Microsoft.EntityFrameworkCore.Query.Internal",
                "Microsoft.EntityFrameworkCore.Infrastructure",
                "Microsoft.EntityFrameworkCore.ChangeTracking",
                "Microsoft.EntityFrameworkCore.Storage",
                "Microsoft.EntityFrameworkCore.Metadata"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string FakeDbSet()
        {
            return @"
// ************************************************************************{{#newline}}
// Fake DbSet{{#newline}}
// Implementing Find:{{#newline}}
//      The Find method is difficult to implement in a generic fashion. If{{#newline}}
//      you need to test code that makes use of the Find method it is{{#newline}}
//      easiest to create a test DbSet for each of the entity types that{{#newline}}
//      need to support find. You can then write logic to find that{{#newline}}
//      particular type of entity, as shown below:{{#newline}}
//      public class FakeBlogDbSet : FakeDbSet<Blog>{{#newline}}
//      {{{#newline}}
//          public override Blog Find(params object[] keyValues){{#newline}}
//          {{{#newline}}
//              var id = (int) keyValues.Single();{{#newline}}
//              return this.SingleOrDefault(b => b.BlogId == id);{{#newline}}
//          }{{#newline}}
//      }{{#newline}}
//      Read more about it here: https://msdn.microsoft.com/en-us/data/dn314431.aspx{{#newline}}
{{DbContextClassModifiers}} class FakeDbSet<TEntity> :{{#newline}}
    DbSet<TEntity>,{{#newline}}
    IQueryable<TEntity>,{{#newline}}
    IAsyncEnumerable<TEntity>,{{#newline}}
    IListSource,{{#newline}}
    IResettableService{{#newline}}
    where TEntity : class{{#newline}}
{{{#newline}}
    private readonly PropertyInfo[] _primaryKeys;{{#newline}}
    private ObservableCollection<TEntity> _data;{{#newline}}
    private IQueryable _query;{{#newline}}
    private IEntityType? _entityType = null;{{#newline}}
    public override IEntityType EntityType{{#newline}}
    {{{#newline}}
        get{{#newline}}
        {{{#newline}}
            if (_entityType == null){{#newline}}
                throw new NotImplementedException(""EntityType is not implemented for FakeDbSet."");{{#newline}}
            return _entityType;{{#newline}}
        }{{#newline}}
    }{{#newline}}{{#newline}}

    public FakeDbSet(){{#newline}}
    {{{#newline}}
        _primaryKeys = Array.Empty<PropertyInfo>();{{#newline}}
        _data        = new ObservableCollection<TEntity>();{{#newline}}
        _query       = _data.AsQueryable();{{#newline}}

{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    public FakeDbSet(params string[] primaryKeys){{#newline}}
    {{{#newline}}
        _primaryKeys = typeof(TEntity).GetProperties().Where(x => primaryKeys.Contains(x.Name)).ToArray();{{#newline}}
        _data        = new ObservableCollection<TEntity>();{{#newline}}
        _query       = _data.AsQueryable();{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    public override TEntity? Find(params object?[]? keyValues){{#newline}}
    {{{#newline}}
        if (_primaryKeys == null){{#newline}}
            throw new ArgumentException(""No primary keys defined"");{{#newline}}
        if (keyValues?.Length != _primaryKeys.Length){{#newline}}
            throw new ArgumentException(""Incorrect number of keys passed to Find method"");{{#newline}}{{#newline}}

        var keyQuery = this.AsQueryable();{{#newline}}
        keyQuery = keyValues{{#newline}}
            .Select((t, i) => i){{#newline}}
            .Aggregate(keyQuery,{{#newline}}
                (current, x) =>{{#newline}}
                    current.Where(entity => _primaryKeys[x].GetValue(entity, null)!.Equals(keyValues[x])));{{#newline}}{{#newline}}

        return keyQuery.SingleOrDefault();{{#newline}}
    }{{#newline}}{{#newline}}

    public override ValueTask<TEntity?> FindAsync(object?[]? keyValues, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        return new ValueTask<TEntity?>(Task<TEntity?>.Factory.StartNew(() => Find(keyValues), cancellationToken));{{#newline}}
    }{{#newline}}{{#newline}}

    public override ValueTask<TEntity?> FindAsync(params object?[]? keyValues){{#newline}}
    {{{#newline}}
        return new ValueTask<TEntity?>(Task<TEntity?>.Factory.StartNew(() => Find(keyValues)));{{#newline}}
    }{{#newline}}{{#newline}}

    public override EntityEntry<TEntity> Add(TEntity entity){{#newline}}
    {{{#newline}}
        _data.Add(entity);{{#newline}}
        return null!;{{#newline}}
    }{{#newline}}{{#newline}}

    public override ValueTask<EntityEntry<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        return new ValueTask<EntityEntry<TEntity>>(Task<EntityEntry<TEntity>>.Factory.StartNew(() => Add(entity), cancellationToken));{{#newline}}
    }{{#newline}}{{#newline}}

    public override void AddRange(params TEntity[] entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        foreach (var entity in entities){{#newline}}
            _data.Add(entity);{{#newline}}
    }{{#newline}}{{#newline}}

    public override void AddRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        foreach (var entity in entities){{#newline}}
            _data.Add(entity);{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task AddRangeAsync(params TEntity[] entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        return Task.Factory.StartNew(() => AddRange(entities));{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        return Task.Factory.StartNew(() => AddRange(entities), cancellationToken);{{#newline}}
    }{{#newline}}{{#newline}}

    public override EntityEntry<TEntity> Attach(TEntity entity){{#newline}}
    {{{#newline}}
        if (entity == null) throw new ArgumentNullException(""entity"");{{#newline}}
        return Add(entity)!;{{#newline}}
    }{{#newline}}{{#newline}}

    public override void AttachRange(params TEntity[] entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        AddRange(entities);{{#newline}}
    }{{#newline}}{{#newline}}

    public override void AttachRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        AddRange(entities);{{#newline}}
    }{{#newline}}{{#newline}}

    public override EntityEntry<TEntity> Remove(TEntity entity){{#newline}}
    {{{#newline}}
        _data.Remove(entity);{{#newline}}
        return null!;{{#newline}}
    }{{#newline}}{{#newline}}

    public override void RemoveRange(params TEntity[] entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        foreach (var entity in entities.ToList()){{#newline}}
            _data.Remove(entity);{{#newline}}
    }{{#newline}}{{#newline}}

    public override void RemoveRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        RemoveRange(entities.ToArray());{{#newline}}
    }{{#newline}}{{#newline}}

    public override EntityEntry<TEntity> Update(TEntity entity){{#newline}}
    {{{#newline}}
        _data.Remove(entity);{{#newline}}
        _data.Add(entity);{{#newline}}
        return null!;{{#newline}}
    }{{#newline}}{{#newline}}

    public override void UpdateRange(params TEntity[] entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        RemoveRange(entities);{{#newline}}
        AddRange(entities);{{#newline}}
    }{{#newline}}{{#newline}}

    public override void UpdateRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        var array = entities.ToArray();
        RemoveRange(array);{{#newline}}
        AddRange(array);{{#newline}}
    }{{#newline}}{{#newline}}

    bool IListSource.ContainsListCollection => true;{{#newline}}{{#newline}}

    public IList GetList(){{#newline}}
    {{{#newline}}
        return _data;{{#newline}}
    }{{#newline}}{{#newline}}

    IList IListSource.GetList(){{#newline}}
    {{{#newline}}
        return _data;{{#newline}}
    }{{#newline}}{{#newline}}

    Type IQueryable.ElementType{{#newline}}
    {{{#newline}}
        get { return _query.ElementType; }{{#newline}}
    }{{#newline}}{{#newline}}

    Expression IQueryable.Expression{{#newline}}
    {{{#newline}}
        get { return _query.Expression; }{{#newline}}
    }{{#newline}}{{#newline}}

    IQueryProvider IQueryable.Provider{{#newline}}
    {{{#newline}}
        get { return new FakeDbAsyncQueryProvider<TEntity>(_data); }{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator IEnumerable.GetEnumerator(){{#newline}}
    {{{#newline}}
        return _data.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator(){{#newline}}
    {{{#newline}}
        return _data.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    public override IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        return new FakeDbAsyncEnumerator<TEntity>(this.AsEnumerable().GetEnumerator());{{#newline}}
    }{{#newline}}{{#newline}}

    public void ResetState(){{#newline}}
    {{{#newline}}
        _data  = new ObservableCollection<TEntity>();{{#newline}}
        _query = _data.AsQueryable();{{#newline}}
    }{{#newline}}{{#newline}}

    public Task ResetStateAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        return Task.Factory.StartNew(() => ResetState());{{#newline}}
    }{{#newline}}

{{#if DbContextClassIsPartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
{{/if}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeDbAsyncQueryProvider<TEntity> : FakeQueryProvider<TEntity>, IAsyncEnumerable<TEntity>, IAsyncQueryProvider{{#newline}}
{{{#newline}}
    public FakeDbAsyncQueryProvider(Expression expression) : base(expression){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public FakeDbAsyncQueryProvider(IEnumerable<TEntity> enumerable) : base(enumerable){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];{{#newline}}
        var executionResult = typeof(IQueryProvider){{#newline}}
            .GetMethods(){{#newline}}
            .First(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod){{#newline}}
            .MakeGenericMethod(expectedResultType){{#newline}}
            .Invoke(this, new object[] { expression });{{#newline}}{{#newline}}

        return (TResult) (typeof(Task).GetMethod(nameof(Task.FromResult)){{#newline}}
            ?.MakeGenericMethod(expectedResultType){{#newline}}
            .Invoke(null, new[] { executionResult }))!;{{#newline}}
    }{{#newline}}{{#newline}}

    public IAsyncEnumerator<TEntity> GetAsyncEnumerator(CancellationToken cancellationToken = default){{#newline}}
    {{{#newline}}
        return new FakeDbAsyncEnumerator<TEntity>(this.AsEnumerable().GetEnumerator());{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeDbAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>{{#newline}}
{{{#newline}}
    public FakeDbAsyncEnumerable(IEnumerable<T> enumerable){{#newline}}
        : base(enumerable){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public FakeDbAsyncEnumerable(Expression expression){{#newline}}
        : base(expression){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        return new FakeDbAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());{{#newline}}
    }{{#newline}}{{#newline}}

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        return GetAsyncEnumerator(cancellationToken);{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator IEnumerable.GetEnumerator(){{#newline}}
    {{{#newline}}
        return this.AsEnumerable().GetEnumerator();{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeDbAsyncEnumerator<T> : IAsyncEnumerator<T>{{#newline}}
{{{#newline}}
    private readonly IEnumerator<T> _inner;{{#newline}}{{#newline}}

    public FakeDbAsyncEnumerator(IEnumerator<T> inner){{#newline}}
    {{{#newline}}
        _inner = inner;{{#newline}}
    }{{#newline}}{{#newline}}

    public T Current{{#newline}}
    {{{#newline}}
        get { return _inner.Current; }{{#newline}}
    }{{#newline}}{{#newline}}

    public ValueTask<bool> MoveNextAsync(){{#newline}}
    {{{#newline}}
        return new ValueTask<bool>(_inner.MoveNext());{{#newline}}
    }{{#newline}}{{#newline}}

    public ValueTask DisposeAsync(){{#newline}}
    {{{#newline}}
        _inner.Dispose();{{#newline}}
        return new ValueTask(Task.CompletedTask);{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}


public abstract class FakeQueryProvider<T> : IOrderedQueryable<T>, IQueryProvider{{#newline}}
{{{#newline}}
    private IEnumerable<T>? _enumerable;{{#newline}}{{#newline}}

    protected FakeQueryProvider(Expression expression){{#newline}}
    {{{#newline}}
        Expression = expression;{{#newline}}
    }{{#newline}}{{#newline}}

    protected FakeQueryProvider(IEnumerable<T> enumerable){{#newline}}
    {{{#newline}}
        _enumerable = enumerable;{{#newline}}
        Expression = enumerable.AsQueryable().Expression;{{#newline}}
    }{{#newline}}{{#newline}}

    public IQueryable CreateQuery(Expression expression){{#newline}}
    {{{#newline}}
        if (expression is MethodCallExpression m){{#newline}}
        {{{#newline}}
            var resultType = m.Method.ReturnType; // it should be IQueryable<T>{{#newline}}
            var tElement = resultType.GetGenericArguments().First();{{#newline}}
            return (IQueryable) CreateInstance(tElement, expression);{{#newline}}
        }{{#newline}}{{#newline}}

        return CreateQuery<T>(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public IQueryable<TEntity> CreateQuery<TEntity>(Expression expression){{#newline}}
    {{{#newline}}
        return (IQueryable<TEntity>) CreateInstance(typeof(TEntity), expression);{{#newline}}
    }{{#newline}}{{#newline}}

    private object CreateInstance(Type tElement, Expression expression){{#newline}}
    {{{#newline}}
        var queryType = GetType().GetGenericTypeDefinition().MakeGenericType(tElement);{{#newline}}
        return Activator.CreateInstance(queryType, expression)!;{{#newline}}
    }{{#newline}}{{#newline}}

    public object Execute(Expression expression){{#newline}}
    {{{#newline}}
        return CompileExpressionItem<object>(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public TResult Execute<TResult>(Expression expression){{#newline}}
    {{{#newline}}
        return CompileExpressionItem<TResult>(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator<T> IEnumerable<T>.GetEnumerator(){{#newline}}
    {{{#newline}}
        if (_enumerable == null) _enumerable = CompileExpressionItem<IEnumerable<T>>(Expression);{{#newline}}
        return _enumerable.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator IEnumerable.GetEnumerator(){{#newline}}
    {{{#newline}}
        if (_enumerable == null) _enumerable = CompileExpressionItem<IEnumerable<T>>(Expression);{{#newline}}
        return _enumerable.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    public Type ElementType => typeof(T);{{#newline}}{{#newline}}

    public Expression Expression { get; }{{#newline}}{{#newline}}

    public IQueryProvider Provider => this;{{#newline}}{{#newline}}

    private static TResult CompileExpressionItem<TResult>(Expression expression){{#newline}}
    {{{#newline}}
        var visitor = new FakeExpressionVisitor();{{#newline}}
        var body = visitor.Visit(expression);{{#newline}}
        var f = Expression.Lambda<Func<TResult>>(body ?? throw new InvalidOperationException(string.Format(""{0} is null"", nameof(body))), (IEnumerable<ParameterExpression>?) null);{{#newline}}
        return f.Compile()();{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeExpressionVisitor : ExpressionVisitor{{#newline}}
{{{#newline}}
}{{#newline}}{{#newline}}

// An inert DbContext with no provider and no connection string.{{#newline}}
//      DatabaseFacade's constructor demands a DbContext, and this is the one the fake hands it. Earlier versions{{#newline}}
//      passed a real, fully configured context here, which meant anything that slipped past the guards in{{#newline}}
//      FakeDatabaseFacade below executed against the real database your connection string points at. This one has{{#newline}}
//      nowhere to go: with no provider configured, the worst any missed route can do is throw.{{#newline}}
public class FakeDbContextShim : DbContext{{#newline}}
{{{#newline}}
    public FakeDbContextShim() : base(new DbContextOptions<FakeDbContextShim>()){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder){{#newline}}
    {{{#newline}}
        // Deliberately empty. Configuring a provider here would defeat the point of the shim.{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}

// Note on what this fake can and cannot intercept:{{#newline}}
//      EF Core exposes ExecuteSqlRaw(), ExecuteSqlInterpolated(), SqlQueryRaw(), GetDbConnection(),{{#newline}}
//      OpenConnection(), Migrate(), UseTransaction() and friends as extension methods on DatabaseFacade{{#newline}}
//      (RelationalDatabaseFacadeExtensions). Extension methods are static, so they are not virtual and this{{#newline}}
//      fake cannot override them one by one. They reach the database by one of three routes:{{#newline}}
//          1. IDatabaseFacadeDependenciesAccessor.Dependencies - ExecuteSqlRaw, SqlQueryRaw, GetDbConnection,{{#newline}}
//             OpenConnection, UseTransaction, Get/SetCommandTimeout, IsRelational, ...{{#newline}}
//          2. IInfrastructure<IServiceProvider>.Instance - Migrate, GetPendingMigrations, GetAppliedMigrations,{{#newline}}
//             GenerateCreateScript, HasPendingModelChanges, ...{{#newline}}
//          3. IDatabaseFacadeDependenciesAccessor.Context, then that DbContext's own service provider -{{#newline}}
//             IsRelational, GetService<T>, ...{{#newline}}
//      All three throw below, and the DbContext handed to the base constructor is an inert FakeDbContextShim{{#newline}}
//      with no provider, so a route we have not thought of cannot reach a database either.{{#newline}}
//      Note that IsRelational() and Get/SetCommandTimeout() therefore throw as well, even though they issue no{{#newline}}
//      SQL themselves. Returning a value for them would mean implementing IDatabaseFacadeDependencies in full -{{#newline}}
//      eleven members of EF Core internals that change between releases, which would break this generated file{{#newline}}
//      every time you upgraded EF Core. Throwing with an explanation is the honest trade.{{#newline}}
//      If your code calls any of these, put the raw SQL behind your own interface and fake that interface{{#newline}}
//      instead, or write an integration test against a real (or in-memory provider) database.{{#newline}}
public class FakeDatabaseFacade : DatabaseFacade, IDatabaseFacadeDependenciesAccessor, IInfrastructure<IServiceProvider>{{#newline}}
{{{#newline}}
    private IDbContextTransaction? _currentTransaction;{{#newline}}{{#newline}}

    // Pass a FakeDbContextShim here, never a real DbContext.{{#newline}}
    public FakeDatabaseFacade(DbContext context) : base(context){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    private static NotSupportedException NotSupported(){{#newline}}
    {{{#newline}}
        return new NotSupportedException({{#newline}}
            ""This is a Fake DbContext, so relational Database operations are not supported. That includes "" +{{#newline}}
            ""ExecuteSqlRaw(), ExecuteSqlInterpolated(), SqlQueryRaw(), GetDbConnection(), OpenConnection(), "" +{{#newline}}
            ""UseTransaction(), BeginTransaction(IsolationLevel), GetCommandTimeout(), SetCommandTimeout(), "" +{{#newline}}
            ""IsRelational() and Migrate(). They are extension methods on DatabaseFacade rather than virtual "" +{{#newline}}
            ""members, so the fake cannot intercept them and would otherwise run against your real database. "" +{{#newline}}
            ""Use the parameterless BeginTransaction(), or put the raw SQL behind your own interface and fake "" +{{#newline}}
            ""that interface instead, or write an integration test against a real database."");{{#newline}}
    }{{#newline}}{{#newline}}

    IDatabaseFacadeDependencies IDatabaseFacadeDependenciesAccessor.Dependencies{{#newline}}
    {{{#newline}}
        get { throw NotSupported(); }{{#newline}}
    }{{#newline}}{{#newline}}

    IServiceProvider IInfrastructure<IServiceProvider>.Instance{{#newline}}
    {{{#newline}}
        get { throw NotSupported(); }{{#newline}}
    }{{#newline}}{{#newline}}

    // IsRelational() and GetService<T>() reach the database through this rather than through Dependencies: they{{#newline}}
    // ask this DbContext for its service provider. Handing out the shim would be safe, but it would surface the{{#newline}}
    // shim's ""No database provider has been configured"" error, which tells the reader nothing about why their{{#newline}}
    // fake behaved that way. Throwing explains it instead.{{#newline}}
    DbContext IDatabaseFacadeDependenciesAccessor.Context{{#newline}}
    {{{#newline}}
        get { throw NotSupported(); }{{#newline}}
    }{{#newline}}{{#newline}}

    // Reported so that IsSqlServer()/IsNpgsql()/IsSqlite() and similar provider checks still branch correctly.{{#newline}}
    // Null when the database type has no EF Core provider, or when a reader plugin makes it unknowable, in which{{#newline}}
    // case every IsXxx() check answers false rather than claiming to be a provider you are not using.{{#newline}}
    public override string? ProviderName{{#newline}}
    {{{#newline}}
        get { return {{DatabaseProviderNameLiteral}}; }{{#newline}}
    }{{#newline}}{{#newline}}

    public override IDbContextTransaction? CurrentTransaction{{#newline}}
    {{{#newline}}
        get { return _currentTransaction; }{{#newline}}
    }{{#newline}}{{#newline}}

    // Called by FakeDbContextTransaction when it is committed, rolled back or disposed. The reference check{{#newline}}
    // matters: a stale transaction being disposed must not clear a newer one that has since begun.{{#newline}}
    internal void ClearTransaction(IDbContextTransaction transaction){{#newline}}
    {{{#newline}}
        if (ReferenceEquals(_currentTransaction, transaction)){{#newline}}
            _currentTransaction = null;{{#newline}}
    }{{#newline}}{{#newline}}

    public override bool EnsureCreated(){{#newline}}
    {{{#newline}}
        return true;{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<bool>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(EnsureCreated());{{#newline}}
    }{{#newline}}{{#newline}}

    public override bool EnsureDeleted(){{#newline}}
    {{{#newline}}
        return true;{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<bool>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(EnsureDeleted());{{#newline}}
    }{{#newline}}{{#newline}}

    public override bool CanConnect(){{#newline}}
    {{{#newline}}
        return true;{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<bool> CanConnectAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<bool>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(CanConnect());{{#newline}}
    }{{#newline}}{{#newline}}

    // EF Core does not support nested transactions, and a real provider throws here rather than quietly{{#newline}}
    // replacing the active one. The fake does the same, so a test cannot pass on something that would fail{{#newline}}
    // against your database.{{#newline}}
    public override IDbContextTransaction BeginTransaction(){{#newline}}
    {{{#newline}}
        if (_currentTransaction != null){{#newline}}
            throw new InvalidOperationException({{#newline}}
                ""The Fake DbContext is already in a transaction. EF Core does not support nested transactions, "" +{{#newline}}
                ""so commit, roll back or dispose the current transaction before beginning another."");{{#newline}}
{{#newline}}
        _currentTransaction = new FakeDbContextTransaction(this);{{#newline}}
        return _currentTransaction;{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled<IDbContextTransaction>(cancellationToken);{{#newline}}
{{#newline}}
        return Task.FromResult(BeginTransaction());{{#newline}}
    }{{#newline}}{{#newline}}

    // Routed through the transaction rather than nulling the field directly, so that committing via{{#newline}}
    // Database.CommitTransaction() and committing via the transaction object end up in the same place.{{#newline}}
    public override void CommitTransaction(){{#newline}}
    {{{#newline}}
        var transaction = _currentTransaction;{{#newline}}
        if (transaction != null){{#newline}}
            transaction.Commit();{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task CommitTransactionAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled(cancellationToken);{{#newline}}
{{#newline}}
        CommitTransaction();{{#newline}}
        return Task.CompletedTask;{{#newline}}
    }{{#newline}}{{#newline}}

    public override void RollbackTransaction(){{#newline}}
    {{{#newline}}
        var transaction = _currentTransaction;{{#newline}}
        if (transaction != null){{#newline}}
            transaction.Rollback();{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task RollbackTransactionAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled(cancellationToken);{{#newline}}
{{#newline}}
        RollbackTransaction();{{#newline}}
        return Task.CompletedTask;{{#newline}}
    }{{#newline}}{{#newline}}

    public override IExecutionStrategy CreateExecutionStrategy(){{#newline}}
    {{{#newline}}
        return new FakeExecutionStrategy();{{#newline}}
    }{{#newline}}{{#newline}}

    public override string ToString(){{#newline}}
    {{{#newline}}
        return string.Empty;{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}

// Runs the operation once, without retrying. This lets code written around{{#newline}}
// Database.CreateExecutionStrategy().Execute(...) run under the fake.{{#newline}}
// Note: the DbContext passed to the operation is null, as the fake is not a real DbContext. Overloads that{{#newline}}
// ignore it (the common strategy.Execute(() => ...) form) work; overloads that use it will not.{{#newline}}
public class FakeExecutionStrategy : IExecutionStrategy{{#newline}}
{{{#newline}}
    public bool RetriesOnFailure { get { return false; } }{{#newline}}{{#newline}}

    public TResult Execute<TState, TResult>({{#newline}}
        TState state,{{#newline}}
        Func<DbContext, TState, TResult> operation,{{#newline}}
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded){{#newline}}
    {{{#newline}}
        return operation(null!, state);{{#newline}}
    }{{#newline}}{{#newline}}

    public Task<TResult> ExecuteAsync<TState, TResult>({{#newline}}
        TState state,{{#newline}}
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,{{#newline}}
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,{{#newline}}
        CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        return operation(null!, state, cancellationToken);{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}

// Committing, rolling back or disposing clears Database.CurrentTransaction, matching real EF Core. A{{#newline}}
// transaction constructed directly rather than by Database.BeginTransaction() has no facade to tell, and so{{#newline}}
// just tracks its own state.{{#newline}}
public class FakeDbContextTransaction : IDbContextTransaction{{#newline}}
{{{#newline}}
    private readonly FakeDatabaseFacade? _database;{{#newline}}{{#newline}}

    public FakeDbContextTransaction(){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public FakeDbContextTransaction(FakeDatabaseFacade database){{#newline}}
    {{{#newline}}
        _database = database;{{#newline}}
    }{{#newline}}{{#newline}}

    public Guid TransactionId { get; } = Guid.NewGuid();{{#newline}}{{#newline}}

    public void Commit(){{#newline}}
    {{{#newline}}
        Clear();{{#newline}}
    }{{#newline}}{{#newline}}

    public void Rollback(){{#newline}}
    {{{#newline}}
        Clear();{{#newline}}
    }{{#newline}}{{#newline}}

    public Task CommitAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled(cancellationToken);{{#newline}}
{{#newline}}
        Commit();{{#newline}}
        return Task.CompletedTask;{{#newline}}
    }{{#newline}}{{#newline}}

    public Task RollbackAsync(CancellationToken cancellationToken = new CancellationToken()){{#newline}}
    {{{#newline}}
        if (cancellationToken.IsCancellationRequested){{#newline}}
            return Task.FromCanceled(cancellationToken);{{#newline}}
{{#newline}}
        Rollback();{{#newline}}
        return Task.CompletedTask;{{#newline}}
    }{{#newline}}{{#newline}}

    public void Dispose(){{#newline}}
    {{{#newline}}
        Clear();{{#newline}}
    }{{#newline}}{{#newline}}

    public ValueTask DisposeAsync(){{#newline}}
    {{{#newline}}
        Dispose();{{#newline}}
        return default;{{#newline}}
    }{{#newline}}{{#newline}}

    private void Clear(){{#newline}}
    {{{#newline}}
        if (_database != null){{#newline}}
            _database.ClearTransaction(this);{{#newline}}
    }{{#newline}}
}";
        }

        public override List<string> PocoUsings(PocoModel data)
        {
            var usings = new List<string>
            {
                "System",
                "System.Collections.Generic",
                "System.Threading",
                "System.Threading.Tasks",
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.HasHierarchyId || Settings.UseDataAnnotations)
                usings.Add("Microsoft.EntityFrameworkCore");

            if (data.HasSqlVector)
                usings.Add("Microsoft.Data.SqlTypes");

            if (data.ColumnTypeNamespaces != null)
                usings.AddRange(data.ColumnTypeNamespaces);

            return usings;
        }

        public override string Poco()
        {
            return @"
{{#if UseHasNoKey}}
{{#else}}
{{#if HasNoPrimaryKey}}
// The table '{{Name}}' is not usable by entity framework because it{{#newline}}
// does not have a primary key. It is listed here for completeness.{{#newline}}
{{/if}}
{{/if}}

{{ClassComment}}
{{ExtendedComments}}
{{ClassAttributes}}
{{ClassModifier}} class {{NameHumanCaseWithSuffix}}{{BaseClasses}}{{#newline}}
{{{#newline}}
{{InsideClassBody}}

{{#each Columns}}
{{#if AddNewLineBefore}}{{#newline}}{{/if}}
{{#if HasSummaryComments}}
    /// <summary>{{#newline}}
    /// {{SummaryComments}}{{#newline}}
    /// </summary>{{#newline}}
{{/if}}
{{#each Attributes}}
    {{this}}{{#newline}}
{{/each}}
    public {{#if OverrideModifier}}override {{/if}}{{#if IsPartial}}partial {{/if}}{{WrapIfNullable}} {{NameHumanCase}} { get; {{PrivateSetterForComputedColumns}}set; }{{PropertyInitialisers}}{{InlineComments}}{{#newline}}
{{#if IncludeFieldNameConstants}}    public const string {{NameHumanCase}}Field = ""{{NameHumanCase}}"";{{#newline}}{{/if}}
{{/each}}

{{#if HasOwnedEntities}}
{{#newline}}
    // Owned entities{{#newline}}
{{#each OwnedEntities}}
    public {{PropertyType}} {{PropertyName}} { get; set; }{{PropertyInitialiser}}{{#newline}}
{{/each}}
{{/if}}

{{#if HasReverseNavigation}}
{{#newline}}
    // Reverse navigation{{#newline}}

{{#each ReverseNavigationProperty}}

{{#if ReverseNavHasComment}}
{{#newline}}
    /// <summary>{{#newline}}
    /// {{ReverseNavComment}}{{#newline}}
    /// </summary>{{#newline}}
{{/if}}

{{#each AdditionalReverseNavigationsDataAnnotations}}
    [{{this}}]{{#newline}}
{{/each}}

{{#each AdditionalDataAnnotations}}
    [{{this}}]{{#newline}}
{{/each}}

    {{Definition}}{{#newline}}
{{/each}}
{{/if}}


{{#if HasForeignKey}}
{{#newline}}
{{ForeignKeyTitleComment}}

{{#each ForeignKeys}}

{{#if HasFkComment}}
{{#newline}}
    /// <summary>{{#newline}}
    /// {{FkComment}}{{#newline}}
    /// </summary>{{#newline}}
{{/if}}

{{#each AdditionalForeignKeysDataAnnotations}}
    [{{this}}]{{#newline}}
{{/each}}

{{#each AdditionalDataAnnotations}}
    [{{this}}]{{#newline}}
{{/each}}

    {{Definition}}{{#newline}}
{{/each}}
{{/if}}

{{#if CreateConstructor}}
{{#newline}}
    public {{NameHumanCaseWithSuffix}}(){{#newline}}
    {{{#newline}}

{{#each ColumnsWithDefaults}}
        {{NameHumanCase}} = {{Default}};{{#newline}}
{{/each}}

{{#each ReverseNavigationCtor}}
        {{this}}{{#newline}}
{{/each}}

{{#if EntityClassesArePartial}}
        InitializePartial();{{#newline}}
{{/if}}

    }{{#newline}}

{{#if EntityClassesArePartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
{{/if}}

{{/if}}

}{{#newline}}
";
        }

        public override List<string> PocoConfigurationUsings(PocoConfigurationModel data)
        {
            var usings = new List<string>
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Metadata.Builders"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (Settings.TrimCharFields)
                usings.Add("Microsoft.EntityFrameworkCore.Storage.ValueConversion");

            if (data.UsesDictionary)
                usings.Add("System.Collections.Generic");

            return usings;
        }

        public override string PocoConfiguration()
        {
            return @"
{{ClassComment}}
{{ClassModifier}} class {{ConfigurationClassName}} : IEntityTypeConfiguration<{{NameHumanCaseWithSuffix}}>{{#newline}}
{{{#newline}}

    public void Configure(EntityTypeBuilder<{{NameHumanCaseWithSuffix}}> builder){{#newline}}
    {{{#newline}}
{{#if NotUsingDataAnnotations}}
{{#if HasSchema}}
        builder.{{ToTableOrView}}(""{{Name}}"", ""{{Schema}}""{{#if HasTableComment}}, t => t.HasComment(@""{{TableComment}}""){{/if}});{{#newline}}
{{#else}}
        builder.{{ToTableOrView}}(""{{Name}}""{{#if HasTableComment}}, t => t.HasComment(@""{{TableComment}}""){{/if}});{{#newline}}
{{/if}}
{{/if}}
        {{PrimaryKeyNameHumanCase}}{{#newline}}{{#newline}}

{{#each Columns}}
        {{this}}{{#newline}}
{{/each}}

{{#if HasForeignKey}}
{{#newline}}
        // Foreign keys{{#newline}}
{{#each ForeignKeys}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}

{{#if HasOwnedEntityConfigs}}
{{#newline}}
        // Owned entities{{#newline}}
{{#each OwnedEntityConfigs}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}

{{#each MappingConfiguration}}
        builder.{{this}}{{#newline}}
{{/each}}

{{#if HasIndexes}}
{{#newline}}
{{#each Indexes}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}

{{#if ConfigurationClassesArePartial}}
{{#newline}}
        InitializePartial(builder);{{#newline}}
{{/if}}

    }{{#newline}}

{{#if ConfigurationClassesArePartial}}
{{#newline}}
    partial void InitializePartial(EntityTypeBuilder<{{NameHumanCaseWithSuffix}}> builder);{{#newline}}
{{/if}}

}{{#newline}}";
        }

        public override List<string> StoredProcReturnModelUsings()
        {
            var usings = new List<string>
            {
                "System",
                "System.Collections.Generic"
            };

            if (Settings.UseDataAnnotations)
                usings.Add("System.ComponentModel.DataAnnotations.Schema");

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string StoredProcReturnModels()
        {
            return @"
{{ResultClassModifiers}} class {{WriteStoredProcReturnModelName}}{{#newline}}
{{{#newline}}
{{#if SingleModel}}
{{#each SingleModelReturnColumns}}
    {{this}}{{#newline}}
{{/each}}
{{#else}}
{{#each MultipleModelReturnColumns}}
    public class ResultSetModel{{Model}}{{#newline}}
    {{{#newline}}
{{#each ReturnColumns}}
        {{this}}{{#newline}}
{{/each}}
    }{{#newline}}
    public List<ResultSetModel{{Model}}> ResultSet{{Model}}{{PropertyGetSet}}{{NullForgivingOperator}}{{#newline}}
{{/each}}
{{/if}}
}{{#newline}}
";
        }

        public override List<string> EnumUsings()
        {
            var usings = new List<string>();

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string Enums()
        {
            return @"
{{#each EnumAttributes}}
{{this}}{{#newline}}
{{/each}}
public enum {{EnumName}}{{#newline}}
{{{#newline}}
{{#each Items}}
{{#each Attributes}}
    {{this}}{{#newline}}
{{/each}}
    {{Key}} = {{Value}},{{#newline}}
{{/each}}
}{{#newline}}
";
        }

        public override List<string> OwnedEntityClassUsings(OwnedEntityClassModel data)
        {
            var usings = new List<string>();

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string OwnedEntityClass()
        {
            return @"
{{ClassModifier}} class {{ClassName}}{{#newline}}
{{{#newline}}
{{#each Properties}}
    public {{WrappedType}} {{PropertyName}} { get; set; }{{PropertyInitialiser}}{{#newline}}
{{/each}}
}{{#newline}}
";
        }
    }
}