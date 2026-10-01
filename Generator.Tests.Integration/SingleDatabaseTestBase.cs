using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.Generators;
using Efrpg.Pluralization;
using Efrpg.Readers;
using Efrpg.Templates;
using NUnit.Framework;

namespace Generator.Tests.Integration
{
    public abstract class SingleDatabaseTestBase
    {
        protected static void SetupDatabase(
            string connectionStringName,
            string dbContextName,
            TemplateType templateType)
        {
            Settings.TemplateType = templateType;
            Settings.ConnectionStringName = connectionStringName;
            Settings.DbContextName = dbContextName;
            Settings.Enumerations = null;
            Settings.PrependSchemaName = true;
            Settings.DisableGeographyTypes = false;
            Settings.AddUnitTestingDbContext = true;
            Settings.UsePascalCase = true;
            Settings.UseMappingTables = false;
            Settings.AdditionalNamespaces.Clear();
            Settings.NullableReverseNavigationProperties = true;

            // Settings is static and shared across the whole test run, so every setting a test can change must be
            // reset here or its value leaks into whichever test happens to run next. AllowNullStrings leaking true
            // turns every DB-nullable string into string? in unrelated fixtures (and suppresses the IsRequired(false)
            // mappings), making the comparison tests order-dependent.
            Settings.AllowNullStrings = false;
            Settings.UseDataAnnotations = false;
            Settings.TrimCharFields = false;
            Settings.GenerateSeparateFiles = false;
            Settings.MergeMultipleStoredProcModelsIfAllSame = true;

            Settings.AddOwnedEntityMappings = delegate (List<OwnedEntityMapping> mappings)
            {
                // Customer table: two Address-typed owned entities (billing + shipping).
                // ShippingAddress_Postcode is NULL vs BillingAddress_Postcode NOT NULL — billing wins on deduplication.
                mappings.Add(new OwnedEntityMapping
                {
                    Schema = Settings.DefaultSchema,
                    Table = "Customer",
                    ColumnPrefix = "BillingAddress_",
                    PropertyName = "BillingAddress",
                    PropertyType = "Address"
                });
                mappings.Add(new OwnedEntityMapping
                {
                    Schema = Settings.DefaultSchema,
                    Table = "Customer",
                    ColumnPrefix = "ShippingAddress_",
                    PropertyName = "ShippingAddress",
                    PropertyType = "Address"
                });

                // Invoice table: Money value object (decimal + fixed-length char).
                mappings.Add(new OwnedEntityMapping
                {
                    Schema = Settings.DefaultSchema,
                    Table = "Invoice",
                    ColumnPrefix = "TotalAmount_",
                    PropertyName = "TotalAmount",
                    PropertyType = "Money"
                });
            };

            ResetFilters();
        }

        protected static void Run(string filename, string singleDbContextSubNamespace, string subFolder,
            List<EnumDefinition> enumDefinitions = null)
        {
            Inflector.IgnoreWordsThatEndWith = new List<string> { "Status", "To", "Data" };
            Inflector.PluralisationService = new EnglishPluralizationService();

            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(subFolder))
                path = Path.Combine(path, subFolder);

            Settings.Root = path;
            var fullPath = Path.Combine(path, $"{filename}_{Settings.DatabaseType}_{Settings.TemplateType}.cs");

            // Delete old generated files
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            if (!string.IsNullOrEmpty(subFolder))
                foreach (var old in Directory.GetFiles(Settings.Root))
                    File.Delete(old);

            var outer = new GeneratedTextTransformation();
            var fileManagement = new FileManagementService(outer);
            var toolResult = GetEfrpgResult();
            var generator = GeneratorFactory.Create(toolResult, fileManagement, singleDbContextSubNamespace);

            // Turn on everything for testing
            Assert.IsNotNull(generator);
            Assert.IsNotNull(generator.FilterList);
            var filters = generator.FilterList.GetFilters();
            Assert.IsNotNull(filters);
            foreach (var filter in filters)
            {
                filter.Value.IncludeViews = true;
                filter.Value.IncludeSynonyms = true;
                filter.Value.IncludeStoredProcedures = true;
                filter.Value.IncludeTableValuedFunctions = true;
                filter.Value.IncludeScalarValuedFunctions = true;

                if (filter.Value is DbContextFilter singleContextFilter)
                    singleContextFilter.EnumDefinitions = enumDefinitions;
            }

            var stopwatch = new Stopwatch();
            var stopwatchGenerator = new Stopwatch();

            stopwatch.Start();
            generator.ReadDatabase();

            stopwatchGenerator.Start();
            generator.GenerateCode();
            stopwatchGenerator.Stop();

            stopwatch.Stop();

            TestContext.Out.WriteLine("Duration: {0:F1} seconds, Generator {1:F1} seconds", stopwatch.ElapsedMilliseconds / 1000.0,
                stopwatchGenerator.ElapsedMilliseconds / 1000.0);
            TestContext.Out.WriteLine($"Writing to {fullPath}");
            TestContext.Out.WriteLine();

            if (outer.FileData.Length > 0)
                using (var sw = new StreamWriter(fullPath))
                {
                    sw.Write(outer.FileData.ToString());
                }

            fileManagement.Process(true);
        }

        private static EfrpgResult GetEfrpgResult(bool includeStoredProcedures = true)
        {
            // includeSynonyms is always true: the tests enable IncludeSynonyms on every filter after the tool has
            // already run, so the synonym rows must be in the result up front.
            return EfrpgToolRunner.ReadDatabase(includeStoredProcedures, includeSynonyms: true);
        }

        protected static void CompareAgainstFolderTestComparison(string subFolder)
        {
            var testRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive\\Documents");
            if (!string.IsNullOrEmpty(subFolder))
                testRootPath = Path.Combine(testRootPath, subFolder);

            TestContext.Out.WriteLine("Reading from: " + testRootPath);
            TestContext.Out.WriteLine();

            var testComparisonFiles = Directory.GetFiles(testRootPath);
            var generatedFiles = Directory.GetFiles(Settings.Root);

            Assert.AreEqual(testComparisonFiles.Length, generatedFiles.Length);

            foreach (var comparisonFile in testComparisonFiles.Where(x => x.ToLower().EndsWith("Audit")))
            {
                var filename = Path.GetFileName(comparisonFile);
                var generatedPath = Path.Combine(Settings.Root, filename);
                var testComparison = File.ReadAllText(comparisonFile);
                var generated = File.ReadAllText(generatedPath);

                TestContext.Out.WriteLine(comparisonFile);
                TestContext.Out.WriteLine(generatedPath);
                TestContext.Out.WriteLine();

                Assert.AreEqual(testComparison, generated);
            }
        }

        protected static void CompareAgainstTestComparison(string database)
        {
            var comparisonFile = $"{database}_{Settings.DatabaseType}_{Settings.TemplateType}.cs";
            var testRootPath = AppDomain.CurrentDomain.BaseDirectory;
            var testComparisonPath = Path.Combine(testRootPath, $"TestComparison\\{comparisonFile}");
            var testComparison = File.ReadAllText(testComparisonPath);
            var generatedPath = Path.Combine(Settings.Root, comparisonFile);
            var generated = File.ReadAllText(generatedPath);

            TestContext.Out.WriteLine(testComparisonPath);
            TestContext.Out.WriteLine(generatedPath);

            Assert.AreEqual(testComparison, generated);
        }

        private static void ResetFilters()
        {
            FilterSettings.Reset();
            FilterSettings.AddDefaults();
            FilterSettings.CheckSettings();
        }
    }
}