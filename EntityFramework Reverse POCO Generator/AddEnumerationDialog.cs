using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Efrpg.Gui;
using Microsoft.VisualStudio.PlatformUI;

namespace EntityFramework_Reverse_POCO_Generator
{
    /// <summary>
    ///     Asks for one enum to append to Settings.Enumerations: the table, its name and value columns, the enum's
    ///     name and an optional group column.
    /// </summary>
    /// <remarks>
    ///     With a schema, the tables are a dropdown of the lookup-shaped ones, or every table once Show all tables is
    ///     ticked, and the columns follow the table, defaulted by <see cref="EnumerationBlock.Suggest"/>. Without one - the connection string could not
    ///     be resolved, or the read failed - every box is free text, because a user who knows the table name should
    ///     not be blocked by a database the dialog cannot reach.
    /// </remarks>
    public sealed class AddEnumerationDialog : DialogWindow
    {
        private readonly DatabaseSchema _schema;
        private readonly TemplateSettingsDocument _document;
        private readonly ComboBox _table;
        private readonly ComboBox _nameField;
        private readonly ComboBox _valueField;
        private readonly ComboBox _groupField;
        private readonly TextBox _name;
        private readonly TextBlock _validation;
        private readonly Button _ok;
        private readonly CheckBox _showAll;
        private readonly bool _hasLookups;
        private System.Collections.Generic.List<string> _offeredTables = new System.Collections.Generic.List<string>();
        private bool _suggesting;
        private bool _filtering;

        public bool Confirmed { get; private set; }

        /// <summary>The entry, valid once <see cref="Confirmed"/> is true.</summary>
        public EnumerationEntry Result { get; private set; }

        public AddEnumerationDialog(DatabaseSchema schema, TemplateSettingsDocument document)
        {
            _schema   = schema;
            _document = document ?? throw new ArgumentNullException(nameof(document));

            Title                 = "Add an enumeration";
            Width                 = 560;
            SizeToContent         = SizeToContent.Height;
            ResizeMode            = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            HasMaximizeButton     = false;
            HasMinimizeButton     = false;

            _table      = Combo(new string[0]);
            _nameField  = Combo(new string[0]);
            _valueField = Combo(new string[0]);
            _groupField = Combo(new string[0]);
            _name       = new TextBox { Padding = new Thickness(6, 4, 6, 4) };
            _validation = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold };
            _ok         = new Button { Content = "Add", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 4, 10, 4), IsDefault = true };

            // Ticked, and greyed out, when no table looks like a lookup: every table is offered then anyway.
            _hasLookups = EnumerationBlock.HasLookupTables(schema);
            _showAll    = new CheckBox
            {
                Content    = "Show all tables",
                Margin     = new Thickness(0, 6, 0, 0),
                IsChecked  = !_hasLookups,
                IsEnabled  = _hasLookups,
                Visibility = schema == null ? Visibility.Collapsed : Visibility.Visible
            };
            _showAll.Checked   += (s, e) => OfferTables();
            _showAll.Unchecked += (s, e) => OfferTables();

            // The combo's own prefix completion would fill in and highlight the rest of the first match, so that
            // the next keystroke replaced it; the contains-filter below is the completion here.
            _table.IsTextSearchEnabled = false;

