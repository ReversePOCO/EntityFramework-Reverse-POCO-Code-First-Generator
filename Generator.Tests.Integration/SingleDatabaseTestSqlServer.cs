using System.Collections.Generic;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.Templates;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class SingleDatabaseTestSqlServer : SingleDatabaseTestBase
    {
        public void SetupSqlServer(
            string database,
            string connectionStringName,
            string dbContextName,
            TemplateType templateType)
        {
            SetupDatabase(connectionStringName, dbContextName, templateType);

            Settings.ConnectionString =
                $"Data Source=(local);Initial Catalog={database};Integrated Security=True;Encrypt=false;TrustServerCertificate=true;Application Name=Generator";
            Settings.DatabaseType = DatabaseType.SqlServer;
        }

        [Test]
        [TestCase("EfrpgTest", ".V3TestE1", "MyDbContext", "EfrpgTestDbContext", TemplateType.Ef6, false, false, false)]
        [TestCase("EfrpgTest", ".V3TestE8a", "MyDbContext", "EfrpgTestDbContext", TemplateType.EfCore8, false, false, false)]
        [TestCase("EfrpgTest", ".V3TestE8b", "MyDbContext", "EfrpgTestDbContext", TemplateType.EfCore8, false, true, false)]
        [TestCase("EfrpgTest", ".V3TestE8c", "MyDbContext", "EfrpgTestDbContext", TemplateType.EfCore8, false, false, true)]
        [TestCase("EfrpgTest", ".V3TestE8d", "MyDbContext", "EfrpgTestDbContext", TemplateType.EfCore8, false, true, true)]
        [TestCase("EfrpgTest", ".V3TestE1Da", "MyDbContext", "EfrpgTestDbContextDa", TemplateType.Ef6, true, false, false)]
        [TestCase("EfrpgTest", ".V3TestE8Da", "MyDbContext", "EfrpgTestDbContextDa", TemplateType.EfCore8, true, false, false)]
        [TestCase("EfrpgTest", ".V3TestE8Db", "MyDbContext", "EfrpgTestDbContextDa", TemplateType.EfCore8, true, true, false)]
        [TestCase("EfrpgTest", ".V3TestE8Dc", "MyDbContext", "EfrpgTestDbContextDa", TemplateType.EfCore8, true, false, true)]
        [TestCase("EfrpgTest", ".V3TestE8Dd", "MyDbContext", "EfrpgTestDbContextDa", TemplateType.EfCore8, true, true, true)]
        public void ReverseEngineerSqlServer(string database, string singleDbContextSubNamespace, string connectionStringName, string dbContextName,
            TemplateType templateType, bool useDataAnnotations, bool allowNullStrings, bool nullableReverseNavigationProperties)
        {
            // Arrange
            SetupSqlServer(database, connectionStringName, dbContextName, templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = true;
            Settings.TrimCharFields = false;
            Settings.UseDataAnnotations = useDataAnnotations;
            Settings.AllowNullStrings = allowNullStrings;
            Settings.NullableReverseNavigationProperties = nullableReverseNavigationProperties;

            Settings.Enumerations = new List<EnumerationSettings>
            {
                new EnumerationSettings
                {
                    Name = "DaysOfWeek", // Enum to generate. e.g. "DaysOfWeek" would result in "public enum DaysOfWeek {...}"
                    Table = "EnumTest.DaysOfWeek", // Database table containing enum values. e.g. "DaysOfWeek"
                    NameField = "TypeName", // Column containing the name for the enum. e.g. "TypeName"
                    ValueField = "TypeId", // Column containing the values for the enum. e.g. "TypeId"
                    DescriptionField = "Description",
                    GenerateDescriptionFromName = true
                },
                new EnumerationSettings
                {
                    Name = "Invalid",
                    Table = "x",
                    NameField = "y",
                    ValueField = "z"
                },
                new EnumerationSettings
                {
                    Name = "CarOptions",
                    Table = "EnumsWithStringAsValue",
                    NameField = "enum_name",
                    ValueField = "value",
                    DescriptionField = "Description",
                    GenerateDescriptionFromName = true
                }
            };

            var enumDefinitions = new List<EnumDefinition>
            {
                new EnumDefinition { Schema = "EnumTest", Table = "OpenDays", Column = "TypeId", EnumType = "DaysOfWeek" }
            };

            // Act
            var filename = database + 
                           (useDataAnnotations ? "Da" : string.Empty) +
                           (allowNullStrings ? "Ans" : string.Empty) + 
                           (nullableReverseNavigationProperties ? "Nrnp" : string.Empty);
            Run(filename, singleDbContextSubNamespace, null, enumDefinitions);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [NonParallelizable]
        [TestCase(TemplateType.EfCore8, ".V4TestE8")]
        public void NonPascalCased(TemplateType templateType, string singleDbContextSubNamespace)
        {
            // Arrange
            SetupSqlServer("EfrpgTest", "My_db_context", "Efrpg_db_context", templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UsePascalCase = false;
            Settings.UseMappingTables = true;

            // Act
            const string filename = "NonPascalCased";
            Run(filename, singleDbContextSubNamespace, null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [NonParallelizable]
        [TestCase(TemplateType.EfCore8, ".V9SpacedSp",   false)]
        [TestCase(TemplateType.EfCore8, ".V9SpacedSpDa", true)]
        public void SpacedColumnStoredProcedure(TemplateType templateType, string singleDbContextSubNamespace, bool useDataAnnotations)
        {
            // Arrange - #721 SP that returns columns whose names contain spaces
            SetupSqlServer("EfrpgTest", "MyDbContext", "EfrpgTestDbContext", templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;
            Settings.AddUnitTestingDbContext = false;
            Settings.UseDataAnnotations = useDataAnnotations;

            FilterSettings.SchemaFilters.Add(new RegexIncludeFilter("dbo.*"));
            FilterSettings.TableFilters.Add(new RegexIncludeFilter("^$")); // exclude all tables and views
            FilterSettings.StoredProcedureFilters.Add(new RegexIncludeFilter("stp test space test"));

            // Act
            var filename = "SpacedColumnSp" + (useDataAnnotations ? "Da" : string.Empty);
            Run(filename, singleDbContextSubNamespace, null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [NonParallelizable]
        [TestCase(TemplateType.EfCore8, ".V9IdenticalMerged",    true)]
        [TestCase(TemplateType.EfCore8, ".V9IdenticalNotMerged", false)]
        public void IdenticalResultSets(TemplateType templateType, string singleDbContextSubNamespace, bool merge)
        {
            // Arrange - #892 SP returning two result sets of the same shape. Turning the merge off must keep both.
            SetupSqlServer("EfrpgTest", "MyDbContext", "EfrpgTestDbContext", templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;
            Settings.AddUnitTestingDbContext = false;
            Settings.MergeMultipleStoredProcModelsIfAllSame = merge;

            FilterSettings.SchemaFilters.Add(new RegexIncludeFilter("dbo.*"));
            FilterSettings.TableFilters.Add(new RegexIncludeFilter("^$")); // exclude all tables and views
            FilterSettings.StoredProcedureFilters.Add(new RegexIncludeFilter("^stp_multiple_identical_results$"));

            // Act
            var filename = "IdenticalResultSets" + (merge ? "Merged" : "NotMerged");
            Run(filename, singleDbContextSubNamespace, null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [NonParallelizable]
        [TestCase(TemplateType.EfCore8, ".V9SpacedTvf",   false)]
        [TestCase(TemplateType.EfCore8, ".V9SpacedTvfDa", true)]
        public void SpacedColumnTableValuedFunction(TemplateType templateType, string singleDbContextSubNamespace, bool useDataAnnotations)
        {
            // Arrange - #721 TVF that returns columns whose names contain spaces
            SetupSqlServer("EfrpgTest", "MyDbContext", "EfrpgTestDbContext", templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;
            Settings.AddUnitTestingDbContext = false;
            Settings.UseDataAnnotations = useDataAnnotations;
            // The TestComparison golden for this test was generated when AllowNullStrings leaked in as true from the
            // preceding ReverseEngineerSqlServer case. Now that SetupDatabase resets it, state the value explicitly
            // so the golden still describes this test's output.
            Settings.AllowNullStrings = true;

            FilterSettings.SchemaFilters.Add(new RegexIncludeFilter("dbo.*"));
            FilterSettings.TableFilters.Add(new RegexIncludeFilter("^$")); // exclude all tables and views
            FilterSettings.StoredProcedureFilters.Add(new RegexIncludeFilter("SpacedColumnTvf"));

            // Act
            var filename = "SpacedColumnTvf" + (useDataAnnotations ? "Da" : string.Empty);
            Run(filename, singleDbContextSubNamespace, null);

            // Assert
            CompareAgainstTestComparison(filename);
        }

        [Test]
        [TestCase("EfrpgTest", ".V8FilterTest", "EfrpgTest", "EfrpgDbContext", false, TemplateType.EfCore8)]
        public void MultipleIncludeFilters(string database, string singleDbContextSubNamespace, string connectionStringName, string dbContextName,
            bool publicTestComparison, TemplateType templateType)
        {
            // Arrange
            SetupSqlServer(database, connectionStringName, dbContextName, templateType);
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = true;
            Settings.AddUnitTestingDbContext = false;

            FilterSettings.SchemaFilters.Add(new RegexIncludeFilter("dbo.*"));
            FilterSettings.SchemaFilters.Add(new RegexIncludeFilter("Beta.*"));

            FilterSettings.TableFilters.Add(new RegexIncludeFilter("^[Cc]ar.*"));
            FilterSettings.TableFilters.Add(new RegexIncludeFilter("Rebel.*"));
            FilterSettings.TableFilters.Add(new RegexIncludeFilter("Harish.*"));

            // Act
            var filename = database + "IncludeFilter";
            Run(filename, singleDbContextSubNamespace, null);

            // Assert
            CompareAgainstTestComparison(filename);
        }
    }
}