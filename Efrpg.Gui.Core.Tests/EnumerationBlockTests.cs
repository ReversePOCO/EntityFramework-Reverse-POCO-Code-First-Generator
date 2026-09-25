using System;
using System.Linq;
using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     Appending enums to Settings.Enumerations: the entry lands as the last element, nothing already in the
    ///     block moves, and the result still parses as one assignment the editor can switch off and on.
    /// </summary>
    [TestFixture]
    public class EnumerationBlockTests
    {
        private static SettingsCatalogue V4 => SettingsCatalogue.Load(RepositoryFiles.SettingsMetadata("v4"));

        private static readonly EnumerationEntry DaysOfWeek =
            new EnumerationEntry("DaysOfWeek", "EnumTest.DaysOfWeek", "TypeName", "TypeId", null);

        [Test]
        public void Append_OnTheShippedTemplate_AddsTheEntryLastAndKeepsEverythingElse()
        {
            var original = RepositoryFiles.DatabaseTemplate();
            var document = TemplateSettingsDocument.Parse(original);
            var before   = document.StatementText(document.Find("Enumerations"));

            var appended = EnumerationBlock.Append(document, DaysOfWeek);

            var after = appended.StatementText(appended.Find("Enumerations")).Replace("\r\n", "\n").Split('\n');
            var head  = before.Replace("\r\n", "\n").Split('\n');
            Assert.That(after.Take(head.Length - 1), Is.EqualTo(head.Take(head.Length - 1)), "every existing line is untouched");
            Assert.That(after.Last().Trim(), Is.EqualTo("};"), "the block still closes the same way");
            Assert.That(string.Join("\n", after), Does.Contain("        new EnumerationSettings\n        {\n            Name       = \"DaysOfWeek\",\n            Table      = \"EnumTest.DaysOfWeek\",\n            NameField  = \"TypeName\",\n            ValueField = \"TypeId\"\n        },\n    };"));
            Assert.That(appended.Text.Replace("\r\n", "\n").Split('\n').Length, Is.EqualTo(original.Replace("\r\n", "\n").Split('\n').Length + 7));
        }

        [Test]
        public void Append_KeepsTheFileLineEndings()
        {
            var lf = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate().Replace("\r\n", "\n"));

            var appended = EnumerationBlock.Append(lf, DaysOfWeek);

            Assert.That(appended.Text, Does.Not.Contain("\r"));
        }

        [Test]
        public void Append_TwiceInARow_GivesTwoEntriesInOrder()
        {
            var document = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate());
            var second   = new EnumerationEntry("OrderStatus", "dbo.OrderStatus", "Name", "Id", null);

            var appended = EnumerationBlock.Append(EnumerationBlock.Append(document, DaysOfWeek), second);
            var text     = appended.StatementText(appended.Find("Enumerations"));

            Assert.That(text.IndexOf("\"DaysOfWeek\""), Is.LessThan(text.IndexOf("\"OrderStatus\"")));
            Assert.That(appended.Find("Enumerations").SpansMultipleLines, Is.True);
        }

        [Test]
        public void Append_AGroupedEnum_WritesTheGroupField()
        {
            var document = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate());
            var grouped  = new EnumerationEntry("{GroupField}Type", "dbo.Lookups", "Name", "Id", "GroupName");

            var text = EnumerationBlock.Append(document, grouped).Text;

            Assert.That(text, Does.Contain("ValueField = \"Id\",\n            GroupField = \"GroupName\"\n").Or.Contain("ValueField = \"Id\",\r\n            GroupField = \"GroupName\"\r\n"));
        }

        /// <summary>
        ///     The block from EfCoreReference\Net10\EfrpgTest.tt, where one append produced code that did not compile:
        ///     its last entry has no trailing comma, and its Settings line starts in column 0 while the entries sit
        ///     eight spaces in, so indenting from the Settings line put the new entry four spaces short.
        /// </summary>
        [Test]
        public void Append_LastEntryWithoutATrailingComma_AddsTheCommaAndMatchesTheEntries()
        {
            const string existing =
                "Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"RegionDescription\",\n" +
                "            Table      = \"Region\",\n" +
                "            NameField  = \"RegionDescription\",\n" +
                "            ValueField = \"RegionID\"\n" +
                "        },\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"ShipperName\",\n" +
                "            Table      = \"Shippers\",\n" +
                "            NameField  = \"CompanyName\",\n" +
                "            ValueField = \"ShipperID\"\n" +
                "        }\n" +
                "    };";

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Is.EqualTo(
                "Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"RegionDescription\",\n" +
                "            Table      = \"Region\",\n" +
                "            NameField  = \"RegionDescription\",\n" +
                "            ValueField = \"RegionID\"\n" +
                "        },\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"ShipperName\",\n" +
                "            Table      = \"Shippers\",\n" +
                "            NameField  = \"CompanyName\",\n" +
                "            ValueField = \"ShipperID\"\n" +
                "        },\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"CarEnum\",\n" +
                "            Table      = \"dbo.Car\",\n" +
                "            NameField  = \"CarMake\",\n" +
                "            ValueField = \"Id\"\n" +
                "        }\n" +
                "    };"));
        }

        /// <summary>
        ///     The new entry takes its indentation from the entries already there, and its inner step from how far
        ///     they sit inside the list's braces, whatever the Settings line itself is indented by.
        /// </summary>
        [TestCase("", "    ", "        ", "    ", TestName = "Append matches entries eight in under a Settings line in column 0")]
        [TestCase("    ", "    ", "        ", "    ", TestName = "Append matches the shipped layout")]
        [TestCase("        ", "        ", "            ", "    ", TestName = "Append matches a block nested a level deeper")]
        [TestCase("    ", "    ", "      ", "  ", TestName = "Append matches two-space indentation")]
        [TestCase("\t", "\t", "\t\t", "\t", TestName = "Append matches tab indentation")]
        public void Append_AnyIndentation_TheNewEntryMatchesTheExistingOnes(string settingsIndent, string braceIndent, string entryIndent, string step)
        {
            var existing = Block(settingsIndent, braceIndent, entryIndent, step, AEnum);

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Is.EqualTo(Block(settingsIndent, braceIndent, entryIndent, step, AEnum, Car)));
        }

        [Test]
        public void Append_LastEntryAlreadyEndsWithAComma_AddsNoSecondCommaAndKeepsTrailingCommas()
        {
            var existing = Block("    ", "    ", "        ", "    ", AEnum).Replace("        }\n    };", "        },\n    };");

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Is.EqualTo(Block("    ", "    ", "        ", "    ", AEnum, Car).Replace("        }\n    };", "        },\n    };")));
        }

        [Test]
        public void Append_LastEntryFollowedByAComment_PutsTheCommaBeforeTheComment()
        {
            var existing = Block("    ", "    ", "        ", "    ", AEnum).Replace("        }\n    };", "        } // the only one\n    };");

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Does.Contain("        }, // the only one\n        new EnumerationSettings\n"));
        }

        [Test]
        public void Append_EmptyList_AddsTheEntryOneStepInsideTheBracesWithNoStrayComma()
        {
            const string existing =
                "    Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "    };";

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Is.EqualTo(
                "    Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"CarEnum\",\n" +
                "            Table      = \"dbo.Car\",\n" +
                "            NameField  = \"CarMake\",\n" +
                "            ValueField = \"Id\"\n" +
                "        },\n" +
                "    };"));
        }

        [Test]
        public void Append_LastEntryClosesOnTheListsOwnLine_SplitsItAddsTheCommaAndIndentsTheCloser()
        {
            const string existing =
                "    Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "        new EnumerationSettings { Name = \"AEnum\", Table = \"dbo.A\", NameField = \"Name\", ValueField = \"Id\" } };";

            var appended = EnumerationBlock.Append(Template(existing), Car);

            Assert.That(Statement(appended), Is.EqualTo(
                "    Settings.Enumerations = new List<EnumerationSettings>\n" +
                "    {\n" +
                "        new EnumerationSettings { Name = \"AEnum\", Table = \"dbo.A\", NameField = \"Name\", ValueField = \"Id\" },\n" +
                "        new EnumerationSettings\n" +
                "        {\n" +
                "            Name       = \"CarEnum\",\n" +
                "            Table      = \"dbo.Car\",\n" +
                "            NameField  = \"CarMake\",\n" +
                "            ValueField = \"Id\"\n" +
                "        }\n" +
                "    };"));
        }

        private static readonly EnumerationEntry AEnum = new EnumerationEntry("AEnum", "dbo.A", "Name", "Id", null);
        private static readonly EnumerationEntry Car   = new EnumerationEntry("CarEnum", "dbo.Car", "CarMake", "Id", null);

        private static TemplateSettingsDocument Template(string statement)
        {
            return TemplateSettingsDocument.Parse("<#\n" + statement + "\n#>");
        }

        private static string Statement(TemplateSettingsDocument document)
        {
            return document.StatementText(document.Find("Enumerations")).Replace("\r\n", "\n");
        }

        /// <summary>A block laid out by hand the way its author indents, with no trailing comma after the last entry.</summary>
        private static string Block(string settingsIndent, string braceIndent, string entryIndent, string step, params EnumerationEntry[] entries)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                settingsIndent + "Settings.Enumerations = new List<EnumerationSettings>",
                braceIndent + "{"
            };

            for (var i = 0; i < entries.Length; i++)
            {
                lines.Add(entryIndent + "new EnumerationSettings");
                lines.Add(entryIndent + "{");
                lines.Add(entryIndent + step + "Name       = \"" + entries[i].Name + "\",");
                lines.Add(entryIndent + step + "Table      = \"" + entries[i].Table + "\",");
                lines.Add(entryIndent + step + "NameField  = \"" + entries[i].NameField + "\",");
                lines.Add(entryIndent + step + "ValueField = \"" + entries[i].ValueField + "\"");
                lines.Add(entryIndent + (i < entries.Length - 1 ? "}," : "}"));
            }

            lines.Add(braceIndent + "};");
            return string.Join("\n", lines);
        }

        [Test]
        public void CannotAppendReason_ExplainsAnAbsentACommentedOutAndAForeignBlock()
        {
            var shipped = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate());
            Assert.That(EnumerationBlock.CannotAppendReason(shipped), Is.Null);

            var off = shipped.WithCommentedOut(shipped.Find("Enumerations"));
            Assert.That(EnumerationBlock.CannotAppendReason(off), Does.Contain("commented out"));

            var gone = shipped.WithoutAssignment(shipped.Find("Enumerations"));
            Assert.That(EnumerationBlock.CannotAppendReason(gone), Does.Contain("not in this template"));

            var foreign = TemplateSettingsDocument.Parse("<#\n    Settings.Enumerations = BuildEnums();\n#>");
            Assert.That(EnumerationBlock.CannotAppendReason(foreign), Does.Contain("cannot be appended"));
        }

        [Test]
        public void MentionsTable_FindsATableAlreadyInTheBlock()
        {
            var document = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate());
            var appended = EnumerationBlock.Append(document, DaysOfWeek);

            Assert.That(EnumerationBlock.MentionsTable(appended, "EnumTest.DaysOfWeek"), Is.True);
            Assert.That(EnumerationBlock.MentionsTable(appended, "dbo.Nothing"), Is.False);
        }

        [Test]
        public void Session_QueuesAnEnumAndWritesItAfterTheOtherEdits()
        {
            var session = SettingsEditSession.Load(RepositoryFiles.DatabaseTemplate(), V4);
            session.Find("GenerateSeparateFiles").SetBoolean(true);
            session.AddEnumeration(DaysOfWeek);

            Assert.That(session.HasChanges, Is.True);
            Assert.That(session.ChangeCount, Is.EqualTo(2));

            var reloaded = SettingsEditSession.Load(session.Apply(), V4);
            Assert.That(reloaded.Find("GenerateSeparateFiles").BooleanValue, Is.True);
            Assert.That(EnumerationBlock.MentionsTable(reloaded.Document, "EnumTest.DaysOfWeek"), Is.True);
        }

        [Test]
        public void Session_SwitchesACommentedOutBlockOnBeforeAppending()
        {
            var shipped = TemplateSettingsDocument.Parse(RepositoryFiles.DatabaseTemplate());
            var off     = shipped.WithCommentedOut(shipped.Find("Enumerations")).Text;
            var session = SettingsEditSession.Load(off, V4);

            session.AddEnumeration(DaysOfWeek);
            var reloaded = SettingsEditSession.Load(session.Apply(), V4);

            Assert.That(reloaded.Find("Enumerations").IsAssigned, Is.True);
            Assert.That(EnumerationBlock.MentionsTable(reloaded.Document, "EnumTest.DaysOfWeek"), Is.True);
        }

        [Test]
        public void Session_AnInvalidEntryIsRefusedUpFront()
        {
            var session = SettingsEditSession.Load(RepositoryFiles.DatabaseTemplate(), V4);

            Assert.That(() => session.AddEnumeration(new EnumerationEntry("Bad Name", "dbo.T", "Name", "Id", null)),
                Throws.InvalidOperationException.With.Message.Contains("identifier"));
        }

        [TestCase("order_status", "OrderStatus")]
        [TestCase("ORDER_STATUS", "OrderStatus")]
        [TestCase("OrderStatus", "OrderStatus")]
        [TestCase("orderStatus", "OrderStatus")]
        [TestCase("1st_thing", "_1stThing")]
        public void PascalCase_NormalisesTableNames(string table, string expected)
        {
            Assert.That(EnumerationBlock.PascalCase(table), Is.EqualTo(expected));
        }

        [Test]
        public void Suggest_PicksTheIntegralKeyAndTheFirstTextColumn()
        {
            var table = new DatabaseObject("dbo", "order_status", DatabaseObjectKind.Table, new[]
            {
                new DatabaseColumn("id", "int", true, 1),
                new DatabaseColumn("code", "nvarchar", false, 2),
                new DatabaseColumn("sort_order", "int", false, 3)
            });

            var entry = EnumerationBlock.Suggest(table);

            Assert.That(entry.Name, Is.EqualTo("OrderStatusEnum"));
            Assert.That(entry.Table, Is.EqualTo("dbo.order_status"));
            Assert.That(entry.NameField, Is.EqualTo("code"));
            Assert.That(entry.ValueField, Is.EqualTo("id"));
            Assert.That(entry.IsValid, Is.True);
        }

        /// <summary>
        ///     The table is usually generated as an entity too, under much the same name, and the generator does not
        ///     check an enum against the classes beside it. The shipped AddEnum example appends Enum for the same reason.
        /// </summary>
        [Test]
        public void Suggest_EnumName_HasEnumAppendedSoItCannotClashWithTheTablesEntity()
        {
            var table = new DatabaseObject("dbo", "Colour", DatabaseObjectKind.Table, new[]
            {
                new DatabaseColumn("Id", "int", true, 1),
                new DatabaseColumn("Name", "varchar", false, 2)
            });

            var entry = EnumerationBlock.Suggest(table);

            Assert.That(entry.Name, Is.EqualTo("ColourEnum"));
        }

        [Test]
        public void Suggest_TableNameAlreadyEndingInEnum_IsNotSuffixedTwice()
        {
            var table = new DatabaseObject("dbo", "status_enum", DatabaseObjectKind.Table, new[]
            {
                new DatabaseColumn("id", "int", true, 1),
                new DatabaseColumn("name", "nvarchar", false, 2)
            });

            var entry = EnumerationBlock.Suggest(table);

            Assert.That(entry.Name, Is.EqualTo("StatusEnum"));
        }

        [Test]
        public void Candidates_AreEveryTableInNameOrderWithViewsLeftOut()
        {
            var schema = DatabaseSchema.Parse(RepositoryFiles.WireContractPayload());

            var candidates = EnumerationBlock.Candidates(schema);

            Assert.That(candidates, Has.All.Property("Kind").EqualTo(DatabaseObjectKind.Table));
            Assert.That(candidates.Count, Is.EqualTo(schema.Count(DatabaseObjectKind.Table)));
            Assert.That(candidates.Select(t => t.FullName), Is.Ordered.Using((System.Collections.Generic.IComparer<string>) StringComparer.OrdinalIgnoreCase));
        }

        [Test]
        public void Candidates_LookupsOnly_ListsOnlyTheLookupTablesInNameOrder()
        {
            var schema = Schema(
                Table("dbo", "Status", ("Id", "int", true), ("Name", "nvarchar", false)),
                Table("Alpha", "Harish3485", ("id", "int", true), ("harish_id", "int", false)),
                Table("dbo", "Colour", ("Id", "int", true), ("Name", "varchar", false)));

            var candidates = EnumerationBlock.Candidates(schema, showAllTables: false);

            Assert.That(candidates.Select(t => t.FullName), Is.EqualTo(new[] { "dbo.Colour", "dbo.Status" }));
        }

        [Test]
        public void Candidates_ShowAllTables_ListsEveryTableInNameOrder()
        {
            var schema = Schema(
                Table("dbo", "Status", ("Id", "int", true), ("Name", "nvarchar", false)),
                Table("Alpha", "Harish3485", ("id", "int", true), ("harish_id", "int", false)));

            var candidates = EnumerationBlock.Candidates(schema, showAllTables: true);

            Assert.That(candidates.Select(t => t.FullName), Is.EqualTo(new[] { "Alpha.Harish3485", "dbo.Status" }));
        }

        /// <summary>An empty dropdown would be a dead end, so a database with no lookup-shaped table offers them all.</summary>
        [Test]
        public void Candidates_LookupsOnlyButNoTableLooksLikeALookup_ListsEveryTable()
        {
            var schema = Schema(Table("Alpha", "Harish3485", ("id", "int", true), ("harish_id", "int", false)));

            var candidates = EnumerationBlock.Candidates(schema, showAllTables: false);

            Assert.That(candidates.Select(t => t.FullName), Is.EqualTo(new[] { "Alpha.Harish3485" }));
        }

        [Test]
        public void HasLookupTables_OnlyTablesWithoutATextColumn_IsFalse()
        {
            var schema = Schema(Table("Alpha", "Harish3485", ("id", "int", true), ("harish_id", "int", false)));

            Assert.That(EnumerationBlock.HasLookupTables(schema), Is.False);
        }

        /// <summary>
        ///     A lookup table often carries description, sort order, active flag and audit columns as well as its key
        ///     and name, and it must still be offered.
        /// </summary>
        [Test]
        public void LooksLikeEnumTable_TenColumnsIncludingAnIntegerAndText_IsALookup()
        {
            var table = WideTable(10);

            Assert.That(EnumerationBlock.LooksLikeEnumTable(table), Is.True);
        }

        [Test]
        public void LooksLikeEnumTable_ElevenColumns_IsNotALookup()
        {
            var table = WideTable(11);

            Assert.That(EnumerationBlock.LooksLikeEnumTable(table), Is.False);
        }

        private static DatabaseObject WideTable(int columnCount)
        {
            var columns = new[] { new DatabaseColumn("Id", "int", true, 1), new DatabaseColumn("Name", "nvarchar", false, 2) }
                .Concat(Enumerable.Range(3, columnCount - 2).Select(n => new DatabaseColumn("Extra" + n, "datetime2", false, n)))
                .ToArray();

            return new DatabaseObject("dbo", "Status", DatabaseObjectKind.Table, columns);
        }

        private static string Table(string schema, string name, params (string Column, string Type, bool Key)[] columns)
        {
            return string.Concat(columns.Select((c, i) =>
                $"<Row schemaName=\"{schema}\" tableName=\"{name}\" isView=\"false\" isSynonym=\"false\" ordinal=\"{i + 1}\" " +
                $"columnName=\"{c.Column}\" typeName=\"{c.Type}\" primaryKey=\"{(c.Key ? "true" : "false")}\" />"));
        }

        private static DatabaseSchema Schema(params string[] tables)
        {
            return DatabaseSchema.Parse("<EfrpgResult schemaVersion=\"2\"><Tables>" + string.Concat(tables) + "</Tables></EfrpgResult>");
        }

        [Test]
        public void Schema_CarriesColumnsForTables()
        {
            var schema = DatabaseSchema.Parse(RepositoryFiles.WireContractPayload());
            var table  = schema.Of(DatabaseObjectKind.Table).First();

            Assert.That(table.Columns, Is.Not.Empty);
            Assert.That(table.Columns.Select(c => c.Ordinal), Is.Ordered);
            Assert.That(schema.Of(DatabaseObjectKind.StoredProcedure).All(p => p.Columns.Count == 0), Is.True);
        }

        [Test]
        public void Entry_ProblemsAreNamedInTheOrderAUserFillsTheFormIn()
        {
            Assert.That(new EnumerationEntry("X", "", "n", "v", null).Problem, Does.Contain("table"));
            Assert.That(new EnumerationEntry("X", "t", "", "v", null).Problem, Does.Contain("name"));
            Assert.That(new EnumerationEntry("X", "t", "n", "", null).Problem, Does.Contain("value"));
            Assert.That(new EnumerationEntry("X", "t", "n", "n", null).Problem, Does.Contain("differ"));
            Assert.That(new EnumerationEntry("", "t", "n", "v", null).Problem, Does.Contain("name"));
            Assert.That(new EnumerationEntry("Status", "t", "n", "v", "g").Problem, Does.Contain("{GroupField}"));
            Assert.That(new EnumerationEntry("{GroupField}Type", "t", "n", "v", "g").IsValid, Is.True);
        }
    }
}
