using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Services;

namespace Win7BookManagement.Tests
{
    [TestClass]
    public class IsbnTests
    {
        [TestMethod]
        public void Isbn10_ConvertsToIsbn13_AndBadCheckDigitIsRejected()
        {
            Assert.AreEqual("9780306406157", Isbn.Normalize("0-306-40615-2"));
            Assert.AreEqual("9787111213826", Isbn.Normalize("978-7-111-21382-6"));
            Assert.AreEqual("", Isbn.Normalize("  "));
            Assert.ThrowsException<InvalidOperationException>(() => Isbn.Normalize("9787111213827"));
            Assert.ThrowsException<InvalidOperationException>(() => Isbn.Normalize("0306406153"));
        }
    }

    [TestClass]
    public class BookImportTests
    {
        private static string Csv(string rows)
        {
            var path = Path.Combine(Path.GetTempPath(), "BookDesk-Import-" + Guid.NewGuid().ToString("N") + ".csv");
            File.WriteAllText(path, string.Join(",", BookImportService.Headers) + "\n" + rows, new UTF8Encoding(true));
            return path;
        }

        [TestMethod]
        public void ThousandRows_PreviewAndCommitWithoutChangingStock()
        {
            using (var db = new TemporaryDatabase())
            {
                var rows = string.Join("\n", Enumerable.Range(1, 1000).Select(i => ",,\"图书" + i + "\",作者,出版社,文学,,,,,10.00,5.00,"));
                var path = Csv(rows);
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    var preview = service.Preview(path);
                    Assert.AreEqual(1000, preview.Count); Assert.IsTrue(preview.All(r => r.Error == null && !r.IsUpdate));
                    Assert.AreEqual(1000, service.Commit(path));
                    Assert.AreEqual(1000, db.Services.Books.Search("", true).Count);
                    Assert.IsTrue(db.Services.Books.Search("", true).All(b => b.StockQuantity == 0));
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void AnyInvalidRow_RollsBackWholeImport()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Csv(",,\"有效书\",,,,,,,,10,5,\n,9787111213827,\"无效ISBN\",,,,,,,,10,5,");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    Assert.AreEqual(1, service.Preview(path).Count(r => r.Error != null));
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path));
                    Assert.AreEqual(0, db.Services.Books.Search("", true).Count);
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void CsvParser_HandlesQuotedCommaAndNewline()
        {
            var rows = BookImportService.ParseCsv("a,\"b,c\",\"d\"\"e\"\n1,2,3").ToArray();
            Assert.AreEqual(2, rows.Length); Assert.AreEqual("b,c", rows[0][1]); Assert.AreEqual("d\"e", rows[0][2]);
        }
    }
}

namespace Win7BookManagement.Tests
{
    [TestClass]
    public class StocktakeTests
    {
        [TestMethod]
        public void Stocktake_PostsOnlyDifferencesOnce_AndPreservesLedger()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "盘点书", ShelfCode = "A", SalePriceCent = 1000 });
                db.Services.Inventory.Adjust(id, 5, "opening");
                var document = db.Services.Stocktake.Create("A", "test");
                db.Services.Stocktake.SaveActual(document.Id, new System.Collections.Generic.Dictionary<long, int> { { id, 3 } });
                Assert.AreEqual(1, db.Services.Stocktake.Post(document.Id));
                Assert.AreEqual(3, db.Services.Books.GetById(id).StockQuantity);
                Assert.ThrowsException<System.InvalidOperationException>(() => db.Services.Stocktake.Post(document.Id));
                Assert.AreEqual(3, db.Services.Books.GetById(id).StockQuantity);
            }
        }

        [TestMethod]
        public void Stocktake_RejectsStaleExpectedStock()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "盘点书", ShelfCode = "A", SalePriceCent = 1000 });
                db.Services.Inventory.Adjust(id, 5, "opening");
                var document = db.Services.Stocktake.Create("A", "test");
                db.Services.Inventory.Adjust(id, -1, "sale-like change");
                Assert.ThrowsException<System.InvalidOperationException>(() => db.Services.Stocktake.Post(document.Id));
                Assert.AreEqual(4, db.Services.Books.GetById(id).StockQuantity);
            }
        }
    }
}

namespace Win7BookManagement.Tests
{
    [TestClass]
    public class LegacyExportAndReceiptTests
    {
        [TestMethod]
        public void GenericReports_SupportXlsAndUtf8Csv()
        {
            using (var db = new TemporaryDatabase())
            {
                var table = new System.Data.DataTable(); table.Columns.Add("书名"); table.Columns.Add("金额", typeof(decimal));
                table.Rows.Add("=危险公式", 12.34m);
                var xls = Path.Combine(db.Root, "report.xls"); var csv = Path.Combine(db.Root, "report.csv");
                db.Services.Excel.Export(table, xls, "测试"); db.Services.Excel.Export(table, csv, "测试");
                Assert.IsTrue(new FileInfo(xls).Length > 0);
                var text = File.ReadAllText(csv, Encoding.UTF8);
                StringAssert.Contains(text, "\"书名\",\"金额\"");
                StringAssert.Contains(text, "\t=危险公式");
            }
        }

        [TestMethod]
        public void Receipt_LoadsImmutableCheckoutSnapshot()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "小票书", SalePriceCent = 1234 });
                db.Services.Inventory.Adjust(id, 2, "opening");
                var order = db.Services.Sales.Checkout(new System.Collections.Generic.List<Win7BookManagement.Models.TransactionLineInput>
                {
                    new Win7BookManagement.Models.TransactionLineInput { BookId = id, Quantity = 1, UnitPriceCent = 1234 }
                }, "note", 10000, "现金", 2000);
                var receipt = db.Services.Receipts.Load(order);
                Assert.AreEqual(1234, receipt.TotalCent); Assert.AreEqual(2000, receipt.ReceivedCent); Assert.AreEqual(766, receipt.ChangeCent);
                Assert.AreEqual("小票书", receipt.Lines[0].Title);
            }
        }
    }
}
