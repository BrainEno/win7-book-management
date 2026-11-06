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

            string normalized;
            Assert.IsTrue(Isbn.TryNormalize(null, out normalized));
            Assert.AreEqual("", normalized);
            Assert.IsTrue(Isbn.TryNormalize("0-306-40615-2", out normalized));
            Assert.AreEqual("9780306406157", normalized);
            Assert.IsFalse(Isbn.TryNormalize("9787111213827", out normalized));
            Assert.IsFalse(Isbn.TryNormalize("BK-000001", out normalized));
        }
    }

    [TestClass]
    public class BookIsbnSavePolicyTests
    {
        [TestMethod]
        public void BlankAndValidIsbn_DoNotRequireConfirmation()
        {
            var blank = BookIsbnSavePolicy.CreatePlan("  ", "自出版");
            Assert.IsFalse(blank.InvalidIsbnNeedsConfirmation);
            Assert.AreEqual("", blank.Isbn);
            Assert.AreEqual("自出版", blank.Note);

            var valid = BookIsbnSavePolicy.CreatePlan("0-306-40615-2", "原有备注");
            Assert.IsFalse(valid.InvalidIsbnNeedsConfirmation);
            Assert.AreEqual("9780306406157", valid.Isbn);
            Assert.AreEqual("原有备注", valid.Note);
        }

        [TestMethod]
        public void InvalidIsbn_RequiresConfirmation_AndPreservesRawInputInNote()
        {
            var plan = BookIsbnSavePolicy.CreatePlan("9787111213827", "签名本");
            Assert.IsTrue(plan.InvalidIsbnNeedsConfirmation);
            Assert.AreEqual("", plan.Isbn);
            Assert.AreEqual("9787111213827", plan.RawIsbnInput);
            StringAssert.Contains(plan.Note, "签名本");
            StringAssert.Contains(plan.Note, BookIsbnSavePolicy.UnverifiedIsbnNotePrefix + "9787111213827");
        }

        [TestMethod]
        public void InvalidIsbn_NoteIsNotDuplicated()
        {
            var first = BookIsbnSavePolicy.CreatePlan("BK-000001", "");
            var second = BookIsbnSavePolicy.CreatePlan("BK-000001", first.Note);
            Assert.AreEqual(first.Note, second.Note);
        }

        [TestMethod]
        public void ConfirmedInvalidIsbnPlan_CanInsertBookWithoutIsbn()
        {
            using (var db = new TemporaryDatabase())
            {
                var plan = BookIsbnSavePolicy.CreatePlan("9787111213827", "自出版画册");
                Assert.IsTrue(plan.InvalidIsbnNeedsConfirmation);

                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book
                {
                    Title = "自出版画册",
                    Isbn = plan.Isbn,
                    Note = plan.Note,
                    SalePriceCent = 1000
                });

                var stored = db.Services.Books.GetById(id);
                Assert.AreEqual("", stored.Isbn);
                Assert.IsFalse(string.IsNullOrWhiteSpace(stored.SelfCode));
                StringAssert.Contains(stored.Note, "9787111213827");
            }
        }
    }

    [TestClass]
    public class BookRepositoryIsbnOptionalTests
    {
        [TestMethod]
        public void BlankIsbn_AllowsManyBooks_WithUniqueSelfCodes()
        {
            using (var db = new TemporaryDatabase())
            {
                var first = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "自出版一" });
                var second = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "自出版二", Isbn = "" });

                var a = db.Services.Books.GetById(first);
                var b = db.Services.Books.GetById(second);
                Assert.AreEqual("", a.Isbn);
                Assert.AreEqual("", b.Isbn);
                Assert.AreNotEqual(a.SelfCode, b.SelfCode);
                Assert.IsTrue(a.SelfCode.StartsWith("BK-", StringComparison.Ordinal));
                Assert.IsTrue(b.SelfCode.StartsWith("BK-", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void SalesLookup_HandlesCodesTitlesAuthorsAndMalformedIsbnWithoutThrowing()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "独立出版画册", Author = "独立作者" });
                var book = db.Services.Books.GetById(id);
                Assert.AreEqual(id, db.Services.Books.FindByExactIdentifier(book.SelfCode).Id);
                Assert.AreEqual(id, db.Services.Books.SearchActiveByIsbnOrTitle("出版画册").Single().Id);
                Assert.AreEqual(id, db.Services.Books.SearchActiveByIsbnOrTitle("独立作者").Single().Id);
                Assert.IsNull(db.Services.Books.FindByExactIdentifier("9787111213827"));
                Assert.AreEqual(0, db.Services.Books.SearchActiveByIsbnOrTitle("9787111213827").Count);
                var numericId = db.Services.Books.Insert(new Win7BookManagement.Models.Book { SelfCode = "9787111213827", Title = "数字编码" });
                Assert.AreEqual(numericId, db.Services.Books.FindByExactIdentifier("9787111213827").Id);
                var isbnId = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "有 ISBN", Isbn = "9780306406157" });
                Assert.AreEqual(isbnId, db.Services.Books.FindByExactIdentifier("0-306-40615-2").Id);
            }
        }

        [TestMethod]
        public void Repository_StillRejectsInvalidNonEmptyIsbn()
        {
            using (var db = new TemporaryDatabase())
            {
                Assert.ThrowsException<InvalidOperationException>(() =>
                    db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "错误ISBN", Isbn = "9787111213827" }));
                Assert.AreEqual(0, db.Services.Books.Search("", true).Count);
            }
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
                var path = Csv(",,\"有效书\",,,,,,,,10,5,\n,,,,,,,,,,10,5,");
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
        public void BlankAndInvalidIsbn_RequireApprovalOnlyForWarning_AndKeepRawValue()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Csv(",,自出版一,,,,,,,,10,5,备注一\n,9787111213827,自出版二,,,,,,,,10,5,备注二");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    var rows = service.Preview(path);
                    Assert.IsTrue(rows.All(r => r.Error == null));
                    Assert.IsNull(rows[0].Warning);
                    StringAssert.Contains(rows[1].Warning, "ISBN 无效");
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path));
                    Assert.AreEqual(2, service.Commit(path, rows));
                    var books = db.Services.Books.Search("", true);
                    Assert.AreEqual(2, books.Count);
                    Assert.IsTrue(books.All(b => b.Isbn == "" && b.SelfCode.StartsWith("BK-")));
                    StringAssert.Contains(books.Single(b => b.Title == "自出版二").Note, "备注二");
                    StringAssert.Contains(books.Single(b => b.Title == "自出版二").Note, "未校验 ISBN 输入：9787111213827");
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void InvalidIsbnUpdate_PreservesExistingVerifiedIsbnAndStock()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "旧书", Isbn = "9787111213826" });
                db.Services.Inventory.Adjust(id, 3, "opening");
                var code = db.Services.Books.GetById(id).SelfCode;
                var path = Csv(code + ",9787111213827,新书,,,,,,,,10,5,备注");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    var rows = service.Preview(path);
                    Assert.IsTrue(rows[0].IsUpdate);
                    Assert.IsNull(rows[0].Error);
                    StringAssert.Contains(rows[0].Warning, "保留已有 ISBN");
                    service.Commit(path, rows);
                    var book = db.Services.Books.GetById(id);
                    Assert.AreEqual("9787111213826", book.Isbn);
                    Assert.AreEqual(3, book.StockQuantity);
                    StringAssert.Contains(book.Note, "9787111213827");
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void DuplicateValidIsbnAndConflictingIdentifiers_StillBlockWholeImport()
        {
            using (var db = new TemporaryDatabase())
            {
                var a = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "甲", Isbn = "9787111213826" });
                var b = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "乙", Isbn = "9780306406157" });
                var code = db.Services.Books.GetById(a).SelfCode;
                var path = Csv(code + ",9780306406157,冲突,,,,,,,,10,5,");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    Assert.IsNotNull(service.Preview(path)[0].Error);
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path));
                    File.WriteAllText(path, string.Join(",", BookImportService.Headers) + "\n,0-306-40615-2,一,,,,,,,,10,5,\n,9780306406157,二,,,,,,,,10,5,", new UTF8Encoding(true));
                    Assert.IsTrue(service.Preview(path).Any(r => r.Error != null));
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path));
                    Assert.AreEqual("甲", db.Services.Books.GetById(a).Title);
                    Assert.AreEqual("乙", db.Services.Books.GetById(b).Title);
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void ChangedFileOrMatch_AfterPreviewCannotBeCommitted()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Csv("MYCODE,9787111213827,原书,,,,,,,,10,5,");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    var rows = service.Preview(path);
                    File.AppendAllText(path, "\n,,新增,,,,,,,,10,5,");
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path, rows));
                    Assert.AreEqual(0, db.Services.Books.Search("", true).Count);
                    rows = service.Preview(path);
                    db.Services.Books.Insert(new Win7BookManagement.Models.Book { SelfCode = "MYCODE", Title = "已有" });
                    Assert.ThrowsException<InvalidOperationException>(() => service.Commit(path, rows));
                    Assert.AreEqual("已有", db.Services.Books.Search("", true).Single().Title);
                }
                finally { File.Delete(path); }
            }
        }

        [TestMethod]
        public void AutoGeneratedCode_DoesNotStealExplicitCodeInLaterRow()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Csv(",,自动编码书,,,,,,,,10,5,\nBK-000001,,手工编码书,,,,,,,,10,5,");
                try
                {
                    var service = new BookImportService(db.Services.Database, db.Services.Books);
                    var rows = service.Preview(path);
                    Assert.IsTrue(rows.All(r => r.Error == null));
                    Assert.AreEqual(2, service.Commit(path, rows));
                    Assert.AreEqual("BK-000001", db.Services.Books.Search("手工编码书", true).Single().SelfCode);
                    Assert.AreNotEqual("BK-000001", db.Services.Books.Search("自动编码书", true).Single().SelfCode);
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
        public void Exporters_RejectUnknownOrMisleadingExtensionsWithoutTouchingFiles()
        {
            using (var db = new TemporaryDatabase())
            {
                var table = new System.Data.DataTable();
                table.Columns.Add("书名"); table.Rows.Add("测试");
                var path = Path.Combine(db.Root, "report.pdf");
                File.WriteAllText(path, "existing file");
                var before = File.ReadAllBytes(path);
                Assert.ThrowsException<InvalidOperationException>(() => db.Services.Excel.Export(table, path, "测试"));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "report.pdf.writing-*").Length);

                var missing = Path.Combine(db.Root, "not-created", "report");
                Assert.ThrowsException<InvalidOperationException>(() => db.Services.Excel.Export(table, missing, "测试"));
                Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(missing)));
                var monthlyCsv = Path.Combine(db.Root, "monthly.csv");
                File.WriteAllText(monthlyCsv, "existing csv");
                before = File.ReadAllBytes(monthlyCsv);
                Assert.ThrowsException<InvalidOperationException>(() => db.Services.SalesMonthlyExcel.Export(table, table, monthlyCsv, DateTime.Today, "书店", 14, ""));
                Assert.ThrowsException<InvalidOperationException>(() => db.Services.PurchaseMonthlyExcel.Export(table, table, monthlyCsv, DateTime.Today, "书店"));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(monthlyCsv));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "monthly.csv.writing-*").Length);
                Assert.ThrowsException<InvalidOperationException>(() => Win7BookManagement.Reporting.CsvReportExporter.Export(table, Path.Combine(db.Root, "wrong.xls")));
                Assert.IsFalse(File.Exists(Path.Combine(db.Root, "wrong.xls")));
            }
        }

        [TestMethod]
        public void Exporters_WriteWorkbookMatchingUppercaseExtension()
        {
            using (var db = new TemporaryDatabase())
            {
                var table = new System.Data.DataTable(); table.Columns.Add("书名"); table.Rows.Add("测试");
                foreach (var extension in new[] { ".XLS", ".XLSX" })
                {
                    var path = Path.Combine(db.Root, "report" + extension);
                    db.Services.Excel.Export(table, path, "测试");
                    using (var stream = File.OpenRead(path))
                    {
                        var workbook = NPOI.SS.UserModel.WorkbookFactory.Create(stream);
                        try
                        {
                            Assert.AreEqual(extension == ".XLS", workbook is NPOI.HSSF.UserModel.HSSFWorkbook);
                            Assert.AreEqual("测试", workbook.GetSheetAt(0).GetRow(2).GetCell(0).StringCellValue);
                        }
                        finally { workbook.Close(); }
                    }
                }
            }
        }

        [TestMethod]
        public void Csv_PreservesTypedNegativeNumbersAndProtectsFormulaLikeText()
        {
            using (var db = new TemporaryDatabase())
            {
                var table = new System.Data.DataTable();
                table.Columns.Add("金额", typeof(decimal));
                table.Columns.Add("数量", typeof(long));
                table.Columns.Add("比率", typeof(double));
                table.Columns.Add("=测试表头", typeof(string));
                table.Columns.Add("空格", typeof(string));
                table.Columns.Add("制表符", typeof(string));
                table.Columns.Add("普通文本", typeof(string));
                table.Columns.Add("空值", typeof(string));
                table.Rows.Add(-12.34m, -3L, -0.5d, "-12.34", "  =1+1", "\t@name", "中文,\"书\"\n续行", DBNull.Value);
                var path = Path.Combine(db.Root, "negative.csv");
                var thread = System.Threading.Thread.CurrentThread;
                var previousCulture = thread.CurrentCulture;
                try
                {
                    thread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                    db.Services.Excel.Export(table, path, "测试");
                }
                finally { thread.CurrentCulture = previousCulture; }

                var bytes = File.ReadAllBytes(path);
                CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
                var rows = BookImportService.ParseCsv(File.ReadAllText(path, Encoding.UTF8)).ToArray();
                Assert.AreEqual(2, rows.Length);
                Assert.AreEqual("\t=测试表头", rows[0][3]);
                CollectionAssert.AreEqual(new[] { "-12.34", "-3", "-0.5", "\t-12.34", "\t  =1+1", "\t\t@name", "中文,\"书\"\n续行", "" }, rows[1]);
            }
        }

        [TestMethod]
        public void Csv_PreservesAllFiniteNumericTypesAndProtectsNonFiniteValues()
        {
            using (var db = new TemporaryDatabase())
            {
                var table = new System.Data.DataTable();
                table.Columns.Add("值", typeof(object));
                foreach (var value in new object[] { (byte)1, (sbyte)-1, (short)-2, (ushort)2, -3, 3U, -4L, 4UL, -1.5f, -2.5d, -3.5m, double.NegativeInfinity })
                    table.Rows.Add(value);
                var path = Path.Combine(db.Root, "types.csv");
                db.Services.Excel.Export(table, path, "测试");
                var rows = BookImportService.ParseCsv(File.ReadAllText(path, Encoding.UTF8)).Skip(1).Select(row => row[0]).ToArray();
                CollectionAssert.AreEqual(new[] { "1", "-1", "-2", "2", "-3", "3", "-4", "4", "-1.5", "-2.5", "-3.5", "\t-Infinity" }, rows);
            }
        }

        [TestMethod]
        public void CsvImportErrorOutput_ProtectsWhitespaceAndControlPrefixedFormulaText()
        {
            var input = new[] { "=1+1", "+1", "-1+1", "@cmd", "  =1+1", "\t=1+1", "\r\n@cmd", "\nordinary", "正常文本" };
            var csv = BookImportService.ToCsv(new[] { input });
            var row = BookImportService.ParseCsv(csv).Single();
            for (var i = 0; i < input.Length - 1; i++)
                Assert.AreEqual("\t" + input[i], row[i], "第 " + i + " 列未添加文本保护前缀。");
            Assert.AreEqual(input[input.Length - 1], row[input.Length - 1]);
        }

        [TestMethod]
        public void Csv_ExportsRealNegativeReturnDayAmountsAndInventoryMovement()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "退款测试", SalePriceCent = 1234 });
                db.Services.Inventory.Adjust(id, 2, "期初库存");
                db.Services.Sales.Checkout(new System.Collections.Generic.List<Win7BookManagement.Models.TransactionLineInput>
                {
                    new Win7BookManagement.Models.TransactionLineInput { BookId = id, Quantity = 1, UnitPriceCent = 1234 }
                }, "", 10000, "现金", 1234);
                var sale = db.Services.Documents.Search("sale", DateTime.Today, DateTime.Today, "");
                var saleId = Convert.ToInt64(sale.Rows[0]["Id"]);
                var line = db.Services.Documents.GetReturnableLines("sale", saleId)[0];
                db.Services.Returns.CreateSalesReturn(saleId, new System.Collections.Generic.List<Win7BookManagement.Models.ReturnLineInput>
                {
                    new Win7BookManagement.Models.ReturnLineInput { SourceItemId = line.SourceItemId, Quantity = 1 }
                }, "", "现金");
                // Test fixture: place the return on the next business day, leaving the sale on today.
                var returnDay = DateTime.Today.AddDays(1);
                using (var connection = db.Services.Database.Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE sales_returns SET returned_at=@at;";
                    command.Parameters.AddWithValue("@at", returnDay.ToString("yyyy-MM-dd 12:00:00", System.Globalization.CultureInfo.InvariantCulture));
                    command.ExecuteNonQuery();
                }
                var report = db.Services.Reports.OperatingDailySummary(returnDay);
                Assert.AreEqual(-12.34m, Convert.ToDecimal(report.Rows[0]["净销售"]));
                Assert.AreEqual(-1L, Convert.ToInt64(report.Rows[0]["净销售册数"]));
                var path = Path.Combine(db.Root, "operating.csv");
                db.Services.Excel.Export(report, path, "经营日报");
                var rows = BookImportService.ParseCsv(File.ReadAllText(path, Encoding.UTF8)).ToArray();
                var amountIndex = Array.IndexOf(rows[0], "净销售");
                var quantityIndex = Array.IndexOf(rows[0], "净销售册数");
                Assert.AreEqual("-12.34", rows[1][amountIndex]);
                Assert.AreEqual("-1", rows[1][quantityIndex]);

                var movement = db.Services.Reports.InventoryMovements(DateTime.Today, DateTime.Today);
                db.Services.Excel.Export(movement, path, "库存流水");
                rows = BookImportService.ParseCsv(File.ReadAllText(path, Encoding.UTF8)).ToArray();
                var movementIndex = Array.IndexOf(rows[0], "数量变化");
                Assert.IsTrue(rows.Skip(1).Any(row => row[movementIndex] == "-1"), "销售出库负数量应保持为数值文本。");
            }
        }

        private static void PublishWorkbookForTest(string path, System.Threading.CancellationToken token, Action<Stream> writer)
        {
            var type = typeof(Win7BookManagement.Reporting.ExcelReportExporter).Assembly.GetType("Win7BookManagement.Reporting.WorkbookFile", true);
            var method = type.GetMethod("PublishCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            try { method.Invoke(null, new object[] { path, token, writer }); }
            catch (System.Reflection.TargetInvocationException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private sealed class CancelAfterFirstWriteStream : Stream
        {
            private readonly Stream _inner;
            private readonly System.Threading.CancellationTokenSource _source;
            public bool Wrote { get; private set; }
            public CancelAfterFirstWriteStream(Stream inner, System.Threading.CancellationTokenSource source) { _inner = inner; _source = source; }
            public override bool CanRead { get { return _inner.CanRead; } }
            public override bool CanSeek { get { return _inner.CanSeek; } }
            public override bool CanWrite { get { return _inner.CanWrite; } }
            public override long Length { get { return _inner.Length; } }
            public override long Position { get { return _inner.Position; } set { _inner.Position = value; } }
            public override void Flush() { _inner.Flush(); }
            public override int Read(byte[] buffer, int offset, int count) { return _inner.Read(buffer, offset, count); }
            public override long Seek(long offset, SeekOrigin origin) { return _inner.Seek(offset, origin); }
            public override void SetLength(long value) { _inner.SetLength(value); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                if (count > 0 && !Wrote) { Wrote = true; _source.Cancel(); }
            }
            public override void WriteByte(byte value)
            {
                _inner.WriteByte(value);
                if (!Wrote) { Wrote = true; _source.Cancel(); }
            }
            // This test wrapper does not own the stream passed to the writer.
        }

        [TestMethod]
        public void WorkbookPublish_CancelAndWriteFailurePreserveExistingFile()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "report.xlsx");
                File.WriteAllText(path, "old report");
                var before = File.ReadAllBytes(path);
                using (var source = new System.Threading.CancellationTokenSource())
                {
                    source.Cancel();
                    var called = false;
                    Assert.ThrowsException<OperationCanceledException>(() => PublishWorkbookForTest(path, source.Token, stream => { called = true; }));
                    Assert.IsFalse(called);
                }
                using (var source = new System.Threading.CancellationTokenSource())
                {
                    Assert.ThrowsException<OperationCanceledException>(() => PublishWorkbookForTest(path, source.Token, stream =>
                    {
                        stream.WriteByte(7);
                        source.Cancel();
                        stream.WriteByte(8);
                    }));
                }
                using (var source = new System.Threading.CancellationTokenSource())
                {
                    // Cancellation after the writer returns must still stop publication.
                    Assert.ThrowsException<OperationCanceledException>(() => PublishWorkbookForTest(path, source.Token, stream =>
                    {
                        stream.WriteByte(7);
                        source.Cancel();
                    }));
                }
                Assert.ThrowsException<IOException>(() => PublishWorkbookForTest(path, System.Threading.CancellationToken.None, stream =>
                {
                    stream.WriteByte(7);
                    throw new IOException("模拟磁盘错误");
                }));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "report.xlsx.writing-*").Length);
            }
        }

        [TestMethod]
        public void WorkbookPublish_CancelsRealXlsxAndXlsSerializationWithoutReplacingExistingFile()
        {
            using (var db = new TemporaryDatabase())
            {
                foreach (var extension in new[] { ".xlsx", ".xls" })
                using (var source = new System.Threading.CancellationTokenSource())
                {
                    var path = Path.Combine(db.Root, "report" + extension);
                    File.WriteAllText(path, "old report");
                    var before = File.ReadAllBytes(path);
                    NPOI.SS.UserModel.IWorkbook workbook = extension == ".xls"
                        ? (NPOI.SS.UserModel.IWorkbook)new NPOI.HSSF.UserModel.HSSFWorkbook()
                        : new NPOI.XSSF.UserModel.XSSFWorkbook();
                    try
                    {
                        workbook.CreateSheet("测试").CreateRow(0).CreateCell(0).SetCellValue("测试内容");
                        CancelAfterFirstWriteStream trigger = null;
                        Assert.ThrowsException<OperationCanceledException>(() => PublishWorkbookForTest(path, source.Token, stream =>
                        {
                            trigger = new CancelAfterFirstWriteStream(stream, source);
                            workbook.Write(trigger);
                        }));
                        Assert.IsNotNull(trigger);
                        Assert.IsTrue(trigger.Wrote, "必须在实际写入后才触发取消。");
                        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                        Assert.AreEqual(0, Directory.GetFiles(db.Root, "report" + extension + ".writing-*").Length);
                    }
                    finally { workbook.Close(); }
                }
            }
        }

        private sealed class CancelOnProgress : IProgress<int>
        {
            private readonly System.Threading.CancellationTokenSource _source;
            public CancelOnProgress(System.Threading.CancellationTokenSource source) { _source = source; }
            public void Report(int value) { _source.Cancel(); }
        }

        [TestMethod]
        public void CsvCancellation_PreservesPreviousFileAndCleansUp()
        {
            using (var db = new TemporaryDatabase())
            using (var source = new System.Threading.CancellationTokenSource())
            {
                var table = new System.Data.DataTable();
                table.Columns.Add("书名");
                table.Rows.Add("第一本"); table.Rows.Add("第二本");
                var path = Path.Combine(db.Root, "report.csv");
                File.WriteAllText(path, "existing export");
                var before = File.ReadAllBytes(path);
                Assert.ThrowsException<OperationCanceledException>(() =>
                    db.Services.Excel.Export(table, path, "测试", new CancelOnProgress(source), source.Token));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "report.csv.writing-*").Length);
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

