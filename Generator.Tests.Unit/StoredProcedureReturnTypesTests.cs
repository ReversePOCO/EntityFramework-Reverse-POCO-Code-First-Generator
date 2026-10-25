using System;
using Efrpg;
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

        private static string Generate(Action configure)
        {
            return DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, configure);
        }
    }
}
