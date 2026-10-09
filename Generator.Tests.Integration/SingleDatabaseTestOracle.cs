using System.Collections.Generic;
using Efrpg;
using Efrpg.Filtering;
using Efrpg.Templates;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Integration
{
    /// <summary>
    ///     Reverse engineers the Oracle EfrpgTest schema in the efrpg-oracle container.
    /// </summary>
    /// <remarks>
    ///     Unlike MySQL and PostgreSQL there is no schema in an Oracle connection string: the user is the schema.
    ///     TestDatabases/Oracle/docker-compose.yml only seeds northwind.sql, so EfrpgTest.sql has to be loaded by
    ///     hand into its own user - see the header of that script for the sqlplus line.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.Oracle)]
    public class SingleDatabaseTestOracle : SingleDatabaseTestBase
    {
        public void SetupOracle(string schema, string connectionStringName, string dbContextName, TemplateType templateType)
        {
            SetupDatabase(connectionStringName, dbContextName, templateType);

            Settings.ConnectionString = $"User Id={schema};Password=abc123;Data Source=localhost:1521/pdb1;";
            Settings.DatabaseType = DatabaseType.Oracle;

            Settings.Enumerations = new List<EnumerationSettings>
            {
                new EnumerationSettings
                {
                    Name = "Status",
                    Table = "EFRPGTEST.STATUS",
                    NameField = "NAME",
                    ValueField = "ID",
                    GroupField = string.Empty
                }
            };
        }

        [Test]
        [NonParallelizable]
        [TestCase(false, false)]
        [TestCase(true,  false)]
        [TestCase(false, true)]
        public void ReverseEngineerOracle_EfCore(bool useDataAnnotations, bool allowNullStrings)
        {
            // Arrange
            // Per-case settings must come after SetupOracle: SetupDatabase resets the leak-prone settings
            // (AllowNullStrings et al.) to defaults, so anything assigned before it is clobbered.
            SetupOracle("efrpgtest", "MyDbContext", "MyDbContext", TemplateType.EfCore8);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;
            Settings.UseDataAnnotations = useDataAnnotations;
            Settings.AllowNullStrings = allowNullStrings;

            // Act
            var filename = "EfrpgTest" +
                           (useDataAnnotations ? "Da" : string.Empty) +
                           (allowNullStrings ? "Ans" : string.Empty);
            Run(filename, ".Oracle", null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [NonParallelizable]
        public void NonPascalCased()
        {
            // Arrange - Oracle folds unquoted identifiers to UPPER_SNAKE_CASE, so this is the setting that decides
            // whether the generated model reads as C# or as a catalogue dump. Worth a golden of its own.
            SetupOracle("efrpgtest", "My_db_context", "Efrpg_db_context", TemplateType.EfCore8);
            Settings.GenerateSeparateFiles = false;
            Settings.UsePascalCase = false;
            Settings.UseMappingTables = false;

            // Act
            const string filename = "NonPascalCased";
            Run(filename, ".Oracle", null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        /// <summary>
        ///     Reverse engineers EFRPGSYN, an application schema that owns one table and reaches the rest of its model
        ///     through private synonyms for another schema's tables, view, procedure, function and package. Loaded by
        ///     TestDatabases/Oracle/EfrpgTestSynonyms.sql, whose header lists what each synonym is there to prove.
        ///     The tool-side assertions on the raw rows live in the Efrpg repository's OracleSynonymIntegrationTests;
        ///     this golden pins what the template makes of them - entities named after the synonyms, and navigation
        ///     properties between them.
        /// </summary>
        [Test]
        [NonParallelizable]
        [Ignore("Golden not yet baselined: run once against the efrpg-oracle container, review the output, add " +
                "TestComparison\\EfrpgTestSyn_Oracle_EfCore8.cs to the project, then remove this attribute.")]
        public void Synonyms()
        {
            // Arrange
            SetupOracle("efrpgsyn", "MyDbContext", "MyDbContext", TemplateType.EfCore8);
            Settings.Enumerations = new List<EnumerationSettings>(); // SetupOracle's STATUS lookup lives in EFRPGTEST
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;

            // Act
            const string filename = "EfrpgTestSyn";
            Run(filename, ".Oracle", null);

            // Assert
            CompareAgainstTestComparison(filename);
        }
    }
}