namespace Win7BookManagement.Tests
{
    [TestClass]
    public class BookLabelTests
    {
        [TestMethod]
        public void Code128B_KnownVectorHasCorrectChecksumAndStop()
        {
            var widths = Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode("ABC");
            Assert.AreEqual("2112141113231311231313212221222331112", string.Concat(widths.Select(x => x.ToString())));
            Assert.AreEqual(88, Win7BookManagement.Reporting.BookLabelPrinter.ModuleCount("ABC"));
        }

        [TestMethod]
        public void Code128B_PrintableCharacterPatternsAllHaveCorrectWidth()
        {
            for (var c = 32; c <= 126; c++)
            {
                var result = Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode(new string((char)c, 1));
                Assert.AreEqual(6, result.Skip(6).Take(6).Count());
                Assert.AreEqual(11, result.Skip(6).Take(6).Sum(), "character " + c);
            }
            // The symbols for c and d independently guard against accidental table corruption.
            Assert.AreEqual("141122", string.Concat(Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode("c").Skip(6).Take(6)));
            Assert.AreEqual("141221", string.Concat(Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode("d").Skip(6).Take(6)));
        }

        [TestMethod]
        public void Labels_RejectUnsupportedAndTooLongCodesInsteadOfPrintingUnscannableBars()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode(""));
            Assert.ThrowsException<InvalidOperationException>(() => Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode("中文"));
            Assert.ThrowsException<InvalidOperationException>(() => Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode("A\nB"));
            var book = new Win7BookManagement.Models.Book { Title = "自出版", SelfCode = "BK-000001" };
            Win7BookManagement.Reporting.BookLabelPrinter.ValidateFit(book, 185, 203);
            Assert.IsTrue(Win7BookManagement.Reporting.BookLabelPrinter.ModuleDots(book.SelfCode, 185, 203) >= 2);
            book.SelfCode = new string('A', 40);
            Assert.ThrowsException<InvalidOperationException>(() => Win7BookManagement.Reporting.BookLabelPrinter.ValidateFit(book, 185, 203));
        }

        [TestMethod]
        public void RenderedLabel_BarPatternAndQuietZonesMatchEncodedStoreCode()
        {
            const int dpi = 300;
            var book = new Win7BookManagement.Models.Book { Title = "自出版", SelfCode = "BK-000001", SalePriceCent = 1234 };
            var widths = Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode(book.SelfCode);
            var bits = new System.Collections.Generic.List<bool>();
            for (var i = 0; i < widths.Length; i++)
                for (var j = 0; j < widths[i]; j++) bits.Add(i % 2 == 0);
            using (var image = new System.Drawing.Bitmap(600, 360))
            {
                image.SetResolution(dpi, dpi);
                using (var graphics = System.Drawing.Graphics.FromImage(image))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    graphics.PageUnit = System.Drawing.GraphicsUnit.Pixel;
                    graphics.ScaleTransform(3, 3); // 300 dpi, drawing units are 1/100 inch
                    Win7BookManagement.Reporting.BookLabelPrinter.DrawLabel(graphics, new System.Drawing.RectangleF(10, 10, 180, 100), book, dpi);
                }
                var dots = Win7BookManagement.Reporting.BookLabelPrinter.ModuleDots(book.SelfCode, 180, dpi);
                var quiet = Win7BookManagement.Reporting.BookLabelPrinter.QuietModules * dots;
                var total = (bits.Count + 20) * dots;
                var start = (int)Math.Round((10 + (180 - total / 3f) / 2f) * 3);
                var y = (int)((10 + 100 * .58f) * 3);
                Func<int, bool> black = x => image.GetPixel(x, y).R < 128;
                Assert.IsFalse(black(start + quiet / 2));
                for (var i = 0; i < bits.Count; i++)
                    Assert.AreEqual(bits[i], black(start + quiet + i * dots + dots / 2), "module " + i);
                Assert.IsFalse(black(start + quiet + bits.Count * dots + quiet / 2));
            }
        }

        [TestMethod]
        public void SelfPublishedLabel_UsesRealStoreCodeAndCanBeLookedUpForSales()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Win7BookManagement.Models.Book { Title = "无ISBN的书", Isbn = "" });
                var book = db.Services.Books.GetById(id);
                Assert.IsTrue(Win7BookManagement.Reporting.BookLabelPrinter.EncodeStoreCode(book.SelfCode).Length > 0);
                Assert.AreEqual("", book.Isbn);
                Assert.AreEqual(id, db.Services.Books.FindByExactIdentifier(book.SelfCode).Id);
            }
        }
    }
}
