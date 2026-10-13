using System.Collections.Generic;
using System.Data;
using Efrpg;
using Efrpg.Filtering;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    [TestFixture]
    [Category(Constants.CI)]
    public class PeriodFilterTests
    {
        private DatabaseType _savedDatabaseType;

        [SetUp]
        public void SetUp()
        {
            _savedDatabaseType = Settings.DatabaseType;
        }

        [TearDown]
        public void TearDown()
        {
            Settings.DatabaseType = _savedDatabaseType;
        }

        private static StoredProcedure Routine(string name, bool isStoredProcedure)
        {
            return new StoredProcedure
            {
                Schema                 = new Schema("EFRPGTEST"),
                DbName                 = name,
                NameHumanCase          = name,
                IsStoredProcedure      = isStoredProcedure,
                IsScalarValuedFunction = !isStoredProcedure,
                ReturnModels           = new List<List<DataColumn>>()
            };
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IsExcluded_OraclePackageMember_IsKept(bool isStoredProcedure)
        {
            // Arrange - the efrpg tool reports an Oracle package member as PACKAGE.MEMBER, and both are callable.
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = new PeriodFilter();

            // Act
            var result = sut.IsExcluded(Routine("ORDER_PKG.TOUCH_PARENT", isStoredProcedure));

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsExcluded_SqlServerRoutineWithAPeriod_IsExcluded()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.SqlServer;
            var sut = new PeriodFilter();

            // Act
            var result = sut.IsExcluded(Routine("odd.name", true));

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsExcluded_OracleSchemaWithAPeriod_IsExcluded()
        {
            // Arrange
            Settings.DatabaseType = DatabaseType.Oracle;
            var sut = new PeriodFilter();

            // Act
            var result = sut.IsExcluded(new Schema("ODD.SCHEMA"));

            // Assert
            Assert.That(result, Is.True);
        }
    }
}
