using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     Reading a .tt into the dialog and writing the dialog back out. Both directions are tested against the
    ///     real shipped Database.tt, because that is the file every user starts from.
    /// </summary>
    [TestFixture]
    public class TemplateConfigurationTests
    {
        private static TemplateSettingsFile Shipped()
        {
            return new TemplateSettingsFile(RepositoryFiles.DatabaseTemplate());
        }

        [Test]
        public void ReadFrom_TheShippedTemplateGivesItsOwnDefaults()
        {
            var configuration = TemplateConfiguration.ReadFrom(Shipped(), "Fallback");

            Assert.That(configuration.Database.Name, Is.EqualTo("SqlServer"));
            Assert.That(configuration.Template.Name, Is.EqualTo("EfCore10"));
            Assert.That(configuration.ConnectionString, Is.EqualTo(DatabaseTarget.Default.ConnectionString));
            Assert.That(configuration.DbContextName, Is.EqualTo("MyDbContext"));
        }

        /// <summary>
        ///     The whole point of reading first: a user reopening the dialog to change one field must not have the
        ///     rest of their template quietly reset to the defaults. Run over the shipped template and over the
        ///     one whose connection string is code.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ReadingAndWritingBackWithoutChangingAnythingLeavesTheFileByteForByteIdentical(bool shipped)
        {
            var original = shipped ? RepositoryFiles.DatabaseTemplate() : RepositoryFiles.AzureTemplate();

            var text = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(original), "Fallback")
                .ApplyTo(new TemplateSettingsFile(original));

            Assert.That(text, Is.EqualTo(original));
        }

        /// <summary>
        ///     The bug this exists for. Reading an expression as "no literal" and falling back to the default
        ///     showed a configured template as unconfigured, and OK then silently wrote nothing for it.
        /// </summary>
        [Test]
        public void ReadFrom_AConnectionStringSetInCodeIsShownAsItselfAndIsNotEditable()
        {
            var configuration = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(RepositoryFiles.AzureTemplate()), "Fallback");

            Assert.That(configuration.IsConnectionStringEditable, Is.False);
            Assert.That(configuration.Source.Kind, Is.EqualTo(ConnectionStringKind.EnvironmentVariable));
            Assert.That(configuration.ConnectionString, Is.EqualTo("Environment.GetEnvironmentVariable(\"ReversePoco\", EnvironmentVariableTarget.User)"));
            Assert.That(configuration.DbContextName, Is.EqualTo("AzureContext"));
        }

        [Test]
        public void ApplyTo_NeverReplacesAConnectionStringSetInCode()
        {
            var azure    = RepositoryFiles.AzureTemplate();
            var settings = new TemplateSettingsFile(azure);
            var source   = ConnectionStringSource.Read(settings);

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Typed", "Renamed", string.Empty, source).ApplyTo(settings);

            Assert.That(settings.Text, Does.Contain("Settings.ConnectionString        = Environment.GetEnvironmentVariable(\"ReversePoco\", EnvironmentVariableTarget.User);"));
            Assert.That(settings.Text, Does.Not.Contain("Initial Catalog=Typed"));
            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("Renamed"));
        }

        private const string DifferentNames =
            "<#\r\n" +
            "    Settings.ConnectionString = Environment.GetEnvironmentVariable(\"ReversePoco\", EnvironmentVariableTarget.User);\r\n" +
            "    Settings.ConnectionStringName = \"MyDbContext\"; // key in appsettings.json\r\n" +
            "    Settings.DbContextName = \"V10ReversePocoTestDbContext\"; // Class name\r\n" +
            "#>\r\n";

        /// <summary>
        ///     The bug: OK with nothing changed rewrote ConnectionStringName to the context name, because every
        ///     fixture happened to keep the two equal.
        /// </summary>
        [Test]
        public void ApplyTo_LeavesAConnectionStringNameThatDiffersFromTheContextNameAlone()
        {
            var settings = new TemplateSettingsFile(DifferentNames);

            var text = TemplateConfiguration.ReadFrom(settings, "Fallback").ApplyTo(settings);

            Assert.That(text, Is.EqualTo(DifferentNames));
        }

        [Test]
        public void ReadFrom_ReadsTheConnectionStringNameAsItsOwnField()
        {
            var current = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(DifferentNames), "Fallback");

            Assert.That(current.DbContextName, Is.EqualTo("V10ReversePocoTestDbContext"));
            Assert.That(current.ConnectionStringName, Is.EqualTo("MyDbContext"));
        }

        /// <summary>What the dialog does: the name box holds the file's value, and only the context is renamed.</summary>
        [Test]
        public void ApplyTo_RenamingTheContextDoesNotDragADifferentConnectionStringNameAlong()
        {
            var settings = new TemplateSettingsFile(DifferentNames);
            var current  = TemplateConfiguration.ReadFrom(settings, "Fallback");

            new TemplateConfiguration(current.Database, current.Template, current.ConnectionString, "Renamed",
                current.ConnectionStringName, string.Empty, current.Source).ApplyTo(settings);

            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("Renamed"));
            Assert.That(settings.GetString("ConnectionStringName"), Is.EqualTo("MyDbContext"));
        }

        [Test]
        public void ApplyTo_WritesAConnectionStringNameOfItsOwn()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "NorthwindDbContext", "Northwind", string.Empty, null).ApplyTo(settings);

            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("NorthwindDbContext"));
            Assert.That(settings.GetString("ConnectionStringName"), Is.EqualTo("Northwind"));
        }

        /// <summary>
        ///     The shorter constructor gives the connection string the context's name, which is what a new template
        ///     gets and what the dialog's name box does while the two boxes still match.
        /// </summary>
        [Test]
        public void ApplyTo_TheShorterConstructorKeepsTheConnectionStringNameInStep()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "NorthwindDbContext", string.Empty).ApplyTo(settings);

            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("NorthwindDbContext"));
            Assert.That(settings.GetString("ConnectionStringName"), Is.EqualTo("NorthwindDbContext"));
        }

        [Test]
        public void ResolveConnectionString_RefusesThePlaceholderWithAPointerToTheConnectionDialog()
        {
            string error;
            var value = TemplateConfiguration.ReadFrom(Shipped(), "Fallback").ResolveConnectionString(out error);

            Assert.That(value, Is.Null);
            Assert.That(error, Does.Contain(TemplateSettingsFile.Placeholder).And.Contain("Reverse POCO: Connection..."));
        }

        [Test]
        public void ResolveConnectionString_ReturnsALiteralAsIs()
        {
            string error;
            var value = new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                " Data Source=(local);Initial Catalog=Northwind ", "X", string.Empty).ResolveConnectionString(out error);

            Assert.That(error, Is.Null);
            Assert.That(value, Is.EqualTo("Data Source=(local);Initial Catalog=Northwind"));
        }

        [Test]
        public void ResolveConnectionString_ResolvesTheAzureTemplateThroughItsVariable()
        {
            var configuration = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(RepositoryFiles.AzureTemplate()), "Fallback");

            string error;
            var value = configuration.ResolveConnectionString(out error);

            // Whether the variable is set depends on the machine; either answer must be the honest one.
            if (value == null)
                Assert.That(error, Does.Contain("ReversePoco").And.Contain("not set"));
            else
                Assert.That(error, Is.Null);
        }

        [Test]
        public void ReadFrom_AnUnrecognisedEnumMemberFallsBackToTheDefault()
        {
            var settings = new TemplateSettingsFile(
                "    Settings.DatabaseType = DatabaseType.Informix;\r\n" +
                "    Settings.TemplateType = TemplateType.EfCore4;\r\n");

            var configuration = TemplateConfiguration.ReadFrom(settings, "Fallback");

            Assert.That(configuration.Database.Name, Is.EqualTo(DatabaseTarget.Default.Name));
            Assert.That(configuration.Template.Name, Is.EqualTo(TemplateTarget.Default.Name));
        }

        [Test]
        public void ReadFrom_AMissingDbContextNameUsesTheFallback()
        {
            var configuration = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(string.Empty), "NorthwindDbContext");

            Assert.That(configuration.DbContextName, Is.EqualTo("NorthwindDbContext"));
        }

        [Test]
        public void ApplyTo_WritesEveryAnswer()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Find("Oracle"), TemplateTarget.Find("Ef6"),
                "Data Source=localhost:1521/pdb1;User Id=hr;Password=secret;", "HrDbContext", "Hr.Data").ApplyTo(settings);

            Assert.That(settings.GetEnum("DatabaseType"), Is.EqualTo("Oracle"));
            Assert.That(settings.GetEnum("TemplateType"), Is.EqualTo("Ef6"));
            Assert.That(settings.GetString("ConnectionString"), Is.EqualTo("Data Source=localhost:1521/pdb1;User Id=hr;Password=secret;"));
            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("HrDbContext"));
            Assert.That(settings.GetString("ConnectionStringName"), Is.EqualTo("HrDbContext"));
            Assert.That(settings.GetString("Namespace"), Is.EqualTo("Hr.Data"));
        }

        /// <summary>
        ///     Settings.Namespace ships as the bare identifier DefaultNamespace, so writing a namespace has to
        ///     replace the whole right-hand side rather than the contents of a literal that is not there.
        /// </summary>
        [Test]
        public void ApplyTo_AnEmptyNamespaceRestoresDefaultNamespace()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "MyDbContext", "Accounts.Billing").ApplyTo(settings);
            Assert.That(settings.GetString("Namespace"), Is.EqualTo("Accounts.Billing"));

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "MyDbContext", string.Empty).ApplyTo(settings);

            Assert.That(settings.GetExpression("Namespace"),
                Is.EqualTo(TemplateConfiguration.DefaultNamespaceExpression));
        }

        [Test]
        public void ReadFrom_TheShippedTemplateReportsNoNamespaceBecauseItUsesDefaultNamespace()
        {
            Assert.That(TemplateConfiguration.ReadFrom(Shipped(), "Fallback").Namespace, Is.Empty);
        }

        private const string ComputedNamespace =
            "<#\r\n" +
            "    Settings.Namespace = DefaultNamespace + \".Domain\"; // Override the default namespace here\r\n" +
            "    Settings.DbContextName = \"MyDbContext\";\r\n" +
            "#>\r\n";

        /// <summary>
        ///     Issue #889. The dialog cannot show a concatenation as a namespace, so it read as empty, and empty was
        ///     written back as DefaultNamespace: OK with nothing changed threw the user's code away.
        /// </summary>
        [Test]
        public void ApplyTo_UnchangedWithANamespaceSetInCode_LeavesTheFileAlone()
        {
            var settings = new TemplateSettingsFile(ComputedNamespace);

            var text = TemplateConfiguration.ReadFrom(settings, "Fallback").ApplyTo(settings);

            Assert.That(text, Is.EqualTo(ComputedNamespace));
        }

        [Test]
        public void ReadFrom_ANamespaceSetInCode_IsShownAsItselfAndIsNotEditable()
        {
            var configuration = TemplateConfiguration.ReadFrom(new TemplateSettingsFile(ComputedNamespace), "Fallback");

            Assert.That(configuration.IsNamespaceEditable, Is.False);
            Assert.That(configuration.NamespaceExpression, Is.EqualTo("DefaultNamespace + \".Domain\""));
            Assert.That(configuration.Namespace, Is.Empty);
        }

        [Test]
        public void ReadFrom_ALiteralNamespace_IsEditable()
        {
            var settings = Shipped();
            settings.TrySetExpression("Namespace", "\"Accounts.Billing\"");

            var configuration = TemplateConfiguration.ReadFrom(settings, "Fallback");

            Assert.That(configuration.IsNamespaceEditable, Is.True);
            Assert.That(configuration.NamespaceExpression, Is.Null);
            Assert.That(configuration.Namespace, Is.EqualTo("Accounts.Billing"));
        }

        [TestCase("", true)]
        [TestCase("Accounts", true)]
        [TestCase("Accounts.Billing", true)]
        [TestCase("_private.Thing1", true)]
        [TestCase("Accounts.", false)]
        [TestCase("1Accounts", false)]
        [TestCase("Accounts Billing", false)]
        [TestCase("\"; Settings.ConnectionString = \"x", false)]
        public void HasValidNamespace_AcceptsOnlyDottedIdentifiers(string candidate, bool expected)
        {
            var configuration = new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "MyDbContext", candidate);

            Assert.That(configuration.HasValidNamespace, Is.EqualTo(expected));
        }

        /// <summary>
        ///     What goes into Settings.Namespace becomes C# in the .tt, so anything that is not a namespace is
        ///     left alone rather than written and broken.
        /// </summary>
        [Test]
        public void ApplyTo_LeavesAnInvalidNamespaceAlone()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", "MyDbContext", "not a namespace").ApplyTo(settings);

            Assert.That(settings.GetExpression("Namespace"),
                Is.EqualTo(TemplateConfiguration.DefaultNamespaceExpression));
        }

        [Test]
        public void ApplyTo_AnEmptyDbContextNameLeavesTheNamesAlone()
        {
            var settings = Shipped();

            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                "Data Source=(local);Initial Catalog=Northwind", string.Empty, string.Empty).ApplyTo(settings);

            Assert.That(settings.GetString("DbContextName"), Is.EqualTo("MyDbContext"));
        }

        /// <summary>
        ///     A named SQL Server instance survives the trip out to the file and back, which is what proves the
        ///     escaping and unescaping are inverses rather than merely both present.
        /// </summary>
        [Test]
        public void AConnectionStringWithBackslashesSurvivesARoundTrip()
        {
            const string connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=Northwind;Password=a""b\";

            var settings = Shipped();
            new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default, connectionString, "X", string.Empty)
                .ApplyTo(settings);

            Assert.That(settings.GetString("ConnectionString"), Is.EqualTo(connectionString));
        }
    }
}
