using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Filtering;
using Efrpg.Generators;
using Efrpg.LanguageMapping;
using Efrpg.Pluralization;
using Efrpg.Readers;
using Efrpg.Templates;

namespace Generator.Tests.Unit.DocSamples
{
    /// <summary>
    ///     Runs the generator over <c>DocSampleSchema.xml</c> so the wiki can show real generated code instead of
    ///     code somebody typed from memory.
    /// </summary>
    /// <remarks>
    ///     Every example on every Settings.* wiki page comes through here. The point is that a snippet on the wiki
    ///     is output the generator actually produced, and that <see cref="WikiSnippetDriftTests"/> can re-run it
    ///     later and notice when it stops being true. Prose written by reading the templates drifts from the
    ///     templates; this cannot.
    ///     Single-file mode only: with GenerateSingleDbContext and no separate files, everything lands in
    ///     GeneratedTextTransformation.FileData and nothing touches the disk. Settings.Root is still pointed at a
    ///     temporary folder because a few code paths read it.
    /// </remarks>
    public static class DocSampleRunner
    {
        /// <summary>
        ///     Samples are joined and compared with '\n' throughout, so a snippet does not change meaning when
        ///     it moves between a Windows checkout and a Linux CI agent.
        /// </summary>
        private const string LineFeed = "\n";

        /// <summary>
        ///     The static state as it was before any sample ran, used to undo one sample's delegates before the
        ///     next one runs. Set by the fixture in [OneTimeSetUp]; see the remarks on
        ///     <see cref="ResetCallbacksToShippedBehaviour"/>.
        /// </summary>
        public static StaticStateSnapshot Pristine;

        /// <summary>
        ///     Generates once with <paramref name="configure"/> applied on top of the shipped Database.tt defaults.
        /// </summary>
        /// <param name="configure">
        ///     Sets the one setting the sample is about. Everything else is left at its shipped default so the
        ///     difference between two runs is attributable to that setting alone.
        /// </param>
        public static string Generate(Action configure)
        {
            return Generate(Schema.Core, configure);
        }

        /// <summary>
        ///     Generates against a named fixture schema.
        /// </summary>
        public static string Generate(Schema schema, Action configure)
        {
            ApplyDatabaseTtDefaults();
            configure?.Invoke();
            FilterSettings.CheckSettings(); // Honour any FilterSettings the sample changed

            var outer = new GeneratedTextTransformation();
            var fileManagement = new FileManagementService(outer);
            var generator = GeneratorFactory.Create(LoadSchema(schema), fileManagement, null);

            if (generator == null || !generator.InitialisationOk)
                throw new InvalidOperationException("The generator would not initialise for a doc sample.");

            EnableEverythingOnTheFilters(generator);

            generator.ReadDatabase();
            generator.GenerateCode();

            // Deliberately no fileManagement.Process(true): that writes an audit file and the generated .cs to
            // Settings.Root. In single-file mode the whole output is already in FileData.
            return Normalise(outer.FileData.ToString());
        }

