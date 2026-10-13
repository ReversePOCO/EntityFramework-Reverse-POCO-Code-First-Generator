using System;
using System.Collections.Generic;
using Efrpg;
using Efrpg.FileManagement;
using Efrpg.Generators;
using Efrpg.Readers;
using Generator.Tests.Common;
using NUnit.Framework;

namespace Generator.Tests.Unit
{
    using Efrpg.Templates;

    [TestFixture]
    [Category(Constants.CI)]
    public class IndexModelBuilderTests
    {
        private Table _orderDetails;

        [OneTimeSetUp]
        public void SetUp()
        {
            _orderDetails = new Table(null, new Schema("dbo"), "order details", false)
            {
                NameHumanCase = "OrderDetails",
                Indexes = new List<RawIndex>
                {
                    new RawIndex("dbo", "Order Details", "PK_Order_Details",      1, "OrderID",   2, true,  true,  false, true),
                    new RawIndex("dbo", "Order Details", "OrderID",               1, "OrderID",   1, false, false, false, false),
                    new RawIndex("dbo", "Order Details", "OrdersOrder_Details",   1, "OrderID",   1, false, false, false, false),
                    new RawIndex("dbo", "Order Details", "PK_Order_Details",      2, "ProductID", 2, true,  true,  false, true),
                    new RawIndex("dbo", "Order Details", "ProductID",             1, "ProductID", 1, false, false, false, false),
                    new RawIndex("dbo", "Order Details", "ProductsOrder_Details", 1, "ProductID", 1, false, false, false, false),
                    new RawIndex("dbo", "Order Details", "PK_test",               1, "xxx",       1, true,  true,  false, true)
                }
            };

            var orderId = new Column
            {
                ParentTable = _orderDetails,
                DbName = "OrderID",
                NameHumanCase = "OrderID",
                Config = "test",
                Indexes = new List<RawIndex>
                {
                    _orderDetails.Indexes[0],// new RawIndex("dbo", "Order Details", "PK_Order_Details",    1, "OrderID", 2, true,  true,  false, true),
                    _orderDetails.Indexes[1],// new RawIndex("dbo", "Order Details", "OrderID",             1, "OrderID", 1, false, false, false, false),
                    _orderDetails.Indexes[2],// new RawIndex("dbo", "Order Details", "OrdersOrder_Details", 1, "OrderID", 1, false, false, false, false)
                }
            };
            _orderDetails.Columns.Add(orderId);

            var productId = new Column
            {
                ParentTable = _orderDetails,
                DbName = "ProductID",
                NameHumanCase = "ProductID",
                Config = "test",
                IsNullable = true,
                Indexes = new List<RawIndex>
                {
                    _orderDetails.Indexes[3],//new RawIndex("dbo", "Order Details", "PK_Order_Details",      2, "ProductID", 2, true,  true,  false, true),
                    _orderDetails.Indexes[4],//new RawIndex("dbo", "Order Details", "ProductID",             1, "ProductID", 1, false, false, false, false),
                    _orderDetails.Indexes[5] //new RawIndex("dbo", "Order Details", "ProductsOrder_Details", 1, "ProductID", 1, false, false, false, false)
                }
            };
            _orderDetails.Columns.Add(productId);

            var test = new Column
            {
                ParentTable = _orderDetails,
                DbName = "xxx",
                NameHumanCase = "test",
                IsNullable = true,
                Config = "test",
                Indexes = new List<RawIndex>
                {
                    _orderDetails.Indexes[6] //new RawIndex("dbo", "Order Details", "PK_test", 1, "xxx", 1, true, true, false, true)
                }
            };
            _orderDetails.Columns.Add(test);
        }

