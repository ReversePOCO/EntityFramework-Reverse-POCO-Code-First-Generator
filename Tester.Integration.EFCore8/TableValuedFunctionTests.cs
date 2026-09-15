using Generator.Tests.Common;
using NUnit.Framework;
using System.Linq;
using V8EfrpgTest;

namespace Tester.Integration.EFCore8
{
    [TestFixture]
    [Category(Constants.Integration)]
    [Category(Constants.DbType.SqlServer)]
    public class TableValuedFunctionTests
    {
        [Test]
        public void Standard_test()
        {
            // Arrange
            using var db = new TestDatabaseStandard.TestDbContext();

            // Act
            var data = db.CsvToInt("123,456", "").ToList();

            // Assert
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual(123, data[0].IntValue);
            Assert.AreEqual(456, data[1].IntValue);
        }

        [Test]
        public void Standard_FunctionCanBeCalledWithinQueryThroughInterfaceReference()
        {
            // Arrange
            using var db = (TestDatabaseStandard.ITestDbContext) new TestDatabaseStandard.TestDbContext();

            // Act
            var data = db.CsvToInt("123,456", "").ToList();
            
            /* EF6 test
             var data = (from f in db.Stafford_Foos
                        let i = db.CsvToInt("123,456", "")
                        select new { f, i })
                    .ToList()
                    .SelectMany(d => d.i)
                    .ToList();*/

            // Assert
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual(123, data[0].IntValue);
            Assert.AreEqual(456, data[1].IntValue);
        }

        [Test]
        public void Standard_FunctionInNonDefaultSchemaCanBeCalled()
        {
            // Arrange
            using var db = new TestDatabaseStandard.TestDbContext();

            // Act
            var data = db.CustomSchema_CsvToIntWithSchema("123,456", "").ToList();

            // Assert
            Assert.AreEqual(2, data.Count);
            Assert.AreEqual(123, data[0].IntValue);
            Assert.AreEqual(456, data[1].IntValue);
        }

        [Test]
        [Description("Issue #721 - TVF returning columns whose names contain spaces should map correctly")]
        public void SpacedColumnTvf_ColumnsAreMappedCorrectly()
        {
            // Arrange
            using var db = new V8EfrpgTestDbContext();

            // Act - [dbo].[SpacedColumnTvf] returns [My Column] and [Is Active]
            var data = db.SpacedColumnTvf(1).ToList();

            // Assert - verify the spaced column names were mapped to valid C# properties
            Assert.IsNotNull(data);
            Assert.AreEqual(1, data.Count, "Expected one row with Id=1 from NoPrimaryKeys");
            Assert.AreEqual("test value", data[0].MyColumn, "MyColumn (mapped from 'My Column') should be 'test value'");
            Assert.AreEqual(true, data[0].IsActive, "IsActive (mapped from 'Is Active') should be true");
        }

        [Test]
        [Description("Under AllowNullStrings a null TVF argument reaches FromSqlRaw as DBNull.Value; [dbo].[182_test1] ignores @test")]
        public void C182Test1_NullArgument_ReturnsEveryRow()
        {
            // Arrange
            using var db = new V8EfrpgTestDbContext();

            // Act
            var data = db.C182Test1(null).ToList();

            // Assert
            Assert.That(data, Is.Not.Empty);
        }
    }
}
