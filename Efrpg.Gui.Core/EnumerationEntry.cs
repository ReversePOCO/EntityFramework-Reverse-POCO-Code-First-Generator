using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Efrpg.Gui
{
    /// <summary>
    ///     One <c>new EnumerationSettings { ... }</c> the GUI appends to Settings.Enumerations: an enum read from a
    ///     lookup table's name and value columns.
    /// </summary>
    public sealed class EnumerationEntry
    {
        private static readonly Regex Identifier = new Regex(@"^[A-Za-z_]\w*$");

        public EnumerationEntry(string name, string table, string nameField, string valueField, string groupField)
        {
            Name       = (name ?? string.Empty).Trim();
            Table      = (table ?? string.Empty).Trim();
            NameField  = (nameField ?? string.Empty).Trim();
            ValueField = (valueField ?? string.Empty).Trim();
            GroupField = (groupField ?? string.Empty).Trim();
        }

        /// <summary>The C# enum to generate. With a group field, must contain <c>{GroupField}</c>.</summary>
        public string Name { get; }

        /// <summary>The table, schema-qualified as the generator expects: <c>EnumTest.DaysOfWeek</c>.</summary>
        public string Table { get; }

        public string NameField { get; }

        public string ValueField { get; }

        /// <summary>Optional: the column that splits one table into several enums.</summary>
        public string GroupField { get; }

        /// <summary>Why this entry cannot be written, or null when it can.</summary>
        public string Problem
        {
            get
            {
                if (Table.Length == 0)
                    return "Choose the table the enum is read from.";
                if (NameField.Length == 0)
                    return "Choose the column holding each member's name.";
                if (ValueField.Length == 0)
                    return "Choose the column holding each member's value.";
                if (string.Equals(NameField, ValueField, StringComparison.OrdinalIgnoreCase))
                    return "The name and value columns must differ.";
                if (Name.Length == 0)
                    return "Give the enum a name.";
                if (GroupField.Length > 0)
                    return Name.Contains("{GroupField}") ? null : "With a group column the name must contain {GroupField}, such as {GroupField}Type.";

                return Identifier.IsMatch(Name) ? null : "The enum name must be a C# identifier, such as OrderStatus.";
            }
        }

        public bool IsValid => Problem == null;

        /// <summary>
        ///     The initialiser as lines, laid out like the block it joins: <paramref name="indent" /> for the entry,
        ///     one <paramref name="step" /> further for its properties, and a trailing comma only when the block's
        ///     entries end with one.
        /// </summary>
        public IReadOnlyList<string> ToLines(string indent, string step = "    ", bool trailingComma = true)
        {
            indent = indent ?? string.Empty;
            var inner = indent + (step ?? "    ");

            var lines = new List<string>
            {
                indent + "new EnumerationSettings",
                indent + "{",
                inner + "Name       = " + Quote(Name) + ",",
                inner + "Table      = " + Quote(Table) + ",",
                inner + "NameField  = " + Quote(NameField) + ",",
                inner + "ValueField = " + Quote(ValueField) + (GroupField.Length > 0 ? "," : string.Empty)
            };

            if (GroupField.Length > 0)
                lines.Add(inner + "GroupField = " + Quote(GroupField));

            lines.Add(indent + (trailingComma ? "}," : "}"));
            return lines;
        }

        public override string ToString()
        {
            return Name + " from " + Table + " (" + NameField + " = " + ValueField + ")";
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in value)
            {
                if (c == '"' || c == '\\')
                    sb.Append('\\');
                sb.Append(c);
            }

            return sb.Append('"').ToString();
        }
    }
}
