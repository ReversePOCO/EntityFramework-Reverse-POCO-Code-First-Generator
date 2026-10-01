using Efrpg;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     PostgreSQL reports a column default with its type cast attached - <c>'Hello world'::character varying</c> -
    ///     which has to come off before the value is turned into C#, or it ends up inside the generated string literal.
    ///     T-SQL uses <c>::</c> for something else entirely (a static method call, as in <c>hierarchyid::GetRoot()</c>),
    ///     so the strip is PostgreSQL only and these tests pin both halves of that.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class ColumnDefaultTests
    {
        private DatabaseType _originalDatabaseType;
        private Efrpg.Templates.TemplateType _originalTemplateType;

        [SetUp]
        public void SetUp()
        {
            // Settings is static, so anything set here leaks into whichever test runs next unless it is put back.
            _originalDatabaseType = Settings.DatabaseType;
            _originalTemplateType = Settings.TemplateType;
        }

        [TearDown]
        public void TearDown()
        {
            Settings.DatabaseType = _originalDatabaseType;
            Settings.TemplateType = _originalTemplateType;
        }

        private static Column CleanUp(DatabaseType databaseType, string propertyType, string defaultValue, bool? defaultIsExpression = null)
        {
            Settings.DatabaseType = databaseType;

            var column = new Column
            {
                PropertyType        = propertyType,
                Default             = defaultValue,
                DefaultIsExpression = defaultIsExpression
            };

            column.CleanUpDefault();
            return column;
        }

        [Test]
        public void CleanUpDefault_PostgreSqlStringWithCast_StripsTheCast()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'Hello world'::character varying");

            Assert.That(column.Default, Is.EqualTo("\"Hello world\""));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlNullWithCast_ClearsTheDefault()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "NULL::character varying");

            Assert.That(column.Default, Is.Empty);
        }

        [Test]
        public void CleanUpDefault_PostgreSqlQuotedNullWithCast_KeepsTheLiteralString()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'NULL'::character varying");

            Assert.That(column.Default, Is.EqualTo("\"NULL\""));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlArrayWithCast_StripsTheCastAndItsBrackets()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'{}'::text[]");

            Assert.That(column.Default, Is.EqualTo("\"{}\""));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlCastOnAQuotedTypeName_StripsTheCast()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'sad'::public.\"Mood\"");

            Assert.That(column.Default, Is.EqualTo("\"sad\""));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlDoubleColonInsideTheValue_IsNotTreatedAsACast()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'a::b'::text");

            Assert.That(column.Default, Is.EqualTo("\"a::b\""));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlDefaultSql_KeepsTheCast()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "string", "'Hello world'::character varying");

            Assert.That(column.DefaultSql, Is.EqualTo("'Hello world'::character varying"));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlNullWithCastOnAUnicodeColumn_DoesNotLoseItsLeadingN()
        {
            // IsUnicode is true for anything not spelled char/varchar/text, so PostgreSQL's
            // "character varying" qualifies. The N-prefix strip must not see NULL::character varying.
            Settings.DatabaseType = DatabaseType.PostgreSQL;
            var column = new Column { PropertyType = "string", IsUnicode = true, Default = "NULL::character varying" };

            column.CleanUpDefault();

            Assert.That(column.Default, Is.Empty);
        }

        [Test]
        public void CleanUpDefault_PostgreSqlGenRandomUuid_BecomesGuidNewGuid()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "Guid", "gen_random_uuid()");

            Assert.That(column.Default, Is.EqualTo("Guid.NewGuid()"));
        }

        [Test]
        public void CleanUpDefault_PostgreSqlUuidGenerateV4_BecomesGuidNewGuid()
        {
            var column = CleanUp(DatabaseType.PostgreSQL, "Guid", "uuid_generate_v4()");

            Assert.That(column.Default, Is.EqualTo("Guid.NewGuid()"));
        }

        [Test]
        public void CleanUpDefault_SqlServerStaticMethodCall_KeepsTheDoubleColon()
        {
            var column = CleanUp(DatabaseType.SqlServer, "string", "hierarchyid::GetRoot()");

            Assert.That(column.HasDefaultValueSql, Is.EqualTo("hierarchyid::GetRoot()"));
        }

        // A default that is SQL rather than a literal cannot be written as C#: turned into a string it would store the
        // function's name, and dropped it would store the CLR default. It has to reach EF Core as HasDefaultValueSql,
        // so that EF leaves the column out of the INSERT and the database runs the expression.

        [TestCase(DatabaseType.SqlServer,  "string",   "(suser_sname())",                            "suser_sname()")]
        [TestCase(DatabaseType.SqlServer,  "string",   "(app_name())",                               "app_name()")]
        [TestCase(DatabaseType.SqlServer,  "string",   "(user_name())",                              "user_name()")]
        [TestCase(DatabaseType.SqlServer,  "string",   "(CONVERT([varchar](30),getdate(),(126)))",   "CONVERT([varchar](30),getdate(),(126))")]
        [TestCase(DatabaseType.SqlServer,  "DateTime", "(dateadd(day,(30),sysutcdatetime()))",       "dateadd(day,(30),sysutcdatetime())")]
        [TestCase(DatabaseType.SqlServer,  "int",      "((1)+(2))",                                  "(1)+(2)")]
        [TestCase(DatabaseType.PostgreSQL, "string",   "CURRENT_USER",                               "CURRENT_USER")]
        [TestCase(DatabaseType.PostgreSQL, "string",   "current_setting('application_name'::text)",  "current_setting('application_name'::text)")]
        [TestCase(DatabaseType.PostgreSQL, "string",   "to_char(now(), 'YYYY-MM-DD'::text)",         "to_char(now(), 'YYYY-MM-DD'::text)")]
        [TestCase(DatabaseType.PostgreSQL, "DateTime", "now()",                                      "now()")]
        [TestCase(DatabaseType.PostgreSQL, "DateTime", "CURRENT_DATE",                               "CURRENT_DATE")]
        [TestCase(DatabaseType.PostgreSQL, "DateTime", "(now() + '30 days'::interval)",              "now() + '30 days'::interval")]
        [TestCase(DatabaseType.PostgreSQL, "int[]",    "'{{1,2},{3,4}}'::integer[]",                 "'{{1,2},{3,4}}'::integer[]")]
        [TestCase(DatabaseType.PostgreSQL, "int[]",    "'{NULL,1}'::integer[]",                      "'{NULL,1}'::integer[]")]
        [TestCase(DatabaseType.PostgreSQL, "string[]", "ARRAY['a'::text, 'b'::text]",                "ARRAY['a'::text, 'b'::text]")]
        [TestCase(DatabaseType.Oracle,     "string",   "USER",                                       "USER")]
        [TestCase(DatabaseType.Oracle,     "string",   "SYS_GUID()",                                 "SYS_GUID()")]
        [TestCase(DatabaseType.Oracle,     "string",   "SYS_CONTEXT('USERENV', 'OS_USER')",          "SYS_CONTEXT('USERENV', 'OS_USER')")]
        [TestCase(DatabaseType.Oracle,     "DateTime", "SYSDATE",                                    "SYSDATE")]
        [TestCase(DatabaseType.Oracle,     "DateTime", "SYSTIMESTAMP",                               "SYSTIMESTAMP")]
        [TestCase(DatabaseType.Oracle,     "DateTime", "SYSDATE + 30 \n",                            "SYSDATE + 30")]
        [TestCase(DatabaseType.SQLite,     "string",   "CURRENT_TIMESTAMP",                          "CURRENT_TIMESTAMP")]
        [TestCase(DatabaseType.SQLite,     "string",   "lower(hex(randomblob(16)))",                 "lower(hex(randomblob(16)))")]
        [TestCase(DatabaseType.SQLite,     "DateTime", "CURRENT_TIMESTAMP",                          "CURRENT_TIMESTAMP")]
        [TestCase(DatabaseType.SQLite,     "DateTime", "datetime('now', '+30 days')",                "datetime('now', '+30 days')")]
        public void CleanUpDefault_ExpressionDefault_BecomesHasDefaultValueSqlWithNoCSharpDefault(DatabaseType databaseType, string propertyType, string reported, string expectedSql)
        {
            var column = CleanUp(databaseType, propertyType, reported);

            Assert.Multiple(() =>
            {
                Assert.That(column.Default, Is.Empty);
                Assert.That(column.HasDefaultValueSql, Is.EqualTo(expectedSql));
            });
        }

        [TestCase(DatabaseType.SqlServer,  "string", "(N'fallback')",                  "\"fallback\"")]
        [TestCase(DatabaseType.SqlServer,  "string", "((0))",                          "\"0\"")]
        [TestCase(DatabaseType.SqlServer,  "string", "('it''s')",                      "\"it's\"")]
        [TestCase(DatabaseType.PostgreSQL, "string", "'fallback'::character varying",  "\"fallback\"")]
        [TestCase(DatabaseType.PostgreSQL, "string", "0",                              "\"0\"")]
        [TestCase(DatabaseType.Oracle,     "string", "'fallback'",                     "\"fallback\"")]
        [TestCase(DatabaseType.Oracle,     "string", "'fallback' \n",                  "\"fallback\"")]
        [TestCase(DatabaseType.Oracle,     "string", "0",                              "\"0\"")]
        [TestCase(DatabaseType.SQLite,     "string", "'fallback'",                     "\"fallback\"")]
        [TestCase(DatabaseType.SQLite,     "int",    "123",                            "123")]
        public void CleanUpDefault_LiteralDefault_StaysACSharpLiteral(DatabaseType databaseType, string propertyType, string reported, string expectedDefault)
        {
            var column = CleanUp(databaseType, propertyType, reported);

            Assert.Multiple(() =>
            {
                Assert.That(column.Default, Is.EqualTo(expectedDefault));
                Assert.That(column.HasDefaultValueSql, Is.Null);
            });
        }

        // A backslash or line break makes the default a verbatim literal, where a quote is escaped by doubling it
        // rather than with a backslash. Issue #893: the quotes were left single, so the initialiser did not compile.

        [TestCase("('C:\\Temp\\say \"hi\"')",  "@\"C:\\Temp\\say \"\"hi\"\"\"")]
        [TestCase("('C:\\Temp')",               "@\"C:\\Temp\"")]
        [TestCase("('line one\nline \"two\"')", "@\"line one\nline \"\"two\"\"\"")]
        [TestCase("('say \"hi\"')",             "\"say \\\"hi\\\"\"")]
        public void CleanUpDefault_StringWithQuotes_EscapesThemForTheLiteralItBecomes(string reported, string expectedDefault)
        {
            var column = CleanUp(DatabaseType.SqlServer, "string", reported);

            Assert.That(column.Default, Is.EqualTo(expectedDefault));
        }

        // SQL Server functions with an exact C# equivalent keep it, as before. The equivalent has to be the whole
        // default: dateadd(day,30,sysutcdatetime()) merely contains one, and is covered above.

        [TestCase("DateTime", "(sysutcdatetime())", "DateTime.UtcNow")]
        [TestCase("DateTime", "(getdate())",        "DateTime.Now")]
        [TestCase("string",   "(newid())",          "Guid.NewGuid().ToString()")]
        [TestCase("string",   "(space((0)))",       "\"\"")]
        [TestCase("Guid",     "(newsequentialid())", "Guid.NewGuid()")]
        public void CleanUpDefault_SqlServerFunctionWithACSharpEquivalent_KeepsTheEquivalent(string propertyType, string reported, string expectedDefault)
        {
            var column = CleanUp(DatabaseType.SqlServer, propertyType, reported);

            Assert.That(column.Default, Is.EqualTo(expectedDefault));
        }

        // EF Core will not save a null into a NOT NULL array even with HasDefaultValueSql, so an array literal the
        // generator can read is written out in C#. Only the forms it cannot read safely are left to the database.

        // EF6 has no HasDefaultValueSql, so it cannot leave a date to the database. An approximate C# time is better
        // there than DateTime.MinValue, and that is what EF6 always generated for a default containing one.

        [TestCase("(dateadd(day,(30),sysutcdatetime()))", "DateTime.UtcNow")]
        [TestCase("(dateadd(day,(30),getdate()))",        "DateTime.Now")]
        public void CleanUpDefault_Ef6DateExpressionContainingTheCurrentTime_KeepsTheApproximateCSharpTime(string reported, string expectedDefault)
        {
            Settings.TemplateType = Efrpg.Templates.TemplateType.Ef6;

            var column = CleanUp(DatabaseType.SqlServer, "DateTime", reported);

            Assert.That(column.Default, Is.EqualTo(expectedDefault));
        }

        [Test]
        public void CleanUpDefault_Ef6StringExpression_HasNoCSharpDefault()
        {
            Settings.TemplateType = Efrpg.Templates.TemplateType.Ef6;

            var column = CleanUp(DatabaseType.SqlServer, "string", "(suser_sname())");

            Assert.That(column.Default, Is.Empty);
        }

        [TestCase("string[]", "'{}'::text[]",                  "Array.Empty<string>()")]
        [TestCase("int[]",    "'{}'::integer[]",               "Array.Empty<int>()")]
        [TestCase("int[]",    "'{1,2}'::integer[]",            "new int[] { 1, 2 }")]
        [TestCase("decimal[]", "'{1.5,2}'::numeric[]",         "new decimal[] { 1.5m, 2m }")]
        [TestCase("string[]", "'{a,\"b c\"}'::text[]",         "new string[] { \"a\", \"b c\" }")]
        [TestCase("bool[]",   "'{t,false}'::boolean[]",        "new bool[] { true, false }")]
        public void CleanUpDefault_PostgreSqlArrayLiteral_BecomesACSharpArray(string propertyType, string reported, string expectedDefault)
        {
            var column = CleanUp(DatabaseType.PostgreSQL, propertyType, reported);

            Assert.That(column.Default, Is.EqualTo(expectedDefault));
        }

        // MySQL's COLUMN_DEFAULT does not quote string literals, so 'fallback' and (UUID()) arrive as the bare text
        // fallback and uuid(). Only the catalogue can tell them apart, and efrpg 1.2.0+ says which it is.

        [Test]
        public void CleanUpDefault_MySqlDefaultTheToolMarksAsAnExpression_BecomesHasDefaultValueSql()
        {
            var column = CleanUp(DatabaseType.MySql, "string", "uuid()", defaultIsExpression: true);

            Assert.That(column.HasDefaultValueSql, Is.EqualTo("uuid()"));
        }

        [Test]
        public void CleanUpDefault_MySqlLiteralThatLooksLikeAFunction_StaysAStringLiteral()
        {
            var column = CleanUp(DatabaseType.MySql, "string", "uuid()", defaultIsExpression: false);

            Assert.That(column.Default, Is.EqualTo("\"uuid()\""));
        }

        [Test]
        public void CleanUpDefault_MySqlUnquotedLiteral_StaysAStringLiteral()
        {
            var column = CleanUp(DatabaseType.MySql, "string", "fallback", defaultIsExpression: false);

            Assert.That(column.Default, Is.EqualTo("\"fallback\""));
        }

        // MySQL reports a literal's value without its quotes, so a value that itself starts with a double quote must
        // still be wrapped: there the quote is data, not the mark of a literal already converted to C#. Found by
        // EfrpgTest's StringDefaultEscaping.QuoteThenBackslash, which came out as @"quoted""" - the value quoted".

        [TestCase("\"quoted\"\\", "@\"\"\"quoted\"\"\\\"")]
        [TestCase("\"quoted\"",   "\"\\\"quoted\\\"\"")]
        public void CleanUpDefault_MySqlLiteralStartingWithADoubleQuote_KeepsTheQuote(string reported, string expectedDefault)
        {
            var column = CleanUp(DatabaseType.MySql, "string", reported, defaultIsExpression: false);

            Assert.That(column.Default, Is.EqualTo(expectedDefault));
        }

        // A tool older than 1.2.0 sends no flag, so the text is all there is: a function call or a date keyword is
        // taken as an expression, anything else as the unquoted literal MySQL reports.

        [TestCase("string",   "uuid()",            true)]
        [TestCase("string",   "current_user()",    true)]
        [TestCase("DateTime", "CURRENT_TIMESTAMP", true)]
        [TestCase("string",   "Hello world",       false)]
        [TestCase("int",      "1",                 false)]
        public void CleanUpDefault_MySqlWithoutTheToolsFlag_ClassifiesByTheText(string propertyType, string reported, bool expectExpression)
        {
            var column = CleanUp(DatabaseType.MySql, propertyType, reported, defaultIsExpression: null);

            Assert.That(column.HasDefaultValueSql != null, Is.EqualTo(expectExpression));
        }
    }
}
