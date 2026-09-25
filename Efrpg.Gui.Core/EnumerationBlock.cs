using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Efrpg.Gui
{
    /// <summary>
    ///     Appends entries to the <c>Settings.Enumerations = new List&lt;EnumerationSettings&gt; { ... };</c> block.
    ///     Append only: the entries already there, the comments and the shipped example are never read or moved,
    ///     which is what makes this safe on a block somebody wrote by hand.
    /// </summary>
    public static class EnumerationBlock
    {
        public const string SettingName = "Enumerations";

        /// <summary>
        ///     The widest table still treated as a lookup. Besides its key and name, a lookup table commonly carries a
        ///     description, a sort order, an active flag and audit columns, and it must still be offered.
        /// </summary>
        public const int MaxLookupColumns = 10;

        private static readonly Regex Initialiser =
            new Regex(@"^\s*new\s+List\s*<\s*EnumerationSettings\s*>\s*(\(\s*\))?\s*\{", RegexOptions.Singleline);

        /// <summary>Why nothing can be appended to this template's block, or null when it can.</summary>
        public static string CannotAppendReason(TemplateSettingsDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var assignment = document.Find(SettingName);
            if (assignment == null)
                return "Settings." + SettingName + " is not in this template. Switch it on first.";

            if (assignment.IsCommentedOut)
                return "Settings." + SettingName + " is commented out. Switch it on first.";

            if (!Initialiser.IsMatch(assignment.ValueText))
                return "Settings." + SettingName + " is not a List<EnumerationSettings> initialiser, so entries cannot be appended. Edit it in the editor.";

            return null;
        }

        /// <summary>
        ///     Returns the document with the entry added as the last element of the initialiser, immediately
        ///     before the <c>}</c> that closes it on the statement's final line.
        /// </summary>
        /// <remarks>
        ///     The entry copies the layout it finds rather than assuming the shipped one. Its indentation is that of
        ///     the entries already there, and its inner step is how far they sit inside the list's braces, so a block
        ///     whose Settings line starts in column 0, or one written with two spaces or tabs, stays consistent. When
        ///     the last entry has no separating comma - the normal way to write a list by hand - one is added after it,
        ///     and the new entry then ends without one too, matching the style it found.
        /// </remarks>
        public static TemplateSettingsDocument Append(TemplateSettingsDocument document, EnumerationEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var reason = CannotAppendReason(document);
            if (reason != null)
                throw new InvalidOperationException(reason);

            if (!entry.IsValid)
                throw new InvalidOperationException(entry.Problem);

            var assignment = document.Find(SettingName);
            var statement  = document.StatementText(assignment).Replace("\r\n", "\n").Split('\n');
            var lastIndex  = statement.Length - 1;
            var last       = statement[lastIndex];
            var close      = ClosingBrace(statement);

            if (close < 0 || statement.Length < 2)
                throw new InvalidOperationException("The Settings." + SettingName + " block does not end with a closing brace on its own line.");

            // The list's own brace is the first in the statement: CannotAppendReason has checked that only
            // "new List<EnumerationSettings>" stands between it and the equals sign.
            var openLine    = Array.FindIndex(statement, l => l.IndexOf('{') >= 0);
            var braceIndent = LeadingWhitespace(statement[openLine]);

            // Anything on the closing line before the brace is the end of the last entry, kept ahead of the new one.
            var before = last.Substring(0, close);

            var body = statement.Skip(openLine + 1).Take(Math.Max(0, lastIndex - openLine - 1)).ToList();
            if (openLine < lastIndex && before.Trim().Length > 0)
                body.Add(before);

            var firstBodyLine = body.FirstOrDefault(l => l.Trim().Length > 0);
            var indent        = firstBodyLine == null ? null : LeadingWhitespace(firstBodyLine);
            var step          = indent != null && indent.Length > braceIndent.Length && indent.StartsWith(braceIndent, StringComparison.Ordinal)
                ? indent.Substring(braceIndent.Length)
                : braceIndent.IndexOf('\t') >= 0 ? "\t" : "    ";
            if (indent == null)
                indent = braceIndent + step;

            // The last character of code before the closing brace says whether a comma is missing: the list's own
            // "{" when it is empty, a "," when the last entry already has one, anything else when it does not.
            var scanner   = new StatementScanner();
            var codeLine  = -1;
            var codeIndex = -1;
            for (var i = 0; i < lastIndex; i++)
            {
                scanner.Feed(statement[i]);
                if (scanner.LastCodeIndex >= 0)
                {
                    codeLine  = i;
                    codeIndex = scanner.LastCodeIndex;
                }
            }

            scanner.Feed(before);
            if (scanner.LastCodeIndex >= 0)
            {
                codeLine  = lastIndex;
                codeIndex = scanner.LastCodeIndex;
            }

            var lastCode   = codeLine < 0 ? '{' : (codeLine == lastIndex ? before : statement[codeLine])[codeIndex];
            var needsComma = lastCode != '{' && lastCode != ',';

            var result = document;
            if (needsComma)
            {
                if (codeLine == lastIndex)
                    before = before.Insert(codeIndex + 1, ",");
                else
                    result = result.WithLinesBeforeLine(assignment.LineNumber + codeLine, new string[0], statement[codeLine].Insert(codeIndex + 1, ","));
            }

            var lines = entry.ToLines(indent, step, trailingComma: !needsComma).ToList();
            if (before.Trim().Length > 0)
                lines.Insert(0, before.TrimEnd());

            return result.WithLinesBeforeLine(assignment.EndLineNumber, lines, before.Trim().Length > 0 ? braceIndent + last.Substring(close) : null);
        }

        /// <summary>
        ///     The list's closing brace on the statement's last line: the last one before the terminating semicolon,
        ///     so a brace in a comment after the statement is not taken for it.
        /// </summary>
        private static int ClosingBrace(IReadOnlyList<string> statement)
        {
            var scanner = new StatementScanner();
            foreach (var line in statement)
                scanner.Feed(line);

            var last = statement[statement.Count - 1];
            return scanner.TerminatorIndex >= 0 ? last.LastIndexOf('}', scanner.TerminatorIndex) : last.LastIndexOf('}');
        }

        private static string LeadingWhitespace(string line)
        {
            return line.Substring(0, line.Length - line.TrimStart(' ', '\t').Length);
        }

        /// <summary>True when the block already names this table, by plain text rather than parsing; for a warning, not a refusal.</summary>
        public static bool MentionsTable(TemplateSettingsDocument document, string table)
        {
            if (document == null || string.IsNullOrWhiteSpace(table))
                return false;

            var assignment = document.Find(SettingName);
            if (assignment == null)
                return false;

            var statement = document.StatementText(assignment);
            var pattern   = "\"" + Regex.Escape(table.Trim()) + "\"";

            return Regex.IsMatch(statement, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        ///     Every table, in name order, so the dropdown reads like a list rather than a ranking. Views are left
        ///     out: an enum is read from a table.
        /// </summary>
        public static IReadOnlyList<DatabaseObject> Candidates(DatabaseSchema schema)
        {
            if (schema == null)
                return new DatabaseObject[0];

            return schema.Of(DatabaseObjectKind.Table)
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        ///     The tables the Add enumeration form offers, in name order: only those that look like lookups, unless
        ///     <paramref name="showAllTables"/> is set. When no table looks like one, every table is offered anyway,
        ///     because an empty dropdown would leave the user nowhere to go.
        /// </summary>
        public static IReadOnlyList<DatabaseObject> Candidates(DatabaseSchema schema, bool showAllTables)
        {
            var tables = Candidates(schema);
            if (showAllTables)
                return tables;

            var lookups = tables.Where(LooksLikeEnumTable).ToList();
            return lookups.Count > 0 ? lookups : tables;
        }

        public static bool HasLookupTables(DatabaseSchema schema)
        {
            return Candidates(schema).Any(LooksLikeEnumTable);
        }

        public static bool LooksLikeEnumTable(DatabaseObject table)
        {
            return table != null &&
                   table.Columns.Any(c => c.IsIntegral) &&
                   table.Columns.Any(c => c.IsText) &&
                   table.Columns.Count <= MaxLookupColumns;
        }

        /// <summary>
        ///     The entry a table most likely wants: its integral key as the value, its first text column as the
        ///     name, and the table's name in PascalCase with Enum appended as the enum's.
        /// </summary>
        /// <remarks>
        ///     The suffix is there because the table is usually generated as an entity too, under much the same name,
        ///     and the generator does not check an enum against the classes beside it: the plain name would be two
        ///     types with one name, which does not compile. The shipped AddEnum example appends Enum for the same reason.
        /// </remarks>
        public static EnumerationEntry Suggest(DatabaseObject table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            var value = table.Columns.FirstOrDefault(c => c.IsPrimaryKey && c.IsIntegral)
                        ?? table.Columns.FirstOrDefault(c => c.IsIntegral);
            var name  = table.Columns.FirstOrDefault(c => c.IsText && !c.IsPrimaryKey)
                        ?? table.Columns.FirstOrDefault(c => c.IsText);

            var enumName = PascalCase(table.Name);
            if (!enumName.EndsWith("Enum", StringComparison.Ordinal))
                enumName += "Enum";

            return new EnumerationEntry(enumName, table.FullName,
                name == null ? string.Empty : name.Name,
                value == null ? string.Empty : value.Name,
                string.Empty);
        }

        /// <summary>order_status, ORDER_STATUS and OrderStatus all become OrderStatus; anything that is not a letter or digit is dropped.</summary>
        public static string PascalCase(string name)
        {
            var parts = Regex.Split(name ?? string.Empty, @"[^A-Za-z0-9]+").Where(p => p.Length > 0).ToList();

            if (parts.Count == 1 && parts[0].Any(char.IsLower))
                return char.ToUpperInvariant(parts[0][0]) + parts[0].Substring(1);

            var result = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
            return result.Length > 0 && char.IsDigit(result[0]) ? "_" + result : result;
        }
    }
}
