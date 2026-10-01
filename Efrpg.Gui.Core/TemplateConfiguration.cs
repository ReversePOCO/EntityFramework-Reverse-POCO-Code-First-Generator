using System;
using System.Text.RegularExpressions;

namespace Efrpg.Gui
{
    /// <summary>
    ///     The set of answers the connection dialog collects, and the rules for reading them out of a .tt and
    ///     writing them back into one.
    /// </summary>
    /// <remarks>
    ///     Kept here rather than in the dialog so it can be unit tested. The dialog lives in the VSIX, which needs a
    ///     running Visual Studio to exercise at all, and rules such as which connection strings may be written back
    ///     are exactly the kind of thing that must not be discovered by a user.
    /// </remarks>
    public sealed class TemplateConfiguration
    {
        public TemplateConfiguration(DatabaseTarget database, TemplateTarget template, string connectionString,
            string dbContextName, string namespaceName)
            : this(database, template, connectionString, dbContextName, namespaceName, null)
        {
        }

        /// <param name="source">
        ///     Where the template's connection string comes from. Null means a literal the dialog may write; anything
        ///     not editable makes <paramref name="connectionString"/> display text only and <see cref="ApplyTo"/>
        ///     leave the line alone.
        /// </param>
        public TemplateConfiguration(DatabaseTarget database, TemplateTarget template, string connectionString,
            string dbContextName, string namespaceName, ConnectionStringSource source)
            : this(database, template, connectionString, dbContextName, dbContextName, namespaceName, source)
        {
        }

        /// <param name="connectionStringName">
        ///     The key in appsettings.json or app.config the generated context reads. The shorter constructors
        ///     give it the context's name, which is what the shipped template does.
        /// </param>
        public TemplateConfiguration(DatabaseTarget database, TemplateTarget template, string connectionString,
            string dbContextName, string connectionStringName, string namespaceName, ConnectionStringSource source)
            : this(database, template, connectionString, dbContextName, connectionStringName, namespaceName, source, null)
        {
        }

        /// <param name="options">The output and fake context choices. Null means the shipped defaults.</param>
        public TemplateConfiguration(DatabaseTarget database, TemplateTarget template, string connectionString,
            string dbContextName, string connectionStringName, string namespaceName, ConnectionStringSource source,
            TemplateOptions options)
        {
            Database             = database ?? throw new ArgumentNullException(nameof(database));
            Template             = template ?? throw new ArgumentNullException(nameof(template));
            ConnectionString     = connectionString ?? string.Empty;
            DbContextName        = (dbContextName ?? string.Empty).Trim();
            ConnectionStringName = (connectionStringName ?? string.Empty).Trim();
            Namespace            = (namespaceName ?? string.Empty).Trim();
            Source               = source ?? ConnectionStringSource.ForLiteral(ConnectionString);
            Options              = options ?? TemplateOptions.Default;
        }

        public DatabaseTarget Database { get; }

        public TemplateTarget Template { get; }

        /// <summary>Separate files, file-scoped namespaces, and the fake context and its Debug-only wrapping.</summary>
        public TemplateOptions Options { get; }

        /// <summary>
        ///     The connection string when <see cref="IsConnectionStringEditable"/>; otherwise the code that sets it,
        ///     for display.
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>Where the connection string comes from, which decides whether the dialog may write it.</summary>
        public ConnectionStringSource Source { get; }

        public bool IsConnectionStringEditable => Source.IsEditable;

        public string DbContextName { get; }

        /// <summary>
        ///     The key in appsettings.json or app.config that the generated DbContext constructor reads. Not used
        ///     by the generator itself, which is why it is easy to forget and worth showing.
        /// </summary>
        public string ConnectionStringName { get; }

        /// <summary>
        ///     The namespace for the generated code, or empty to keep the template's <c>DefaultNamespace</c>, which
        ///     resolves at generation time to the namespace of the project the .tt sits in.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        ///     The right-hand side of Settings.Namespace when it is code - a concatenation, a call - rather than a
        ///     literal or <c>DefaultNamespace</c>, for display; otherwise null. <see cref="Namespace"/> is then empty,
        ///     and <see cref="ApplyTo"/> leaves the line alone.
        /// </summary>
        public string NamespaceExpression { get; private set; }

        public bool IsNamespaceEditable => NamespaceExpression == null;

        /// <summary>What the shipped template holds in Settings.Namespace, and what empty means here.</summary>
        public const string DefaultNamespaceExpression = "DefaultNamespace";

        private static readonly Regex NamespacePattern =
            new Regex(@"^[A-Za-z_]\w*(\.[A-Za-z_]\w*)*$");

        /// <summary>
        ///     True when the namespace can be written into the .tt as a string literal without producing code that
        ///     does not compile. An empty namespace is valid and means "leave it as DefaultNamespace".
        /// </summary>
        public bool HasValidNamespace => Namespace.Length == 0 || NamespacePattern.IsMatch(Namespace);

        /// <summary>What a brand-new template should open on: the shipped defaults, with a name derived from the file.</summary>
        public static TemplateConfiguration ForNewTemplate(string dbContextName)
        {
            return new TemplateConfiguration(DatabaseTarget.Default, TemplateTarget.Default,
                DatabaseTarget.Default.ConnectionString, dbContextName, string.Empty);
        }

