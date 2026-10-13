using System.Collections.Generic;
using System.Linq;
using System.Text;
using Efrpg;
using Efrpg.Filtering;
using Efrpg.Templates;
using Generator.Tests.Common;
using Generator.Tests.Unit.DocSamples;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     Generated code for MySQL, Oracle and PostgreSQL that compiled but could not build an EF model, or did not
    ///     compile at all. Each payload row below is shaped like the row the efrpg tool returned for the matching
    ///     column of the EfrpgTest databases, and each expected line is one the real provider accepted when the
    ///     generated context was built and every table read.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class NonSqlServerModelTests
    {
        private StaticStateSnapshot _snapshot;

        [OneTimeSetUp]
        public void CaptureStaticState()
        {
            _snapshot = StaticStateSnapshot.Capture();
            DocSampleRunner.Pristine = _snapshot;
        }

        [OneTimeTearDown]
        public void RestoreStaticState()
        {
            _snapshot.Restore();
        }

        private static string Column(string schema, string table, int ordinal, string name, string typeName, bool isNullable = true,
            int maxLength = 0, int precision = 0, bool primaryKey = false, bool isForeignKey = false, bool isStoreGenerated = false,
            string defaultValue = "", bool defaultIsExpression = false)
        {
            return string.Format(
                "<Row schemaName=\"{0}\" tableName=\"{1}\" isView=\"false\" isSynonym=\"false\" ordinal=\"{2}\" columnName=\"{3}\" typeName=\"{4}\" " +
                "isNullable=\"{5}\" maxLength=\"{6}\" precision=\"{7}\" scale=\"0\" dateTimePrecision=\"0\" isIdentity=\"false\" isComputed=\"false\" " +
                "isRowGuid=\"false\" generatedAlwaysType=\"0\" isStoreGenerated=\"{8}\" primaryKey=\"{9}\" primaryKeyOrdinal=\"{10}\" " +
                "isForeignKey=\"{11}\" synonymTriggerName=\"\" default=\"{12}\" defaultIsExpression=\"{13}\" />",
                schema, table, ordinal, name, typeName, Bool(isNullable), maxLength, precision, Bool(isStoreGenerated), Bool(primaryKey),
                primaryKey ? 1 : 0, Bool(isForeignKey), defaultValue, Bool(defaultIsExpression));
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Payload(string defaultSchema, IEnumerable<string> columns, string foreignKeys = "")
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<EfrpgResult schemaVersion=\"3\" toolVersion=\"1.2.0\" defaultSchema=\"{0}\" databaseDetails=\"\" ", defaultSchema);
            sb.Append("includeSchema=\"true\" canReadStoredProcedures=\"true\" hasIdentityColumnSupport=\"true\" doNotSpecifySizeForMaxLength=\"false\">");
            sb.Append("<Licence status=\"Valid\" file=\"\" registeredTo=\"\" company=\"\" licenceType=\"Commercial\" numLicences=\"1\" ");
            sb.Append("validUntil=\"31 DEC 9999\" />");
            sb.Append("<Tables>").Append(string.Concat(columns)).Append("</Tables>");
            sb.Append("<ForeignKeys>").Append(foreignKeys).Append("</ForeignKeys>");
            sb.Append("<Indexes /><ExtendedProperties /><Sequences /><Triggers /><MemoryOptimisedTables /><StoredProcedures /><Errors />");
            sb.Append("</EfrpgResult>");
            return sb.ToString();
        }

        private static string Line(string generated, string contains)
        {
            return generated.Split('\n').FirstOrDefault(x => x.Contains(contains)) ?? string.Empty;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Oracle
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void Generate_OracleForeignKeyWiderThanItsKey_TakesTheKeysType()
        {
            // Arrange - EFRPGTEST.EMPLOYEE: an unconstrained NUMBER key, referenced by a NUMBER(19) column. Oracle allows
            // it, and EF refuses a relationship whose two ends have different CLR types.
            var payload = Payload("EFRPGTEST", new[]
            {
                Column("EFRPGTEST", "EMPLOYEE", 1, "EMPLOYEE_ID", "number", isNullable: false, primaryKey: true),
                Column("EFRPGTEST", "EMPLOYEE", 2, "MANAGER_ID", "number(19)", precision: 19, isForeignKey: true)
            },
            "<Row constraintName=\"FK_EMPLOYEE_MANAGER\" parentName=\"\" childName=\"\" pkSchema=\"EFRPGTEST\" pkTableName=\"EMPLOYEE\" " +
            "pkColumn=\"EMPLOYEE_ID\" fkSchema=\"EFRPGTEST\" fkTableName=\"EMPLOYEE\" fkColumn=\"MANAGER_ID\" ordinal=\"1\" cascadeOnDelete=\"false\" " +
            "isNotEnforced=\"false\" hasUniqueConstraint=\"false\" />");

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, () => Settings.DatabaseType = DatabaseType.Oracle);

            // Assert
            Assert.That(result, Does.Contain("public decimal? ManagerId { get; set; }"));
        }

        [Test]
        public void Generate_OracleTimestampWithTimeZone_IsDateTimeOffset()
        {
            // Arrange - Oracle.EntityFrameworkCore maps TIMESTAMP WITH TIME ZONE to DateTimeOffset only.
            var payload = Payload("EFRPGTEST", new[]
            {
                Column("EFRPGTEST", "STAMPS", 1, "ID", "number(10)", isNullable: false, precision: 10, primaryKey: true),
                Column("EFRPGTEST", "STAMPS", 2, "TIMESTAMP_TZ", "timestamp with time zone")
            });

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, () => Settings.DatabaseType = DatabaseType.Oracle);

            // Assert
            Assert.That(result, Does.Contain("public DateTimeOffset? TimestampTz { get; set; }"));
        }

        [Test]
        public void Generate_EnumNamedAfterItsTable_SuffixesTheEntityRatherThanTheEnum()
        {
            // Arrange - an enum read from dbo.Category and named Category. The enum is named by the user; the entity's
            // name is derived, so it is the entity that gives way, as one table already does to another.
            Settings.Enumerations = null;

            // Act
            var result = DocSampleRunner.Generate(() =>
            {
                Settings.ElementsToGenerate = Elements.Poco | Elements.Context | Elements.Interface | Elements.PocoConfiguration;
                Settings.Enumerations = new List<EnumerationSettings>
                {
                    new EnumerationSettings { Name = "Category", Table = "dbo.Category", NameField = "CategoryName", ValueField = "CategoryId" }
                };
            });

            // Assert
            Assert.That(result, Does.Contain("public class Category1"));
            Assert.That(result, Does.Not.Contain("public class Category\n"));
        }

        // ---------------------------------------------------------------------------------------------------------
        // PostgreSQL
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void Generate_PostgreSqlSchemaWithSpaces_PrefixesAValidIdentifier()
        {
            // Arrange
            var payload = Payload("public", new[]
            {
                Column("Mixed Case Schema", "Spaced Table Name", 1, "Id", "integer", isNullable: false, precision: 32, primaryKey: true)
            });

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, () => Settings.DatabaseType = DatabaseType.PostgreSQL);

            // Assert
            Assert.That(result, Does.Contain("public class MixedCaseSchema_SpacedTableName"));
        }

        [Test]
        public void CleanSchemaName_SchemaWithSpacesAndHyphens_IsAValidIdentifierFragment()
        {
            // Act
            var result = NamingHelper.CleanSchemaName("Mixed Case-Schema");

            // Assert
            Assert.That(result, Is.EqualTo("MixedCase_Schema"));
        }

        [Test]
        public void Generate_PostgreSqlCidr_IsNpgsqlCidr()
        {
            // Arrange - Npgsql 8 maps cidr to NpgsqlCidr; NpgsqlInet is for inet, and EF refuses it on a cidr column.
            var payload = Payload("public", new[]
            {
                Column("public", "networks", 1, "id", "integer", isNullable: false, precision: 32, primaryKey: true),
                Column("public", "networks", 2, "cidr", "cidr")
            });

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, () => Settings.DatabaseType = DatabaseType.PostgreSQL);

            // Assert
            Assert.That(result, Does.Contain("public NpgsqlCidr? Cidr { get; set; }"));
        }

        [Test]
        public void Generate_PostgreSqlMacaddr_ImportsTheNamespaceOfPhysicalAddress()
        {
            // Arrange
            var payload = Payload("public", new[]
            {
                Column("public", "devices", 1, "id", "integer", isNullable: false, precision: 32, primaryKey: true),
                Column("public", "devices", 2, "macaddr", "macaddr")
            });

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, () => Settings.DatabaseType = DatabaseType.PostgreSQL);

            // Assert
            Assert.That(result, Does.Contain("using System.Net.NetworkInformation;"));
        }

        // ---------------------------------------------------------------------------------------------------------
        // MySQL
        // ---------------------------------------------------------------------------------------------------------

        private static string MySqlDefaultsAndGenerated()
        {
            return Payload("EfrpgTest", new[]
            {
                Column("EfrpgTest", "DefaultsAndGenerated", 1, "Id", "int", isNullable: false, precision: 10, primaryKey: true, isStoreGenerated: true),
                Column("EfrpgTest", "DefaultsAndGenerated", 2, "ExternalRef", "char", isNullable: false, maxLength: 36, defaultValue: "uuid()",
                    defaultIsExpression: true),
                Column("EfrpgTest", "DefaultsAndGenerated", 3, "ModifiedAt", "timestamp", isNullable: false, isStoreGenerated: true,
                    defaultValue: "CURRENT_TIMESTAMP", defaultIsExpression: true)
            });
        }

        [Test]
        public void Generate_MySqlContext_PassesPomeloAServerVersion()
        {
            // Act
            var result = DocSampleRunner.GenerateFromPayload(MySqlDefaultsAndGenerated(), () => Settings.DatabaseType = DatabaseType.MySql);

            // Assert - UseMySql has no overload without one.
            Assert.That(Line(result, "optionsBuilder.UseMySql("), Does.Contain(", ServerVersion.AutoDetect(@\""));
        }

        [Test]
        public void Generate_MySqlChar36String_LeavesTheStoreTypeToPomelo()
        {
            // Act
            var result = DocSampleRunner.GenerateFromPayload(MySqlDefaultsAndGenerated(), () => Settings.DatabaseType = DatabaseType.MySql);

            // Assert - char(36) is Pomelo's default Guid store type, so naming it on a string property finds no
            // mapping and the model throws; IsFixedLength with HasMaxLength(36) produces the same column.
            var line = Line(result, "x.ExternalRef).HasColumnName");
            Assert.That(line, Does.Not.Contain("HasColumnType(\"char(36)\")"));
            Assert.That(line, Does.Contain(".IsFixedLength()"));
            Assert.That(line, Does.Contain(".HasMaxLength(36)"));
        }

        [Test]
        public void Generate_MySqlOnUpdateTimestamp_IsNotARowVersion()
        {
            // Act
            var result = DocSampleRunner.GenerateFromPayload(MySqlDefaultsAndGenerated(), () => Settings.DatabaseType = DatabaseType.MySql);

            // Assert - only SQL Server's timestamp is a rowversion; in MySQL it is a date and time.
            var line = Line(result, "x.ModifiedAt).HasColumnName");
            Assert.That(line, Does.Not.Contain("IsRowVersion"));
            Assert.That(line, Does.Not.Contain("IsFixedLength"));
        }

        [Test]
        public void Generate_SqlServerTimestamp_IsStillARowVersion()
        {
            // Arrange
            var payload = Payload("dbo", new[]
            {
                Column("dbo", "Versioned", 1, "Id", "int", isNullable: false, precision: 10, primaryKey: true),
                Column("dbo", "Versioned", 2, "RowVer", "timestamp", isNullable: false, isStoreGenerated: true)
            });

            // Act
            var result = DocSampleRunner.GenerateFromPayload(payload, null);

            // Assert
            Assert.That(Line(result, "x.RowVer).HasColumnName"), Does.Contain("IsRowVersion"));
        }

        // ---------------------------------------------------------------------------------------------------------
        // EF6 on PostgreSQL
        // ---------------------------------------------------------------------------------------------------------

        private static string PostgreSqlUnmappableForEf6()
        {
            return Payload("public", new[]
            {
                Column("public", "oddities", 1, "id", "integer", isNullable: false, precision: 32, primaryKey: true),
                Column("public", "oddities", 2, "bit_8", "bit", maxLength: 8),
                Column("public", "oddities", 3, "int_array", "int4[]")
            });
        }

        [Test]
        public void Generate_Ef6PostgreSqlColumnEf6CannotMap_IsLeftOut()
        {
            // Act - EF6 maps only primitive types: Property(x => x.Bit8) does not compile for a BitArray, and BitArray?
            // is not C# 7.3.
            var result = DocSampleRunner.GenerateFromPayload(PostgreSqlUnmappableForEf6(), () =>
            {
                Settings.DatabaseType = DatabaseType.PostgreSQL;
                Settings.TemplateType = TemplateType.Ef6;
            });

            // Assert
            Assert.That(result, Does.Contain("public int Id { get; set; }"));
            Assert.That(result, Does.Not.Contain("Bit8"));
            Assert.That(result, Does.Not.Contain("IntArray"));
        }

        [Test]
        public void Generate_EfCorePostgreSqlBitColumn_IsKept()
        {
            // Act
            var result = DocSampleRunner.GenerateFromPayload(PostgreSqlUnmappableForEf6(), () => Settings.DatabaseType = DatabaseType.PostgreSQL);

            // Assert - only EF6 lacks the mapping; Npgsql's EF Core provider reads BitArray and arrays.
            Assert.That(result, Does.Contain("Bit8"));
            Assert.That(result, Does.Contain("IntArray"));
        }
    }
}
