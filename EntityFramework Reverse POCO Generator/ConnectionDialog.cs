using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Efrpg.Gui;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Asks for the database type, the template flavour, the connection string and the DbContext name - when a
    ///     template is first added, and again afterwards from the .tt file's right-click menu.
    /// </summary>
    /// <remarks>
    ///     This is the piece the whole GUI exists for. A newcomer today gets a .tt containing
    ///     <c>Initial Catalog=**TODO**</c> and a wall of settings, and has to work out which line matters; the same
    ///     developer installing EF Core Power Tools gets asked and is done.
    ///
    ///     The database dropdown sits above the connection string because it decides what the connection string
    ///     looks like: Oracle, PostgreSQL, MySQL and SQL Server share no keywords, so an Oracle user handed a SQL
    ///     Server connection string is no better off than with the placeholder. Choosing the database first and
    ///     being given the right skeleton is the point.
    ///
    ///     Nothing here reads the database. A brand-new template cannot be reverse engineered at all - efrpg rejects
    ///     the placeholder without connecting - so the connection string has to be collected before any schema call
    ///     can be worth making. The table picker comes after this, not instead of it.
    /// </remarks>
    public sealed class ConnectionDialog : DialogWindow
    {
        private readonly ComboBox _database;
        private readonly ComboBox _template;
        private readonly TextBox _connectionString;
        private readonly TextBox _dbContextName;
        private readonly TextBox _connectionStringName;
        private readonly TextBox _namespace;
        private readonly CheckBox _separateFiles;
        private readonly CheckBox _fileScopedNamespaces;
        private readonly CheckBox _fakeContext;
        private readonly CheckBox _fakeContextDebugOnly;

        /// <summary>
        ///     What the DbContext name box last held, so the connection string name can follow it while the two are
        ///     the same and stop following the moment the user makes them differ.
        /// </summary>
        private string _lastDbContextName;
        private readonly TextBlock _connectionHint;
        private readonly TextBlock _validation;
        private readonly Button _ok;
        private readonly Button _test;
        private readonly bool _isNewTemplate;

        private const string TestButtonText = "_Test connection";
        private readonly CancellationTokenSource _closing = new CancellationTokenSource();
        private JoinableTask _testRun;

        /// <summary>
        ///     What was in the connection string box for each database the user has visited, so switching away and
        ///     back restores their text rather than the default.
        /// </summary>
        private readonly Dictionary<DatabaseTarget, string> _typed = new Dictionary<DatabaseTarget, string>();

        /// <summary>The database whose connection string the box currently shows.</summary>
        private DatabaseTarget _shownDatabase;

        /// <summary>
        ///     Where the template's connection string comes from. When it is code rather than a literal the box is
        ///     read-only and shows the code, and OK leaves that line alone.
        /// </summary>
        private readonly ConnectionStringSource _source;

        /// <summary>
        ///     False when Settings.Namespace is code. The box then shows that code read-only, and OK leaves the line
        ///     alone.
        /// </summary>
        private readonly bool _namespaceEditable;

        /// <summary>
        ///     The schema from the last successful Test, when the connection string has not changed since, so the
        ///     object picker that follows can open on it instead of reading the database a second time.
        /// </summary>
        public DatabaseSchema TestedSchema { get; private set; }

        /// <summary>The answers, valid once <see cref="Confirmed"/> is true.</summary>
        public TemplateConfiguration Result
        {
            get
            {
                return new TemplateConfiguration(SelectedDatabase, SelectedTemplate, _connectionString.Text.Trim(),
                    _dbContextName.Text.Trim(), _connectionStringName.Text.Trim(),
                    _namespaceEditable ? _namespace.Text.Trim() : string.Empty, _source,
                    new TemplateOptions(
                        _separateFiles.IsChecked == true,
                        _fileScopedNamespaces.IsChecked == true,
                        _fakeContext.IsChecked == true,
                        _fakeContextDebugOnly.IsChecked == true));
            }
        }

        private DatabaseTarget SelectedDatabase => (DatabaseTarget) _database.SelectedItem;

        private TemplateTarget SelectedTemplate => (TemplateTarget) _template.SelectedItem;

        /// <summary>
        ///     True when the user pressed OK. On false the caller leaves the .tt exactly as it found it, which for a
        ///     new template means the placeholder is still there - a working starting point.
        /// </summary>
        public bool Confirmed { get; private set; }

        /// <summary>
        ///     Opens showing <paramref name="current"/>, which for an existing .tt is what that file already says.
        /// </summary>
        /// <remarks>
        ///     Starting from the file's own values is not a nicety. A user reopening this to change one field would
        ///     otherwise press OK and have their connection string replaced by the SQL Server default.
        /// </remarks>
        public ConnectionDialog(TemplateConfiguration current, bool isNewTemplate)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            _isNewTemplate = isNewTemplate;
            _shownDatabase = current.Database;
            _source        = current.Source;
            _namespaceEditable = current.IsNamespaceEditable;

            Title                 = "EntityFramework Reverse POCO Generator";
            Width                 = 680;
            MinWidth              = 520;
            SizeToContent         = SizeToContent.Height;

            // Widening is worth allowing: connection strings are long, and the box wraps rather than scrolls.
            // Height stays tied to the content, so there is never dead space below the buttons.
            ResizeMode            = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            HasMaximizeButton     = false;
            HasMinimizeButton     = false;

            _database         = new ComboBox { ItemsSource = DatabaseTarget.All, SelectedItem = current.Database, Padding = new Thickness(6, 4, 6, 4) };
            _template         = new ComboBox { ItemsSource = TemplateTarget.All, SelectedItem = current.Template, Padding = new Thickness(6, 4, 6, 4) };
            _connectionString = new TextBox { Text = current.ConnectionString, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(6, 4, 6, 4), TextWrapping = TextWrapping.Wrap, IsReadOnly = !_source.IsEditable, Opacity = _source.IsEditable ? 1.0 : 0.75 };
            _dbContextName    = new TextBox { Text = current.DbContextName, Padding = new Thickness(6, 4, 6, 4) };
            _connectionStringName = new TextBox { Text = current.ConnectionStringName, Padding = new Thickness(6, 4, 6, 4) };
            _lastDbContextName    = current.DbContextName;
            _namespace        = new TextBox { Text = current.NamespaceExpression ?? current.Namespace, Padding = new Thickness(6, 4, 6, 4), IsReadOnly = !_namespaceEditable, Opacity = _namespaceEditable ? 1.0 : 0.75 };
            _separateFiles        = Option("Generate a file per class, in sub-folders", current.Options.GenerateSeparateFiles);
            _fileScopedNamespaces = Option("Use file-scoped namespaces (C# 10)", current.Options.UseFileScopedNamespaces);
            _fakeContext          = Option("Generate a FakeDbContext for unit tests", current.Options.AddUnitTestingDbContext);
            _fakeContextDebugOnly = Option("Wrap the fake in #if DEBUG so Release builds leave it out", current.Options.FakeDbContextInDebugOnlyMode);
            _fakeContextDebugOnly.Margin = new Thickness(22, 0, 0, 6);
            _connectionHint   = Hint(current.Database.Hint);
            _validation       = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            _ok               = new Button { Content = "OK", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 4, 10, 4), IsDefault = true };
            _test             = new Button { Content = TestButtonText, MinWidth = 130, Padding = new Thickness(10, 4, 10, 4) };

            _database.SelectionChanged    += (s, e) => DatabaseChanged();
            _connectionString.TextChanged += (s, e) => { TestedSchema = null; Validate(); };
            _namespace.TextChanged        += (s, e) => Validate();
            _dbContextName.TextChanged    += (s, e) => FollowDbContextName();
            _fakeContext.Checked          += (s, e) => FollowFakeContext();
            _fakeContext.Unchecked        += (s, e) => FollowFakeContext();
            _ok.Click                     += (s, e) => { Confirmed = true; DialogResult = true; Close(); };
            _test.Click                   += (s, e) => Test();

            // The read can take a couple of minutes against a slow or unreachable server. Closing the dialog
            // cancels it - which kills the child process - and then waits for it, so no continuation is left
            // running against a window that has gone. Joining here is safe rather than deadlocking precisely
            // because it goes through the JoinableTaskFactory, which lets this thread run the continuation.
            Closed += (s, e) => CancelTest();

            Content = Build();
            FollowFakeContext();
            Validate();
        }

        /// <summary>The Debug-only choice means nothing without a fake context to wrap, so it follows that box.</summary>
        private void FollowFakeContext()
        {
            _fakeContextDebugOnly.IsEnabled = _fakeContext.IsChecked == true;
        }

        private static CheckBox Option(string text, bool isChecked)
        {
            return new CheckBox { Content = text, IsChecked = isChecked, Margin = new Thickness(0, 0, 0, 6) };
        }

        /// <summary>
        ///     Runs the real schema read, through the same tool and the same wire format the T4 template uses on
        ///     save.
        /// </summary>
        /// <remarks>
        ///     Testing any other way - opening a connection here directly - would prove something subtly different
        ///     from what happens at generation time, which is what "it tested fine but generation fails" is made
        ///     of. Reporting the object counts rather than a bare "OK" also answers the question behind the
        ///     question: people press Test to find out whether they pointed it at the right database.
        /// </remarks>
        private void Test()
        {
            // VSSDK007 wants this awaited at the call site, which a Click handler cannot do. It is not abandoned:
            // it is kept in _testRun and joined when the dialog closes, after cancellation, so nothing outlives
            // the window it reports into. FileAndForget puts any fault in the activity log rather than losing it.
#pragma warning disable VSSDK007
            _testRun = ThreadHelper.JoinableTaskFactory.RunAsync(TestAsync);
#pragma warning restore VSSDK007

            _testRun.Task.FileAndForget("efrpg/gui/testconnection");
        }

        private void CancelTest()
        {
            _closing.Cancel();

            if (_testRun != null)
                _testRun.Join();
        }

        private async Task TestAsync()
        {
            string problem       = null;
            var boxText          = _connectionString.Text;
            var connectionString = _source.IsEditable ? boxText.Trim() : _source.Resolve(out problem);
            var databaseType     = SelectedDatabase != null ? SelectedDatabase.Name : DatabaseTarget.Default.Name;

            if (connectionString == null)
            {
                Report(problem, true);
                return;
            }

            _test.IsEnabled = false;
            _test.Content   = "Connecting...";
            Report("Connecting to " + databaseType + "...", true);

            try
            {
                await TaskScheduler.Default;

                var result = await SchemaReading.ReadAsync(databaseType, connectionString, _closing.Token);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_closing.Token);

                // Only kept while it still describes what is in the box. A read that finished after the user
                // edited the connection string is about a different database.
                if (result.Succeeded && _connectionString.Text == boxText && SelectedDatabase != null && SelectedDatabase.Name == databaseType)
                    TestedSchema = result.Schema;

                Report(Describe(result), !result.Succeeded);
            }
            catch (OperationCanceledException)
            {
                // The dialog was closed while the read was in flight. There is nobody left to tell.
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Report(ex.Message, true);
            }
            finally
            {
                if (!_closing.IsCancellationRequested)
                {
                    _test.IsEnabled = true;
                    _test.Content   = TestButtonText;
                }
            }
        }

        private static string Describe(SchemaReadResult result)
        {
            if (!result.Succeeded)
                return "Could not connect. " + result.Error;

            var schema = result.Schema;
            var summary = "Connected successfully. Found " +
                          Plural(schema.Count(DatabaseObjectKind.Table), "table") + ", " +
                          Plural(schema.Count(DatabaseObjectKind.View), "view") + ", " +
                          Plural(schema.Count(DatabaseObjectKind.StoredProcedure), "stored procedure") + ", " +
                          Plural(schema.Count(DatabaseObjectKind.TableValuedFunction) + schema.Count(DatabaseObjectKind.ScalarValuedFunction), "function") + ".";

            if (!schema.CanReadStoredProcedures)
                summary += " This login cannot read stored procedure definitions.";

            if (schema.Errors.Count > 0)
                summary += " " + Plural(schema.Errors.Count, "warning") + ": " + schema.Errors[0];

            return summary;
        }

        private static string Plural(int count, string noun)
        {
            return count + " " + noun + (count == 1 ? string.Empty : "s");
        }

        /// <summary>
        ///     One line under the buttons, always in the dialog's own text colour.
        /// </summary>
        /// <remarks>
        ///     It used to use ToolWindowValidationErrorTextBrushKey, which is tuned for a tool window's background
        ///     and came out as a pale red that was hard to read against a dialog. Legibility is worth more here than
        ///     the colour was: the message is one line under the buttons with nothing competing for attention, and
        ///     bold carries the emphasis instead. DialogTextBrushKey is the one key guaranteed to be readable on
        ///     this background in every theme.
        /// </remarks>
        private void Report(string message, bool isProblem)
        {
            _validation.Text       = message;
            _validation.FontWeight = isProblem ? FontWeights.SemiBold : FontWeights.Normal;
            _validation.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            _validation.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.DialogTextBrushKey);
        }

        /// <summary>
        ///     Shows a connection string for the database just chosen: whatever was last typed for it in this
        ///     dialog, or its default. What was typed for the previous database is kept for when it is chosen again.
        /// </summary>
        /// <remarks>
        ///     The first version swapped only while the box held an untouched default, to avoid overwriting typed
        ///     text. That left a SQLite string sitting under an Oracle selection: the providers share no keywords,
        ///     so the "preserved" text could not connect to anything and had to be deleted by hand to get the Oracle
        ///     skeleton back. Keeping the text per database loses nothing and never shows a string for the wrong
        ///     provider; a mis-click on the dropdown is undone by clicking back.
        /// </remarks>
        private void DatabaseChanged()
        {
            var target = SelectedDatabase;
            if (target == null || ReferenceEquals(target, _shownDatabase))
                return;

            if (!_source.IsEditable)
            {
                // The box shows code, not a value, so only the hint and the test follow the database.
                _shownDatabase       = target;
                TestedSchema         = null;
                _connectionHint.Text = target.Hint;
                Validate();
                return;
            }

            if (_shownDatabase != null)
                _typed[_shownDatabase] = _connectionString.Text;

            string remembered;
            _connectionString.Text = _typed.TryGetValue(target, out remembered) ? remembered : target.ConnectionString;
            _shownDatabase         = target;

            TestedSchema         = null;
            _connectionHint.Text = target.Hint;
            Validate();
        }

        /// <summary>
        ///     The shipped template gives the connection string the context's name, so while the two boxes match,
        ///     renaming the context renames both. Once they differ the user has made a choice, and it sticks.
        /// </summary>
        private void FollowDbContextName()
        {
            if (_connectionStringName.Text == _lastDbContextName)
                _connectionStringName.Text = _dbContextName.Text;

            _lastDbContextName = _dbContextName.Text;
        }

        private UIElement Build()
        {
            var cancel = new Button
            {
                Content = _isNewTemplate ? "_Skip" : "_Cancel",
                MinWidth = 90,
                Padding = new Thickness(10, 4, 10, 4),
                IsCancel = true
            };
            cancel.Click += (s, e) => { Confirmed = false; DialogResult = false; Close(); };

            var confirm = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            confirm.Children.Add(_ok);
            confirm.Children.Add(cancel);

            // Test on the left, away from the buttons that dismiss the dialog: it is the one button here that can
            // be pressed repeatedly, and it must not sit where a hurried double-click on OK would land.
            var buttons = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(_test, Dock.Left);
            DockPanel.SetDock(confirm, Dock.Right);
            buttons.Children.Add(_test);
            buttons.Children.Add(confirm);

            var body = new StackPanel { Margin = new Thickness(16) };
            body.Children.Add(new TextBlock
            {
                Text = _isNewTemplate
                    ? "Point the template at your database. Next you choose which tables and procedures to generate. You can change any of this later by right-clicking the .tt file."
                    : "Changing any of these rewrites that one line of the .tt and regenerates the output. Everything else in the file is left alone. Every other setting is under \"Reverse POCO: All settings...\".",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });
            body.Children.Add(TwoColumns(
                Label("Database"), _database,
                Label("Template"), _template));
            body.Children.Add(new Border { Height = 12 });
            body.Children.Add(Label("Connection string"));
            body.Children.Add(_connectionString);
            body.Children.Add(_connectionHint);
            body.Children.Add(new TextBlock
            {
                Text = _source.IsEditable
                    ? "Stored in the .tt file, which is usually in source control. Prefer integrated security over a password."
                    : "Set in code, from " + _source.Description + ". Edit it in the .tt to change it; OK leaves this line alone.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(0, 0, 0, 12)
            });
            body.Children.Add(TwoColumns(
                Label("DbContext name"), _dbContextName,
                Label("Connection string name"), _connectionStringName));
            body.Children.Add(new TextBlock
            {
                Text = "The connection string name is the key in appsettings.json or app.config that the generated DbContext reads. It follows the DbContext name until you change it.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(0, 4, 0, 12)
            });
            body.Children.Add(Label("Namespace"));
            body.Children.Add(_namespace);
            body.Children.Add(new TextBlock
            {
                Text = _namespaceEditable
                    ? "Leave the namespace blank to use the namespace of the project the .tt sits in."
                    : "Set in code. Edit it in the .tt to change it; OK leaves this line alone.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(0, 4, 0, 12)
            });
            body.Children.Add(Label("Output"));
            body.Children.Add(_separateFiles);
            body.Children.Add(_fileScopedNamespaces);
            body.Children.Add(_fakeContext);
            body.Children.Add(_fakeContextDebugOnly);
            body.Children.Add(new Border { Height = 8 });
            body.Children.Add(_validation);
            body.Children.Add(buttons);
            return body;
        }

        /// <summary>Two labelled controls side by side, each taking half the width.</summary>
        private static UIElement TwoColumns(UIElement leftLabel, UIElement left, UIElement rightLabel, UIElement right)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());

            Place(grid, leftLabel,  0, 0);
            Place(grid, rightLabel, 0, 2);
            Place(grid, left,       1, 0);
            Place(grid, right,      1, 2);
            return grid;
        }

        private static void Place(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        }

        private static TextBlock Hint(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 4, 0, 4) };
        }

        /// <summary>
        ///     The one thing worth blocking on: leaving a placeholder in place produces a template that cannot
        ///     generate anything, and the error arrives much later when the user saves the file.
        /// </summary>
        private void Validate()
        {
            var namespaceIsValid = Result.HasValidNamespace;
            string problem       = null;
            bool canConnect;

            if (_source.IsEditable)
            {
                var stillPlaceholder = _connectionString.Text.IndexOf(TemplateSettingsFile.Placeholder, StringComparison.Ordinal) >= 0;

                canConnect = !stillPlaceholder && _connectionString.Text.Trim().Length > 0;

                if (stillPlaceholder)
                    problem = "Replace every " + TemplateSettingsFile.Placeholder + " above" +
                              (_isNewTemplate ? ", or press Skip to edit the .tt yourself." : ".");
            }
            else
            {
                // Code the dialog will not rewrite. Whether it can be tested depends on whether the value can be
                // fetched here: an environment variable or a file can, anything else cannot.
                string unresolved;
                canConnect = _source.Resolve(out unresolved) != null;

                if (!canConnect)
                    problem = unresolved + (_source.CanResolve ? string.Empty : " Test needs a value it can read.");
            }

            if (problem == null && !namespaceIsValid)
                problem = "A namespace must be one or more identifiers separated by dots, such as Accounts.Billing.";

            Report(problem ?? string.Empty, true);

            // When the connection string is code, OK is about the other fields and writes nothing for that line.
            _ok.IsEnabled = (canConnect || !_source.IsEditable) && namespaceIsValid;

            // Testing does not care about the namespace, only about reaching the database.
            _test.IsEnabled = canConnect;
        }
    }
}