        /// <summary>
        ///     What an existing .tt already says.
        /// </summary>
        /// <remarks>
        ///     Anything unreadable falls back to the default rather than failing: a user who has replaced a setting
        ///     with an expression still deserves a working dialog for the other fields, and <see cref="ApplyTo"/>
        ///     will refuse to overwrite the expression anyway. The connection string is the exception: an expression
        ///     there is shown as itself, read-only, because showing the placeholder instead would tell the user
        ///     their configured template is unconfigured.
        /// </remarks>
        public static TemplateConfiguration ReadFrom(TemplateSettingsFile settings, string fallbackDbContextName)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var database = DatabaseTarget.Find(settings.GetEnum("DatabaseType")) ?? DatabaseTarget.Default;
            var template = TemplateTarget.Find(settings.GetEnum("TemplateType")) ?? TemplateTarget.Default;
            var source   = ConnectionStringSource.Read(settings);

            string connectionString;
            switch (source.Kind)
            {
                case ConnectionStringKind.Literal:
                    connectionString = source.Value;
                    break;

                case ConnectionStringKind.Missing:
                    connectionString = database.ConnectionString;
                    break;

                default:
                    connectionString = source.Expression;
                    break;
            }

            var dbContextName = settings.GetString("DbContextName") ?? fallbackDbContextName;

            return new TemplateConfiguration(
                    database,
                    template,
                    connectionString,
                    dbContextName,
                    settings.GetString("ConnectionStringName") ?? dbContextName,
                    ReadNamespace(settings),
                    source,
                    TemplateOptions.ReadFrom(settings))
                {
                    NamespaceExpression = NamespaceSetInCode(settings)
                };
        }

        /// <summary>
        ///     Settings.Namespace ships as the bare identifier <c>DefaultNamespace</c> and becomes a quoted string
        ///     once somebody overrides it, so both shapes have to be understood. Anything else - a concatenation, a
        ///     call - reads back as empty, which leaves <see cref="ApplyTo"/> declining to touch it.
        /// </summary>
        private static string ReadNamespace(TemplateSettingsFile settings)
        {
            var literal = settings.GetString("Namespace");

            return literal ?? string.Empty;
        }

        /// <summary>
        ///     The right-hand side of Settings.Namespace when it is neither a string literal nor
        ///     <c>DefaultNamespace</c>, or null.
        /// </summary>
        private static string NamespaceSetInCode(TemplateSettingsFile settings)
        {
            if (settings.GetString("Namespace") != null)
                return null;

            var expression = settings.GetExpression("Namespace");

            return expression == null || expression == DefaultNamespaceExpression ? null : expression;
        }

        /// <summary>
        ///     The connection string to actually connect with: the literal, or what the template's own code
        ///     produces when that code is a shape this assembly understands. Null with a reason otherwise, including
        ///     when the literal still carries the placeholder.
        /// </summary>
        public string ResolveConnectionString(out string error)
        {
            if (!IsConnectionStringEditable)
                return Source.Resolve(out error);

            error = null;

            if (ConnectionString.IndexOf(TemplateSettingsFile.Placeholder, StringComparison.Ordinal) >= 0)
            {
                error = "The template's connection string still contains " + TemplateSettingsFile.Placeholder +
                        ", so there is no database to read yet. Use \"Reverse POCO: Connection...\" first.";
                return null;
            }

            if (ConnectionString.Trim().Length == 0)
            {
                error = "The template has no connection string. Use \"Reverse POCO: Connection...\" first.";
                return null;
            }

            return ConnectionString.Trim();
        }

        /// <summary>
        ///     Writes every answer into the settings file and returns the new text. Settings the file does not
        ///     express as a plain single-line assignment are left exactly as they are.
        /// </summary>
        public string ApplyTo(TemplateSettingsFile settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // A connection string set in code is the user's, and the dialog showed it read-only. TrySetString
            // would refuse anyway; being explicit here is what keeps that from ever being "fixed".
            if (IsConnectionStringEditable)
                settings.TrySetString("ConnectionString", ConnectionString);

            settings.TrySetEnum("DatabaseType", Database.Name);

            settings.TrySetEnum("TemplateType", Template.Name);

            if (DbContextName.Length > 0)
                settings.TrySetString("DbContextName", DbContextName);

            if (ConnectionStringName.Length > 0)
                settings.TrySetString("ConnectionStringName", ConnectionStringName);

            WriteNamespace(settings);
            Options.ApplyTo(settings);

            return settings.Text;
        }

        /// <summary>
        ///     Writes Settings.Namespace, switching the right-hand side between a quoted string and the bare
        ///     <c>DefaultNamespace</c> identifier rather than only replacing a literal.
        /// </summary>
        /// <remarks>
        ///     An invalid namespace is left alone rather than written. What goes here becomes C# in the .tt, and a
        ///     user who has replaced the setting with an expression of their own gets to keep it.
        /// </remarks>
        private void WriteNamespace(TemplateSettingsFile settings)
        {
            if (!HasValidNamespace)
                return;

            // Empty reads back from code too, where writing DefaultNamespace would discard the user's expression.
            // Only a literal is the dialog's to clear.
            if (Namespace.Length == 0 && NamespaceSetInCode(settings) != null)
                return;

            settings.TrySetExpression("Namespace",
                Namespace.Length == 0 ? DefaultNamespaceExpression : "\"" + Namespace + "\"");
        }
    }
}
