using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Efrpg
{
    public static class NamingHelper
    {
        private static readonly Regex RemoveNonAlphaNumeric = new Regex(@"[^\w\d\s_-]", RegexOptions.Compiled);
        private static readonly Regex RemoveTrailingSymbols = new Regex(@"[$-/:-?{-~!""^_`\[\]]+$", RegexOptions.Compiled);

        public static readonly List<string> ReservedKeywords = new List<string>
        {
            // C#
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "volatile",
            "void", "while",

            // .NET
            "Task"
        };

        /// <summary>
        ///     Appends the lowest numeric suffix that makes <paramref name="name" /> unique among
        ///     <paramref name="taken" />, or returns it unchanged when it already is.
        /// </summary>
        /// <remarks>
        ///     The comparison is ordinal, because C# identifiers are case sensitive: two names differing only by
        ///     case are legal as separate members and do not need disambiguating.
        /// </remarks>
        public static string MakeUnique(string name, ICollection<string> taken)
        {
            if (taken == null || !taken.Contains(name))
                return name;

            var suffix = 1;
            string candidate;
            do
            {
                candidate = name + suffix++;
            } while (taken.Contains(candidate));

            return candidate;
        }

        /// <summary>
        ///     A schema name made fit to prefix a C# identifier, as PrependSchemaName does. The raw name can hold
        ///     anything a quoted identifier can, such as PostgreSQL's "Mixed Case Schema", which produced a class
        ///     named Mixed Case Schema_SpacedTableName. Tables, routines and foreign keys must all use this, or a
        ///     relationship stops finding the entity it points at.
        /// </summary>
        public static string CleanSchemaName(string schema)
        {
            return CleanUp(schema).Replace(" ", string.Empty).Replace("$", string.Empty);
        }

        public static readonly Func<string, string> CleanUp = (str) =>
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            if (str.Any(char.IsLetterOrDigit))
                str = RemoveTrailingSymbols.Replace(str.Replace('-', '_').Replace('.', '_'), string.Empty);
            var len = str.Length;
            if (len == 0)
                return string.Empty;

            var sb = new StringBuilder(len + 20);
            var replacedCharacter = false;
            for (var n = 0; n < len; ++n)
            {
                var c = str[n];
                if (c != '_' && c != '-' && (char.IsSymbol(c) || char.IsPunctuation(c)))
                {
                    int ascii = c;
                    sb.AppendFormat("{0}", ascii);
                    replacedCharacter = true;
                    continue;
                }
                sb.Append(c);
            }
            if (replacedCharacter)
                str = sb.ToString();

            str = RemoveNonAlphaNumeric.Replace(str, string.Empty);
            if (char.IsDigit(str[0]))
                str = "C" + str;

            return str;
        };

        public static string ExtractSqlServerParamDefault(string definition, string paramName)
        {
            if (string.IsNullOrEmpty(definition) || string.IsNullOrEmpty(paramName))
                return null;

            var pattern = Regex.Escape(paramName)
                + @"\b\s+(?:[^=,@()]|\([^)]*\))*=\s*('(?:[^']|'')*'|NULL|[-+]?\d[\d.]*(?:[eE][+-]?\d+)?)";

            var match = Regex.Match(definition, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static string NormaliseParamDefault(string rawDefault)
        {
            if (rawDefault == null)
                return null;

            var s = rawDefault.Trim();

            while (s.Length >= 2 && s.StartsWith("(") && s.EndsWith(")"))
                s = s.Substring(1, s.Length - 2).Trim();

            var castIdx = s.IndexOf("::", StringComparison.Ordinal);
            if (castIdx >= 0)
                s = s.Substring(0, castIdx).Trim();

            if (s.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return null;

            return s.Length > 0 ? s : null;
        }
    }
}
