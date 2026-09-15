using Efrpg;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    /// <summary>
    ///     A DB-nullable reference-type column that is not a string - spatial, hierarchyid, sql_variant - used to keep its
    ///     bare type under AllowNullStrings. The file carries '#nullable enable', so a POCO with a constructor raised
    ///     CS8618 for every such property: nothing sets it and the type says it cannot be null.
    /// </summary>
    [TestFixture]
    [Category(Constants.CI)]
    public class ColumnNullabilityTests
    {
        [TearDown]
        public void TearDown()
        {
            // Settings is static, so anything set here leaks into whichever test runs next unless it is put back.
            Settings.AllowNullStrings = false;
        }

        [TestCase(true,  true,  "NetTopologySuite.Geometries.Point?")]
        [TestCase(true,  false, "NetTopologySuite.Geometries.Point")]
        [TestCase(false, true,  "NetTopologySuite.Geometries.Point")]
        public void WrapIfNullable_SpatialColumn_AnnotatedOnlyWhenNullableUnderAllowNullStrings(bool allowNullStrings, bool isNullable, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            var column = new Column { PropertyType = "NetTopologySuite.Geometries.Point", IsNullable = isNullable };

            // Act
            var result = column.WrapIfNullable();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
