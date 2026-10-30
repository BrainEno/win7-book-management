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
