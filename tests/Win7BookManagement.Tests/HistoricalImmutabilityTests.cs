using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPOI.XSSF.UserModel;
using Win7BookManagement.Models;

namespace Win7BookManagement.Tests
{
    /// <summary>
    /// Regression coverage for issues #91-#95: empty report exports, historical
    /// sales/purchase snapshot immutability, inventory-adjust rollback safety and
    /// dictionary disabling. These behaviours are data invariants, so they are
    /// asserted against a real SQLite database rather than mocked repositories.
    /// </summary>
    [TestClass]
    public class HistoricalImmutabilityTests
    {
        [TestMethod]
        public void EmptyMonthExports_ProduceStructurallyCompleteWorkbooks()
        {
            using (var db = new TemporaryDatabase())
            {
                // A fixed month keeps the test deterministic and covers a leap-year
                // February (29 daily sheets) on an otherwise empty database.
                var emptyMonth = new DateTime(2020, 2, 1);

                var salesDetail = db.Services.Reports.SalesDetail(emptyMonth, emptyMonth);
                Assert.AreEqual(0, salesDetail.Rows.Count);

                var genericPath = Path.Combine(db.Root, "empty-sales-detail.xlsx");
                db.Services.Excel.Export(salesDetail, genericPath, "空销售明细");
                Assert.IsTrue(new FileInfo(genericPath).Length > 0);

                using (var stream = File.OpenRead(genericPath))
                {
                    var workbook = new XSSFWorkbook(stream);
                    try
                    {
                        var sheet = workbook.GetSheet("空销售明细");
                        Assert.IsNotNull(sheet, "空结果导出缺少工作表。");
                        Assert.AreEqual("空销售明细", sheet.GetRow(0).GetCell(0).StringCellValue);
                        Assert.AreEqual("日期", sheet.GetRow(1).GetCell(0).StringCellValue);
                        Assert.AreEqual("2:2", sheet.RepeatingRows.FormatAsString());
                        Assert.IsFalse(string.IsNullOrWhiteSpace(workbook.GetPrintArea(0)));
                        Assert.AreEqual("空销售明细", sheet.Header.Center);
                        Assert.AreEqual(1, workbook.NumberOfSheets);
                    }
                    finally
                    {
                        workbook.Close();
                    }
                }

                var monthlySales = db.Services.Reports.SalesMonthlyExportDetail(emptyMonth);
                var monthlyReturns = db.Services.Reports.SalesMonthlyReturnExportDetail(emptyMonth);
                Assert.AreEqual(0, monthlySales.Rows.Count);
                Assert.AreEqual(0, monthlyReturns.Rows.Count);

                var monthlySalesPath = Path.Combine(db.Root, "empty-sales-monthly.xlsx");
                db.Services.SalesMonthlyExcel.Export(
                    monthlySales,
                    monthlyReturns,
                    monthlySalesPath,
                    emptyMonth,
                    "测试书店",
                    0,
                    "");
                Assert.IsTrue(new FileInfo(monthlySalesPath).Length > 0);

                using (var stream = File.OpenRead(monthlySalesPath))
                {
                    var workbook = new XSSFWorkbook(stream);
                    try
                    {
                        var days = DateTime.DaysInMonth(emptyMonth.Year, emptyMonth.Month);
                        Assert.AreEqual(days + 1, workbook.NumberOfSheets);
                        Assert.IsNotNull(workbook.GetSheet("销售月报表"));
                        Assert.IsNotNull(workbook.GetSheet("0201"));
                        Assert.IsNotNull(workbook.GetSheet("0229"), "闰年 2 月应生成 0229 日明细表。");
                    }
                    finally
                    {
                        workbook.Close();
                    }
                }

                var monthlyPurchases = db.Services.Reports.PurchaseMonthlyExportDetail(emptyMonth);
                var monthlyPurchaseReturns =
                    db.Services.Reports.PurchaseMonthlyReturnExportDetail(emptyMonth);
                Assert.AreEqual(0, monthlyPurchases.Rows.Count);
                Assert.AreEqual(0, monthlyPurchaseReturns.Rows.Count);

                var monthlyPurchasePath = Path.Combine(db.Root, "empty-purchase-monthly.xlsx");
                db.Services.PurchaseMonthlyExcel.Export(
                    monthlyPurchases,
                    monthlyPurchaseReturns,
                    monthlyPurchasePath,
                    emptyMonth,
                    "测试书店");
                Assert.IsTrue(new FileInfo(monthlyPurchasePath).Length > 0);

                using (var stream = File.OpenRead(monthlyPurchasePath))
                {
                    var workbook = new XSSFWorkbook(stream);
                    try
                    {
                        var days = DateTime.DaysInMonth(emptyMonth.Year, emptyMonth.Month);
                        Assert.AreEqual(days + 2, workbook.NumberOfSheets);
                        Assert.IsNotNull(workbook.GetSheet("采购月报表"));
                        Assert.IsNotNull(workbook.GetSheet("供应商汇总"));
                        Assert.IsNotNull(workbook.GetSheet("0229"));
                    }
                    finally
                    {
                        workbook.Close();
                    }
                }
            }
        }

