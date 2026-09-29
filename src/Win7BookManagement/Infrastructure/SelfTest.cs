using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Win7BookManagement.Models;

namespace Win7BookManagement.Infrastructure
{
    public static class SelfTest
    {
        public static int Run()
        {
            var root = Path.Combine(Path.GetTempPath(), "Win7BookManagement-SelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var dbPath = Path.Combine(root, "test.db");
                var services = new ApplicationServices(dbPath);

                var supplierId = services.Suppliers.Insert(new Supplier { Name = "测试供应商" });
                var bookId = services.Books.Insert(new Book
                {
                    Isbn = "9780000000001",
                    Title = "自检图书",
                    Author = "Test",
                    SalePriceCent = 2000,
                    ListPriceCent = 3000
                });

                services.Inventory.Adjust(bookId, 5, "opening");
                services.Purchases.Receive(
                    supplierId,
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput { BookId = bookId, Quantity = 4, UnitPriceCent = 1000 }
                    },
                    "purchase");

                services.Sales.Checkout(
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput { BookId = bookId, Quantity = 2, UnitPriceCent = 2000 }
                    },
                    "sale");

                var book = services.Books.GetById(bookId);
                if (book == null || book.StockQuantity != 7)
                    throw new InvalidOperationException("库存事务自检失败。");

                try
                {
                    services.Sales.Checkout(
                        new List<TransactionLineInput>
                        {
                            new TransactionLineInput { BookId = bookId, Quantity = 999, UnitPriceCent = 2000 }
                        },
                        "should rollback");
                    throw new InvalidOperationException("库存不足校验未生效。");
                }
                catch (InvalidOperationException)
                {
                    if (services.Books.GetById(bookId).StockQuantity != 7)
                        throw new InvalidOperationException("失败销售没有正确回滚。");
                }

                var sales = services.Reports.SalesDetail(DateTime.Today, DateTime.Today);
                if (sales.Rows.Count != 1)
                    throw new InvalidOperationException("销售报表自检失败。");

                var snapshot = services.Reports.InventorySnapshot(DateTime.Today);
                if (snapshot.Rows.Count != 1 || Convert.ToInt32(snapshot.Rows[0]["库存数量"]) != 7)
                    throw new InvalidOperationException("库存快照自检失败。");

                var excelPath = Path.Combine(root, "sales.xlsx");
                services.Excel.Export(sales, excelPath, "销售明细");
                if (!File.Exists(excelPath) || new FileInfo(excelPath).Length == 0)
                    throw new InvalidOperationException("Excel 导出自检失败。");

                var backupPath = Path.Combine(root, "backup.db");
                services.Backup.CreateBackup(backupPath);
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
                    throw new InvalidOperationException("数据库备份自检失败。");

                return 0;
            }
            catch
            {
                return 1;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
