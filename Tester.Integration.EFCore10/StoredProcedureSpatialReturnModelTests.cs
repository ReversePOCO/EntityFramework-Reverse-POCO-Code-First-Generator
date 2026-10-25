using System.Linq;
using System.Threading.Tasks;
using Generator.Tests.Common;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using V10EfrpgTest;

namespace Tester.Integration.EFCore10
{
    /// <summary>
    ///     dbo.SpatialTypesNoParams returns geography and geometry columns. The efrpg tool runs on .NET 10, where
    ///     FillSchema cannot load Microsoft.SqlServer.Types, and up to efrpg 1.3.0 the exception was discarded, so the
    ///     procedure generated as a method returning int. The proof that its result set is discovered again is that the
    ///     generated method reads the row in ColumnNameAndTypes back as NetTopologySuite types.
    /// </summary>
    [TestFixture]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class StoredProcedureSpatialReturnModelTests
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
        public void SpatialTypesNoParams_GeographyAndGeometryColumns_ReadsTheRow()
        {
            // Act
            var rows = _db.SpatialTypesNoParams();

            // Assert
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].GeographyType.Coordinate, Is.EqualTo(new Coordinate(0, 0)));
            Assert.That(rows[0].GeometryType, Is.InstanceOf<LineString>().With.Property(nameof(LineString.NumPoints)).EqualTo(3));
        }

        [Test]
        public async Task SpatialTypesNoParamsAsync_GeographyAndGeometryColumns_ReadsTheRow()
        {
            // Act
            var rows = await _db.SpatialTypesNoParamsAsync();

            // Assert
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].GeographyType.Coordinate, Is.EqualTo(new Coordinate(0, 0)));
        }
    }
}
