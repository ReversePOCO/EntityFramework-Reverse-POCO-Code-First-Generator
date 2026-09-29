using System;
using System.Linq;
using System.Xml.Linq;
using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     Parsed against the captured wire contract payload rather than hand-written XML, so this fails alongside
    ///     WireContractTests if the tool's output ever moves.
    /// </summary>
    [TestFixture]
    public class DatabaseSchemaTests
    {
        private static DatabaseSchema Real()
        {
            return DatabaseSchema.Parse(RepositoryFiles.WireContractPayload());
        }

        [Test]
        public void Parse_ReadsTheHeader()
        {
            var schema = Real();

            Assert.That(schema.DefaultSchema, Is.EqualTo("dbo"));
            Assert.That(schema.SchemaVersion, Is.EqualTo(PayloadSchemaVersion()));
            Assert.That(schema.ToolVersion, Is.Not.Empty);
            Assert.That(schema.CanReadStoredProcedures, Is.True);
        }

        /// <summary>
        ///     The gate's version is a floor, not the current one: the payload comes from the newest tool, which is
        ///     free to be ahead of what the template needs, as schema 3 is ahead of the gate's 2.
        /// </summary>
        [Test]
        public void Parse_PayloadSchemaVersion_MeetsTheToolGateFloor()
        {
            var schema = Real();

            Assert.That(schema.SchemaVersion, Is.GreaterThanOrEqualTo(EfrpgToolGate.RequiredSchemaVersion));
        }

        private static int PayloadSchemaVersion()
        {
            return int.Parse(XDocument.Parse(RepositoryFiles.WireContractPayload()).Root!.Attribute("schemaVersion")!.Value); // the fixture always carries it
        }

        [Test]
        public void Parse_FindsTablesAndStoredProcedures()
        {
            var schema = Real();

            Assert.That(schema.Count(DatabaseObjectKind.Table), Is.GreaterThan(0));
            Assert.That(schema.Count(DatabaseObjectKind.StoredProcedure), Is.GreaterThan(0));
        }

        /// <summary>
        ///     Every row under Tables is a column, so a table with four columns appears four times in the payload
        ///     and must appear once in the picker.
        /// </summary>
        [Test]
        public void Parse_CollapsesTheColumnRowsIntoOneEntryPerTable()
        {
            var names = Real().Objects
                .Where(o => o.Kind == DatabaseObjectKind.Table || o.Kind == DatabaseObjectKind.View)
                .Select(o => o.FullName)
                .ToList();

            Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(names.Count));
        }

        /// <summary>
        ///     Every row under StoredProcedures is a parameter, so a procedure with three parameters appears three
        ///     times in the payload and must appear once in the picker. The captured payload has 15 rows for 11
        ///     routines; shipped uncollapsed, each copy had its own checkbox and only the first ever responded.
        /// </summary>
        [Test]
        public void Parse_CollapsesTheParameterRowsIntoOneEntryPerRoutine()
        {
            var names = Real().Objects
                .Where(o => o.Kind != DatabaseObjectKind.Table && o.Kind != DatabaseObjectKind.View)
                .Select(o => o.FullName)
                .ToList();

            Assert.That(names, Is.Not.Empty);
            Assert.That(names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(names.Count));
            Assert.That(names.Count, Is.LessThan(RepositoryFiles.WireContractPayload().Split(new[] { "<StoredProcedures>" }, StringSplitOptions.None)[1].Split(new[] { "<Row " }, StringSplitOptions.None).Length - 1),
                "the payload should hold more rows than routines, or this test proves nothing");
        }

        [Test]
        public void Parse_QualifiesNamesWithTheirSchema()
        {
            Assert.That(Real().Objects.Select(o => o.FullName), Has.Some.Contains("."));
        }

        [Test]
        public void Parse_OrdersBySchemaThenName()
        {
            var objects = Real().Objects.ToList();

            Assert.That(objects, Is.Ordered);
        }

        /// <summary>A synonym is an alias for something already in the list, so listing it twice is misleading.</summary>
        [Test]
        public void Parse_SkipsSynonyms()
        {
            var payload =
                "<EfrpgResult schemaVersion=\"1\"><Tables>" +
                "<Row schemaName=\"dbo\" tableName=\"Real\" isView=\"false\" isSynonym=\"false\" columnName=\"a\" />" +
                "<Row schemaName=\"dbo\" tableName=\"Alias\" isView=\"false\" isSynonym=\"true\" columnName=\"a\" />" +
                "</Tables></EfrpgResult>";

            Assert.That(DatabaseSchema.Parse(payload).Objects.Select(o => o.Name), Is.EqualTo(new[] { "Real" }));
        }

        [Test]
        public void Parse_TellsViewsFromTables()
        {
            var payload =
                "<EfrpgResult><Tables>" +
                "<Row schemaName=\"dbo\" tableName=\"T\" isView=\"false\" columnName=\"a\" />" +
                "<Row schemaName=\"dbo\" tableName=\"V\" isView=\"true\" columnName=\"a\" />" +
                "</Tables></EfrpgResult>";

            var schema = DatabaseSchema.Parse(payload);

            Assert.That(schema.Of(DatabaseObjectKind.Table).Single().Name, Is.EqualTo("T"));
            Assert.That(schema.Of(DatabaseObjectKind.View).Single().Name, Is.EqualTo("V"));
        }

        /// <summary>
        ///     The two function kinds are told apart because the generator switches them on with separate flags,
        ///     and the picker has to know which one to write.
        /// </summary>
        [Test]
        public void Parse_TellsBothKindsOfFunctionFromStoredProcedures()
        {
            var payload =
                "<EfrpgResult><StoredProcedures>" +
                "<Row schema=\"dbo\" name=\"P\" isStoredProcedure=\"true\" />" +
                "<Row schema=\"dbo\" name=\"T\" isStoredProcedure=\"false\" isTableValuedFunction=\"true\" />" +
                "<Row schema=\"dbo\" name=\"S\" isStoredProcedure=\"false\" isScalarValuedFunction=\"true\" />" +
                "</StoredProcedures></EfrpgResult>";

            var schema = DatabaseSchema.Parse(payload);

            Assert.That(schema.Of(DatabaseObjectKind.StoredProcedure).Single().Name, Is.EqualTo("P"));
            Assert.That(schema.Of(DatabaseObjectKind.TableValuedFunction).Single().Name, Is.EqualTo("T"));
            Assert.That(schema.Of(DatabaseObjectKind.ScalarValuedFunction).Single().Name, Is.EqualTo("S"));
        }

        /// <summary>The captured payload carries table-valued functions, so the split is exercised on real bytes.</summary>
        [Test]
        public void Parse_FindsTableValuedFunctionsInTheRealPayload()
        {
            Assert.That(Real().Count(DatabaseObjectKind.TableValuedFunction), Is.GreaterThan(0));
        }

        [Test]
        public void Parse_KeepsTheErrorsTheToolReported()
        {
            Assert.That(Real().Errors, Is.Not.Empty);
        }

        /// <summary>
        ///     A database with no schemas - SQLite - must not produce names that start with a dot.
        /// </summary>
        [Test]
        public void FullName_OmitsAnEmptySchema()
        {
            Assert.That(new DatabaseObject(string.Empty, "Customers", DatabaseObjectKind.Table).FullName,
                Is.EqualTo("Customers"));
        }

        /// <summary>
        ///     Attributes this parser does not know about are ignored, which is what lets a newer tool serve an
        ///     older GUI - the same forward-compatibility rule the wire format itself is built on.
        /// </summary>
        [Test]
        public void Parse_IgnoresAttributesItDoesNotKnow()
        {
            var payload =
                "<EfrpgResult somethingNew=\"1\"><Tables>" +
                "<Row schemaName=\"dbo\" tableName=\"T\" isView=\"false\" columnName=\"a\" alsoNew=\"2\" />" +
                "</Tables><SomethingElseEntirely /></EfrpgResult>";

            Assert.That(DatabaseSchema.Parse(payload).Objects.Single().Name, Is.EqualTo("T"));
        }

        /// <summary>
        ///     A tool that wrote a message to stdout instead of stderr would otherwise be parsed as an empty
        ///     database, which reads to the user as "your database has no tables".
        /// </summary>
        [TestCase("")]
        [TestCase("Unhandled exception: something went wrong")]
        [TestCase("<SomethingElse />")]
        public void Parse_RefusesAnythingThatIsNotAnEfrpgResult(string payload)
        {
            Assert.That(() => DatabaseSchema.Parse(payload), Throws.TypeOf<FormatException>());
        }
    }
}
