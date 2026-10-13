using System.Collections.Generic;
using System.Linq;
using Efrpg.Filtering;
using Efrpg.TemplateModels;

namespace Efrpg.Templates
{
    /// <summary>
    /// {{Mustache}} template documentation available at https://github.com/jehugaleahsa/mustache-sharp
    /// </summary>
    public class TemplateEf6 : Template
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
                usings.Add("System.Data.Entity");
                usings.Add("System.Data.Entity.Spatial");
                usings.Add("System.Linq");
            }

            if (data.hasStoredProcs)
                usings.Add("System.Collections.Generic");

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Data.Entity.Infrastructure");
                usings.Add("System.Collections.Generic");
                usings.Add("System.Data.Entity.Validation");
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
    Task<int> SaveChangesAsync();{{#newline}}
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);{{#newline}}
    DbChangeTracker ChangeTracker { get; }{{#newline}}
    DbContextConfiguration Configuration { get; }{{#newline}}
    Database Database { get; }{{#newline}}
    DbEntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;{{#newline}}
    DbEntityEntry Entry(object entity);{{#newline}}
    IEnumerable<DbEntityValidationResult> GetValidationErrors();{{#newline}}
    DbSet Set(Type entityType);{{#newline}}
    DbSet<TEntity> Set<TEntity>() where TEntity : class;{{#newline}}
    string ToString();{{#newline}}
{{/if}}


{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}
{{#if HasError}}
    // Unable to determine return model for '{{FunctionName}}'. Error: {{Error}}{{#newline}}
{{/if}}
    {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseTrue}});{{#newline}}
{{#if SingleReturnModel}}
    {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsTrueTrue}});{{#newline}}
{{/if}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}
    Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseTrueToken}});{{#newline}}
{{/if}}
{{#newline}}
{{/each}}
{{/if}}

{{#if hasTableValuedFunctions}}
{{#newline}}
    // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
{{#newline}}
    [DbFunction(""{{DbContextName}}"", ""{{Name}}"")]{{#newline}}
    [CodeFirstStoreFunctions.DbFunctionDetails(DatabaseSchema = ""{{Schema}}""{{#if SingleReturnModel}}, ResultColumnName = ""{{SingleReturnColumnName}}""{{/if}})]{{#newline}}
    IQueryable<{{ReturnClassName}}> {{ExecName}}({{WriteStoredProcFunctionParamsFalseTrue}});{{#newline}}
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
                "System.Data.Common",
                "System.Data.Entity",
                "System.Data.Entity.Core.Objects",
                "System.Data.Entity.Infrastructure",
                "System.Data.Entity.Infrastructure.Interception",
                "System.Data.Entity.Infrastructure.Annotations",
                "System.ComponentModel.DataAnnotations.Schema",
                "System.Data.SqlTypes",
                "System.Threading.Tasks",
                "System.Threading"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.tables.Any() || data.hasStoredProcs)
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Data.Entity.Spatial");
                usings.Add("System.Linq");
            }

            if (data.hasStoredProcs)
            {
                usings.Add("System.Collections.Generic");
            }

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Data.Entity.Infrastructure");
                usings.Add("System.Collections.Generic");
                usings.Add("System.Data.Entity.Validation");
            }

            if (Settings.DatabaseType == DatabaseType.SqlServer)
                usings.Add("System.Data.SqlClient");

            if (Settings.DatabaseType == DatabaseType.PostgreSQL)
            {
                usings.Add("Npgsql");
                usings.Add("NpgsqlTypes");
            }

            return usings;
        }

        public override string DatabaseContext()
        {
            return @"
{{DbContextClassModifiers}} class {{DbContextName}} : {{DbContextBaseClass}}{{contextInterface}}{{#newline}}
{{{#newline}}

{{#each tables}}
    {{DbSetModifier}} DbSet<{{DbSetName}}> {{PluralTableName}} { get; set; }{{Comment}}{{#newline}}
{{/each}}
{{#newline}}


    static {{DbContextName}}(){{#newline}}
    {{{#newline}}
        System.Data.Entity.Database.SetInitializer{{setInitializer}}{{#newline}}
    }{{#newline}}{{#newline}}


{{#if AddParameterlessConstructorToDbContext}}
    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(){{#newline}}
{{#if HasDefaultConstructorArgument}}
        : base({{DefaultConstructorArgument}}){{#newline}}
{{/if}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}
{{/if}}


    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(string connectionString){{#newline}}
        : base(connectionString){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(string connectionString, DbCompiledModel model){{#newline}}
        : base(connectionString, model){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(DbConnection existingConnection, bool contextOwnsConnection){{#newline}}
        : base(existingConnection, contextOwnsConnection){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(DbConnection existingConnection, DbCompiledModel model, bool contextOwnsConnection){{#newline}}
        : base(existingConnection, model, contextOwnsConnection){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

{{#if IncludeObjectContextConstructor}}
    /// <inheritdoc />{{#newline}}
    public {{DbContextName}}(ObjectContext objectContext, bool dbContextOwnsObjectContext){{#newline}}
        : base(objectContext, dbContextOwnsObjectContext){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}
{{/if}}


    protected override void Dispose(bool disposing){{#newline}}
    {{{#newline}}
{{#if DbContextClassIsPartial}}
        DisposePartial(disposing);{{#newline}}
{{/if}}
        base.Dispose(disposing);{{#newline}}
    }{{#newline}}
{{#newline}}


    public bool IsSqlParameterNull({{SqlParameter}} param){{#newline}}
    {{{#newline}}
        var sqlValue = param.{{SqlParameterValue}};{{#newline}}
        var nullableValue = sqlValue as INullable;{{#newline}}
        if (nullableValue != null){{#newline}}
            return nullableValue.IsNull;{{#newline}}
        return (sqlValue == null || sqlValue == DBNull.Value);{{#newline}}
    }{{#newline}}{{#newline}}


    protected override void OnModelCreating(DbModelBuilder modelBuilder){{#newline}}
    {{{#newline}}
        base.OnModelCreating(modelBuilder);{{#newline}}
{{#if hasTableValuedFunctions}}
{{#newline}}
        modelBuilder.Conventions.Add(new CodeFirstStoreFunctions.FunctionsConvention<{{DbContextName}}>(""{{DefaultSchema}}""));{{#newline}}
{{#if hasTableValuedFunctionComplexTypes}}
{{#newline}}
{{#each tableValuedFunctionComplexTypes}}
        modelBuilder.ComplexType<{{this}}>();{{#newline}}
{{/each}}
{{/if}}
{{/if}}

{{#if hasTables}}
{{#newline}}
{{#each tables}}
        modelBuilder.Configurations.Add(new {{DbSetConfigName}}());{{#newline}}
{{/each}}
{{/if}}

{{#if hasIndexes}}
{{#newline}}
        // Indexes{{#newline}}
{{#each indexes}}
        {{this}}{{#newline}}
{{/each}}
{{/if}}

{{#if DbContextClassIsPartial}}
{{#newline}}
        OnModelCreatingPartial(modelBuilder);{{#newline}}
{{/if}}

    }{{#newline}}
{{#newline}}



    public static DbModelBuilder CreateModel(DbModelBuilder modelBuilder, string schema){{#newline}}
    {{{#newline}}
{{#each tables}}
        modelBuilder.Configurations.Add(new {{DbSetConfigName}}(schema));{{#newline}}
{{/each}}
{{#newline}}
{{#if DbContextClassIsPartial}}
        OnCreateModelPartial(modelBuilder, schema);{{#newline}}
{{#newline}}
{{/if}}
        return modelBuilder;{{#newline}}
    }{{#newline}}


{{#if DbContextClassIsPartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
    partial void DisposePartial(bool disposing);{{#newline}}
    partial void OnModelCreatingPartial(DbModelBuilder modelBuilder);{{#newline}}
    static partial void OnCreateModelPartial(DbModelBuilder modelBuilder, string schema);{{#newline}}
{{/if}}


{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}
{{#if HasReturnModels}}
    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
{{#if SingleReturnModel}}
    {{{#newline}}
        int procResult;{{#newline}}
        return {{FunctionName}}({{WriteStoredProcFunctionOverloadCall}});{{#newline}}
    }{{#newline}}
{{#newline}}
    public {{ReturnType}} {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
{{/if}}
    {{{#newline}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
{{#if SingleReturnModel}}
        var procResultData = Database.SqlQuery<{{WriteStoredProcReturnModelName}}>(""{{Exec}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrue}}).ToList();{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{#if HasProcResult}}
        procResult = (int) procResultParam.Value;{{#newline}}
{{#else}}
        procResult = 0;{{#newline}}
{{/if}}
{{#else}}
        var procResultData = new {{WriteStoredProcReturnModelName}}();{{#newline}}
        var cmd = Database.Connection.CreateCommand();{{#newline}}
        cmd.CommandTimeout = Database.CommandTimeout ?? cmd.CommandTimeout;{{#newline}}
        cmd.CommandType = CommandType.StoredProcedure;{{#newline}}
        cmd.CommandText = ""{{Exec}}"";{{#newline}}
{{#each Parameters}}
        cmd.Parameters.Add({{this}});{{#newline}}
{{/each}}
{{#newline}}
        try{{#newline}}
        {{{#newline}}
            DbInterception.Dispatch.Connection.Open(Database.Connection, new DbInterceptionContext());{{#newline}}
            var reader = cmd.ExecuteReader();{{#newline}}
            var objectContext = ((IObjectContextAdapter) this).ObjectContext;{{#newline}}
{{#each ReturnModelResultSetReaderCommand}}
{{#newline}}
            procResultData.ResultSet{{Index}} = objectContext.Translate<{{WriteStoredProcReturnModelName}}.ResultSetModel{{Index}}>(reader).ToList();{{#newline}}
            reader.{{ReaderCommand}}();{{#newline}}
{{/each}}
        }{{#newline}}
        finally{{#newline}}
        {{{#newline}}
            DbInterception.Dispatch.Connection.Close(Database.Connection, new DbInterceptionContext());{{#newline}}
        }{{#newline}}
{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{/if}}
        return procResultData;{{#newline}}
    }{{#newline}}

{{#else}}{{#! if HasReturnModels }}
{{#if HasError}}
    // Unable to determine return model for '{{FunctionName}}'. Error: {{Error}}{{#newline}}
{{/if}}
    public int {{FunctionName}}({{WriteStoredProcFunctionParamsTrueFalse}}){{#newline}}
    {{{#newline}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
{{#if HasProcResult}}
{{#newline}}
{{/if}}
        Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, ""{{ExecWithNoReturnModel}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrue}});{{#newline}}
{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{#if HasProcResult}}
        return (int)procResultParam.Value;{{#newline}}
{{#else}}
        return 0;{{#newline}}
{{/if}}
    }{{#newline}}
{{/if}}{{#! if HasReturnModels }}
{{#newline}}
{{#if AsyncFunctionCannotBeCreated}}
    // {{FunctionName}}Async() cannot be created due to having out parameters, or is relying on the procedure result ({{ReturnType}}){{#newline}}
{{#else}}{{#! if AsyncFunctionCannotBeCreated }}
    public async Task<{{ReturnType}}> {{FunctionName}}Async({{WriteStoredProcFunctionParamsFalseFalseToken}}){{#newline}}
    {{{#newline}}
{{#if HasNoReturnModels}}
{{WriteStoredProcFunctionDeclareSqlParameterTrue}}
{{#if HasProcResult}}
{{#newline}}
{{/if}}
        await Database.ExecuteSqlCommandAsync(TransactionalBehavior.DoNotEnsureTransaction, ""{{AsyncExec}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayTrueToken}});{{#newline}}
{{#newline}}
{{#if HasProcResult}}
        return (int)procResultParam.Value;{{#newline}}
{{#else}}
        return 0;{{#newline}}
{{/if}}
{{#else}}
{{WriteStoredProcFunctionDeclareSqlParameterFalse}}
{{#if SingleReturnModel}}
        var procResultData = await Database.SqlQuery<{{WriteStoredProcReturnModelName}}>(""{{AsyncExec}}""{{WriteStoredProcFunctionSqlParameterAnonymousArrayFalse}}).ToListAsync(cancellationToken);{{#newline}}
{{#else}}
        var procResultData = new {{WriteStoredProcReturnModelName}}();{{#newline}}
        var cmd = Database.Connection.CreateCommand();{{#newline}}
        cmd.CommandTimeout = Database.CommandTimeout ?? cmd.CommandTimeout;{{#newline}}
        cmd.CommandType = CommandType.StoredProcedure;{{#newline}}
        cmd.CommandText = ""{{Exec}}"";{{#newline}}
{{#each Parameters}}
        cmd.Parameters.Add({{this}});{{#newline}}
{{/each}}
{{#newline}}
        try{{#newline}}
        {{{#newline}}
            await DbInterception.Dispatch.Connection.OpenAsync(Database.Connection, new DbInterceptionContext(), new CancellationToken()).ConfigureAwait(false);{{#newline}}
            var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);{{#newline}}
            var objectContext = ((IObjectContextAdapter) this).ObjectContext;{{#newline}}
{{#each ReturnModelResultSetReaderCommand}}
{{#newline}}
            procResultData.ResultSet{{Index}} = objectContext.Translate<{{WriteStoredProcReturnModelName}}.ResultSetModel{{Index}}>(reader).ToList();{{#newline}}
{{#if NotLastRecord}}
            await reader.NextResultAsync().ConfigureAwait(false);{{#newline}}
{{/if}}
{{/each}}
        }{{#newline}}
        finally{{#newline}}
        {{{#newline}}
            DbInterception.Dispatch.Connection.Close(Database.Connection, new DbInterceptionContext());{{#newline}}
        }{{#newline}}
{{#newline}}
{{WriteStoredProcFunctionSetSqlParametersFalse}}
{{/if}}{{#! if AsyncFunctionCannotBeCreated }}
        return procResultData;{{#newline}}
{{/if}}
    }{{#newline}}
{{#newline}}
{{/if}}
{{/each}}
{{/if}}

{{#if hasTableValuedFunctions}}
{{#newline}}
    // Table Valued Functions{{#newline}}
{{#each tableValuedFunctions}}
{{#newline}}
    [DbFunction(""{{DbContextName}}"", ""{{Name}}"")]{{#newline}}
    [CodeFirstStoreFunctions.DbFunctionDetails(DatabaseSchema = ""{{Schema}}""{{#if SingleReturnModel}}, ResultColumnName = ""{{SingleReturnColumnName}}""{{/if}})]{{#newline}}
    public IQueryable<{{ReturnClassName}}> {{ExecName}}({{WriteStoredProcFunctionParamsFalseFalse}}){{#newline}}
    {{{#newline}}
{{WriteTableValuedFunctionDeclareSqlParameter}}
{{#newline}}
        return ((IObjectContextAdapter)this).ObjectContext.CreateQuery<{{ReturnClassName}}>(""[{{DbContextName}}].[{{Name}}]({{WriteStoredProcFunctionSqlAtParams}})"", {{WriteTableValuedFunctionSqlParameterAnonymousArray}});{{#newline}}
    }{{#newline}}
{{/each}}
{{/if}}

{{#if hasScalarValuedFunctions}}
{{#newline}}
    // Scalar Valued Functions{{#newline}}
{{#each scalarValuedFunctions}}
{{#newline}}
    [DbFunction(""CodeFirstDatabaseSchema"", ""{{Name}}"")]{{#newline}}
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
                "System",
                "System.Data.Entity.Infrastructure"
            };
            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");
            return usings;
        }

        public override string DatabaseContextFactory()
        {
            return @"
{{classModifier}} class {{contextName}}Factory : IDbContextFactory<{{contextName}}>{{#newline}}
{{{#newline}}
    public {{contextName}} Create(){{#newline}}
    {{{#newline}}
        return new {{contextName}}();{{#newline}}
    }{{#newline}}
}";
        }

        public override List<string> FakeDatabaseContextUsings(FakeContextModel data, IDbContextFilter filter)
        {
            var usings = new List<string>
            {
                "System",
                "System.Data",
                "System.Data.Common",
                "System.Data.Entity",
                "System.Data.Entity.Core.Objects",
                "System.Data.Entity.Infrastructure",
                "System.Data.Entity.Infrastructure.Interception",
                "System.Data.Entity.Infrastructure.Annotations",
                "System.Data.Entity.Spatial",
                "System.Data.SqlTypes",
                "System.Threading.Tasks",
                "System.Threading"
            };

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            if (data.tables.Any() || data.hasStoredProcs)
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Linq");
            }

            if (data.hasStoredProcs)
                usings.Add("System.Collections.Generic");

            if (!Settings.UseInheritedBaseInterfaceFunctions)
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Data.Entity.Infrastructure");
                usings.Add("System.Collections.Generic");
                usings.Add("System.Data.Entity.Validation");
            }

            if (Settings.DatabaseType == DatabaseType.SqlServer)
                usings.Add("System.Data.SqlClient");

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
    {{DbSetModifier}} DbSet<{{DbSetName}}> {{PluralTableName}} { get; set; }{{Comment}}{{#newline}}
{{/each}}
{{#newline}}

    public Fake{{DbContextName}}(){{#newline}}
    {{{#newline}}
        _changeTracker = null;{{#newline}}
        _configuration = null;{{#newline}}
        _database = null;{{#newline}}
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
    public int SaveChanges(){{#newline}}
    {{{#newline}}
        ++SaveChangesCount;{{#newline}}
        return 1;{{#newline}}
    }{{#newline}}
{{#newline}}

    public Task<int> SaveChangesAsync(){{#newline}}
    {{{#newline}}
        ++SaveChangesCount;{{#newline}}
        return Task<int>.Factory.StartNew(() => 1);{{#newline}}
    }{{#newline}}{{#newline}}

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        ++SaveChangesCount;{{#newline}}
        return Task<int>.Factory.StartNew(() => 1, cancellationToken);{{#newline}}
    }{{#newline}}
{{#newline}}



{{#if DbContextClassIsPartial}}
    partial void InitializePartial();{{#newline}}
{{#newline}}
{{/if}}

    protected virtual void Dispose(bool disposing){{#newline}}
    {{{#newline}}
    }{{#newline}}
{{#newline}}

    public void Dispose(){{#newline}}
    {{{#newline}}
        Dispose(true);{{#newline}}
    }{{#newline}}
{{#newline}}

    private DbChangeTracker _changeTracker;{{#newline}}
{{#newline}}
    public DbChangeTracker ChangeTracker { get { return _changeTracker; } }{{#newline}}
{{#newline}}
    private DbContextConfiguration _configuration;{{#newline}}
{{#newline}}
    public DbContextConfiguration Configuration { get { return _configuration; } }{{#newline}}
{{#newline}}
    private Database _database;{{#newline}}
{{#newline}}
    public Database Database { get { return _database; } }{{#newline}}
{{#newline}}
    public DbEntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}
{{#newline}}
    public DbEntityEntry Entry(object entity){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}
{{#newline}}
    public IEnumerable<DbEntityValidationResult> GetValidationErrors(){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}
{{#newline}}
    public DbSet Set(Type entityType){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}
{{#newline}}
    public DbSet<TEntity> Set<TEntity>() where TEntity : class{{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}
{{#newline}}
    public override string ToString(){{#newline}}
    {{{#newline}}
        throw new NotImplementedException();{{#newline}}
    }{{#newline}}


{{#if hasStoredProcs}}
{{#newline}}
    // Stored Procedures{{#newline}}
{{#each storedProcs}}

{{#if HasReturnModels}}
{{#newline}}
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
    [DbFunction(""{{DbContextName}}"", ""{{Name}}"")]{{#newline}}
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
                "System.Linq",
                "System.Linq.Expressions",
                "System.Reflection",
                "System.Data.Entity",
                "System.Collections.ObjectModel",
                "System.Collections.Generic",
                "System.Data.Entity.Infrastructure",
                "System.Threading",
                "System.Threading.Tasks"
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
{{DbContextClassModifiers}} class FakeDbSet<TEntity> : DbSet<TEntity>, IQueryable, IEnumerable<TEntity>, IDbAsyncEnumerable<TEntity> where TEntity : class
{{#newline}}
{{{#newline}}
    private readonly PropertyInfo[] _primaryKeys;{{#newline}}
    private readonly ObservableCollection<TEntity> _data;{{#newline}}
    private readonly IQueryable _query;{{#newline}}{{#newline}}

    public FakeDbSet(){{#newline}}
    {{{#newline}}
        _primaryKeys = Array.Empty<PropertyInfo>();{{#newline}}
        _data = new ObservableCollection<TEntity>();{{#newline}}
        _query = _data.AsQueryable();{{#newline}}

{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    public FakeDbSet(params string[] primaryKeys){{#newline}}
    {{{#newline}}
        _primaryKeys = typeof(TEntity).GetProperties().Where(x => primaryKeys.Contains(x.Name)).ToArray();{{#newline}}
        _data = new ObservableCollection<TEntity>();{{#newline}}
        _query = _data.AsQueryable();{{#newline}}
{{#if DbContextClassIsPartial}}
        InitializePartial();{{#newline}}
{{/if}}
    }{{#newline}}{{#newline}}

    public override TEntity Find(params object[] keyValues){{#newline}}
    {{{#newline}}
        if (_primaryKeys == null){{#newline}}
            throw new ArgumentException(""No primary keys defined"");{{#newline}}
        if (keyValues.Length != _primaryKeys.Length){{#newline}}
            throw new ArgumentException(""Incorrect number of keys passed to Find method"");{{#newline}}{{#newline}}

        var keyQuery = this.AsQueryable();{{#newline}}
        keyQuery = keyValues{{#newline}}
            .Select((t, i) => i){{#newline}}
            .Aggregate(keyQuery,{{#newline}}
                (current, x) =>{{#newline}}
                    current.Where(entity => _primaryKeys[x].GetValue(entity, null).Equals(keyValues[x])));{{#newline}}{{#newline}}

        return keyQuery.SingleOrDefault();{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<TEntity> FindAsync(CancellationToken cancellationToken, params object[] keyValues){{#newline}}
    {{{#newline}}
        return Task<TEntity>.Factory.StartNew(() => Find(keyValues), cancellationToken);{{#newline}}
    }{{#newline}}{{#newline}}

    public override Task<TEntity> FindAsync(params object[] keyValues){{#newline}}
    {{{#newline}}
        return Task<TEntity>.Factory.StartNew(() => Find(keyValues));{{#newline}}
    }{{#newline}}{{#newline}}

    public override IEnumerable<TEntity> AddRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        var items = entities.ToList();{{#newline}}
        foreach (var entity in items){{#newline}}
        {{{#newline}}
            _data.Add(entity);{{#newline}}
        }{{#newline}}
        return items;{{#newline}}
    }{{#newline}}{{#newline}}

    public override TEntity Add(TEntity item){{#newline}}
    {{{#newline}}
        if (item == null) throw new ArgumentNullException(""item"");{{#newline}}
        _data.Add(item);{{#newline}}
        return item;{{#newline}}
    }{{#newline}}{{#newline}}

    public override IEnumerable<TEntity> RemoveRange(IEnumerable<TEntity> entities){{#newline}}
    {{{#newline}}
        if (entities == null) throw new ArgumentNullException(""entities"");{{#newline}}
        var items = entities.ToList();{{#newline}}
        foreach (var entity in items){{#newline}}
        {{{#newline}}
            _data.Remove(entity);{{#newline}}
        }{{#newline}}
        return items;{{#newline}}
    }{{#newline}}{{#newline}}

    public override TEntity Remove(TEntity item){{#newline}}
    {{{#newline}}
        if (item == null) throw new ArgumentNullException(""item"");{{#newline}}
        _data.Remove(item);{{#newline}}
        return item;{{#newline}}
    }{{#newline}}{{#newline}}

    public override TEntity Attach(TEntity item){{#newline}}
    {{{#newline}}
        if (item == null) throw new ArgumentNullException(""item"");{{#newline}}
        _data.Add(item);{{#newline}}
        return item;{{#newline}}
    }{{#newline}}{{#newline}}

    public override TEntity Create(){{#newline}}
    {{{#newline}}
        return Activator.CreateInstance<TEntity>();{{#newline}}
    }{{#newline}}{{#newline}}

    public override TDerivedEntity Create<TDerivedEntity>(){{#newline}}
    {{{#newline}}
        return Activator.CreateInstance<TDerivedEntity>();{{#newline}}
    }{{#newline}}{{#newline}}

    public override ObservableCollection<TEntity> Local{{#newline}}
    {{{#newline}}
        get { return _data; }{{#newline}}
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
        get { return new FakeDbAsyncQueryProvider<TEntity>(_query.Provider); }{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator IEnumerable.GetEnumerator(){{#newline}}
    {{{#newline}}
        return _data.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator(){{#newline}}
    {{{#newline}}
        return _data.GetEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    IDbAsyncEnumerator<TEntity> IDbAsyncEnumerable<TEntity>.GetAsyncEnumerator(){{#newline}}
    {{{#newline}}
        return new FakeDbAsyncEnumerator<TEntity>(_data.GetEnumerator());{{#newline}}
    }{{#newline}}

{{#if DbContextClassIsPartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
{{/if}}
}

{{#newline}}{{#newline}}
{{DbContextClassModifiers}} class FakeDbAsyncQueryProvider<TEntity> : IDbAsyncQueryProvider{{#newline}}
{{{#newline}}
    private readonly IQueryProvider _inner;{{#newline}}{{#newline}}

    public FakeDbAsyncQueryProvider(IQueryProvider inner){{#newline}}
    {{{#newline}}
        _inner = inner;{{#newline}}
    }{{#newline}}{{#newline}}

    public IQueryable CreateQuery(Expression expression){{#newline}}
    {{{#newline}}
        var m = expression as MethodCallExpression;{{#newline}}
        if (m != null){{#newline}}
        {{{#newline}}
            var resultType = m.Method.ReturnType; // it should be IQueryable<T>{{#newline}}
            var tElement = resultType.GetGenericArguments()[0];{{#newline}}
            var queryType = typeof(FakeDbAsyncEnumerable<>).MakeGenericType(tElement);{{#newline}}
            return (IQueryable) Activator.CreateInstance(queryType, expression);{{#newline}}
        }{{#newline}}
        return new FakeDbAsyncEnumerable<TEntity>(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression){{#newline}}
    {{{#newline}}
        var queryType = typeof(FakeDbAsyncEnumerable<>).MakeGenericType(typeof(TElement));{{#newline}}
        return (IQueryable<TElement>) Activator.CreateInstance(queryType, expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public object Execute(Expression expression){{#newline}}
    {{{#newline}}
        return _inner.Execute(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public TResult Execute<TResult>(Expression expression){{#newline}}
    {{{#newline}}
        return _inner.Execute<TResult>(expression);{{#newline}}
    }{{#newline}}{{#newline}}

    public Task<object> ExecuteAsync(Expression expression, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        return Task.FromResult(Execute(expression));{{#newline}}
    }{{#newline}}{{#newline}}

    public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        return Task.FromResult(Execute<TResult>(expression));{{#newline}}
    }{{#newline}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeDbAsyncEnumerable<T> : EnumerableQuery<T>, IDbAsyncEnumerable<T>, IQueryable<T>{{#newline}}
{{{#newline}}
    public FakeDbAsyncEnumerable(IEnumerable<T> enumerable){{#newline}}
        : base(enumerable){{#newline}}
    { }{{#newline}}{{#newline}}

    public FakeDbAsyncEnumerable(Expression expression){{#newline}}
        : base(expression){{#newline}}
    { }{{#newline}}{{#newline}}

    public IDbAsyncEnumerator<T> GetAsyncEnumerator(){{#newline}}
    {{{#newline}}
        return new FakeDbAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());{{#newline}}
    }{{#newline}}{{#newline}}

    IDbAsyncEnumerator IDbAsyncEnumerable.GetAsyncEnumerator(){{#newline}}
    {{{#newline}}
        return GetAsyncEnumerator();{{#newline}}
    }{{#newline}}{{#newline}}

    IQueryProvider IQueryable.Provider{{#newline}}
    {{{#newline}}
        get { return new FakeDbAsyncQueryProvider<T>(this); }{{#newline}}
    }{{#newline}}{{#newline}}
}{{#newline}}{{#newline}}


{{DbContextClassModifiers}} class FakeDbAsyncEnumerator<T> : IDbAsyncEnumerator<T>{{#newline}}
{{{#newline}}
    private readonly IEnumerator<T> _inner;{{#newline}}{{#newline}}

    public FakeDbAsyncEnumerator(IEnumerator<T> inner){{#newline}}
    {{{#newline}}
        _inner = inner;{{#newline}}
    }{{#newline}}{{#newline}}

    public void Dispose(){{#newline}}
    {{{#newline}}
        _inner.Dispose();{{#newline}}
    }{{#newline}}{{#newline}}

    public Task<bool> MoveNextAsync(CancellationToken cancellationToken){{#newline}}
    {{{#newline}}
        return Task.FromResult(_inner.MoveNext());{{#newline}}
    }{{#newline}}{{#newline}}

    public T Current{{#newline}}
    {{{#newline}}
        get { return _inner.Current; }{{#newline}}
    }{{#newline}}{{#newline}}

    object IDbAsyncEnumerator.Current{{#newline}}
    {{{#newline}}
        get { return Current; }{{#newline}}
    }{{#newline}}
}";
        }

        public override List<string> PocoUsings(PocoModel data)
        {
            var usings = new List<string>
            {
                "System",
                "System.Data.Entity.Infrastructure",
                "System.Collections.Generic",
                "System.Threading",
                "System.Threading.Tasks",
            };

            if (data.HasSpatial)
                usings.Add("System.Data.Entity.Spatial");

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string Poco()
        {
            return @"
{{#if HasNoPrimaryKey}}
// The table '{{Name}}' is not usable by entity framework because it{{#newline}}
// does not have a primary key. It is listed here for completeness.{{#newline}}
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
                "System",
                "System.Data.Entity.Spatial",
                "System.Data.Entity.ModelConfiguration",
                "System.ComponentModel.DataAnnotations.Schema"
            };

            if (data.HasSpatial)
                usings.Add("System.Data.Entity.Spatial");

            if (Settings.IncludeCodeGeneratedAttribute)
                usings.Add("System.CodeDom.Compiler");

            return usings;
        }

        public override string PocoConfiguration()
        {
            return @"
{{ClassComment}}
{{ClassModifier}} class {{ConfigurationClassName}} : EntityTypeConfiguration<{{NameHumanCaseWithSuffix}}>{{#newline}}
{{{#newline}}

    public {{ConfigurationClassName}}(){{#newline}}
        : this(""{{Schema}}""){{#newline}}
    {{{#newline}}
    }{{#newline}}{{#newline}}

    public {{ConfigurationClassName}}(string schema){{#newline}}
    {{{#newline}}
{{#if NotUsingDataAnnotations}}
{{#if HasSchema}}
        ToTable(""{{Name}}"", schema);{{#newline}}
{{#else}}
        ToTable(""{{Name}}"");{{#newline}}
{{/if}}
{{/if}}
        HasKey({{PrimaryKeyNameHumanCase}});{{#newline}}{{#newline}}

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

{{#each MappingConfiguration}}
        {{this}}{{#newline}}
{{/each}}

{{#if ConfigurationClassesArePartial}}
{{#newline}}
        InitializePartial();{{#newline}}
{{/if}}

    }{{#newline}}

{{#if ConfigurationClassesArePartial}}
{{#newline}}
    partial void InitializePartial();{{#newline}}
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
    public List<ResultSetModel{{Model}}> ResultSet{{Model}}{{PropertyGetSet}}{{#newline}}
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

        // Owned entity class generation is EF Core only
        public override List<string> OwnedEntityClassUsings(OwnedEntityClassModel data) => new List<string>();
        public override string OwnedEntityClass() => string.Empty;
    }
}