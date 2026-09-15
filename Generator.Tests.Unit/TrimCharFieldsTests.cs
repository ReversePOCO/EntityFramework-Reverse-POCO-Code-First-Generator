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
    ///     TrimCharFields adds a TrimEnd value converter to fixed-width char columns. Under AllowNullStrings a nullable
    ///     one is a string? property, and HasConversion on a PropertyBuilder&lt;string?&gt; takes a
    ///     ValueConverter&lt;string?, TProvider&gt;, so a ValueConverter&lt;string, string&gt; raised CS8620.
    /// </summary>
    [TestFixture, NonParallelizable]
    [Category(Constants.CI)]
    public class TrimCharFieldsTests
    {
        [SetUp]
        public void SetUp()
        {
            FilterSettings.Reset();
            FilterSettings.AddDefaults();
            FilterSettings.CheckSettings();
            Settings.TemplateType       = TemplateType.EfCore8;
            Settings.UseDataAnnotations = false;
            Settings.TrimCharFields     = true;
        }

        [TearDown]
        public void TearDown()
        {
            // Settings is static, so anything set here leaks into whichever test runs next unless it is put back.
            Settings.TrimCharFields   = false;
            Settings.AllowNullStrings = false;
        }

        [TestCase(true,  true,  ".HasConversion(new ValueConverter<string?, string?>(v => v == null ? null : v.TrimEnd(), v => v == null ? null : v.TrimEnd()))")]
        [TestCase(true,  false, ".HasConversion(new ValueConverter<string, string>(v => v.TrimEnd(), v => v.TrimEnd()))")]
        [TestCase(false, true,  ".HasConversion(new ValueConverter<string, string>(v => v.TrimEnd(), v => v.TrimEnd()))")]
        public void SetupEntityAndConfig_TrimmedCharColumn_ConverterMatchesPropertyNullability(bool allowNullStrings, bool isNullable, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;

            var fileManagement = new FileManagementService(new GeneratedTextTransformation());
            var generator      = new GeneratorEfCore(fileManagement);
            generator.Init(FakeDatabaseReader.CreateResult(), string.Empty);

            var table  = new Table(null, new Schema("dbo"), "Code", false) { NameHumanCase = "Code" };
            var column = new Column
            {
                DbName          = "Type",
                NameHumanCase   = "Type",
                SqlPropertyType = "char",
                PropertyType    = "string",
                MaxLength       = 2,
                IsNullable      = isNullable,
                IsFixedLength   = true,
                ParentTable     = table,
            };
            table.Columns.Add(column);

            // Act
            generator.SetupEntityAndConfig(column);

            // Assert
            StringAssert.Contains(expected, column.Config);
        }
    }
}
