using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Efrpg;
using Generator.Tests.Common;
using Generator.Tests.Unit.DocSamples;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     Visual Studio can run an unchanged template again without reloading it, so the static Settings class still
    ///     holds the previous transformation's values when the template body runs. A template that appends to a
    ///     collection - the vanilla Database.tt's own StoredProcedureReturnTypes.Add example - then threw "An item with
    ///     the same key has already been added" on every run after the first (#902).
    /// </summary>
    /// <remarks>
    ///     None of this reproduces outside Visual Studio: TextTransform.exe starts a new process per run, and every
    ///     other test builds Settings from scratch. So the fix is tested in two halves - Settings.Reset puts the state
    ///     back, and the shipped include calls it before the template body runs. Either half alone fixes nothing.
    /// </remarks>
    [TestFixture]
    [Category(Constants.CI)]
    public class SettingsResetTests
    {
        private const string Include = "EF.Reverse.POCO.v4.ttinclude";

        private StaticStateSnapshot _snapshot;

        [SetUp]
        public void CaptureStaticState()
        {
            _snapshot = StaticStateSnapshot.Capture();
        }

        [TearDown]
        public void RestoreStaticState()
        {
            _snapshot.Restore();
        }

        [Test]
        public void Reset_TemplateAddsAStoredProcedureReturnTypeOnEveryRun_SecondRunDoesNotThrow()
        {
            RunTemplate();

            Assert.That(RunTemplate, Throws.Nothing);
        }

        [Test]
        public void Reset_AfterEveryCollectionSettingIsAppendedTo_EachHoldsItsDefaultContentsAgain()
        {
            Settings.Reset();
            var defaults = CollectionCounts();
            Assert.That(defaults.Keys, Does.Contain(nameof(Settings.StoredProcedureReturnTypes)),
                "The reflection found no collections, so the comparison below would pass vacuously");
            foreach (var collection in CollectionSettings())
                Append(collection.GetValue(null));

            Settings.Reset();

            Assert.That(CollectionCounts(), Is.EquivalentTo(defaults));
        }

        [Test]
        public void Reset_AfterAStringSettingIsAppendedTo_ItHoldsItsDefaultAgain()
        {
            Settings.Reset();
            var defaultValue = Settings.DbContextInterfaceBaseClasses;
            Settings.DbContextInterfaceBaseClasses += ", IUnitOfWork";

            Settings.Reset();

            Assert.That(Settings.DbContextInterfaceBaseClasses, Is.EqualTo(defaultValue));
        }

        [Test]
        public void Reset_AfterDbContextInterfaceNameIsSet_ItIsDerivedFromDbContextNameAgain()
        {
            Settings.DbContextInterfaceName = "IMyRepository";

            Settings.Reset();

            Assert.That(Settings.DbContextInterfaceName, Is.EqualTo("I" + Settings.DbContextName));
        }

        [Test]
        public void Include_Prologue_ResetsSettingsBeforeAnythingElseTouchesThem()
        {
            var include = File.ReadAllText(Path.Combine(RepositoryRoot(), "EntityFramework.Reverse.POCO.Generator", Include));
            var prologue = include.Substring(0, include.IndexOf("<#+", StringComparison.Ordinal));

            var firstUse = Regex.Match(prologue, @"(?m)^\s*Settings\.\w+");

            Assert.That(firstUse.Value.Trim(), Is.EqualTo("Settings.Reset"),
                Include + " must call Settings.Reset() in its prologue, before it sets TemplateFile and Root and before " +
                "the template body runs. Edit header2 in BuildTT/BuildTT.cs and run BuildTT.");
        }

        /// <summary>
        ///     What one transformation of a template that uses the example does to Settings: the include's prologue,
        ///     then the line from Database.tt.
        /// </summary>
        private static void RunTemplate()
        {
            Settings.Reset();
            Settings.StoredProcedureReturnTypes.Add("SalesByYear", "SummaryOfSalesByYear");
        }

        /// <summary>
        ///     Arrays are left out: a template can only replace one, never grow it.
        /// </summary>
        private static IEnumerable<FieldInfo> CollectionSettings()
        {
            return typeof(Settings)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(x => typeof(IList).IsAssignableFrom(x.FieldType) || typeof(IDictionary).IsAssignableFrom(x.FieldType))
                .Where(x => !x.FieldType.IsArray);
        }

        private static Dictionary<string, int> CollectionCounts()
        {
            return CollectionSettings().ToDictionary(x => x.Name, x => ((ICollection) x.GetValue(null)).Count);
        }

        private static void Append(object collection)
        {
            var dictionary = collection as IDictionary;
            if (dictionary != null)
            {
                dictionary.Add("#902", null);
                return;
            }

            ((IList) collection).Add(null);
        }

        private static string RepositoryRoot()
        {
            var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (folder != null && !File.Exists(Path.Combine(folder.FullName, "EF.Reverse.POCO.GeneratorV4.sln")))
                folder = folder.Parent;

            Assert.That(folder, Is.Not.Null,
                "Could not find EF.Reverse.POCO.GeneratorV4.sln above " + AppDomain.CurrentDomain.BaseDirectory +
                ". This test reads the checked-in " + Include + " from the working tree, not from the output folder.");

            return folder.FullName;
        }
    }
}