        /// <summary>
        ///     Generates with <c>GenerateSeparateFiles</c> on and returns the names of the files produced,
        ///     which is the only way to show what the folder settings do.
        /// </summary>
        /// <remarks>
        ///     This is the one path that does write to disk, into a temporary folder that is emptied first. The
        ///     return value is the relative path of each file, sorted, so the sample is a stable file listing
        ///     rather than code.
        /// </remarks>
        public static string GenerateFileListing(Action configure)
        {
            ApplyDatabaseTtDefaults();
            Settings.GenerateSeparateFiles = true;
            configure?.Invoke();
            FilterSettings.CheckSettings();

            var root = Path.Combine(TempRoot(), "files");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            Settings.Root = root;

            var outer = new GeneratedTextTransformation();
            var fileManagement = new FileManagementService(outer);
            var generator = GeneratorFactory.Create(LoadSchema(Schema.Core), fileManagement, null);

            if (generator == null || !generator.InitialisationOk)
                throw new InvalidOperationException("The generator would not initialise for a doc sample.");

            EnableEverythingOnTheFilters(generator);
            generator.ReadDatabase();
            generator.GenerateCode();
            fileManagement.Process(true);

            var files = Directory
                .GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/').Replace(Path.DirectorySeparatorChar, '/'))
                .Where(f => !f.EndsWith("Audit.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            return files.Count == 0 ? "// No files generated." : string.Join(LineFeed, files);
        }

        /// <summary>
        ///     Turns on views, synonyms, stored procedures and functions for the run.
        /// </summary>
        /// <remarks>
        ///     The filters are built when the generator is created, from FilterSettings as it stood at that
        ///     moment, so flipping FilterSettings.IncludeViews inside a sample's configure action would be too
        ///     late. Setting it on the filters afterwards is what the integration tests do too.
        /// </remarks>
        private static void EnableEverythingOnTheFilters(Efrpg.Generators.Generator generator)
        {
            if (generator.FilterList == null)
                return;

            foreach (var filter in generator.FilterList.GetFilters())
            {
                filter.Value.IncludeViews                 = FilterSettings.IncludeViews;
                filter.Value.IncludeSynonyms              = FilterSettings.IncludeSynonyms;
                filter.Value.IncludeStoredProcedures      = FilterSettings.IncludeStoredProcedures;
                filter.Value.IncludeTableValuedFunctions  = FilterSettings.IncludeTableValuedFunctions;
                filter.Value.IncludeScalarValuedFunctions = FilterSettings.IncludeScalarValuedFunctions;
            }
        }

        /// <summary>
        ///     Which fixture schema a sample generates from.
        /// </summary>
        public enum Schema
        {
            /// <summary>Category, Product and sales.Order. Short enough to read once.</summary>
            Core,

            /// <summary>Adds many-to-many, a view, a stored procedure, rowversion and an extended property.</summary>
            Extras
        }

        private static EfrpgResult LoadSchema(Schema schema)
        {
            return EfrpgResultXmlReader.Read(File.ReadAllText(SchemaPath(schema)));
        }

        public static string SchemaPath()
        {
            return SchemaPath(Schema.Core);
        }

        public static string SchemaPath(Schema schema)
        {
            var filename = schema == Schema.Extras ? "DocSampleExtrasSchema.xml" : "DocSampleSchema.xml";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DocSamples", filename);
        }

        /// <summary>
        ///     Resets every setting a sample might touch to the value the shipped Database.tt assigns.
        /// </summary>
        /// <remarks>
        ///     Settings is static and shared across the whole test run, so anything not reset here leaks from one
        ///     sample into the next and the snippets become order-dependent. The values here are the .tt values,
        ///     not the Settings.cs field initialisers - those differ for UseLazyLoading, DisableGeographyTypes,
        ///     UseResharper and the folder settings, and the .tt is what a user actually gets.
        /// </remarks>
        private static void ApplyDatabaseTtDefaults()
        {
            // First, undo whatever the previous sample did - including its delegates, which cannot be written
            // out by hand. Then lay the Database.tt values on top. Order matters: restoring afterwards would
            // wipe the values assigned below.
            ResetCallbacksToShippedBehaviour();

            Settings.DatabaseType                 = DatabaseType.SqlServer;
            Settings.TemplateType                 = TemplateType.EfCore10;
            Settings.ConnectionString             = "Data Source=(local);Initial Catalog=DocSamples;Integrated Security=True";
            Settings.ConnectionStringActions      = "";
            Settings.ConnectionStringName         = "MyDbContext";
            Settings.DbContextName                = "MyDbContext";
            Settings.DbContextInterfaceName       = null;
            Settings.GenerateSeparateFiles        = false;
            Settings.Namespace                    = "MyApp.Data";
            Settings.UseFileScopedNamespaces      = false;
            Settings.AddUnitTestingDbContext      = false; // Off: the fake context doubles the length of every snippet
            Settings.FakeDbContextInDebugOnlyMode = false;

            Settings.ElementsToGenerate = Elements.Poco | Elements.Context | Elements.Interface | Elements.PocoConfiguration | Elements.Enum;

            Settings.ContextFolder             = "";
            Settings.InterfaceFolder           = "";
            Settings.PocoFolder                = "";
            Settings.PocoConfigurationFolder   = "";
            Settings.OwnedEntityFolder         = "";
            Settings.UseFolderNameInNamespace  = false;

            Settings.CommandTimeout                         = 600;
            Settings.DbContextInterfaceBaseClasses          = "IDisposable";
            Settings.DbContextBaseClass                     = "DbContext";
            Settings.OnConfiguration                        = OnConfiguration.ConnectionString;
            Settings.AddParameterlessConstructorToDbContext = true;
            Settings.ConfigurationClassName                 = "Configuration";
            Settings.UseMappingTables                       = false;

            Settings.EntityClassesModifiers        = "public";
            Settings.ConfigurationClassesModifiers = "public";
            Settings.DbContextClassModifiers       = "public";
            Settings.DbContextInterfaceModifiers   = "public";
            Settings.ResultClassModifiers          = "public";

            Settings.UsePascalCase                          = true;
            Settings.UsePascalCaseForEnumMembers            = true;
            Settings.UseDataAnnotations                     = false;
            Settings.UsePropertyInitialisers                = false;
            Settings.UseLazyLoading                         = false;
            Settings.UseInheritedBaseInterfaceFunctions     = false;
            Settings.IncludeComments                        = CommentsStyle.AtEndOfField;
            Settings.IncludeExtendedPropertyComments        = CommentsStyle.InSummaryBlock;
            Settings.DisableGeographyTypes                  = true;
            Settings.CollectionInterfaceType                = "ICollection";
            Settings.CollectionType                         = "List";
            Settings.NullableShortHand                      = true;
            Settings.AddIDbContextFactory                   = true;
            Settings.UsePrivateSetterForComputedColumns     = true;
            Settings.IncludeGeneratorVersionInCode          = false;
            Settings.TrimCharFields                         = false;
            Settings.IncludeFieldNameConstants              = false;
            Settings.UsePropertiesForStoredProcResultSets   = false;
            Settings.MergeMultipleStoredProcModelsIfAllSame = true;
            Settings.AdditionalNamespaces                   = new List<string>();
            Settings.AdditionalContextInterfaceItems        = new List<string>();
            Settings.AdditionalFileHeaderText               = new List<string>();
            Settings.AdditionalFileFooterText               = new List<string>();
            Settings.OrderProperties                        = OrderProperties.Ordinal;


            Settings.UseRegions                          = true;
            Settings.UseNamespace                        = true;
            Settings.UsePragma                           = false;
            Settings.AllowNullStrings                    = false;
            Settings.NullableReverseNavigationProperties = false;
            Settings.UseResharper                        = false; // The .tt ships true; the banner is pure noise in a snippet
            Settings.ShowLicenseInfo                     = false;
            Settings.IncludeConnectionSettingComments    = false;
            Settings.IncludeCodeGeneratedAttribute       = false;
            Settings.IncludeColumnsWithDefaults          = true;
            Settings.GenerateHasDefaultValueSql          = false;

            // Null, not empty: a non-empty list makes the generator re-invoke the efrpg tool to read enum rows,
            // which needs a database.
            Settings.Enumerations = null;
            Settings.HiLoSequences = new List<HiLoSequence>();

            Settings.AdditionalReverseNavigationsDataAnnotations = new string[0];
            Settings.AdditionalForeignKeysDataAnnotations        = new string[0];

            Settings.ContextNamespace           = "";
            Settings.InterfaceNamespace         = "";
            Settings.PocoNamespace              = "";
            Settings.PocoConfigurationNamespace = "";

            Settings.PrependSchemaName = true;
            Settings.TableSuffix       = null;

            Settings.StoredProcedureReturnTypes = new Dictionary<string, string>();

            Settings.Root = TempRoot();

            Inflector.IgnoreWordsThatEndWith = new List<string> { "Status", "To", "Data" };
            Inflector.PluralisationService   = new EnglishPluralizationService();

            FilterSettings.Reset();
            FilterSettings.AddDefaults();
            FilterSettings.CheckSettings();
        }

        /// <summary>
        ///     Puts the delegate settings back to the shipped implementations.
        /// </summary>
        /// <remarks>
        ///     These cannot be reset by writing them out again here. Settings.ForeignKeyName alone is seventy
        ///     lines of clash-resolution logic, and a hand-written stand-in would be a second copy of
        ///     Settings.cs to keep in step - one that would quietly make every sample wrong rather than fail.
        ///     So the fixture hands us a snapshot taken before any sample ran, and we restore from that.
        ///     This is not theoretical: the sample that overrides ForeignKeyName leaked its "Group" navigation
        ///     property name into four unrelated samples before this was added.
        /// </remarks>
        private static void ResetCallbacksToShippedBehaviour()
        {
            if (Pristine == null)
                throw new InvalidOperationException(
                    "DocSampleRunner.Pristine has not been set. A fixture that generates samples must capture " +
                    "StaticStateSnapshot in [OneTimeSetUp] and assign it to DocSampleRunner.Pristine, or one " +
                    "sample's delegates leak into the next.");

            Pristine.Restore();
        }

        private static string TempRoot()
        {
            var path = Path.Combine(Path.GetTempPath(), "efrpg-doc-samples");
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        ///     Removes the parts of the output that are not the generator's answer to the question the sample asks.
        /// </summary>
        /// <remarks>
        ///     The trial-licence banner is the important one. Whether it appears depends on whether the machine
        ///     running the tests has a ReversePOCO.txt, which would otherwise make every snippet differ between
        ///     the author's machine and CI.
        /// </remarks>
        private static string Normalise(string generated)
        {
            var lines = StabiliseGeneratorVersion(generated)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(line => !IsLicenceNoise(line))
                .ToList();

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
                lines.RemoveAt(0);
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.RemoveAt(lines.Count - 1);

            return string.Join("\n", lines);
        }

        /// <summary>
        ///     Replaces the generator version inside a [GeneratedCode] attribute with a placeholder.
        /// </summary>
        /// <remarks>
        ///     BuildTT bumps the version on release, which would otherwise make this one snippet fail the drift
        ///     test on every version bump - a failure that says nothing and trains people to ignore the test. The
        ///     shape is what the page is documenting; the digits are not.
        /// </remarks>
        private static string StabiliseGeneratorVersion(string generated)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                generated,
                @"(\[GeneratedCode\(""EF\.Reverse\.POCO\.Generator"", "")[^""]*("")",
                "${1}v4.x.x${2}");
        }

        private static bool IsLicenceNoise(string line)
        {
            var t = line.Trim();
            return t.StartsWith("// ****")
                || t.StartsWith("// This is not a commercial licence")
                || t.StartsWith("// Academic licence")
                || t.StartsWith("// Licence file")
                || t.StartsWith("// Your licence file")
                || t.StartsWith("// Please obtain your licence")
                || t.StartsWith("// Defaulting to Trial version");
        }
    }

    /// <summary>
    ///     A before and an after, ready to be diffed or written out.
    /// </summary>
    public class DocSamplePair
    {
        public string Before { get; private set; }
        public string After { get; private set; }

        public DocSamplePair(string before, string after)
        {
            Before = before;
            After = after;
        }
    }
}
