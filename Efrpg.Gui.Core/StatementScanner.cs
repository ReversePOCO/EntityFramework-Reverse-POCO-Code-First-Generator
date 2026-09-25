namespace Efrpg.Gui
{
    /// <summary>
    ///     Walks C# source one line at a time, tracking string, character and comment state, so that a statement's
    ///     terminating semicolon and its trailing // comment can be found without being fooled by a semicolon or a
    ///     double slash inside a literal.
    /// </summary>
    /// <remarks>
    ///     Database.tt is full of things that defeat a naive search. Help text contains URLs, so the first "//" on a
    ///     line is not always the comment. String defaults contain escaped paths. Most of the file is commented-out
    ///     example code, and those blocks contain braces and quotes that do not balance, so brace depth is only
    ///     meaningful if comments are skipped properly. Getting this wrong does not fail loudly - it silently drops
    ///     or mangles a setting - which is why it is a scanner rather than a regex.
    ///
    ///     Lives here rather than in BuildTT because both need it and neither may have its own copy: BuildTT reads
    ///     the settings to emit the metadata, and the settings editor reads the same shapes out of a customer's .tt
    ///     to decide what it may safely rewrite. BuildTT compiles this same file by link.
    /// </remarks>
    public class StatementScanner
    {
        private bool _inVerbatimString;
        private bool _inBlockComment;
        private int  _depth;

        /// <summary>
        ///     True once the semicolon that ends the statement has been seen at brace depth zero.
        /// </summary>
        public bool Finished { get; private set; }

        /// <summary>
        ///     Index of the terminating semicolon within the line last passed to <see cref="Feed" />, or -1 if the
        ///     statement did not end on that line.
        /// </summary>
        public int TerminatorIndex { get; private set; }

        /// <summary>
        ///     Index of the "//" that starts a line comment within the line last passed to <see cref="Feed" />, or -1
        ///     if that line has no comment outside a literal.
        /// </summary>
        public int LineCommentIndex { get; private set; }

        /// <summary>
        ///     Index of the last character within the line last passed to <see cref="Feed" /> that is code: not
        ///     whitespace and not part of a comment. A literal counts as code, whatever it contains. -1 when the line
        ///     has none. This is how an append finds the character a separating comma belongs after.
        /// </summary>
        public int LastCodeIndex { get; private set; }

        public void Feed(string line)
        {
            TerminatorIndex  = -1;
            LineCommentIndex = -1;
            LastCodeIndex    = -1;

            for (var i = 0; i < line.Length; i++)
            {
                var c    = line[i];
                var next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (_inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        _inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (_inVerbatimString)
                {
                    LastCodeIndex = i;
                    if (c != '"')
                        continue;

                    if (next == '"')
                    {
                        i++; // "" is an escaped quote inside a verbatim string
                        LastCodeIndex = i;
                    }
                    else
                        _inVerbatimString = false;
                    continue;
                }

                if (!char.IsWhiteSpace(c) && !(c == '/' && (next == '/' || next == '*')))
                    LastCodeIndex = i;

                switch (c)
                {
                    case '@':
                        if (next == '"')
                        {
                            _inVerbatimString = true;
                            i++;
                            LastCodeIndex = i;
                        }
                        break;

                    case '"':
                        i = SkipQuoted(line, i, '"');
                        LastCodeIndex = i < line.Length ? i : line.Length - 1;
                        break;

                    case '\'':
                        i = SkipQuoted(line, i, '\'');
                        LastCodeIndex = i < line.Length ? i : line.Length - 1;
                        break;

                    case '/':
                        if (next == '/')
                        {
                            LineCommentIndex = i;
                            return; // The rest of the line is comment, and cannot affect depth or termination.
                        }
                        if (next == '*')
                        {
                            _inBlockComment = true;
                            i++;
                        }
                        break;

                    case '(':
                    case '[':
                    case '{':
                        _depth++;
                        break;

                    case ')':
                    case ']':
                    case '}':
                        _depth--;
                        break;

                    case ';':
                        if (_depth <= 0 && !Finished)
                        {
                            Finished        = true;
                            TerminatorIndex = i;
                        }
                        break;
                }
            }
        }

        /// <summary>
        ///     Returns the index of the closing quote, or the end of the line when the literal is not closed on this
        ///     line. Backslash escapes are honoured so that "\"" does not end early.
        /// </summary>
        private static int SkipQuoted(string line, int openingQuote, char quote)
        {
            for (var i = openingQuote + 1; i < line.Length; i++)
            {
                if (line[i] == '\\')
                {
                    i++;
                    continue;
                }
                if (line[i] == quote)
                    return i;
            }

            return line.Length;
        }
    }
}