            _table.SelectionChanged += (s, e) => TableChanged();
            _table.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => { FilterTables(); Validate(); }));
            foreach (var box in new[] { _nameField, _valueField, _groupField })
            {
                box.SelectionChanged += (s, e) => Validate();
                box.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => Validate()));
            }
            _name.TextChanged += (s, e) => Validate();
            _ok.Click         += (s, e) => { Result = Entry(); Confirmed = true; DialogResult = true; Close(); };

            Content = Build();

            OfferTables();

            Validate();
        }

        /// <summary>
        ///     Fills the dropdown with the lookup-shaped tables, or every table when Show all tables is ticked. The
        ///     table already chosen stays chosen, with its columns as the user left them; if it drops out of the list,
        ///     the first table is selected and its columns suggested afresh.
        /// </summary>
        private void OfferTables()
        {
            var current = (_table.Text ?? string.Empty).Trim();
            _offeredTables = EnumerationBlock.Candidates(_schema, _showAll.IsChecked == true).Select(t => t.FullName).ToList();
            var keep = _offeredTables.FirstOrDefault(t => string.Equals(t, current, StringComparison.OrdinalIgnoreCase));

            if (keep != null)
            {
                _filtering = true;
                try
                {
                    _table.ItemsSource  = _offeredTables;
                    _table.SelectedItem = keep;
                }
                finally
                {
                    _filtering = false;
                }

                return;
            }

            _table.ItemsSource = _offeredTables;
            if (_offeredTables.Count > 0)
                _table.SelectedIndex = 0;
        }

        private static ComboBox Combo(System.Collections.Generic.IReadOnlyList<string> items)
        {
            return new ComboBox { ItemsSource = items, IsEditable = true, Padding = new Thickness(6, 4, 6, 4) };
        }

        private DatabaseObject SelectedTable()
        {
            var name = _table.Text ?? string.Empty;

            return _schema == null
                ? null
                : _schema.Of(DatabaseObjectKind.Table).FirstOrDefault(t => string.Equals(t.FullName, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Narrows the dropdown to the tables whose name contains what has been typed, case-insensitively, and
        ///     opens it, so a table is found by typing part of its name rather than scrolling for it. Typing the
        ///     whole name selects it.
        /// </summary>
        private void FilterTables()
        {
            if (_filtering || _schema == null)
                return;

            var typed = (_table.Text ?? string.Empty).Trim();
            var exact = _offeredTables.FirstOrDefault(t => string.Equals(t, typed, StringComparison.OrdinalIgnoreCase));

            _filtering = true;
            try
            {
                if (exact != null)
                {
                    if (!ReferenceEquals(_table.ItemsSource, _offeredTables))
                        _table.ItemsSource = _offeredTables;
                    if (!Equals(_table.SelectedItem, exact))
                        _table.SelectedItem = exact;
                    _table.IsDropDownOpen = false;
                    return;
                }

                var matches = _offeredTables.Where(t => t.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                // Swapping the list clears the editable text when the old selection drops out of it, so the text
                // and caret are put back afterwards.
                var text = _table.Text;
                _table.ItemsSource = matches;
                _table.SelectedItem = null;
                _table.Text = text;

                // The combo selects the whole text after it is set, so the next keystroke would replace it. The
                // caret goes back to the end with nothing selected, once the combo has finished its own update.
                var editor = _table.Template == null ? null : _table.Template.FindName("PART_EditableTextBox", _table) as TextBox;
                if (editor != null)
                    editor.Dispatcher.BeginInvoke(new Action(() => editor.Select(editor.Text.Length, 0)), DispatcherPriority.Input);

                _table.IsDropDownOpen = typed.Length > 0 && matches.Count > 0;
            }
            finally
            {
                _filtering = false;
            }
        }

        /// <summary>The columns follow the table, and the suggestion fills the boxes a user would otherwise type.</summary>
        private void TableChanged()
        {
            if (_filtering)
                return;

            var table = _table.SelectedItem == null ? null : SelectedTableByName(_table.SelectedItem.ToString());
            if (table == null)
                return;

            _suggesting = true;
            try
            {
                var columns = table.Columns.Select(c => c.Name).ToList();
                _nameField.ItemsSource  = columns;
                _valueField.ItemsSource = columns;
                _groupField.ItemsSource = new[] { string.Empty }.Concat(columns).ToList();

                var suggestion = EnumerationBlock.Suggest(table);
                _nameField.Text  = suggestion.NameField;
                _valueField.Text = suggestion.ValueField;
                _groupField.Text = string.Empty;
                _name.Text       = suggestion.Name;
            }
            finally
            {
                _suggesting = false;
            }

            Validate();
        }

        private DatabaseObject SelectedTableByName(string fullName)
        {
            return _schema == null
                ? null
                : _schema.Of(DatabaseObjectKind.Table).FirstOrDefault(t => string.Equals(t.FullName, fullName, StringComparison.OrdinalIgnoreCase));
        }

        private EnumerationEntry Entry()
        {
            return new EnumerationEntry(_name.Text, _table.Text, _nameField.Text, _valueField.Text, _groupField.Text);
        }

        private void Validate()
        {
            if (_suggesting)
                return;

            var entry   = Entry();
            var problem = entry.Problem;

            if (problem == null && EnumerationBlock.MentionsTable(_document, entry.Table))
                problem = "Note: Settings.Enumerations already mentions " + entry.Table + ". Adding it again generates a second enum from the same table.";

            _validation.Text       = problem ?? string.Empty;
            _validation.Visibility = problem == null ? Visibility.Collapsed : Visibility.Visible;
            _ok.IsEnabled          = entry.IsValid;
        }

        private UIElement Build()
        {
            var body = new StackPanel { Margin = new Thickness(16) };

            var lookupShape = $"an integer column, a text column and no more than {EnumerationBlock.MaxLookupColumns} columns";
            const string appended = "The entry is appended to Settings.Enumerations when you save.";
            body.Children.Add(new TextBlock
            {
                Text = _schema == null
                    ? "The database could not be read, so type the table and column names. " + appended
                    : _hasLookups
                        ? $"Only tables that look like lookups are listed: {lookupShape}. Tick Show all tables to pick any other. {appended}"
                        : $"No table looks like a lookup ({lookupShape}), so every table is listed. {appended}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });

            body.Children.Add(Label("Table (schema.table)"));
            body.Children.Add(_table);
            body.Children.Add(_showAll);
            body.Children.Add(Gap());
            body.Children.Add(Label("Enum name"));
            body.Children.Add(_name);
            body.Children.Add(Hint("The C# enum to generate. With a group column, include {GroupField} in the name, such as {GroupField}Type."));
            body.Children.Add(Gap());
            body.Children.Add(TwoColumns(Label("Name column"), _nameField, Label("Value column"), _valueField));
            body.Children.Add(Gap());
            body.Children.Add(Label("Group column (optional)"));
            body.Children.Add(_groupField);
            body.Children.Add(Hint("When one table holds several enums, the column that says which. Leave blank for one enum per table."));
            body.Children.Add(new Border { Height = 14 });
            body.Children.Add(_validation);

            var cancel = new Button { Content = "Cancel", MinWidth = 90, Padding = new Thickness(10, 4, 10, 4), IsCancel = true };
            cancel.Click += (s, e) => { Confirmed = false; DialogResult = false; Close(); };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(_ok);
            buttons.Children.Add(cancel);
            body.Children.Add(buttons);

            return body;
        }

        private static UIElement TwoColumns(UIElement leftLabel, UIElement left, UIElement rightLabel, UIElement right)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());

            Place(grid, leftLabel, 0, 0);
            Place(grid, rightLabel, 0, 2);
            Place(grid, left, 1, 0);
            Place(grid, right, 1, 2);
            return grid;
        }

        private static void Place(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static UIElement Gap()
        {
            return new Border { Height = 10 };
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        }

        private static TextBlock Hint(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 4, 0, 0) };
        }
    }
}