        [TestMethod]
        public void BookMasterEdit_DoesNotRewriteHistoricalSalesReporting()
        {
            using (var db = new TemporaryDatabase())
            {
                var bookId = db.Services.Books.Insert(new Book
                {
                    Title = "快照测试书",
                    Author = "原作者",
                    Publisher = "原出版社",
                    Category = "原分类",
                    PublicationYear = "2020",
                    ListPriceCent = 3000,
                    SalePriceCent = 2000,
                    DefaultPurchasePriceCent = 1200
                });
                db.Services.Inventory.Adjust(bookId, 3, "期初库存");

                var orderNo = db.Services.Sales.Checkout(
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput
                        {
                            BookId = bookId,
                            Quantity = 1,
                            UnitPriceCent = 1800,
                            BaseUnitPriceCent = 2000,
                            DiscountBasisPoints = 10000
                        }
                    },
                    "snapshot sale",
                    10000,
                    "现金",
                    2000);

                var book = db.Services.Books.GetById(bookId);
                book.Author = "后改作者";
                book.Publisher = "后改出版社";
                book.Category = "后改分类";
                book.PublicationYear = "2099";
                book.DefaultPurchasePriceCent = 9999;
                book.SalePriceCent = 8888;
                book.ListPriceCent = 7777;
                db.Services.Books.Update(book);

                var detail = db.Services.Reports.SalesDetail(DateTime.Today, DateTime.Today);
                Assert.AreEqual(1, detail.Rows.Count);
                var row = detail.Rows[0];
                Assert.AreEqual(orderNo, Convert.ToString(row["销售单号"]));
                Assert.AreEqual("原作者", Convert.ToString(row["作者"]));
                Assert.AreEqual("原出版社", Convert.ToString(row["出版社"]));
                Assert.AreEqual("原分类", Convert.ToString(row["分类"]));
                Assert.AreEqual(20.00m, Convert.ToDecimal(row["原单价"]));
                Assert.AreEqual(20.00m, Convert.ToDecimal(row["实收单价"]));
                Assert.AreEqual("现金", Convert.ToString(row["收款方式"]));

                var monthly = db.Services.Reports.SalesMonthlyExportDetail(DateTime.Today);
                Assert.AreEqual(1, monthly.Rows.Count);
                var monthlyRow = monthly.Rows[0];
                Assert.AreEqual("原作者", Convert.ToString(monthlyRow["author"]));
                Assert.AreEqual("原出版社", Convert.ToString(monthlyRow["publisher"]));
                Assert.AreEqual("原分类", Convert.ToString(monthlyRow["category"]));
                Assert.AreEqual("2020", Convert.ToString(monthlyRow["publication_year"]));
                Assert.AreEqual(3000L, Convert.ToInt64(monthlyRow["list_price_cent"]));
                Assert.AreEqual(1200L, Convert.ToInt64(monthlyRow["cost_ref_cent"]));
                Assert.AreEqual("默认进价", Convert.ToString(monthlyRow["cost_ref_source"]));
            }
        }

