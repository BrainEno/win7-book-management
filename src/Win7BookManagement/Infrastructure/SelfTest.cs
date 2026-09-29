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

                if (services.Settings.GetLowStockThreshold() != 3)
                    throw new InvalidOperationException("默认低库存设置自检失败。");

                services.Settings.SetLowStockThreshold(5);
                if (services.Settings.GetLowStockThreshold() != 5)
                    throw new InvalidOperationException("设置保存自检失败。");

                if (services.Settings.IsOnboardingCompleted())
                    throw new InvalidOperationException("新数据库不应默认完成新手引导。");

                services.Settings.SetOnboardingCompleted(true);
                if (!services.Settings.IsOnboardingCompleted())
                    throw new InvalidOperationException("新手引导完成状态保存失败。");

                services.Settings.SetHomeGuideExpanded(false);
                if (services.Settings.IsHomeGuideExpanded())
                    throw new InvalidOperationException("首页指南展开状态保存失败。");

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

                if (services.Books.GetById(bookId).StockQuantity != 7)
                    throw new InvalidOperationException("采购/销售库存事务自检失败。");

                var saleDocs = services.Documents.Search("sale", DateTime.Today, DateTime.Today, "");
                var purchaseDocs = services.Documents.Search("purchase", DateTime.Today, DateTime.Today, "");
                if (saleDocs.Rows.Count != 1 || purchaseDocs.Rows.Count != 1)
                    throw new InvalidOperationException("单据中心原单查询自检失败。");

                var saleId = Convert.ToInt64(saleDocs.Rows[0]["Id"]);
                var purchaseId = Convert.ToInt64(purchaseDocs.Rows[0]["Id"]);
                var saleReturnable = services.Documents.GetReturnableLines("sale", saleId);
                var purchaseReturnable = services.Documents.GetReturnableLines("purchase", purchaseId);

                services.Returns.CreateSalesReturn(
                    saleId,
                    new List<ReturnLineInput>
                    {
                        new ReturnLineInput { SourceItemId = saleReturnable[0].SourceItemId, Quantity = 1 }
                    },
                    "sale return");

                if (services.Books.GetById(bookId).StockQuantity != 8)
                    throw new InvalidOperationException("销售退货没有正确加回库存。");

                services.Returns.CreatePurchaseReturn(
                    purchaseId,
                    new List<ReturnLineInput>
                    {
                        new ReturnLineInput { SourceItemId = purchaseReturnable[0].SourceItemId, Quantity = 1 }
                    },
                    "purchase return");

                if (services.Books.GetById(bookId).StockQuantity != 7)
                    throw new InvalidOperationException("采购退货没有正确扣减库存。");

                try
                {
                    services.Returns.CreateSalesReturn(
                        saleId,
                        new List<ReturnLineInput>
                        {
                            new ReturnLineInput { SourceItemId = saleReturnable[0].SourceItemId, Quantity = 2 }
                        },
                        "over return");
                    throw new InvalidOperationException("超量销售退货校验未生效。");
                }
                catch (InvalidOperationException)
                {
                    if (services.Books.GetById(bookId).StockQuantity != 7)
                        throw new InvalidOperationException("失败退货没有正确回滚。");
                }

                var saleReturnDocs = services.Documents.Search("sale_return", DateTime.Today, DateTime.Today, "");
                var purchaseReturnDocs = services.Documents.Search("purchase_return", DateTime.Today, DateTime.Today, "");
                if (saleReturnDocs.Rows.Count != 1 || purchaseReturnDocs.Rows.Count != 1)
                    throw new InvalidOperationException("退货单据查询自检失败。");

                var saleItems = services.Documents.GetItems("sale", saleId);
                if (saleItems.Rows.Count != 1 || Convert.ToInt32(saleItems.Rows[0]["已退"]) != 1)
                    throw new InvalidOperationException("原销售单退货状态自检失败。");

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

                var dashboard = services.Dashboard.GetSummary(5);
                if (dashboard.ActiveTitles != 1 ||
                    dashboard.StockUnits != 7 ||
                    dashboard.TodaySalesOrders != 1 ||
                    dashboard.TodaySalesQuantity != 1 ||
                    dashboard.TodaySalesCent != 2000)
                    throw new InvalidOperationException("退货后的净销售经营概览自检失败。");

                var sales = services.Reports.SalesDetail(DateTime.Today, DateTime.Today);
                var saleReturns = services.Reports.SalesReturnDetail(DateTime.Today, DateTime.Today);
                var purchaseReturns = services.Reports.PurchaseReturnDetail(DateTime.Today, DateTime.Today);
                if (sales.Rows.Count != 1 || saleReturns.Rows.Count != 1 || purchaseReturns.Rows.Count != 1)
                    throw new InvalidOperationException("销售/退货报表自检失败。");

                var snapshot = services.Reports.InventorySnapshot(DateTime.Today);
                if (snapshot.Rows.Count != 1 || Convert.ToInt32(snapshot.Rows[0]["库存数量"]) != 7)
                    throw new InvalidOperationException("库存快照自检失败。");

                var excelPath = Path.Combine(root, "sales-returns.xlsx");
                services.Excel.Export(saleReturns, excelPath, "销售退货明细");
                if (!File.Exists(excelPath) || new FileInfo(excelPath).Length == 0)
                    throw new InvalidOperationException("退货 Excel 导出自检失败。");

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
