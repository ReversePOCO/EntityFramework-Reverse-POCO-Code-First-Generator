using System;
using System.IO;
using Efrpg;
using Efrpg.Templates;
using Generator.Tests.Common;
using Generator.Tests.Unit.DocSamples;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     Settings.StoredProcedureReturnTypes points a stored procedure at a type that already exists instead of a
    ///     generated ReturnModel.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class StoredProcedureReturnTypesTests
    {
        private StaticStateSnapshot _snapshot;

        [OneTimeSetUp]
        public void CaptureStaticState()
        {
            _snapshot = StaticStateSnapshot.Capture();
            DocSampleRunner.Pristine = _snapshot;
        }

        [OneTimeTearDown]
        public void RestoreStaticState()
        {
            _snapshot.Restore();
        }

        [Test]
        public void GenerateCode_ProcMappedToAnEntity_DoesNotMarkTheEntityKeyless()
        {
            // #754 - EF Core then fails to build the model: "cannot be marked as keyless because it contains a key".
            var generated = Generate(() => Settings.StoredProcedureReturnTypes.Add("GetStudentsByCourse", "Student"));

            Assert.That(generated, Does.Not.Contain("modelBuilder.Entity<Student>()"));
        }

        [Test]
        public void GenerateCode_ProcMappedToAnEntity_StillReturnsThatEntity()
        {
            var generated = Generate(() => Settings.StoredProcedureReturnTypes.Add("GetStudentsByCourse", "Student"));

            Assert.That(generated, Does.Contain("List<Student> GetStudentsByCourse(int? courseId);"));
        }

        [Test]
        public void GenerateCode_ProcMappedToATypeThatIsNotAnEntity_RegistersItAsKeyless()
        {
            // A hand-written result class is in the model only because this line puts it there; Set<T>() needs it.
            var generated = Generate(() => Settings.StoredProcedureReturnTypes.Add("GetStudentsByCourse", "StudentSummary"));

            Assert.That(generated, Does.Contain("modelBuilder.Entity<StudentSummary>().HasNoKey();"));
        }

        [Test]
        public void GenerateCode_ProcNotMapped_RegistersItsReturnModelAsKeyless()
        {
            var generated = Generate(() => { });

            Assert.That(generated, Does.Contain("modelBuilder.Entity<GetStudentsByCourseReturnModel>().HasNoKey();"));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadIsMapped_ReturnsAListOfTheMappedType()
        {
            // #709 - the mapping was ignored whenever discovery came back empty, which is exactly when it is needed.
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () => Settings.StoredProcedureReturnTypes.Add("GetLegacyReport", "LegacyReport"));

            Assert.That(generated, Does.Contain("List<LegacyReport> GetLegacyReport(int? courseId);"));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadIsMapped_KeepsTheProcResultOverload()
        {
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () => Settings.StoredProcedureReturnTypes.Add("GetLegacyReport", "LegacyReport"));

            Assert.That(generated, Does.Contain("List<LegacyReport> GetLegacyReport(int? courseId, out int procResult);"));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadIsMapped_ReadsTheRowsAsTheMappedType()
        {
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () => Settings.StoredProcedureReturnTypes.Add("GetLegacyReport", "LegacyReport"));

            Assert.That(generated, Does.Contain("Set<LegacyReport>()").And.Contain("modelBuilder.Entity<LegacyReport>().HasNoKey();"));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadIsMapped_DropsTheUnableToDetermineComment()
        {
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () => Settings.StoredProcedureReturnTypes.Add("GetLegacyReport", "LegacyReport"));

            Assert.That(generated, Does.Not.Contain("Unable to determine return model for 'GetLegacyReport'"));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadIsMappedOnEf6_ReadsTheRowsAsTheMappedType()
        {
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () =>
            {
                Settings.TemplateType = TemplateType.Ef6;
                Settings.StoredProcedureReturnTypes.Add("GetLegacyReport", "LegacyReport");
            });

            Assert.That(generated, Does.Contain("Database.SqlQuery<LegacyReport>("));
        }

        [Test]
        public void GenerateCode_ProcWhoseResultSetCouldNotBeReadNotMapped_StillReturnsInt()
        {
            var generated = GenerateWithProc(LegacyReportDiscoveryFailed, () => { });

            Assert.That(generated, Does.Contain("int GetLegacyReport(int? courseId);"));
        }

        [Test]
        public void GenerateCode_TempTableProcMapped_ReturnsAListOfTheMappedType()
        {
            // #833 - nothing in the payload says what the procedure returns, so the mapping is the only source of it.
            var generated = GenerateWithProc(TestSp2SelectsFromATempTable, () => Settings.StoredProcedureReturnTypes.Add("Test_SP2", "TestSp2Result"));

            Assert.That(generated, Does.Contain("List<TestSp2Result> TestSp2();")
                .And.Contain("Task<List<TestSp2Result>> TestSp2Async(CancellationToken cancellationToken = default(CancellationToken));"));
        }

        /// <summary>
        ///     What the efrpg tool sends for a procedure that selects from a temp table. SET FMTONLY never creates the
        ///     table, so both the schema-only reader and FillSchema fail with "Invalid object name '#temp'", and the tool
        ///     swallows that: no result set, and no error either.
        /// </summary>
        private const string TestSp2SelectsFromATempTable =
            @"<Row schema=""dbo"" name=""Test_SP2"" isTableValuedFunction=""false"" isScalarValuedFunction=""false"" isStoredProcedure=""true"" returnModelsRead=""true"" returnModelError="""" />";

        /// <summary>
        ///     What the efrpg tool sends when SET FMTONLY discovery throws: no result set, and the message.
        /// </summary>
        private const string LegacyReportDiscoveryFailed =
            @"<Row schema=""dbo"" name=""GetLegacyReport"" isTableValuedFunction=""false"" isScalarValuedFunction=""false"" isStoredProcedure=""true"" returnModelsRead=""true"" returnModelError=""Invalid column name 'RankPosCW'."">
                <Parameter ordinal=""1"" mode=""In"" name=""@CourseId"" nameHumanCase="""" dataType=""int"" returnDataType="""" sqlDbType=""Int"" returnSqlDbType=""VarChar"" propertyType=""int"" returnPropertyType=""string"" userDefinedTypeName="""" dateTimePrecision=""0"" maxLength=""0"" precision=""10"" scale=""0"" isSpatial=""false"" hasDefault=""false"" />
              </Row>";

        private static string Generate(Action configure)
        {
            return DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, configure);
        }

        private static string GenerateWithProc(string procRow, Action configure)
        {
            var payload = File.ReadAllText(DocSampleRunner.SchemaPath(DocSampleRunner.Schema.Extras))
                .Replace("</StoredProcedures>", procRow + "</StoredProcedures>");
            return DocSampleRunner.GenerateFromPayload(payload, configure);
        }
    }
}