        [Test]
        public void Ef6ModelBuilder()
        {
            // Arrange
            var fileManagement = new FileManagementService(new GeneratedTextTransformation());
            var sut = new GeneratorEf6(fileManagement);

            // Act
            var list = new List<string>();
            foreach (var column in _orderDetails.Columns)
            {
                var s = sut.IndexModelBuilder(column);
                if(!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
                TestContext.Out.WriteLine(s);
            }

            // Assert
            Assert.AreEqual(2, list.Count);

            Assert.AreEqual(@"modelBuilder.Entity<OrderDetails>()
            .Property(e => e.OrderID)
            .HasColumnAnnotation(
                IndexAnnotation.AnnotationName,
                new IndexAnnotation(new[]
                {
                    new IndexAttribute(""OrderID"", 1),
                    new IndexAttribute(""OrdersOrder_Details"", 1)
                }));", list[0].Trim());

            Assert.AreEqual(@"modelBuilder.Entity<OrderDetails>()
            .Property(e => e.ProductID)
            .HasColumnAnnotation(
                IndexAnnotation.AnnotationName,
                new IndexAnnotation(new[]
                {
                    new IndexAttribute(""ProductID"", 1),
                    new IndexAttribute(""ProductsOrder_Details"", 1)
                }));", list[1].Trim());
        }

        [Test]
        [TestCase(TemplateType.Ef6,     "HasName")]
        [TestCase(TemplateType.EfCore8, "HasDatabaseName")]
        [TestCase(TemplateType.EfCore9, "HasDatabaseName")]
        public void EfCoreModelBuilder(TemplateType templateType, string hasName)
        {
            // Arrange
            var fileManagement = new FileManagementService(new GeneratedTextTransformation());
            var sut = new GeneratorEfCore(fileManagement);
            Settings.TemplateType = templateType;

            // Act
            var list = sut.IndexModelBuilder(_orderDetails);
            foreach (var str in list)
                TestContext.Out.WriteLine(str);

            // Assert
            Assert.AreEqual(4, list.Count);

            Assert.AreEqual($@"builder.HasIndex(x => x.OrderID).{hasName}(""OrderID"");",                 list[0]);
            Assert.AreEqual($@"builder.HasIndex(x => x.OrderID).{hasName}(""OrdersOrder_Details"");",     list[1]);
            Assert.AreEqual($@"builder.HasIndex(x => x.ProductID).{hasName}(""ProductID"");",             list[2]);
            Assert.AreEqual($@"builder.HasIndex(x => x.ProductID).{hasName}(""ProductsOrder_Details"");", list[3]);
        }

        [Test]
        [TestCase(TemplateType.EfCore8, "HasDatabaseName")]
        [TestCase(TemplateType.EfCore9, "HasDatabaseName")]
        public void EfCoreModelBuilder_WithIncludeColumns(TemplateType templateType, string hasName)
        {
            // Arrange
            var fileManagement = new FileManagementService(new GeneratedTextTransformation());
            var sut = new GeneratorEfCore(fileManagement);
            Settings.TemplateType = templateType;

            var table = new Table(null, new Schema("dbo"), "orders", false)
            {
                NameHumanCase = "Orders",
                Indexes = new List<RawIndex>
                {
                    new RawIndex("dbo", "orders", "IX_Orders_Customer", 1, "CustomerID", 1, false, false, false, false, "", "OrderDate,Amount"),
                }
            };

            var customerId = new Column
            {
                ParentTable = table,
                DbName = "CustomerID",
                NameHumanCase = "CustomerId",
                Config = "test",
            };
            table.Columns.Add(customerId);

            var orderDate = new Column
            {
                ParentTable = table,
                DbName = "OrderDate",
                NameHumanCase = "OrderDate",
                Config = "test",
            };
            table.Columns.Add(orderDate);

            var amount = new Column
            {
                ParentTable = table,
                DbName = "Amount",
                NameHumanCase = "Amount",
                Config = "test",
            };
            table.Columns.Add(amount);

            // Act
            var list = sut.IndexModelBuilder(table);
            foreach (var str in list)
                TestContext.Out.WriteLine(str);

            // Assert
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual($@"builder.HasIndex(x => x.CustomerId).{hasName}(""IX_Orders_Customer"").IncludeProperties(""OrderDate"", ""Amount"");", list[0]);
        }

        [Test]
        [TestCase(TemplateType.EfCore8, "HasDatabaseName")]
        [TestCase(TemplateType.EfCore9, "HasDatabaseName")]
        public void EfCoreModelBuilder_IncludeColumns_HiddenColumnSkipped(TemplateType templateType, string hasName)
        {
            // Arrange
            var fileManagement = new FileManagementService(new GeneratedTextTransformation());
            var sut = new GeneratorEfCore(fileManagement);
            Settings.TemplateType = templateType;

            var table = new Table(null, new Schema("dbo"), "orders", false)
            {
                NameHumanCase = "Orders",
                Indexes = new List<RawIndex>
                {
                    new RawIndex("dbo", "orders", "IX_Orders_Customer", 1, "CustomerID", 1, false, false, false, false, "", "OrderDate,HiddenCol"),
                }
            };

            var customerId = new Column
            {
                ParentTable = table,
                DbName = "CustomerID",
                NameHumanCase = "CustomerId",
                Config = "test",
            };
            table.Columns.Add(customerId);

            var orderDate = new Column
            {
                ParentTable = table,
                DbName = "OrderDate",
                NameHumanCase = "OrderDate",
                Config = "test",
            };
            table.Columns.Add(orderDate);

            var hiddenCol = new Column
            {
                ParentTable = table,
                DbName = "HiddenCol",
                NameHumanCase = "HiddenCol",
                Config = "test",
                Hidden = true,
            };
            table.Columns.Add(hiddenCol);

            // Act
            var list = sut.IndexModelBuilder(table);
            foreach (var str in list)
                TestContext.Out.WriteLine(str);

            // Assert
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual($@"builder.HasIndex(x => x.CustomerId).{hasName}(""IX_Orders_Customer"").IncludeProperties(""OrderDate"");", list[0]);
        }

        // IsClustered() is an extension method in Microsoft.EntityFrameworkCore.SqlServer. MySQL reports every InnoDB
        // primary key as clustered, so emitting it for any other provider produced code that does not compile.
        private static Table ClusteredTable()
        {
            var table = new Table(null, new Schema("EfrpgTest"), "orders", false)
            {
                NameHumanCase = "Orders",
                Indexes = new List<RawIndex>
                {
                    new RawIndex("EfrpgTest", "orders", "PRIMARY",     1, "Id",         1, true,  true,  false, true),
                    new RawIndex("EfrpgTest", "orders", "IX_Customer", 1, "CustomerId", 1, false, false, false, true)
                }
            };

            table.Columns.Add(new Column { ParentTable = table, DbName = "Id",         NameHumanCase = "Id",         Config = "test", IsPrimaryKey = true });
            table.Columns.Add(new Column { ParentTable = table, DbName = "CustomerId", NameHumanCase = "CustomerId", Config = "test" });
            return table;
        }

        [TestCase(DatabaseType.SqlServer,  true)]
        [TestCase(DatabaseType.MySql,      false)]
        [TestCase(DatabaseType.Oracle,     false)]
        [TestCase(DatabaseType.PostgreSQL, false)]
        [TestCase(DatabaseType.SQLite,     false)]
        public void PrimaryKeyModelBuilder_ClusteredKey_EmitsIsClusteredOnlyForSqlServer(DatabaseType databaseType, bool expected)
        {
            // Arrange
            var savedDatabaseType = Settings.DatabaseType;
            Settings.DatabaseType = databaseType;
            Settings.TemplateType = TemplateType.EfCore8;
            var sut = new GeneratorEfCore(new FileManagementService(new GeneratedTextTransformation()));

            try
            {
                // Act
                var result = sut.PrimaryKeyModelBuilder(ClusteredTable());

                // Assert
                Assert.That(result.Contains("IsClustered"), Is.EqualTo(expected), result);
            }
            finally
            {
                Settings.DatabaseType = savedDatabaseType;
            }
        }

        [TestCase(DatabaseType.SqlServer,  true)]
        [TestCase(DatabaseType.MySql,      false)]
        [TestCase(DatabaseType.Oracle,     false)]
        [TestCase(DatabaseType.PostgreSQL, false)]
        [TestCase(DatabaseType.SQLite,     false)]
        public void IndexModelBuilder_ClusteredIndex_EmitsIsClusteredOnlyForSqlServer(DatabaseType databaseType, bool expected)
        {
            // Arrange
            var savedDatabaseType = Settings.DatabaseType;
            Settings.DatabaseType = databaseType;
            Settings.TemplateType = TemplateType.EfCore8;
            var sut = new GeneratorEfCore(new FileManagementService(new GeneratedTextTransformation()));

            try
            {
                // Act
                var result = string.Join(Environment.NewLine, sut.IndexModelBuilder(ClusteredTable()));

                // Assert
                Assert.That(result.Contains("IsClustered"), Is.EqualTo(expected), result);
            }
            finally
            {
                Settings.DatabaseType = savedDatabaseType;
            }
        }
    }
}