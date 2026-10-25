using System.Linq;
using Generator.Tests.Common;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using V10EfrpgTest;

namespace Tester.Integration.EFCore10
{
    /// <summary>
    ///     Issue #614. FK_Burak_Test1 is a composite foreign key from Burak1 (id_t, num) to the unique constraint
    ///     U_Burak2 (id, num) rather than to Burak2's primary key. (id_t, num) is not unique in Burak1, so one Burak2 can
    ///     have many Burak1, but it was generated one-to-one because the key it references is unique.
    /// </summary>
    [TestFixture]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class ForeignKeyToUniqueConstraintTests
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
        public void Burak2_TwoChildrenThroughACompositeForeignKeyToAUniqueConstraint_LoadsBoth()
        {
            // The context retries on failure, so its transaction has to run inside the execution strategy. It is never
            // committed, so the rows go when it is disposed.
            var (expected, loaded) = _db.Database.CreateExecutionStrategy().Execute(() =>
            {
                // Arrange - Burak1 also has FK_Burak_Test2 on (id, num), so each child's own id must be a Burak2 with its num
                using var transaction = _db.Database.BeginTransaction();
                var parentId = InsertBurak2(7);
                var secondId = InsertBurak2(7);
                _db.Database.ExecuteSql(
                    $"SET IDENTITY_INSERT dbo.Burak1 ON; INSERT dbo.Burak1 (id, id_t, num) VALUES ({parentId}, {parentId}, 7), ({secondId}, {parentId}, 7); SET IDENTITY_INSERT dbo.Burak1 OFF;");

                // Act
                var parent = _db.Burak2.Include(x => x.Burak1_IdT).Single(x => x.Id == parentId);

                return (new[] { parentId, secondId }, parent.Burak1_IdT.Select(x => x.Id).ToArray());
            });

            // Assert
            Assert.That(loaded, Is.EquivalentTo(expected));
        }

        private long InsertBurak2(long num)
        {
            return _db.Database.SqlQuery<long>($"INSERT dbo.Burak2 (num) OUTPUT INSERTED.id AS Value VALUES ({num})").AsEnumerable().Single();
        }
    }
}
