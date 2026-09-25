using Efrpg.Gui;
using NUnit.Framework;

namespace Efrpg.Gui.Tests
{
    /// <summary>
    ///     LastCodeIndex is how an append finds the character a comma belongs after, so anything that only looks like
    ///     code - a comment, or whitespace - must not count, and anything inside a literal must.
    /// </summary>
    [TestFixture]
    public class StatementScannerTests
    {
        [TestCase("        }", 8, TestName = "LastCodeIndex is the brace ending an entry")]
        [TestCase("        } // the only one", 8, TestName = "LastCodeIndex ignores a trailing line comment")]
        [TestCase("        } /* note */   ", 8, TestName = "LastCodeIndex ignores a trailing block comment and whitespace")]
        [TestCase("    Name = \"a // not a comment\"", 30, TestName = "LastCodeIndex treats slashes inside a string as code")]
        [TestCase("    Path = @\"C:\\temp\\\"", 21, TestName = "LastCodeIndex treats a verbatim string's closing quote as code")]
        [TestCase("    // new EnumerationSettings", -1, TestName = "LastCodeIndex is minus one for a comment-only line")]
        [TestCase("", -1, TestName = "LastCodeIndex is minus one for an empty line")]
        public void LastCodeIndex_OneLine_IsTheLastCharacterThatIsCode(string line, int expected)
        {
            var scanner = new StatementScanner();

            scanner.Feed(line);

            Assert.That(scanner.LastCodeIndex, Is.EqualTo(expected));
        }

        [Test]
        public void LastCodeIndex_InsideABlockCommentSpanningLines_IgnoresTheCommentedOutCode()
        {
            var scanner = new StatementScanner();
            scanner.Feed("    {");
            scanner.Feed("        /*new EnumerationSettings");

            scanner.Feed("        }*/");

            Assert.That(scanner.LastCodeIndex, Is.EqualTo(-1));
        }
    }
}
