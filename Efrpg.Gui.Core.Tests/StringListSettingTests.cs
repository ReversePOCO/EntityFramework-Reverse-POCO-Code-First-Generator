using System.Collections.Generic;
using System.Linq;
using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     The six list settings are edited as one item per line: read from whichever spelling the file uses,
    ///     written back in the same spelling, comments inside the initialiser ignored rather than misread.
    /// </summary>
    [TestFixture]
    public class StringListSettingTests
    {
        private static SettingsCatalogue V4 => SettingsCatalogue.Load(RepositoryFiles.SettingsMetadata("v4"));

        private static SettingsEditSession Shipped()
        {
            return SettingsEditSession.Load(RepositoryFiles.DatabaseTemplate(), V4);
        }

        public static IEnumerable<string> ListSettings()
        {
            return Shipped().Items.Where(i => i.Kind == SettingKind.StringList).Select(i => i.Name).OrderBy(n => n).ToList();
        }

        [Test]
        public void TheShippedTemplateHasSixListSettingsAndAllAreEditable()
        {
            var items = Shipped().Items.Where(i => i.Kind == SettingKind.StringList).ToList();

            Assert.That(items.Count, Is.EqualTo(6));
            Assert.That(items.Select(i => i.ReadOnlyReason), Has.All.Null);
            Assert.That(items.Select(i => i.StringListValue.Count), Has.All.EqualTo(0), "the shipped lists are empty, examples included");
        }

        [TestCase("new List<string>()", StringListForm.List, new string[0])]
        [TestCase("new List<string> { \"A\", \"B.C\" }", StringListForm.List, new[] { "A", "B.C" })]
        [TestCase("new List<string>{\"A\",}", StringListForm.List, new[] { "A" })]
        [TestCase("new string[0]", StringListForm.Array, new string[0])]
        [TestCase("new string[] { @\"C:\\x\", \"q\\\"uote\" }", StringListForm.Array, new[] { "C:\\x", "q\"uote" })]
        [TestCase("new string[]\r\n    {\r\n        // \"JsonIgnore\" // an example\r\n    }", StringListForm.Array, new string[0])]
        [TestCase("new string[]\r\n    {\r\n        \"JsonIgnore\", /* old */ \"Other\"\r\n    }", StringListForm.Array, new[] { "JsonIgnore", "Other" })]
        [TestCase("new List<string>\r\n{\r\n    \"A\", // \"B\" is retired\r\n    /* \"C\", */ \"D\"\r\n}", StringListForm.List, new[] { "A", "D" })]
        public void TryReadStringList_ReadsEverySpellingTheTemplatesUse(string rhs, StringListForm expectedForm, string[] expected)
        {
            IReadOnlyList<string> items;
            StringListForm form;

            Assert.That(SettingValue.TryReadStringList(rhs, out items, out form), Is.True);
            Assert.That(form, Is.EqualTo(expectedForm));
            Assert.That(items, Is.EqualTo(expected));
        }

        /// <summary>
        ///     Issue #890. Comments were stripped before literals were recognised, so a // or /* inside a value - a
        ///     URL, a pattern - was taken for a comment, and the value lost text or failed to read at all.
        /// </summary>
        [TestCase("new List<string> { \"a/*b*/c\" }", new[] { "a/*b*/c" })]
        [TestCase("new List<string> { \"https://example.com\" }", new[] { "https://example.com" })]
        [TestCase("new List<string> { @\"\\\\server//share\" }", new[] { "\\\\server//share" })]
        [TestCase("new List<string> { @\"a \"\" /* b\", \"c */ d\" }", new[] { "a \" /* b", "c */ d" })]
        public void TryReadStringList_CommentDelimitersInsideALiteral_AreKeptAsText(string rhs, string[] expected)
        {
            IReadOnlyList<string> items;
            StringListForm form;

            var read = SettingValue.TryReadStringList(rhs, out items, out form);

            Assert.That(read, Is.True);
            Assert.That(items, Is.EqualTo(expected));
        }

        [TestCase("BuildList()")]
        [TestCase("new List<string> { someVariable }")]
        [TestCase("new List<string> { \"A\", Other }")]
        [TestCase("new[] { 1, 2 }")]
        public void TryReadStringList_RefusesAnythingThatIsNotLiterals(string rhs)
        {
            IReadOnlyList<string> items;
            StringListForm form;

            Assert.That(SettingValue.TryReadStringList(rhs, out items, out form), Is.False);
        }

        [TestCaseSource(nameof(ListSettings))]
        public void SettingItems_WritesThemBackInTheFilesSpellingAndReadsThemAgain(string name)
        {
            var session = Shipped();
            var item    = session.Find(name);
            var wasArray = item.Assignment.ValueText.TrimStart().StartsWith("new string[]");

            item.SetStringList(new[] { "First", "Second.Third" });
            var text = session.Apply();

            var reloaded = SettingsEditSession.Load(text, V4).Find(name);
            Assert.That(reloaded.StringListValue, Is.EqualTo(new[] { "First", "Second.Third" }));
            Assert.That(reloaded.Assignment.ValueText, wasArray ? Does.StartWith("new string[]") : Does.StartWith("new List<string>"));
            Assert.That(reloaded.IsEditable, Is.True);
        }

        [TestCaseSource(nameof(ListSettings))]
        public void ClearingTheItems_LeavesAnEmptyListInTheFilesSpelling(string name)
        {
            var session = Shipped();
            session.Find(name).SetStringList(new[] { "X" });
            var withItem = SettingsEditSession.Load(session.Apply(), V4);

            withItem.Find(name).SetStringList(new string[0]);
            var reloaded = SettingsEditSession.Load(withItem.Apply(), V4).Find(name);

            Assert.That(reloaded.StringListValue, Is.Empty);
            Assert.That(reloaded.Assignment.SpansMultipleLines, Is.False, "an empty list is one line whichever way it was spelt");
        }

        [Test]
        public void ALongList_IsWrittenOneItemPerLineIndentedForTheStatement()
        {
            var session = Shipped();
            var items   = Enumerable.Range(1, 8).Select(i => "Some.Fairly.Long.Namespace.Number" + i).ToList();

            session.Find("AdditionalNamespaces").SetStringList(items);
            var text = session.Apply();

            var reloaded = SettingsEditSession.Load(text, V4).Find("AdditionalNamespaces");
            Assert.That(reloaded.StringListValue, Is.EqualTo(items));
            Assert.That(reloaded.Assignment.SpansMultipleLines, Is.True);
            Assert.That(text, Does.Contain("    {\r\n        \"Some.Fairly.Long.Namespace.Number1\",\r\n"));
        }

        [Test]
        public void ANewlineInsideAnItem_IsEscapedNotWrittenRaw()
        {
            var session = Shipped();

            session.Find("AdditionalFileHeaderText").SetStringList(new[] { "line one\nline two" });
            var reloaded = SettingsEditSession.Load(session.Apply(), V4).Find("AdditionalFileHeaderText");

            Assert.That(reloaded.StringListValue.Single(), Is.EqualTo("line one\nline two"));
        }

        [Test]
        public void TableSuffix_NowSitsInOtherSettings()
        {
            Assert.That(Shipped().Find("TableSuffix").Section, Is.EqualTo("Other settings"));
        }

        /// <summary>
        ///     The template keeps these under their own banners; the editor does not, because a group of one or two
        ///     rows is a page of nothing. The template is untouched: only the editor's grouping differs.
        /// </summary>
        [TestCase("PrependSchemaName", "Schema")]
        [TestCase("AdditionalReverseNavigationsDataAnnotations", "Additional attributes on on reverse navigation and foreign key properties")]
        [TestCase("AdditionalForeignKeysDataAnnotations", "Additional attributes on on reverse navigation and foreign key properties")]
        public void Section_SettingGroupedAwayFromItsBanner_ShowsUnderOtherSettings(string name, string banner)
        {
            var item = Shipped().Find(name);

            Assert.That(item.Definition.Section, Is.EqualTo(banner), "the template's own heading is still known");
            Assert.That(item.Section, Is.EqualTo("Other settings"));
        }

        /// <summary>
        ///     The editor's left-hand list is the groups that have a value row. "Schema" survives only as a heading on
        ///     the Callbacks page, over the two schema callbacks, which is where it belongs.
        /// </summary>
        [Test]
        public void Sections_ShippedTemplate_HasNoSchemaOrAdditionalAttributesGroupOfValues()
        {
            var valueSections = Shipped().Items.Where(i => !i.IsCode).Select(i => i.Section).Distinct().ToList();

            Assert.That(valueSections, Does.Not.Contain("Schema"));
            Assert.That(valueSections, Has.None.StartsWith("Additional attributes"));
        }

        [Test]
        public void Items_AnnotationLists_FollowAdditionalFileFooterText()
        {
            var names = Shipped().Items.Select(i => i.Name).ToList();
            var footer = names.IndexOf("AdditionalFileFooterText");

            Assert.That(names[footer + 1], Is.EqualTo("AdditionalReverseNavigationsDataAnnotations"));
            Assert.That(names[footer + 2], Is.EqualTo("AdditionalForeignKeysDataAnnotations"));
        }

        /// <summary>Regrouping must not scatter a heading through the "All settings" list.</summary>
        [Test]
        public void Items_ShippedTemplate_ListsEachGroupContiguously()
        {
            var runs = new List<string>();
            foreach (var item in Shipped().Items)
                if (runs.Count == 0 || runs[runs.Count - 1] != item.Section)
                    runs.Add(item.Section);

            Assert.That(runs, Is.Unique);
        }

        /// <summary>Display grouping is not placement: an added line still goes under the template's own banner.</summary>
        [Test]
        public void Apply_PrependSchemaNameAddedBack_LandsUnderTheSchemaBanner()
        {
            var original = RepositoryFiles.DatabaseTemplate();
            var lines    = original.Split('\n');
            var kept     = lines.Where(l => !l.TrimStart().StartsWith("Settings.PrependSchemaName ")).ToList();
            var session  = SettingsEditSession.Load(string.Join("\n", kept), V4);

            session.Find("PrependSchemaName").SetBoolean(false);
            var written = session.Apply().Split('\n');

            var banner = System.Array.FindIndex(written, l => l.TrimStart().StartsWith("// Schema **"));
            var added  = System.Array.FindIndex(written, l => l.TrimStart().StartsWith("Settings.PrependSchemaName "));
            Assert.That(added, Is.GreaterThan(banner));
            Assert.That(added - banner, Is.LessThan(6), "beside its banner, not off in Other settings");
        }

        [Test]
        public void TheEnumCallbacks_HaveTheirOwnSection()
        {
            var session = Shipped();

            Assert.That(session.Find("AddEnumDefinitions").Section, Is.EqualTo("Enum callbacks"));
            Assert.That(session.Find("AddEnum").Section, Is.EqualTo("Enum callbacks"));
            Assert.That(session.Find("UpdateEnum").Section, Is.EqualTo("Enum callbacks"));
            Assert.That(session.Find("UpdateEnumMember").Section, Is.EqualTo("Enum callbacks"));

            var sections = session.Sections.ToList();
            Assert.That(sections.IndexOf("Enum callbacks"), Is.LessThan(sections.IndexOf("Call-backs")), "the enum section comes first");

            Assert.That(session.Find("Enumerations").Section, Is.EqualTo("Enums"));
            Assert.That(session.Find("UsePascalCaseForEnumMembers").Section, Is.EqualTo("Enums"));
            Assert.That(session.Find("HiLoSequences").Section, Is.EqualTo("HiLo sequences"), "not an enum, so not on the Enums page");
        }
    }
}
