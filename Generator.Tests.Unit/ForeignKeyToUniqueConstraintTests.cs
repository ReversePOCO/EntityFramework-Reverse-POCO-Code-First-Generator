using System.Collections.Generic;
using System.Text.RegularExpressions;
using Efrpg;
using Efrpg.Generators;
using Generator.Tests.Common;
using Generator.Tests.Unit.DocSamples;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     A composite foreign key that references a unique constraint rather than the primary key (#614). Whether it is
    ///     one-to-one is decided by the child: the parent's columns are always unique, because a foreign key can only
    ///     reference a key, so they say nothing about how many children each parent has.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class ForeignKeyToUniqueConstraintTests
    {
        private StaticStateSnapshot _snapshot;
        private string _generated;

        [OneTimeSetUp]
        public void GenerateOnce()
        {
            _snapshot = StaticStateSnapshot.Capture();
            DocSampleRunner.Pristine = _snapshot;
            _generated = DocSampleRunner.GenerateFromPayload(Payload, () => { });
        }

        [OneTimeTearDown]
        public void RestoreStaticState()
        {
            _snapshot.Restore();
        }

        [Test]
        public void GenerateCode_CompositeForeignKeyNotUniqueInTheChild_IsManyToOne()
        {
            var configuration = Section("public class Table1Configuration");

            Assert.That(configuration, Does.Contain(".WithMany(b => b.Table1).HasPrincipalKey(p => new { p.Id, p.Num }).HasForeignKey(c => new { c.IdT, c.Num })"));
        }

        [Test]
        public void GenerateCode_CompositeForeignKeyNotUniqueInTheChild_GivesTheParentACollection()
        {
            var entity = Section("public class Table2\n");

            Assert.That(entity, Does.Contain("public ICollection<Table1> Table1 { get; set; }"));
        }

        [Test]
        public void GenerateCode_CompositeForeignKeyThatIsAUniqueConstraintOfTheChild_IsOneToOne()
        {
            var configuration = Section("public class ProfileConfiguration");

            Assert.That(configuration, Does.Contain(".WithOne(b => b.Profile).HasPrincipalKey<Table2>(p => new { p.Id, p.Num }).HasForeignKey<Profile>(c => new { c.Table2Id, c.Table2Num })"));
        }

        [Test]
        public void GenerateCode_CompositeForeignKeyToANullableUniqueConstraint_IsNotGenerated()
        {
            // HasPrincipalKey makes Cc and Dd an alternate key, which EF Core makes required, so it would refuse to track
            // any Lookup row with a null in either column.
            Assert.That(_generated, Does.Not.Contain("FK_LookupUser_Lookup\")"));
        }

        [Test]
        public void CalcRelationship_CompositeRelationshipAddedAsUnique_IsOneToOne()
        {
            // Settings.AddExtraForeignKeys documents isUnique as choosing one-to-one, whatever the child's indexes say.
            var parent = new Table(null, new Schema("dbo"), "Parent", false);
            var child = new Table(null, new Schema("dbo"), "Child", false);
            child.Columns.Add(new Column { DbName = "Id", IsPrimaryKey = true });
            var foreignKey = new ForeignKey("Child", "dbo", "Parent", "dbo", "A", "A", "AddRelationship: Test", "Child", "Parent", 1, false, false, "", "", true)
            {
                IsDeclaredUnique = true
            };
            var fkCols = new List<ColumnAndForeignKey>
            {
                new ColumnAndForeignKey { Column = new Column { DbName = "A" }, ForeignKey = foreignKey },
                new ColumnAndForeignKey { Column = new Column { DbName = "B" }, ForeignKey = foreignKey }
            };
            var pkCols = new List<ColumnAndForeignKey>
            {
                new ColumnAndForeignKey { Column = new Column { DbName = "A", IsPrimaryKey = true }, ForeignKey = foreignKey },
                new ColumnAndForeignKey { Column = new Column { DbName = "B", IsPrimaryKey = true }, ForeignKey = foreignKey }
            };

            var relationship = Efrpg.Generators.Generator.CalcRelationship(parent, child, fkCols, pkCols);

            Assert.That(relationship, Is.EqualTo(Relationship.OneToOne));
        }

        private string Section(string start)
        {
            var match = Regex.Match(_generated, Regex.Escape(start) + @".*?\n}\n", RegexOptions.Singleline);
            Assert.That(match.Success, Is.True, start + " was not generated");
            return match.Value;
        }

        private static string Column(string table, int ordinal, string name, bool isNullable = false, bool primaryKey = false)
        {
            return $@"<Row schemaName=""dbo"" tableName=""{table}"" isView=""false"" isSynonym=""false"" ordinal=""{ordinal}"" columnName=""{name}"" typeName=""bigint"" " +
                   $@"isNullable=""{Bool(isNullable)}"" maxLength=""0"" precision=""19"" scale=""0"" dateTimePrecision=""0"" isIdentity=""{Bool(primaryKey)}"" isComputed=""false"" " +
                   $@"isRowGuid=""false"" generatedAlwaysType=""0"" isStoreGenerated=""{Bool(primaryKey)}"" primaryKey=""{Bool(primaryKey)}"" primaryKeyOrdinal=""{(primaryKey ? 1 : 0)}"" " +
                   @"isForeignKey=""false"" synonymTriggerName="""" default="""" />";
        }

        private static string ForeignKey(string name, string pkTable, string pkColumn, string fkTable, string fkColumn, int ordinal)
        {
            return $@"<Row constraintName=""{name}"" parentName="""" childName="""" pkSchema=""dbo"" pkTableName=""{pkTable}"" pkColumn=""{pkColumn}"" " +
                   $@"fkSchema=""dbo"" fkTableName=""{fkTable}"" fkColumn=""{fkColumn}"" ordinal=""{ordinal}"" cascadeOnDelete=""false"" isNotEnforced=""false"" hasUniqueConstraint=""false"" />";
        }

        private static string Index(string table, string name, int keyOrdinal, string column, int columnCount, bool isPrimaryKey)
        {
            return $@"<Row schema=""dbo"" tableName=""{table}"" indexName=""{name}"" keyOrdinal=""{keyOrdinal}"" columnName=""{column}"" columnCount=""{columnCount}"" " +
                   $@"isUnique=""true"" isPrimaryKey=""{Bool(isPrimaryKey)}"" isUniqueConstraint=""{Bool(!isPrimaryKey)}"" isClustered=""{Bool(isPrimaryKey)}"" filterDefinition="""" includedColumns="""" />";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        // Table1 and Table2 are the schema in the issue. Profile's foreign key is also a unique constraint of Profile, so
        // that one really is one-to-one. Lookup's unique constraint is on nullable columns.
        private static readonly string Payload =
            @"<EfrpgResult schemaVersion=""2"" toolVersion=""1.1.0"" defaultSchema=""dbo"" databaseDetails="""" includeSchema=""true"" canReadStoredProcedures=""true"" " +
            @"hasIdentityColumnSupport=""true"" doNotSpecifySizeForMaxLength=""false"">" +
            @"<Licence status=""Valid"" file="""" registeredTo="""" company="""" licenceType=""Commercial"" numLicences=""1"" validUntil=""31 DEC 9999"" />" +
            "<Tables>" +
            Column("Table2", 1, "id", primaryKey: true) + Column("Table2", 2, "num") +
            Column("Table1", 1, "id", primaryKey: true) + Column("Table1", 2, "id_t") + Column("Table1", 3, "num") +
            Column("Profile", 1, "Id", primaryKey: true) + Column("Profile", 2, "Table2Id") + Column("Profile", 3, "Table2Num") +
            Column("Lookup", 1, "Id", primaryKey: true) + Column("Lookup", 2, "Cc", isNullable: true) + Column("Lookup", 3, "Dd", isNullable: true) +
            Column("LookupUser", 1, "Id", primaryKey: true) + Column("LookupUser", 2, "C", isNullable: true) + Column("LookupUser", 3, "D", isNullable: true) +
            "</Tables><ForeignKeys>" +
            ForeignKey("FK_Table1_Table2", "Table2", "id", "Table1", "id_t", 1) + ForeignKey("FK_Table1_Table2", "Table2", "num", "Table1", "num", 2) +
            ForeignKey("FK_Profile_Table2", "Table2", "id", "Profile", "Table2Id", 1) + ForeignKey("FK_Profile_Table2", "Table2", "num", "Profile", "Table2Num", 2) +
            ForeignKey("FK_LookupUser_Lookup", "Lookup", "Cc", "LookupUser", "C", 1) + ForeignKey("FK_LookupUser_Lookup", "Lookup", "Dd", "LookupUser", "D", 2) +
            "</ForeignKeys><Indexes>" +
            Index("Table2", "PK_Table2", 1, "id", 1, true) + Index("Table2", "U_Table2", 1, "id", 2, false) + Index("Table2", "U_Table2", 2, "num", 2, false) +
            Index("Table1", "PK_Table1", 1, "id", 1, true) +
            Index("Profile", "PK_Profile", 1, "Id", 1, true) + Index("Profile", "UQ_Profile_Table2", 1, "Table2Id", 2, false) +
            Index("Profile", "UQ_Profile_Table2", 2, "Table2Num", 2, false) +
            Index("Lookup", "PK_Lookup", 1, "Id", 1, true) + Index("Lookup", "UQ_Lookup_CcDd", 1, "Cc", 2, false) + Index("Lookup", "UQ_Lookup_CcDd", 2, "Dd", 2, false) +
            Index("LookupUser", "PK_LookupUser", 1, "Id", 1, true) +
            "</Indexes><ExtendedProperties /><Sequences /><Triggers /><MemoryOptimisedTables /><StoredProcedures /><Errors /></EfrpgResult>";
    }
}
