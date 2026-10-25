using System.Text.RegularExpressions;
using Efrpg;
using Efrpg.Templates;
using Generator.Tests.Common;
using Generator.Tests.Unit.DocSamples;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     EF6 reads a stored procedure with more than one result set through its own command, opening and closing
    ///     Database.Connection around it. A caller who already has that connection open - most often because they are
    ///     inside Database.BeginTransaction - must find it still open, and still in their transaction, afterwards (#845).
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class Ef6StoredProcConnectionTests
    {
        private StaticStateSnapshot _snapshot;
        private string _generated;

        [OneTimeSetUp]
        public void GenerateOnce()
        {
            _snapshot = StaticStateSnapshot.Capture();
            DocSampleRunner.Pristine = _snapshot;
            _generated = DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, () => Settings.TemplateType = TemplateType.Ef6);
        }

        [OneTimeTearDown]
        public void RestoreStaticState()
        {
            _snapshot.Restore();
        }

        [Test]
        public void GenerateCode_MultipleResultSetProc_OpensTheConnectionOnlyWhenItIsClosed()
        {
            var body = CourseReportBody();

            Assert.That(body, Does.Match(@"var wasOpen = Database\.Connection\.State == ConnectionState\.Open;\s+try\s+\{\s+" +
                                         @"if \(!wasOpen\)\s+DbInterception\.Dispatch\.Connection\.Open\("));
        }

        [Test]
        public void GenerateCode_MultipleResultSetProc_ClosesOnlyAConnectionItOpened()
        {
            var body = CourseReportBody();

            Assert.That(body, Does.Match(@"finally\s+\{\s+if \(!wasOpen\)\s+DbInterception\.Dispatch\.Connection\.Close\("));
        }

        [Test]
        public void GenerateCode_MultipleResultSetProc_EnlistsTheCommandInTheCurrentTransaction()
        {
            // SqlClient refuses to run a command without its transaction on a connection that has one pending.
            var body = CourseReportBody();

            Assert.That(body, Does.Contain("cmd.Transaction = Database.CurrentTransaction?.UnderlyingTransaction;"));
        }

        private string CourseReportBody()
        {
            var match = Regex.Match(_generated, @"public GetCourseReportReturnModel GetCourseReport\(int\? courseId = null\)\n\s+\{\n(?<body>.*?)\n        \}\n",
                RegexOptions.Singleline);
            Assert.That(match.Success, Is.True, "GetCourseReport was not generated, so there is nothing to check");
            return match.Groups["body"].Value;
        }
    }
}
