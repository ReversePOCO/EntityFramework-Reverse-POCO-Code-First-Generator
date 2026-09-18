using System;
using System.Linq;
using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     Run against a real v3.14.1 Database.tt recovered from the commit before database reading moved into the
    ///     efrpg tool, rather than a reconstruction of one.
    /// </summary>
    [TestFixture]
    public class TemplateUpgradeTests
    {
        private static TemplateUpgradeResult Upgraded()
        {
            return TemplateUpgrade.Upgrade(RepositoryFiles.V3Template());
        }

        [Test]
        public void IsV3_RecognisesTheRealV3TemplateAndNotTheV4One()
        {
            Assert.That(TemplateUpgrade.IsV3(RepositoryFiles.V3Template()), Is.True);
            Assert.That(TemplateUpgrade.IsV3(RepositoryFiles.DatabaseTemplate()), Is.False);
        }

        [Test]
        public void TheRealV3TemplateUpgrades()
        {
            var result = Upgraded();

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Succeeded, Is.True);
        }

        /// <summary>
        ///     v4 removed multi-context generation, so the eight settings behind it are compile errors in a v4
        ///     template. A stock v3 file assigns all eight, four of them as multi-line delegates; every one goes,
        ///     whole, and the prose above them stays.
        /// </summary>
        [Test]
        public void TheMultiContextSettingsAreDeletedWholeIncludingTheDelegates()
        {
            var result = Upgraded();
            var code   = result.Text.Replace("\r\n", "\n").Split('\n')
                .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .ToList();

            Assert.That(code, Has.None.Contains("Settings.GenerateSingleDbContext"));
            Assert.That(code, Has.None.Contains("Settings.MultiContext"));
            Assert.That(code, Has.None.Contains("allFields"), "a delegate body was left behind");
            Assert.That(result.Changes.Count(c => c.Description.Contains("multi-context generation was removed")), Is.EqualTo(8));
            Assert.That(result.Text, Does.Contain("// If GenerateSingleDbContext = true"), "prose is not rewritten");
        }

        [Test]
        public void ATemplateThatGeneratesMultipleContextsIsRefusedAndPointedAtV3()
        {
            var template = RepositoryFiles.V3Template().Replace(
                "Settings.GenerateSingleDbContext              = true;",
                "Settings.GenerateSingleDbContext              = false;");

            var result = TemplateUpgrade.Upgrade(template);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain("multiple DbContexts").And.Contain("v3"));
        }

        [Test]
        public void TheTemplateFolderSettingIsDeleted()
        {
            // The stock v3 prose still mentions it, in a comment line and in the trailing comment on the
            // TemplateType line; only code counts.
            var code = Upgraded().Text.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.IndexOf("//", StringComparison.Ordinal) is var i && i >= 0 ? l.Substring(0, i) : l)
                .ToList();

            Assert.That(code, Has.None.Contains("Settings.TemplateFolder"));
        }

        [TestCase("Settings.GeneratorType")]
        [TestCase("Settings.GenerationLanguage")]
        [TestCase("Settings.FileExtension")]
        [TestCase("Settings.IncludeQueryTraceOn9481Flag")]
        public void ASettingV4DroppedIsDeleted(string setting)
        {
            var code = Upgraded().Text.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.IndexOf("//", StringComparison.Ordinal) is var i && i >= 0 ? l.Substring(0, i) : l)
                .ToList();

            Assert.That(code, Has.None.Contains(setting));
        }

        [TestCase("Settings.TemplateType                 = TemplateType.EfCore10;", "Settings.TemplateType                 = TemplateType.FileBasedCore10;", "file-based")]
        [TestCase("Settings.GeneratorType                = GeneratorType.EfCore;", "Settings.GeneratorType                = GeneratorType.Custom;", "GeneratorType.Custom")]
        public void ATemplateUsingARemovedTemplateOrGeneratorTypeIsRefused(string stock, string replaced, string reason)
        {
            var template = RepositoryFiles.V3Template().Replace(stock, replaced);
            Assert.That(template, Is.Not.EqualTo(RepositoryFiles.V3Template()), "the fixture line must exist");

            var result = TemplateUpgrade.Upgrade(template);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain(reason).And.Contain("v3"));
        }

        /// <summary>A removed setting used somewhere the upgrade does not rewrite is a refusal, not a compile error later.</summary>
        [Test]
        public void AMultiContextSettingUsedInCodeElsewhereIsRefused()
        {
            var template = RepositoryFiles.V3Template().Replace(
                "    Settings.GenerateSingleDbContext              = true;",
                "    Settings.GenerateSingleDbContext              = true;\r\n    if (Settings.MultiContextAttributeDelimiter == '~') { }");

            var result = TemplateUpgrade.Upgrade(template);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain("Settings.MultiContext"));
        }

        /// <summary>The six edits, each of which the template does not compile or does not run without.</summary>
        [Test]
        public void AllSixRequiredEditsAreMade()
        {
            var text = Upgraded().Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.StartWith("<#@ include file=\"" + TemplateUpgrade.V4Include + "\" #>"),
                    "the include directive");
                Assert.That(text, Does.Not.Contain("Settings.FileManagerType"), "FileManagerType is gone");
                Assert.That(text, Does.Not.Contain("Settings.DatabaseReaderPlugin"), "DatabaseReaderPlugin is gone");
                Assert.That(text, Does.Contain("if (Settings.GenerateSeparateFiles)"), "the sub-folder condition");
                Assert.That(text, Does.Contain("NamingHelper.CleanUp(fkName)"), "CleanUp moved to NamingHelper");
                Assert.That(text, Does.Contain("EfrpgToolRunner.ReadDatabase("), "the entry point");
            });
        }

        [Test]
        public void EveryEditIsReportedSoTheUserCanSeeItBeforeItIsWritten()
        {
            var changes = Upgraded().Changes;

            // The six v3-to-v4 edits, the eight multi-context settings v4 removed, and TemplateFolder,
            // GeneratorType, GenerationLanguage, FileExtension and IncludeQueryTraceOn9481Flag.
            Assert.That(changes.Count, Is.EqualTo(19));
            Assert.That(changes.Select(c => c.Description), Has.All.Not.Empty);
        }

        /// <summary>
        ///     The twelve cosmetic differences between a stock v3 and a stock v4 file are deliberately left alone.
        ///     A customer will have edited some of them, and rewriting their comments is the over-reach the
        ///     refusal rule exists to prevent.
        /// </summary>
        [Test]
        public void TheCosmeticDifferencesAreLeftAlone()
        {
            var text = Upgraded().Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("// v3.14.1"), "the version header is the user's, not ours");
                Assert.That(text, Does.Contain("SqlCe"), "trailing comments are left as written");
                Assert.That(text.Split(new[] { TemplateUpgrade.V3Include }, StringSplitOptions.None).Length - 1,
                    Is.EqualTo(2), "the two prose mentions of the v3 include stay");
            });
        }

        /// <summary>
        ///     Only complete lines are removed, so the settings above and below keep their alignment and the file
        ///     shrinks by exactly the deleted statements: the two one-line settings, and the eight multi-context
        ///     statements however many lines each spans in the fixture.
        /// </summary>
        [Test]
        public void OnlyTheDeletedSettingsChangeTheLineCountBeforeTheEntryPoint()
        {
            var before = RepositoryFiles.V3Template();
            var after  = Upgraded().Text;

            var beforeHead = before.Substring(0, before.IndexOf("var outer =", StringComparison.Ordinal));
            var afterHead  = after.Substring(0, after.IndexOf("var outer =", StringComparison.Ordinal));

            var multiContextLines = TemplateSettingsDocument.Parse(before).Assignments
                .Where(a => !a.IsCommentedOut && (a.Name == "GenerateSingleDbContext" || a.Name.StartsWith("MultiContext", StringComparison.Ordinal)))
                .Sum(a => a.EndLineNumber - a.LineNumber + 1);

            Assert.That(multiContextLines, Is.GreaterThan(8), "the four delegates span several lines each");
            // FileManagerType, DatabaseReaderPlugin, TemplateFolder, GeneratorType, GenerationLanguage,
            // FileExtension and IncludeQueryTraceOn9481Flag are one line each
            Assert.That(Lines(afterHead), Is.EqualTo(Lines(beforeHead) - 7 - multiContextLines));
        }

        [Test]
        public void TheLineEndingsAreNotChanged()
        {
            var text = Upgraded().Text;

            Assert.That(text.Replace("\r\n", string.Empty), Does.Not.Contain("\n"),
                "a CRLF template must stay CRLF, or the next commit shows every line as changed.");
        }

        [Test]
        public void AnLfTemplateStaysLf()
        {
            var text = TemplateUpgrade.Upgrade(RepositoryFiles.V3Template().Replace("\r\n", "\n")).Text;

            Assert.That(text, Does.Not.Contain("\r"));
        }

        /// <summary>
        ///     The block this replaces wholesale is the one most likely to vary between customer files, and it must
        ///     be exactly what the current Database.tt ships or the upgrade emits last year's code.
        /// </summary>
        [Test]
        public void TheReplacementEntryPointIsWhatTheShippedTemplateActuallyCarries()
        {
            var shipped = RepositoryFiles.DatabaseTemplate();
            var tail    = shipped.Substring(shipped.IndexOf("    var outer = (GeneratedTextTransformation)", StringComparison.Ordinal));

            Assert.That(tail.TrimEnd('\r', '\n'), Is.EqualTo(TemplateUpgrade.V4EntryPoint),
                "BuildTT's footer has changed. Update TemplateUpgrade.V4EntryPoint to match.");
        }

        /// <summary>
        ///     Migrating 24 in-repo templates by script took two passes because some carried an extra commented-out
        ///     line inside this block. Those must still upgrade.
        /// </summary>
        [Test]
        public void AnExtraCommentInsideTheEntryPointDoesNotStopTheUpgrade()
        {
            var text = RepositoryFiles.V3Template()
                .Replace("    var fileManagement = new FileManagementService(outer);",
                         "    // a note the user left themselves\r\n    var fileManagement = new FileManagementService(outer);");

            Assert.That(TemplateUpgrade.Upgrade(text).Succeeded, Is.True);
        }

        private static string WithEntryPoint(string tail)
        {
            var before = RepositoryFiles.V3Template();

            return before.Substring(0, before.IndexOf("    var outer =", StringComparison.Ordinal)) + tail;
        }

        /// <summary>
        ///     The exact tail shipped in v3.2.0 through v3.10.0: the machine.config comment sits inside the if block
        ///     and is followed by a blank line.
        /// </summary>
        [Test]
        public void TheV39EntryPointWithTheCommentInsideTheIfBlockUpgrades()
        {
            var text = WithEntryPoint(
                "    var outer = (GeneratedTextTransformation) this;\r\n" +
                "    var fileManagement = new FileManagementService(outer);\r\n" +
                "    var generator = GeneratorFactory.Create(fileManagement, FileManagerFactory.GetFileManagerType());\r\n" +
                "    if (generator != null && generator.InitialisationOk)\r\n" +
                "    {\r\n" +
                "        // Show where the machine.config file is\r\n" +
                "        // fileManagement.WriteLine(\"// \" + System.Runtime.InteropServices.RuntimeEnvironment.SystemConfigurationFile);\r\n" +
                "\r\n" +
                "        generator.ReadDatabase();\r\n" +
                "        generator.GenerateCode();\r\n" +
                "    }\r\n" +
                "    fileManagement.Process(true);#>");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.EndWith(TemplateUpgrade.V4EntryPoint));
        }

        /// <summary>
        ///     A real hand-edited file with its identifiers swapped for Northwind ones: v3.9.0 header, no null check
        ///     in the entry point, custom filters and callbacks throughout. Everything the user wrote must survive
        ///     and every removed setting must go.
        /// </summary>
        [Test]
        public void ARealV390CustomerTemplateUpgrades()
        {
            var result = TemplateUpgrade.Upgrade(RepositoryFiles.V390CustomerTemplate());

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Text, Does.StartWith("<#@ include file=\"" + TemplateUpgrade.V4Include + "\" #>"));
            Assert.That(result.Text, Does.EndWith(TemplateUpgrade.V4EntryPoint));

            // Assignments only: a stock file's trailing comments still mention some removed settings in prose.
            var assigned = result.Text.Replace("\r\n", "\n").Split('\n')
                .Select(l => System.Text.RegularExpressions.Regex.Match(l, @"^\s*Settings\.(\w+)\s*="))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.That(assigned, Has.None.EqualTo("FileManagerType"));
            Assert.That(assigned, Has.None.EqualTo("GeneratorType"));
            Assert.That(assigned, Has.None.EqualTo("TemplateFolder"));
            Assert.That(assigned, Has.None.EqualTo("DatabaseReaderPlugin"));
            Assert.That(assigned, Has.None.EqualTo("GenerationLanguage"));
            Assert.That(assigned, Has.None.EqualTo("FileExtension"));
            Assert.That(assigned, Has.None.EqualTo("IncludeQueryTraceOn9481Flag"));
            Assert.That(assigned, Has.None.EqualTo("GenerateSingleDbContext"));
            Assert.That(assigned, Has.None.StartsWith("MultiContext"));
            Assert.That(result.Text, Does.Not.Contain("FileManagerFactory"));
            Assert.That(result.Text, Does.Not.Contain("allFields"), "a multi-context delegate body was left behind");
            Assert.That(result.Text, Does.Contain("if (Settings.GenerateSeparateFiles)\r\n"));

            Assert.That(result.Text, Does.Contain("new RegexIncludeFilter(\"^Categories$|Products|Suppliers"), "custom filter kept");
            Assert.That(result.Text, Does.Contain("\"employees_to_territories\""), "custom foreign key kept");
            Assert.That(result.Text, Does.Contain("column.ParentTable.NameHumanCase == \"OrderDetail\""), "custom column hiding kept");
            Assert.That(result.Text, Does.Contain("new CustomPluralizationEntry(\"OrderDetail\""), "custom pluralisation kept");
            Assert.That(result.Text, Does.Contain("Settings.AdditionalNamespaces               = new List<string>{\"Northwind.Common.Enums\"};"), "custom namespace kept");
        }

        /// <summary>
        ///     Between v3.5.0 and v3.6.0 the sub-folder block had no <c>if</c> around it, only a prose comment
        ///     naming Settings.FileManagerType. Prose is not a compile error and must not block the upgrade.
        /// </summary>
        [Test]
        public void AProseCommentNamingFileManagerTypeDoesNotBlockTheUpgrade()
        {
            var before = RepositoryFiles.V3Template();
            var start  = before.IndexOf("    if (Settings.GenerateSeparateFiles && Settings.FileManagerType", StringComparison.Ordinal);
            var end    = before.IndexOf("    }\r\n", start, StringComparison.Ordinal) + "    }\r\n".Length;
            var text   = before.Substring(0, start) +
                         "    // Only activated if Settings.GenerateSeparateFiles = true && Settings.FileManagerType = FileManagerType.EfCore\r\n" +
                         "    Settings.ContextFolder           = @\"\";\r\n" +
                         "    Settings.PocoFolder              = @\"Entities\";\r\n" +
                         before.Substring(end);

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Contain("// Only activated if Settings.GenerateSeparateFiles = true && Settings.FileManagerType = FileManagerType.EfCore"),
                "prose is not rewritten");
        }

        /// <summary>
        ///     The one delegate whose signature changed: v4 added the JSON column mappings parameter. A three-parameter
        ///     UpdateColumn is CS1593 against the v4 include, which a real customer file hit.
        /// </summary>
        [Test]
        public void UpdateColumnGainsTheJsonColumnMappingsParameter()
        {
            var result = TemplateUpgrade.Upgrade(RepositoryFiles.V390CustomerTemplate());

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Contain(
                "Settings.UpdateColumn = delegate(Column column, Table table, List<EnumDefinition> enumDefinitions, List<JsonColumnMapping> jsonColumnMappings)"));
            Assert.That(result.Text, Does.Not.Contain("Table table, List<EnumDefinition> enumDefinitions)"));
            Assert.That(result.Changes.Select(c => c.Description), Has.One.Contains("UpdateColumn takes a fourth parameter"));
        }

        /// <summary>
        ///     The stock v3 data-annotations block compares Settings.DatabaseType with DatabaseType.SqlCe, which v4
        ///     removed. The comparison becomes false and the rest of the user's block is untouched.
        /// </summary>
        [Test]
        public void TheSqlCeComparisonInTheDataAnnotationsBlockBecomesFalse()
        {
            var result = TemplateUpgrade.Upgrade(RepositoryFiles.V390CustomerTemplate());

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Contain("var doNotSpecifySize = (false && column.MaxLength > 4000);"));
            Assert.That(result.Text, Does.Not.Contain("DatabaseType.SqlCe"));
        }

        [Test]
        public void ATemplateReadingASqlCeDatabaseIsRefusedAndPointedAtV3()
        {
            var text = RepositoryFiles.V3Template().Replace(
                "Settings.DatabaseType                 = DatabaseType.SqlServer;",
                "Settings.DatabaseType                 = DatabaseType.SqlCe;");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain("SqlCe").And.Contain("v3"));
        }

        /// <summary>
        ///     v4 has no EfCore2 to EfCore7. The stock v3 template shipped with EfCore3, EfCore6 and EfCore7 at
        ///     different times, so a lot of real files carry one of them.
        /// </summary>
        [TestCase("EfCore3")]
        [TestCase("EfCore6")]
        [TestCase("EfCore7")]
        public void ATemplateTypeBelowEfCore8IsRaisedToEfCore8WithAWarning(string old)
        {
            var text = RepositoryFiles.V390CustomerTemplate().Replace(
                "Settings.TemplateType            = TemplateType.EfCore8;",
                "Settings.TemplateType            = TemplateType." + old + ";");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Contain("Settings.TemplateType            = TemplateType.EfCore8;"));
            Assert.That(result.Changes.Select(c => c.Description), Has.One.Contains("EF Core 8 or later"));
        }

        [TestCase("EfCore9")]
        [TestCase("Ef6")]
        public void ATemplateTypeV4SupportsIsLeftAlone(string current)
        {
            var text = RepositoryFiles.V390CustomerTemplate().Replace(
                "Settings.TemplateType            = TemplateType.EfCore8;",
                "Settings.TemplateType            = TemplateType." + current + ";");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Text, Does.Contain("TemplateType." + current + ";"));
            Assert.That(result.Changes.Select(c => c.Description), Has.None.Contains("EF Core 8 or later"));
        }

        /// <summary>
        ///     The first v3.0.8 templates, November 2019, assigned string arrays to two settings that are lists.
        /// </summary>
        [Test]
        public void StringArraysAssignedToListSettingsBecomeLists()
        {
            var text = RepositoryFiles.V3Template()
                .Replace("Settings.AdditionalNamespaces                   = new List<string>();",
                         "Settings.AdditionalNamespaces                   = new []{ \"\" };")
                .Replace("Settings.AdditionalContextInterfaceItems        = new List<string>();",
                         "Settings.AdditionalContextInterfaceItems        = new string[] { \"void Save();\" };");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Contain("Settings.AdditionalNamespaces                   = new List<string>{ \"\" };"));
            Assert.That(result.Text, Does.Contain("Settings.AdditionalContextInterfaceItems        = new List<string>{ \"void Save();\" };"));
            Assert.That(result.Changes.Count(c => c.Description.Contains("List<string> in v4")), Is.EqualTo(2));
        }

        /// <summary>
        ///     Master-only for two weeks in early 2026: one ApplyColumnCustomizations call that v3.12.0 split into four.
        /// </summary>
        [Test]
        public void ApplyColumnCustomizationsIsSplitIntoTheFourCallsThatReplacedIt()
        {
            var before = RepositoryFiles.V3Template();
            var start  = before.IndexOf("        Settings.ApplyJsonPropertyNameAttribute(column);", StringComparison.Ordinal);
            var end    = before.IndexOf("Settings.ApplyEnumTypeReplacement(column, table, enumDefinitions);", start, StringComparison.Ordinal)
                         + "Settings.ApplyEnumTypeReplacement(column, table, enumDefinitions);".Length;
            var text   = before.Substring(0, start) +
                         "        Settings.ApplyColumnCustomizations(column, table, enumDefinitions, jsonColumnMappings);" +
                         before.Substring(end);

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Not.Contain("ApplyColumnCustomizations"));
            Assert.That(result.Text, Does.Contain(
                "        Settings.ApplyJsonPropertyNameAttribute(column);\r\n" +
                "        Settings.ApplyJsonColumnMappings(column, table, jsonColumnMappings);\r\n" +
                "        Settings.ApplyDataAnnotations(column);\r\n" +
                "        Settings.ApplyEnumTypeReplacement(column, table, enumDefinitions);"));
        }

        [Test]
        public void AnUpdateColumnAlreadyTakingFourParametersIsLeftAlone()
        {
            var text = RepositoryFiles.V3Template().Replace(
                "List<EnumDefinition> enumDefinitions)",
                "List<EnumDefinition> enumDefinitions, List<JsonColumnMapping> jsonColumnMappings)");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Changes.Select(c => c.Description), Has.None.Contains("UpdateColumn takes a fourth parameter"));
        }

        /// <summary>
        ///     Settings.ForeignKeyNamingStrategy was in the stock template for one master commit in 2020 and in the
        ///     v3 include until the end, so a user may have set it by hand. v4 keeps only the Current behaviour.
        /// </summary>
        [Test]
        public void ForeignKeyNamingStrategyIsDeletedWithAReason()
        {
            var text = RepositoryFiles.V3Template().Replace(
                "    Settings.CommandTimeout ",
                "    Settings.ForeignKeyNamingStrategy = ForeignKeyNamingStrategy.Current; // Legacy (same as versions <= v3.1.3), Latest\r\n" +
                "    Settings.CommandTimeout ");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.Not.Contain("ForeignKeyNamingStrategy"));
            Assert.That(result.Changes.Select(c => c.Description), Has.One.Contains("Settings.ForeignKeyNamingStrategy no longer exists"));
        }

        /// <summary>
        ///     v3.0.8 to v3.5.0 shipped without the null check on <c>generator</c>. Files that old are still in use.
        /// </summary>
        [Test]
        public void ThePreV36EntryPointWithoutTheNullCheckUpgrades()
        {
            var text = WithEntryPoint(
                "    var outer = (GeneratedTextTransformation) this;\r\n" +
                "    var fileManagement = new FileManagementService(outer);\r\n" +
                "    var generator = GeneratorFactory.Create(fileManagement, FileManagerFactory.GetFileManagerType());\r\n" +
                "    if (generator.InitialisationOk)\r\n" +
                "    {\r\n" +
                "        generator.ReadDatabase();\r\n" +
                "        generator.GenerateCode();\r\n" +
                "    }\r\n" +
                "    fileManagement.Process(true);#>");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
            Assert.That(result.Text, Does.EndWith(TemplateUpgrade.V4EntryPoint));
        }

        /// <summary>
        ///     Tabs, braces on the same line, doubled spaces and a bare LF: none of it changes the code, so none of
        ///     it may block the upgrade.
        /// </summary>
        [Test]
        public void WhitespaceAndBracePlacementInsideTheEntryPointDoNotStopTheUpgrade()
        {
            var text = WithEntryPoint(
                "\tvar outer  =  (GeneratedTextTransformation)  this;\n" +
                "\tvar fileManagement = new FileManagementService( outer );\n" +
                "\tvar generator = GeneratorFactory.Create(fileManagement,\n" +
                "\t\tFileManagerFactory.GetFileManagerType());\n" +
                "\tif (generator != null && generator.InitialisationOk) {\n" +
                "\t\tgenerator.ReadDatabase(); generator.GenerateCode();\n" +
                "\t}\n" +
                "\tfileManagement.Process(true);\n" +
                "#>");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Blockers, Is.Empty);
        }

        /// <summary>
        ///     A half-applied migration leaves a template that neither compiles nor matches the guide, which is
        ///     worse than not offering the button.
        /// </summary>
        [Test]
        public void ARestructuredEntryPointIsRefused()
        {
            var text = RepositoryFiles.V3Template()
                .Replace("        generator.GenerateCode();", "        generator.GenerateCode();\r\n        MyOwnPostProcessing();");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Text, Is.Null);
            Assert.That(result.Blockers.Single(), Does.Contain("entry point"));
        }

        [Test]
        public void AMissingEntryPointIsRefused()
        {
            var before = RepositoryFiles.V3Template();
            var text   = before.Substring(0, before.IndexOf("    var outer =", StringComparison.Ordinal));

            Assert.That(TemplateUpgrade.Upgrade(text).Succeeded, Is.False);
        }

        /// <summary>
        ///     Anything still naming a v4-removed type will not compile, so leaving it for the user to find at
        ///     generation time is not an option.
        /// </summary>
        [Test]
        public void ALeftoverReferenceToARemovedTypeIsRefused()
        {
            var text = RepositoryFiles.V3Template()
                .Replace("    FilterSettings.Reset();",
                         "    if (Settings.FileManagerType == FileManagerType.Null) { }\r\n    FilterSettings.Reset();");

            var result = TemplateUpgrade.Upgrade(text);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain("FileManagerType"));
        }

        [Test]
        public void AV4TemplateIsRefusedRatherThanUpgradedAgain()
        {
            var result = TemplateUpgrade.Upgrade(RepositoryFiles.DatabaseTemplate());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Blockers.Single(), Does.Contain("nothing to upgrade"));
        }

        /// <summary>
        ///     Somebody who has already deleted one of the two settings by hand has done half the upgrade, which is
        ///     not a reason to refuse the other half.
        /// </summary>
        [Test]
        public void ASettingAlreadyRemovedByHandIsNotAProblem()
        {
            var lines = RepositoryFiles.V3Template()
                .Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Where(l => !l.TrimStart().StartsWith("Settings.DatabaseReaderPlugin", StringComparison.Ordinal));

            var result = TemplateUpgrade.Upgrade(string.Join("\r\n", lines));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Changes.Count, Is.EqualTo(18));
        }

        private static int Lines(string text)
        {
            return text.Split('\n').Length;
        }
    }
}
