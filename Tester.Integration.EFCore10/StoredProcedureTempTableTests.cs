using System.Linq;
using System.Threading.Tasks;
using Generator.Tests.Common;
using NUnit.Framework;
using V10EfrpgTest;

namespace Tester.Integration.EFCore10
{
    /// <summary>
    ///     Issue #833. dbo.MultipleReturnColumnsFromTempTable selects from a temp table, so the generator cannot discover
    ///     its result set. EfrpgTest.tt maps it to TempTableResult with Settings.StoredProcedureReturnTypes, and the proof
    ///     is that the generated method returns the rows rather than an int.
    /// </summary>
    [TestFixture]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class StoredProcedureTempTableTests
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
        public void MultipleReturnColumnsFromTempTable_MappedReturnType_ReadsTheRows()
        {
            // Act
            var rows = _db.MultipleReturnColumnsFromTempTable();

            // Assert
            Assert.That(rows.Select(r => (r.Col1, r.Col2)), Is.EqualTo(new[] { (1, 2) }));
        }

        [Test]
        public async Task MultipleReturnColumnsFromTempTableAsync_MappedReturnType_ReadsTheRows()
        {
            // Act
            var rows = await _db.MultipleReturnColumnsFromTempTableAsync();

            // Assert
            Assert.That(rows.Select(r => (r.Col1, r.Col2)), Is.EqualTo(new[] { (1, 2) }));
        }
    }
}
