using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.Generators;
using Efrpg.Templates;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     Issue #166: a column defaulting to NEWSEQUENTIALID() was given Guid.NewGuid() in the entity's constructor, so the
    ///     database never generated the sequential GUID. The value is now left to the database, and each template has to
    ///     tell EF so, or EF inserts Guid.Empty. The column here is not the key: a uniqueidentifier key with a store
    ///     generated default is already treated as an identity column, which hid the problem for keys.
    /// </summary>
    [TestFixture, NonParallelizable]
    [Category(Constants.CI)]
    public class NewSequentialIdTests
    {
        private DatabaseType _originalDatabaseType;
        private TemplateType _originalTemplateType;
        private bool _originalUseDataAnnotations;

        [SetUp]
        public void SetUp()
        {
            // Settings is static, so anything set here leaks into whichever test runs next unless it is put back.
            _originalDatabaseType       = Settings.DatabaseType;
            _originalTemplateType       = Settings.TemplateType;
            _originalUseDataAnnotations = Settings.UseDataAnnotations;

            FilterSettings.Reset();
            FilterSettings.AddDefaults();
            FilterSettings.CheckSettings();
            Settings.DatabaseType       = DatabaseType.SqlServer;
            Settings.UseDataAnnotations = false;
        }

        [TearDown]
        public void TearDown()
        {
            Settings.DatabaseType       = _originalDatabaseType;
            Settings.TemplateType       = _originalTemplateType;
            Settings.UseDataAnnotations = _originalUseDataAnnotations;
        }

        private static Column CreateNewSequentialIdColumn()
        {
            var table = new Table(null, new Schema("dbo"), "Token", false) { NameHumanCase = "Token" };
            var column = new Column
            {
                DbName           = "RowId",
                NameHumanCase    = "RowId",
                SqlPropertyType  = "uniqueidentifier",
                PropertyType     = "Guid",
                Default          = "(newsequentialid())",
                IsStoreGenerated = true,
                IsUnicode        = true,
                ParentTable      = table,
            };
            table.Columns.Add(column);

            column.CleanUpDefault();
            return column;
        }

        [Test]
        public void SetupConfig_EfCoreNewSequentialIdColumn_HasDefaultValueSql()
        {
            Settings.TemplateType = TemplateType.EfCore10;
            var generator = new GeneratorEfCore(new FileManagementService(new GeneratedTextTransformation()));
            generator.Init(FakeDatabaseReader.CreateResult(), string.Empty);
            var column = CreateNewSequentialIdColumn();

            generator.SetupEntityAndConfig(column);

            Assert.That(column.Config, Does.Contain(".HasDefaultValueSql(@\"newsequentialid()\")"));
        }

        [Test]
        public void SetupConfig_Ef6NewSequentialIdColumn_IsDatabaseGeneratedIdentity()
        {
            Settings.TemplateType = TemplateType.Ef6;
            var generator = new GeneratorEf6(new FileManagementService(new GeneratedTextTransformation()));
            generator.Init(FakeDatabaseReader.CreateResult(), string.Empty);
            var column = CreateNewSequentialIdColumn();

            generator.SetupEntityAndConfig(column);

            Assert.That(column.Config, Does.Contain(".HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity)"));
        }
    }
}
