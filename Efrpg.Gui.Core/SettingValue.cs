using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Efrpg.Gui
{
    /// <summary>
    ///     Turns the right-hand side of a settings assignment into something a form can show, and back again.
    /// </summary>
    /// <remarks>
    ///     Every read can fail, and failing is the useful answer. A setting the metadata calls a string may well
    ///     hold an identifier, a concatenation or a call in a real template - <c>Settings.Namespace</c> ships as the
    ///     bare <c>DefaultNamespace</c> - and the editor has to notice that and leave it alone rather than replace
    ///     working code with a quoted approximation of it.
    /// </remarks>
    public static class SettingValue
    {
        private static readonly Regex Regular  = new Regex(@"^""(?<value>(?:[^""\\]|\\.)*)""$", RegexOptions.Singleline);
        private static readonly Regex Verbatim = new Regex(@"^@""(?<value>(?:[^""]|"""")*)""$", RegexOptions.Singleline);
        private static readonly Regex Member   = new Regex(@"^[A-Za-z_]\w*\.(?<member>[A-Za-z_]\w*)$");
        private static readonly Regex Character = new Regex(@"^'(?<value>\\.|[^'])'$");

        /// <summary>The right-hand side of a string setting that is switched off, such as <c>Settings.TableSuffix = null;</c>.</summary>
        public const string Null = "null";

        public static bool IsNull(string rhs)
        {
            return (rhs ?? string.Empty).Trim() == Null;
        }

        /// <summary>
        ///     Reads a string literal, regular or verbatim, or <c>null</c>, which reads as empty. False for anything
        ///     else, including <c>string.Empty</c> and <c>Path.Combine(...)</c>, which are code and stay code.
        /// </summary>
        public static bool TryReadText(string rhs, out string value, out bool isVerbatim)
        {
            value      = null;
            isVerbatim = false;

            var trimmed = (rhs ?? string.Empty).Trim();

            if (trimmed == Null)
            {
                value = string.Empty;
                return true;
            }

            var verbatim = Verbatim.Match(trimmed);
            if (verbatim.Success)
            {
                value      = verbatim.Groups["value"].Value.Replace("\"\"", "\"");
                isVerbatim = true;
                return true;
            }

            var regular = Regular.Match(trimmed);
            if (!regular.Success)
                return false;

            value = Unescape(regular.Groups["value"].Value);
            return true;
        }

        /// <summary>
        ///     Writes a string literal, keeping the form the template already used. A template written with
        ///     <c>@""</c> folder paths stays that way, so the diff is the value and not the style.
        /// </summary>
        public static string WriteText(string value, bool verbatim)
        {
            value = value ?? string.Empty;

            // A verbatim literal cannot express a newline escape and does not need to; it can hold the real
            // newline. But a value with one would turn a single line into several and break the one-line
            // guarantee, so those fall back to a regular literal.
            if (verbatim && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0)
                return "@\"" + value.Replace("\"", "\"\"") + "\"";

            return "\"" + Escape(value) + "\"";
        }

        private static readonly Regex ListHead = new Regex(
            @"^new\s+(?<form>List\s*<\s*string\s*>|string\s*\[\s*\]|\[\s*\])\s*(?:\(\s*\))?\s*(?:\{(?<body>.*)\})?\s*$", RegexOptions.Singleline);

        private static readonly Regex EmptyArray = new Regex(@"^new\s+string\s*\[\s*0\s*\]$");

        private static readonly Regex Literal = new Regex(@"@""(?:[^""]|"""")*""|""(?:[^""\\]|\\.)*""", RegexOptions.Singleline);

        /// <summary>
        ///     Reads a list or array of string literals: <c>new List&lt;string&gt;()</c>, <c>new string[0]</c>, or
        ///     either with an initialiser. Comments inside the initialiser are ignored, so the shipped
        ///     <c>// "JsonIgnore"</c> example does not become an item. Anything that is not a literal - a call, a
        ///     variable - fails the read, and the setting stays read-only.
        /// </summary>
        public static bool TryReadStringList(string rhs, out IReadOnlyList<string> items, out StringListForm form)
        {
            items = null;
            form  = StringListForm.List;

            var text = StripComments(rhs ?? string.Empty).Trim();

            if (EmptyArray.IsMatch(text))
            {
                items = new string[0];
                form  = StringListForm.Array;
                return true;
            }

            var head = ListHead.Match(text);
            if (!head.Success)
                return false;

            form = head.Groups["form"].Value.StartsWith("List", StringComparison.Ordinal) ? StringListForm.List : StringListForm.Array;

            var list = new List<string>();
            var body = head.Groups["body"].Success ? head.Groups["body"].Value : string.Empty;

            // Every literal is an item; whatever is left after removing them must be commas and whitespace only.
            var remainder = Literal.Replace(body, match =>
            {
                string value;
                bool verbatim;
                TryReadText(match.Value, out value, out verbatim);
                list.Add(value);
                return string.Empty;
            });

            if (remainder.Trim(' ', '\t', '\r', '\n', ',').Length > 0)
                return false;

            items = list;
            return true;
        }

        /// <summary>
        ///     Writes the list in the form the file used: on one line when it is short, otherwise one item per
        ///     line inside braces indented for the assignment it belongs to.
        /// </summary>
        public static string WriteStringList(IEnumerable<string> items, StringListForm form, string indent, string newLine)
        {
            var values = (items ?? new string[0]).Where(i => i != null).ToList();
            var head   = form == StringListForm.List ? "new List<string>" : "new string[]";

            if (values.Count == 0)
                return form == StringListForm.List ? "new List<string>()" : "new string[0]";

            var literals = values.Select(v => WriteText(v, false)).ToList();
            var oneLine  = head + " { " + string.Join(", ", literals) + " }";

            if (oneLine.Length <= 110)
                return oneLine;

            indent  = indent ?? string.Empty;
            newLine = string.IsNullOrEmpty(newLine) ? "\r\n" : newLine;

            return head + newLine + indent + "{" + newLine +
                   string.Join("," + newLine, literals.Select(l => indent + "    " + l)) + newLine +
                   indent + "}";
        }

        // Literals are matched alongside the comments and put back untouched, so a // or /* inside a value - a URL, a
        // pattern - is not taken for a comment. Whichever starts first wins, which is the C# rule.
        private static readonly Regex LiteralOrComment = new Regex(
            @"(?<literal>@""(?:[^""]|"""")*""|""(?:[^""\\]|\\.)*"")|/\*.*?\*/|//[^\r\n]*", RegexOptions.Singleline);

        private static string StripComments(string text)
        {
            return LiteralOrComment.Replace(text, match => match.Groups["literal"].Success ? match.Value : string.Empty);
        }

        public static bool TryReadBoolean(string rhs, out bool value)
        {
            var trimmed = (rhs ?? string.Empty).Trim();

            value = trimmed == "true";
            return trimmed == "true" || trimmed == "false";
        }

        public static string WriteBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        public static bool TryReadNumber(string rhs, out int value)
        {
            return int.TryParse((rhs ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out value);
        }

        public static string WriteNumber(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryReadCharacter(string rhs, out string value)
        {
            var match = Character.Match((rhs ?? string.Empty).Trim());

            value = match.Success ? Unescape(match.Groups["value"].Value) : null;
            return match.Success;
        }

        public static string WriteCharacter(string value)
        {
            var first = string.IsNullOrEmpty(value) ? ' ' : value[0];

            switch (first)
            {
                case '\'': return @"'\''";
                case '\\': return @"'\\'";
                case '\r': return @"'\r'";
                case '\n': return @"'\n'";
                case '\t': return @"'\t'";
                default:   return "'" + first + "'";
            }
        }

        /// <summary>
        ///     Reads <c>DatabaseType.SqlServer</c>, or for a flags setting <c>Elements.Poco | Elements.Context</c>,
        ///     as the list of member names. Every part must be a member the metadata knows, so a combination
        ///     including something computed is refused whole rather than half understood.
        /// </summary>
        public static bool TryReadEnum(string rhs, SettingDefinition definition, out IReadOnlyList<string> members)
        {
            members = null;

            if (definition == null)
                return false;

            var parts = (rhs ?? string.Empty).Split('|').Select(p => p.Trim()).ToList();

            if (parts.Count > 1 && !definition.IsFlags)
                return false;

            var names = new List<string>();

            foreach (var part in parts)
            {
                var match = Member.Match(part);
                if (!match.Success)
                    return false;

                var name = match.Groups["member"].Value;
                if (definition.FindMember(name) == null)
                    return false;

                names.Add(name);
            }

            members = names;
            return names.Count > 0;
        }

        /// <summary>
        ///     Writes <c>Type.Member</c>, or the members joined with <c>|</c> for a flags setting. The type name
        ///     comes from the metadata, so it matches what the generator declares rather than what happened to be
        ///     in the file.
        /// </summary>
        public static string WriteEnum(SettingDefinition definition, IEnumerable<string> members)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var names = (members ?? new string[0]).ToList();

            if (names.Count == 0)
                throw new ArgumentException("An enum setting needs at least one member.", nameof(members));

            return string.Join(" | ", names.Select(n => definition.Type + "." + n).ToArray());
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            var result = new StringBuilder(value.Length);

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    result.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case 'r': result.Append('\r'); break;
                    case 'n': result.Append('\n'); break;
                    case 't': result.Append('\t'); break;
                    case '0': result.Append('\0'); break;
                    default:  result.Append(value[i]); break;
                }
            }

            return result.ToString();
        }
    }
}
