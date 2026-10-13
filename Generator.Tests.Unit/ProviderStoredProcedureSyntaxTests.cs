using System.Collections.Generic;
using System.Data;
using Efrpg;
using Efrpg.Generators;
using Efrpg.TemplateModels;
using Efrpg.Templates;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     The stored procedure code the template writes for PostgreSQL, MySQL and Oracle. It used to be SQL Server's
    ///     everywhere - EXEC @procResult = [schema].[name] ... OUTPUT, SqlDbType on a provider parameter that has no such
    ///     property, and a return status no other database has - so none of it compiled, let alone ran. Every expected
    ///     call below was executed against the provider's real driver through EF Core before it was written down.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class ProviderStoredProcedureSyntaxTests
    {
        private DatabaseType _savedDatabaseType;
        private TemplateType _savedTemplateType;

        [SetUp]
        public void SetUp()
        {
            _savedDatabaseType = Settings.DatabaseType;
            _savedTemplateType = Settings.TemplateType;
            Settings.TemplateType = TemplateType.EfCore8;
        }

        [TearDown]
        public void TearDown()
        {
            Settings.DatabaseType = _savedDatabaseType;
            Settings.TemplateType = _savedTemplateType;
        }

        private static StoredProcedureParameter Parameter(int ordinal, string name, string nameHumanCase, StoredProcedureParameterMode mode,
            string propertyType = "decimal", string dataType = "number")
        {
            return new StoredProcedureParameter
            {
                Ordinal       = ordinal,
                Name          = name,
                NameHumanCase = nameHumanCase,
                Mode          = mode,
                PropertyType  = propertyType,
                DataType      = dataType,
                SqlDbType     = dataType
            };
        }

        private static StoredProcedure Procedure(string schema, string name, params StoredProcedureParameter[] parameters)
        {
            return new StoredProcedure
            {
                Schema            = new Schema(schema),
                DbName            = name,
                NameHumanCase     = name,
                IsStoredProcedure = true,
                ReturnModels      = new List<List<DataColumn>>(),
                Parameters        = new List<StoredProcedureParameter>(parameters)
            };
        }

        // EFRPGTEST.COUNT_CHILDREN(PARENT_KEY_ONE IN NUMBER, CHILD_COUNT OUT NUMBER, RUNNING_TOTAL IN OUT NUMBER)
        private static StoredProcedure OracleCountChildren()
        {
            return Procedure("EFRPGTEST", "COUNT_CHILDREN",
                Parameter(1, "PARENT_KEY_ONE", "parentKeyOne", StoredProcedureParameterMode.In),
                Parameter(2, "CHILD_COUNT",    "childCount",   StoredProcedureParameterMode.Out),
                Parameter(3, "RUNNING_TOTAL",  "runningTotal", StoredProcedureParameterMode.InOut));
        }

        [TestCase(DatabaseType.Oracle)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSQL)]
        public void WriteStoredProcFunctionParams_NonSqlServerOutParameter_IsAnOutArgument(DatabaseType databaseType)
        {
            // Arrange
            Settings.DatabaseType = databaseType;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionParams(true, false);

            // Assert
            Assert.That(result, Is.EqualTo("decimal? parentKeyOne, out decimal? childCount, ref decimal? runningTotal"));
        }

        [Test]
        public void WriteStoredProcFunctionParams_InParameterBeforeAnOutParameter_IsNotOptional()
        {
            // Arrange - C# allows no optional parameter before an out parameter, so the tail heuristic must stop there.
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGSYN", "SUBMIT_ORDER",
                Parameter(1, "P_CUSTOMER_ID", "pCustomerId", StoredProcedureParameterMode.In),
                Parameter(2, "P_ORDER_ID",    "pOrderId",    StoredProcedureParameterMode.Out));

            // Act
            var result = sut.WriteStoredProcFunctionParams(false, false);

            // Assert
            Assert.That(result, Is.EqualTo("decimal? pCustomerId, out decimal? pOrderId"));
        }

        [Test]
        public void WriteStoredProcFunctionParams_SqlServerOutParameter_IsStillLeftOut()
        {
            // Arrange - SQL Server reports OUTPUT parameters as INOUT; OUT only ever marks a function's return value.
            Settings.DatabaseType = DatabaseType.SqlServer;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionParams(false, false);

            // Assert
            Assert.That(result, Is.EqualTo("decimal? parentKeyOne, ref decimal? runningTotal"));
        }

        [TestCase(DatabaseType.Oracle)]
        [TestCase(DatabaseType.MySql)]
        public void WriteStoredProcFunctionDeclareSqlParameter_OracleOrMySql_UsesTheProviderNeutralDbType(DatabaseType databaseType)
        {
            // Arrange - with OracleDbType instead, ODP.NET returns OracleDecimal, which the (decimal) cast cannot unbox.
            Settings.DatabaseType = databaseType;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(true);

            // Assert
            Assert.That(result, Does.Contain("ParameterName = \"PARENT_KEY_ONE\", DbType = DbType.Decimal, Direction = ParameterDirection.Input"));
            Assert.That(result, Does.Not.Contain("SqlDbType"));
        }

        [Test]
        public void WriteStoredProcFunctionDeclareSqlParameter_PostgreSql_LeavesTheTypeToNpgsql()
        {
            // Arrange - Npgsql infers from the value; DbType.DateTime would reject a UTC value bound for timestamptz.
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(true);

            // Assert
            Assert.That(result, Does.Contain("new NpgsqlParameter { ParameterName = \"PARENT_KEY_ONE\", Direction = ParameterDirection.Input"));
            Assert.That(result, Does.Not.Contain("SqlDbType"));
        }

        [TestCase(DatabaseType.Oracle)]
        [TestCase(DatabaseType.MySql)]
        [TestCase(DatabaseType.PostgreSQL)]
        public void WriteStoredProcFunctionDeclareSqlParameter_NonSqlServer_DeclaresNoProcResult(DatabaseType databaseType)
        {
            // Arrange
            Settings.DatabaseType = databaseType;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(true);

            // Assert
            Assert.That(result, Does.Not.Contain("procResult"));
        }

        [Test]
        public void WriteStoredProcFunctionDeclareSqlParameter_SqlServer_KeepsSqlDbTypeAndProcResult()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.SqlServer;
            var sut = Procedure("dbo", "Proc", Parameter(1, "@id", "id", StoredProcedureParameterMode.In, "int", "int"));
            sut.Parameters[0].SqlDbType = "Int";

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(true);

            // Assert
            Assert.That(result, Does.Contain("new SqlParameter { ParameterName = \"@id\", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Input"));
            Assert.That(result, Does.Contain(
                "var procResultParam = new SqlParameter { ParameterName = \"@procResult\", SqlDbType = SqlDbType.Int, Direction = ParameterDirection.Output };"));
        }

        [Test]
        public void WriteStoredProcFunctionDeclareSqlParameter_NonSqlServerUnmappableType_OmitsTheDbType()
        {
            // Arrange - leaves the provider to infer the type from the value rather than guess at one.
            Settings.DatabaseType = DatabaseType.MySql;
            var sut = Procedure("EfrpgTest", "proc", Parameter(1, "duration", "duration", StoredProcedureParameterMode.In, "TimeSpan", "time"));

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(false);

            // Assert
            Assert.That(result, Does.Contain("new MySqlParameter { ParameterName = \"duration\", Direction = ParameterDirection.Input"));
        }

        [TestCase(StoredProcedureParameterMode.Out)]
        [TestCase(StoredProcedureParameterMode.InOut)]
        public void WriteStoredProcFunctionDeclareSqlParameter_OracleStringOutWithoutLength_SizesThePlSqlBuffer(StoredProcedureParameterMode mode)
        {
            // Arrange - ALL_ARGUMENTS has no length for a PL/SQL parameter, and ODP.NET raises ORA-06502 without one.
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGTEST", "GET_NAME", Parameter(1, "NAME", "name", mode, "string", "varchar2"));

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(false);

            // Assert
            Assert.That(result, Does.Contain(", Size = 32767"));
        }

        [Test]
        public void WriteStoredProcFunctionDeclareSqlParameter_OracleRefCursor_BindsAsRefCursor()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGTEST", "GET_CHILDREN_BY_PARENT",
                Parameter(1, "RESULTS", "results", StoredProcedureParameterMode.Out, "object", "ref cursor"));

            // Act
            var result = sut.WriteStoredProcFunctionDeclareSqlParameter(false);

            // Assert
            Assert.That(result, Does.Contain(
                "new OracleParameter { ParameterName = \"RESULTS\", OracleDbType = OracleDbType.RefCursor, Direction = ParameterDirection.Output"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_Oracle_IsAnAnonymousBlockWithBindVariables()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = OracleCountChildren();

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert - written into a C# string literal, hence the escaped quotes.
            Assert.That(result, Is.EqualTo("BEGIN \\\"EFRPGTEST\\\".\\\"COUNT_CHILDREN\\\"(:PARENT_KEY_ONE, :CHILD_COUNT, :RUNNING_TOTAL); END;"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_OraclePackageMember_QuotesThePackageAndMemberSeparately()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGTEST", "ORDER_PKG.TOUCH_PARENT",
                Parameter(1, "KEY_ONE", "keyOne", StoredProcedureParameterMode.In),
                Parameter(2, "KEY_TWO", "keyTwo", StoredProcedureParameterMode.In));

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert
            Assert.That(result, Is.EqualTo("BEGIN \\\"EFRPGTEST\\\".\\\"ORDER_PKG\\\".\\\"TOUCH_PARENT\\\"(:KEY_ONE, :KEY_TWO); END;"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_OracleNoParameters_CallsWithEmptyParentheses()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGTEST", "NO_PARAMETERS_PROC");

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert
            Assert.That(result, Is.EqualTo("BEGIN \\\"EFRPGTEST\\\".\\\"NO_PARAMETERS_PROC\\\"(); END;"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_PostgreSql_IsACallStatement()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var sut = Procedure("public", "touch_parent",
                Parameter(1, "key_one", "keyOne", StoredProcedureParameterMode.In, "int", "integer"),
                Parameter(2, "key_two", "keyTwo", StoredProcedureParameterMode.In, "int", "integer"));

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert
            Assert.That(result, Is.EqualTo("CALL \\\"public\\\".\\\"touch_parent\\\"(@key_one, @key_two)"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_PostgreSqlOutParameter_PassesNullInItsPlace()
        {
            // Arrange - Npgsql refuses an out-only parameter named in the SQL, and fills it from the row CALL returns.
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var sut = Procedure("public", "count_children",
                Parameter(1, "parent_key_one", "parentKeyOne", StoredProcedureParameterMode.In,    "int", "integer"),
                Parameter(2, "child_count",    "childCount",   StoredProcedureParameterMode.Out,   "int", "integer"),
                Parameter(3, "running_total",  "runningTotal", StoredProcedureParameterMode.InOut, "int", "integer"));

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert
            Assert.That(result, Is.EqualTo("CALL \\\"public\\\".\\\"count_children\\\"(@parent_key_one, NULL, @running_total)"));
        }

        [Test]
        public void WriteStoredProcFunctionCallSql_MySql_IsACallStatementWithBacktickQuoting()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.MySql;
            var sut = Procedure("EfrpgTest", "GetChildrenByParent",
                Parameter(1, "parentKeyOne", "parentKeyOne", StoredProcedureParameterMode.In, "int", "int"),
                Parameter(2, "parentKeyTwo", "parentKeyTwo", StoredProcedureParameterMode.In, "int", "int"));

            // Act
            var result = sut.WriteStoredProcFunctionCallSql();

            // Assert
            Assert.That(result, Is.EqualTo("CALL `EfrpgTest`.`GetChildrenByParent`(@parentKeyOne, @parentKeyTwo)"));
        }

        [TestCase(DatabaseType.MySql,      StoredProcedureParameterMode.Out,   true)]
        [TestCase(DatabaseType.MySql,      StoredProcedureParameterMode.InOut, true)]
        [TestCase(DatabaseType.MySql,      StoredProcedureParameterMode.In,    false)]
        [TestCase(DatabaseType.Oracle,     StoredProcedureParameterMode.Out,   false)]
        [TestCase(DatabaseType.PostgreSQL, StoredProcedureParameterMode.InOut, false)]
        [TestCase(DatabaseType.SqlServer,  StoredProcedureParameterMode.InOut, false)]
        public void ExecutesAsStoredProcedureCommand_ByProviderAndMode_OnlyForMySqlOutputParameters(DatabaseType databaseType,
            StoredProcedureParameterMode mode, bool expected)
        {
            // Arrange - MySqlConnector refuses any direction but Input on a text command, so CALL cannot carry them.
            Settings.DatabaseType = databaseType;
            var sut = Procedure("EfrpgTest", "CountChildren", Parameter(1, "childCount", "childCount", mode, "int", "int"));

            // Act
            var result = sut.ExecutesAsStoredProcedureCommand();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(DatabaseType.SqlServer,  true)]
        [TestCase(DatabaseType.Oracle,     false)]
        [TestCase(DatabaseType.MySql,      false)]
        [TestCase(DatabaseType.PostgreSQL, false)]
        [TestCase(DatabaseType.SQLite,     false)]
        public void ReturnsProcResult_ByProvider_OnlySqlServerHasAReturnStatus(DatabaseType databaseType, bool expected)
        {
            // Arrange
            Settings.DatabaseType = databaseType;

            // Act
            var result = StoredProcedure.ReturnsProcResult();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void WriteTableValuedFunctionDeclareSqlParameter_NameWithoutAtPrefix_KeepsItsFirstCharacter()
        {
            // Arrange - EF6 ObjectParameter names; only SQL Server's names carry an @ to strip.
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            Settings.TemplateType = TemplateType.Ef6;
            var sut = Procedure("public", "children_of", Parameter(1, "parent_key_one", "parentKeyOne", StoredProcedureParameterMode.In, "int", "integer"));

            // Act
            var result = sut.WriteTableValuedFunctionDeclareSqlParameter();

            // Assert
            Assert.That(result, Does.Contain("new ObjectParameter(\"parent_key_one\", typeof(int))"));
        }

        [Test]
        public void WriteStoredProcFunctionSqlAtParams_PostgreSqlFunctionWithOutParameters_PassesInputsAsEntitySqlParameters()
        {
            // Arrange - an OUT parameter of a PostgreSQL function is a result column, not an argument, and OUTPUT is T-SQL.
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            Settings.TemplateType = TemplateType.Ef6;
            var sut = Procedure("public", "count_children",
                Parameter(1, "parent_key_one", "parentKeyOne", StoredProcedureParameterMode.In,    "int", "integer"),
                Parameter(2, "child_count",    "childCount",   StoredProcedureParameterMode.Out,   "int", "integer"),
                Parameter(3, "running_total",  "runningTotal", StoredProcedureParameterMode.InOut, "int", "integer"));
            sut.IsStoredProcedure     = false;
            sut.IsTableValuedFunction = true;

            // Act
            var result = sut.WriteStoredProcFunctionSqlAtParams();

            // Assert
            Assert.That(result, Is.EqualTo("@parent_key_one, @running_total"));
        }

        [Test]
        public void WriteStoredProcFunctionSqlAtParams_SqlServer_IsUnchanged()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.SqlServer;
            var sut = Procedure("dbo", "Proc",
                Parameter(1, "@a", "a", StoredProcedureParameterMode.In,    "int", "int"),
                Parameter(2, "@b", "b", StoredProcedureParameterMode.InOut, "int", "int"));

            // Act
            var result = sut.WriteStoredProcFunctionSqlAtParams();

            // Assert
            Assert.That(result, Is.EqualTo("@a, @b OUTPUT"));
        }

        [TestCase(DatabaseType.SqlServer,  "dbo",       "CsvToInt",       "[dbo].[CsvToInt]")]
        [TestCase(DatabaseType.PostgreSQL, "public",    "count_children", "\\\"public\\\".\\\"count_children\\\"")]
        [TestCase(DatabaseType.Oracle,     "EFRPGTEST", "CHILDREN",       "\\\"EFRPGTEST\\\".\\\"CHILDREN\\\"")]
        [TestCase(DatabaseType.MySql,      "EfrpgTest", "Children",       "`EfrpgTest`.`Children`")]
        public void WriteStoredProcFunctionQualifiedName_ByProvider_UsesThatProvidersQuoting(DatabaseType databaseType, string schema, string name,
            string expected)
        {
            // Arrange
            Settings.DatabaseType = databaseType;
            var sut = Procedure(schema, name);

            // Act
            var result = sut.WriteStoredProcFunctionQualifiedName();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        private static StoredProcedure PostgreSqlCountChildrenFunction()
        {
            var sut = Procedure("public", "count_children",
                Parameter(1, "parent_key_one", "parentKeyOne", StoredProcedureParameterMode.In,    "int", "integer"),
                Parameter(2, "child_count",    "childCount",   StoredProcedureParameterMode.Out,   "int", "integer"),
                Parameter(3, "running_total",  "runningTotal", StoredProcedureParameterMode.InOut, "int", "integer"));
            sut.IsStoredProcedure     = false;
            sut.IsTableValuedFunction = true;
            return sut;
        }

        [Test]
        public void WriteNetCoreTableValuedFunctionsSqlAtParams_PostgreSqlFunctionWithOutParameter_HasNoPlaceholderForTheResultColumn()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var sut = PostgreSqlCountChildrenFunction();

            // Act
            var result = sut.WriteNetCoreTableValuedFunctionsSqlAtParams();

            // Assert
            Assert.That(result, Is.EqualTo("{0}, {1}"));
        }

        [Test]
        public void WriteStoredProcFunctionSqlParameterAnonymousArray_PostgreSqlFunctionWithOutParameter_PassesOnlyTheArguments()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var sut = PostgreSqlCountChildrenFunction();

            // Act
            var result = sut.WriteStoredProcFunctionSqlParameterAnonymousArray(false, false);

            // Assert
            Assert.That(result, Is.EqualTo(", parentKeyOne, runningTotal"));
        }

        [Test]
        public void WriteTableValuedFunctionDeclareSqlParameter_PostgreSqlFunctionWithOutParameter_DeclaresOnlyTheArguments()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            Settings.TemplateType = TemplateType.Ef6;
            var sut = PostgreSqlCountChildrenFunction();

            // Act
            var result = sut.WriteTableValuedFunctionDeclareSqlParameter();

            // Assert
            Assert.That(result, Does.Not.Contain("childCount"));
        }

        [TestCase(DatabaseType.MySql,  "MySqlConnector")]
        [TestCase(DatabaseType.Oracle, "Oracle.ManagedDataAccess.Client")]
        public void DatabaseContextUsings_StoredProcsOnProvider_ImportsTheParameterTypesNamespace(DatabaseType databaseType, string expected)
        {
            // Arrange
            Settings.DatabaseType = databaseType;
            var data = new ContextModel { tables = new List<TableTemplateData>(), hasStoredProcs = true };

            // Act
            var result = new TemplateEfCore8().DatabaseContextUsings(data);

            // Assert
            Assert.That(result, Does.Contain(expected));
        }

        [Test]
        public void DatabaseContextUsings_MySqlStoredProcs_ImportsStorageForTheAmbientTransaction()
        {
            // Arrange - GetDbTransaction(), which a MySQL stored procedure command needs, lives there.
            Settings.DatabaseType = DatabaseType.MySql;
            var data = new ContextModel { tables = new List<TableTemplateData>(), hasStoredProcs = true };

            // Act
            var result = new TemplateEfCore8().DatabaseContextUsings(data);

            // Assert
            Assert.That(result, Does.Contain("Microsoft.EntityFrameworkCore.Storage"));
        }

        [Test]
        public void DbFunctionArguments_OraclePackageFunction_IsABuiltInThreePartName()
        {
            // Arrange - EF quotes a DbFunction name as one identifier, so ORDER_PKG.DOUBLE_IT must not be its name.
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = Procedure("EFRPGTEST", "ORDER_PKG.DOUBLE_IT");
            sut.IsStoredProcedure      = false;
            sut.IsScalarValuedFunction = true;

            // Act
            var result = sut.WriteDbFunctionArguments();

            // Assert
            Assert.That(result, Is.EqualTo("\"\\\"EFRPGTEST\\\".\\\"ORDER_PKG\\\".\\\"DOUBLE_IT\\\"\", IsBuiltIn = true"));
        }

        [TestCase(DatabaseType.Oracle,    "EFRPGTEST", "IS_HIGH_VALUE")]
        [TestCase(DatabaseType.SqlServer, "dbo",       "udfNetSale")]
        public void DbFunctionArguments_StandaloneFunction_IsNameAndSchema(DatabaseType databaseType, string schema, string name)
        {
            // Arrange
            Settings.DatabaseType = databaseType;
            var sut = Procedure(schema, name);
            sut.IsStoredProcedure      = false;
            sut.IsScalarValuedFunction = true;

            // Act
            var result = sut.WriteDbFunctionArguments();

            // Assert
            Assert.That(result, Is.EqualTo("\"" + name + "\", \"" + schema + "\""));
        }
    }
}
