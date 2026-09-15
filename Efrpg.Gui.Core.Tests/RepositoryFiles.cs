using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     Reaches the two generated files the wizard is written against: the Database.tt it edits, and the
    ///     settings metadata that records what the generator's enums actually contain.
    /// </summary>
    /// <remarks>
    ///     Both are produced by BuildTT from the sources under Generator/, so they are the closest thing this
    ///     netstandard2.0 assembly has to a compiler check against a net48 project it cannot reference. Testing
    ///     against them means a database type or template type added to the generator and not to the wizard's
    ///     dropdown fails here rather than quietly going missing from the UI.
    /// </remarks>
    public static class RepositoryFiles
    {
        private static readonly Lazy<string> RepositoryRoot = new(FindRoot);

        private static string FindRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "EF.Reverse.POCO.GeneratorV4.sln")))
                folder = folder.Parent;

            if (folder == null)
                throw new FileNotFoundException("Could not find the repository root above " + AppContext.BaseDirectory);

            return folder.FullName;
        }

        /// <summary>The Database.tt that ships in the item template, exactly as the wizard finds it on disk.</summary>
        public static string DatabaseTemplate()
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot.Value,
                "EntityFramework.Reverse.POCO.Generator", "Database.tt"));
        }

        /// <summary>
        ///     A real v3.14.1 Database.tt, recovered from the commit before database reading moved into the
        ///     efrpg tool. The upgrade is tested against the file customers actually have, not a reconstruction.
        /// </summary>
        public static string V3Template()
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot.Value,
                "Efrpg.Gui.Core.Tests", "Fixtures", "Database.v3.14.1.tt"));
        }

        /// <summary>
        ///     A real tester template whose connection string is an environment variable rather than a literal -
        ///     the way credentials are kept out of source control, and the case the connection dialog once got wrong.
        /// </summary>
        public static string AzureTemplate()
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot.Value, "Tester.Integration.EFCore10", "Azure.tt"));
        }

        /// <summary>
        ///     A real efrpg payload, captured from the EfrpgTest database and kept as the wire contract fixture.
        /// </summary>
        /// <remarks>
        ///     Parsing the same bytes the tool actually emitted is worth far more than parsing XML written by the
        ///     author of the parser, and it means the GUI's name extractor fails alongside WireContractTests if the
        ///     payload shape ever moves.
        /// </remarks>
        public static string WireContractPayload()
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot.Value,
                "Generator.Tests.Unit", "WireContract", "EfrpgResult.xml"));
        }

        /// <summary>The settings metadata for a template version, as shipped beside Database.tt.</summary>
        public static string SettingsMetadata(string version)
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot.Value,
                "EntityFramework.Reverse.POCO.Generator", "settings-metadata." + version + ".json"));
        }

        /// <summary>The wiki checkout beside this repository, or null on a machine that has only the generator.</summary>
        public static string? WikiFolder()
        {
            var root = new DirectoryInfo(RepositoryRoot.Value);
            if (root.Parent == null)
                return null;

            var wiki = Path.Combine(root.Parent.FullName, root.Name + ".wiki");

            return Directory.Exists(wiki) ? wiki : null;
        }

        /// <summary>
        ///     Real .tt files from this repository, for the round-trip tests.
        /// </summary>
        /// <remarks>
        ///     Deliberately varied: the shipped template, Northwind with its own filters, several tester
        ///     templates that have been hand-edited over years, and a v3 file out of git history. The settings
        ///     editor can silently destroy a paying customer's customisation, so it is tested against files
        ///     nobody wrote for it.
        /// </remarks>
        public static IReadOnlyList<string> TemplateFixtures()
        {
            var paths = new[]
            {
                Path.Combine("EntityFramework.Reverse.POCO.Generator", "Database.tt"),
                Path.Combine("EntityFramework.Reverse.POCO.Generator", "Northwind.tt"),
                Path.Combine("Tester.Integration.EFCore10", "EfrpgTest.tt"),
                Path.Combine("Tester.Integration.EFCore10", "Northwind.tt"),
                Path.Combine("Tester.Integration.EFCore10", "Azure.tt"),
                Path.Combine("Tester.Integration.EFCore8", "EfrpgTest_no_pascal_casing.tt"),
                Path.Combine("Tester.Integration.Ef6", "EfrpgTest.tt"),
                Path.Combine("Efrpg.Gui.Core.Tests", "Fixtures", "Database.v3.14.1.tt")
            };

            return paths.Select(p => Path.Combine(RepositoryRoot.Value, p)).Where(File.Exists).ToList();
        }

        /// <summary>
        ///     The members of one enum setting, in declaration order, as recorded by BuildTT's reflection over
        ///     Efrpg.Settings.
        /// </summary>
        public static IReadOnlyList<string> EnumMembers(string settingName)
        {
            var json = File.ReadAllText(Path.Combine(RepositoryRoot.Value,
                "EntityFramework.Reverse.POCO.Generator", "settings-metadata.v4.json"));

            using var document = JsonDocument.Parse(json);

            var setting = document.RootElement
                .GetProperty("settings")
                .EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("name").GetString() == settingName);

            if (setting.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("settings-metadata.v4.json has no setting named " + settingName + ".");

            return setting.GetProperty("enumMembers")
                .EnumerateArray()
                .Select(m => m.GetProperty("name").GetString()!)
                .ToList();
        }
    }
}
