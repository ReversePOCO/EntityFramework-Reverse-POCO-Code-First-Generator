using Generator.Tests.Common;
using NUnit.Framework;
using V10EfrpgTest;
using Da = V10EfrpgTestDataAnnotations;

namespace Tester.Integration.EFCore10
{
    /// <summary>
    ///     Issue #888. EfrpgTest.tt retypes two return columns of EnumTest.GetDaysOfWeek as the generated DaysOfWeek
    ///     enum: EnumId through a definition scoped to the procedure, AlternateEnumId through a "*" definition. The
    ///     compile-time proof is that the assertions below type-check; the runtime proof is that EF Core materialises
    ///     the int result columns into the enum, including NULL into the nullable one.
    /// </summary>
    [TestFixture]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class StoredProcedureEnumReturnColumnTests
    {
        [SetUp]
        public void SetUp()
        {
            _db = new V10EfrpgTestDbContext();
        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();
        }

        private V10EfrpgTestDbContext _db = null!;

        [Test]
        public void GetDaysOfWeek_ProcedureScopedDefinition_MaterialisesEnumId()
        {
            // Arrange
            var expected = new[] { DaysOfWeek.Sun, DaysOfWeek.Mon, DaysOfWeek.Tue, DaysOfWeek.Wed, DaysOfWeek.Thu, DaysOfWeek.Fri, DaysOfWeek.Sat };

            // Act
            var rows = _db.EnumTest_GetDaysOfWeek();

            // Assert
            Assert.That(rows.Select(r => r.EnumId), Is.EqualTo(expected));
            Assert.That(rows.Single(r => r.EnumId == DaysOfWeek.Fri).TypeName, Is.EqualTo("Fri"));
        }

        [Test]
        public void GetDaysOfWeek_WildcardDefinitionOnNullableColumn_MaterialisesNullAsNullableEnum()
        {
            // Arrange
            DaysOfWeek? expected = null;

            // Act
            var rows = _db.EnumTest_GetDaysOfWeek();

            // Assert
            Assert.That(rows, Is.Not.Empty);
            Assert.That(rows.Select(r => r.AlternateEnumId), Is.All.EqualTo(expected));
        }

        [Test]
        public async Task GetDaysOfWeekAsync_ReturnsSameEnumValuesAsSync()
        {
            // Arrange
            var sync = _db.EnumTest_GetDaysOfWeek();

            // Act
            var async = await _db.EnumTest_GetDaysOfWeekAsync();

            // Assert
            Assert.That(async.Select(r => r.EnumId), Is.EqualTo(sync.Select(r => r.EnumId)));
        }

        [Test]
        public void GetDaysOfWeek_UseDataAnnotationsContext_MaterialisesEnumsTheSameWay()
        {
            // Arrange
            using var db = new Da.V10EfrpgTestDataAnnotationsDbContext();
            var expected = new[] { Da.DaysOfWeek.Sun, Da.DaysOfWeek.Mon, Da.DaysOfWeek.Tue, Da.DaysOfWeek.Wed, Da.DaysOfWeek.Thu, Da.DaysOfWeek.Fri, Da.DaysOfWeek.Sat };

            // Act
            var rows = db.EnumTest_GetDaysOfWeek();

            // Assert
            Assert.That(rows.Select(r => r.EnumId), Is.EqualTo(expected));
            Assert.That(rows.Select(r => r.AlternateEnumId), Is.All.Null);
        }
    }
}