        [TestMethod]
        public void DefaultPurchasePriceEdit_DoesNotRewriteHistoricalCosts()
        {
            using (var db = new TemporaryDatabase())
            {
                var bookId = db.Services.Books.Insert(new Book
                {
                    Title = "成本快照书",
                    DefaultPurchasePriceCent = 2000,
                    SalePriceCent = 3000
                });

                var draft = db.Services.Purchases.SaveDraft(
                    null,
                    "",
                    DateTime.Today,
                    null,
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput
                        {
                            BookId = bookId,
                            Quantity = 2,
                            UnitPriceCent = 2000,
                            BaseUnitPriceCent = 2000,
                            DiscountBasisPoints = 8000
                        }
                    },
                    "cost snapshot");
                db.Services.Purchases.Review(draft.Id);

                var before = db.Services.Purchases.GetDocument(draft.Id);
                Assert.AreEqual(2000L, before.Lines[0].BaseUnitCostCent);
                Assert.AreEqual(1600L, before.Lines[0].LineDiscountedUnitCostCent);
                Assert.AreEqual(1600L, before.Lines[0].UnitCostCent);

                var book = db.Services.Books.GetById(bookId);
                book.DefaultPurchasePriceCent = 9999;
                db.Services.Books.Update(book);

                var after = db.Services.Purchases.GetDocument(draft.Id);
                Assert.AreEqual(2000L, after.Lines[0].BaseUnitCostCent);
                Assert.AreEqual(1600L, after.Lines[0].LineDiscountedUnitCostCent);
                Assert.AreEqual(1600L, after.Lines[0].UnitCostCent);
                Assert.AreEqual(3200L, after.Lines[0].LineTotalCent);

                var report = db.Services.Reports.PurchaseDetail(DateTime.Today, DateTime.Today);
                Assert.AreEqual(1, report.Rows.Count);
                Assert.AreEqual(20.00m, Convert.ToDecimal(report.Rows[0]["原进价"]));
                Assert.AreEqual(16.00m, Convert.ToDecimal(report.Rows[0]["折后进价"]));
                Assert.AreEqual(16.00m, Convert.ToDecimal(report.Rows[0]["实际进价"]));
            }
        }

        [TestMethod]
        public void InventoryAdjustFailure_RollsBackStockAndLedger()
        {
            using (var db = new TemporaryDatabase())
            {
                var bookId = db.Services.Books.Insert(new Book { Title = "库存回滚书" });
                db.Services.Inventory.Adjust(bookId, 2, "期初库存");

                var stockBefore = db.Services.Books.GetById(bookId).StockQuantity;
                var ledgerBefore = CountLedgerRows(db, bookId, "ADJUSTMENT");
                Assert.AreEqual(2, stockBefore);
                Assert.AreEqual(1L, ledgerBefore);

                Assert.ThrowsException<InvalidOperationException>(
                    () => db.Services.Inventory.Adjust(bookId, -3, "超出库存"));
                Assert.AreEqual(stockBefore, db.Services.Books.GetById(bookId).StockQuantity);
                Assert.AreEqual(ledgerBefore, CountLedgerRows(db, bookId, "ADJUSTMENT"));

                Assert.ThrowsException<InvalidOperationException>(
                    () => db.Services.Inventory.Adjust(987654321L, 1, "不存在的图书"));
                Assert.AreEqual(stockBefore, db.Services.Books.GetById(bookId).StockQuantity);
                Assert.AreEqual(ledgerBefore, CountLedgerRows(db, bookId, "ADJUSTMENT"));

                Assert.ThrowsException<InvalidOperationException>(
                    () => db.Services.Inventory.Adjust(bookId, 0, "零调整"));
                Assert.AreEqual(stockBefore, db.Services.Books.GetById(bookId).StockQuantity);
                Assert.AreEqual(ledgerBefore, CountLedgerRows(db, bookId, "ADJUSTMENT"));
            }
        }

        [TestMethod]
        public void DisablingDictionaryValue_KeepsHistoricalDocumentSnapshots()
        {
            using (var db = new TemporaryDatabase())
            {
                var paymentId = db.Services.Dictionaries.Insert(new DictionaryValue
                {
                    DictionaryKey = DictionaryKeys.PaymentMethod,
                    Value = "测试支付",
                    SortOrder = 90,
                    Note = "self-test",
                    IsActive = true
                });

                var bookId = db.Services.Books.Insert(new Book
                {
                    Title = "字典测试书",
                    SalePriceCent = 2000
                });
                db.Services.Inventory.Adjust(bookId, 2, "期初库存");

                var orderNo = db.Services.Sales.Checkout(
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput { BookId = bookId, Quantity = 1, UnitPriceCent = 2000 }
                    },
                    "dictionary disable sale",
                    10000,
                    "测试支付",
                    null);

                CollectionAssert.Contains(
                    new List<string>(db.Services.Dictionaries.GetActiveValues(DictionaryKeys.PaymentMethod)),
                    "测试支付");

                var payment = db.Services.Dictionaries.GetById(paymentId);
                payment.IsActive = false;
                db.Services.Dictionaries.Update(payment);

                CollectionAssert.DoesNotContain(
                    new List<string>(db.Services.Dictionaries.GetActiveValues(DictionaryKeys.PaymentMethod)),
                    "测试支付");
                Assert.IsNotNull(db.Services.Dictionaries.GetById(paymentId), "停用不应删除字典值本身。");
                Assert.IsFalse(db.Services.Dictionaries.GetById(paymentId).IsActive);

                var documents = db.Services.Documents.Search("sale", DateTime.Today, DateTime.Today, "");
                DataRow matched = null;
                foreach (DataRow document in documents.Rows)
                {
                    if (string.Equals(Convert.ToString(document["单号"]), orderNo, StringComparison.Ordinal))
                    {
                        matched = document;
                        break;
                    }
                }
                Assert.IsNotNull(matched, "停用字典值后历史销售单应仍可查询。");
                Assert.AreEqual("测试支付", Convert.ToString(matched["收款方式"]));

                var detail = db.Services.Reports.SalesDetail(DateTime.Today, DateTime.Today);
                Assert.AreEqual(1, detail.Rows.Count);
                Assert.AreEqual("测试支付", Convert.ToString(detail.Rows[0]["收款方式"]));
            }
        }

        private static long CountLedgerRows(TemporaryDatabase db, long bookId, string type)
        {
            using (var connection = db.Services.Database.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(1) FROM inventory_transactions WHERE book_id=@id AND type=@type;";
                command.Parameters.AddWithValue("@id", bookId);
                command.Parameters.AddWithValue("@type", type);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }
    }
}
