using System;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Templates;
using Generator.Tests.Common;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System.IO;

namespace Generator.Tests.Integration
{
    [TestFixture]
    [NonParallelizable]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SQLite)]
    public class SingleDatabaseTestSQLite : SingleDatabaseTestBase
    {
        private static readonly string Filename = Path.Combine(Path.GetTempPath(), "Efrpg.db");
        private static readonly string ConnectionString = "Data Source=" + Filename;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (File.Exists(Filename))
                File.Delete(Filename);

            var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText =
                @"
PRAGMA foreign_keys = ON;

CREATE TABLE Efrpg
(
    Id     INTEGER,

    text1  TEXT,
    text2  CHARACTER(22),
    text3  VARCHAR(33),
    text4  VARYING CHARACTER(44),
    text5  NCHAR(55),
    text6  NATIVE CHARACTER(66),
    text7  NVARCHAR(77),
    text8  TEXT(88) DEFAULT 'Hello',
    text9  CLOB,

    int1   INTEGER  DEFAULT 123,
    int2   INT UNIQUE,
    int3   SMALLINT,
    int4   MEDIUMINT,
    int5   BIGINT,
    int6   UNSIGNED BIG INT,
    int7   INT2,
    INT8   INT8,

    blob1  BLOB,
    blob2  BLOB(22),

    real1  REAL CHECK (real1 > 123),
    real2  DOUBLE,
    real3  DOUBLE PRECISION,
    real4  FLOAT,

    num1   NUMERIC,
    num2   DECIMAL(10, 5),
    num22  DECIMAL (10, 5),
    num222 DECIMAL  (10,   5),
    num3   BOOLEAN,

    date1   DATE,
    date2   DATETIME,

    CONSTRAINT [PK_Efrpg] PRIMARY KEY (Id)
);

CREATE TABLE EfrpgItems
(
    Id INTEGER PRIMARY KEY,
    EfrpgId INTEGER NOT NULL,
    ParentEfrpgId INTEGER NULL,
    Test INT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (EfrpgId) REFERENCES [Efrpg] (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
    FOREIGN KEY (ParentEfrpgId) REFERENCES [Efrpg] (Id) ON DELETE CASCADE ON UPDATE NO ACTION
);

-- Defaults that are expressions rather than literals. dflt_value is the text as written, so CURRENT_TIMESTAMP
-- and the function calls arrive unquoted while the literals keep their quotes. SQLite has no column types, so a
-- TEXT column defaulting to CURRENT_TIMESTAMP is a string property: it must get HasDefaultValueSql, not the
-- string ""CURRENT_TIMESTAMP"". EfrpgItems.CreatedAt above is the DATETIME version of the same default.
CREATE TABLE ExpressionDefault
(
    Id          INTEGER PRIMARY KEY,
    ChangedAt   TEXT     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Token       TEXT     NOT NULL DEFAULT (lower(hex(randomblob(16)))),
    DueAt       DATETIME DEFAULT (datetime('now', '+30 days')),
    LiteralText TEXT     NOT NULL DEFAULT 'fallback',
    NumberText  TEXT     NOT NULL DEFAULT 0
);

-- String defaults that need escaping in C# (#281, #893). A backslash makes the default a verbatim literal, where a
-- double quote is escaped by doubling it, and the generator left those quotes single, so a default with both did
-- not compile. The same table is in every dialect's EfrpgTest; the doubled quotes here are C#, not SQL.
CREATE TABLE StringDefaultEscaping
(
    Id                 INTEGER PRIMARY KEY,
    QuoteOnly          TEXT NOT NULL DEFAULT 'say ""hi""',
    BackslashOnly      TEXT NOT NULL DEFAULT 'C:\Temp',
    BackslashAndQuote  TEXT NOT NULL DEFAULT 'C:\Temp\say ""hi""',
    QuoteThenBackslash TEXT NOT NULL DEFAULT '""quoted""\'
);

CREATE INDEX [IX_Efrpg] ON [Efrpg] ([TEXT3]);
CREATE INDEX [IX_Efrpg_Composite] ON [Efrpg] (int1, int2);

CREATE VIEW ThisIsAView AS
SELECT Id, text1, int1, blob1, real1, num1
FROM Efrpg;

CREATE TRIGGER efrpg_trigger
    AFTER INSERT
    ON Efrpg
BEGIN
    SELECT 'Test';
END;

INSERT INTO Efrpg (Id, int1, int2)
VALUES (1, 2, 3),
       (2, 3, 4);
INSERT INTO EfrpgItems (Id, EfrpgId, Test)
VALUES (1, 1, 3),
       (2, 2, 4);
";
            cmd.ExecuteNonQuery();
            connection.Close();

            TestContext.Out.WriteLine($"Database: {Filename}");
            TestContext.Out.WriteLine($"ConnectionString: {ConnectionString}");
        }

        [Test]
        public void ReverseEngineer()
        {
            // Arrange
            Settings.GenerateSeparateFiles = false;
            Settings.UseMappingTables = false;
            Settings.ConnectionString = ConnectionString;
            Settings.DatabaseType = DatabaseType.SQLite;
            SetupDatabase("MyDbContext", "MyDbContext", TemplateType.EfCore8);

            // Act
            Run("EfrpgTest", ".SQLite", null);

            // Assert
            CompareAgainstTestComparison("EfrpgTest");
        }
    }
}