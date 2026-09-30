using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Win7BookManagement.Forms;
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
                    SelfCode = "BK-TEST-001",
                    PublicationYear = "2026",
                    Edition = "1版1印",
                    Binding = "平装",
                    ShelfCode = "A-01-1",
                    DefaultPurchasePriceCent = 1200,
                    SalePriceCent = 2000,
                    ListPriceCent = 3000,
                    Note = "self-test"
                });

                var storedBook = services.Books.GetById(bookId);
                if (storedBook.SelfCode != "BK-TEST-001" ||
                    storedBook.ShelfCode != "A-01-1" ||
                    storedBook.DefaultPurchasePriceCent != 1200 ||
                    storedBook.PublicationYear != "2026")
                    throw new InvalidOperationException("扩展图书资料字段保存自检失败。");

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

                VerifyLegacyBookSchemaUpgrade(root);
                VerifyUiLayoutContracts(services, storedBook);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test-error.txt"),
                        ex.ToString());
                }
                catch
                {
                }
                return 1;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void VerifyUiLayoutContracts(ApplicationServices services, Book storedBook)
        {
            var forms = new Form[]
            {
                new MainForm(services),
                new DashboardForm(services, delegate(string key) { }, delegate { }),
                new BookListForm(services),
                new BookEditForm(services, storedBook),
                new BookLookupDialog(services),
                new PurchaseForm(services),
                new SalesForm(services),
                new InventoryForm(services),
                new DocumentCenterForm(services),
                new ReportsForm(services),
                new SupplierForm(services),
                new BackupForm(services),
                new SettingsForm(services),
                new HelpForm(services, delegate { }, delegate(string key) { })
            };

            var viewports = new[]
            {
                new Size(1024, 768),
                new Size(1366, 768),
                new Size(1920, 1080)
            };

            try
            {
                foreach (var form in forms)
                {
                    form.CreateControl();

                    foreach (var viewport in viewports)
                    {
                        form.Size = viewport;
                        UiTheme.Apply(form);
                        form.PerformLayout();
                        VerifyControlTree(
                            form,
                            form.GetType().Name + "@" + viewport.Width + "x" + viewport.Height);
                    }
                }
            }
            finally
            {
                foreach (var form in forms)
                    form.Dispose();
            }
        }

        private static void VerifyControlTree(Control control, string formName)
        {
            var label = control as Label;
            if (label != null &&
                !label.AutoSize &&
                !string.IsNullOrWhiteSpace(label.Text) &&
                label.Visible)
            {
                var required = TextRenderer.MeasureText(
                    "国Ag",
                    label.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPrefix |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding).Height +
                    label.Padding.Vertical + 2;

                if (label.Height > 0 && label.Height < required)
                {
                    throw new InvalidOperationException(
                        formName + " 存在可能裁字的 Label：" +
                        label.Text + "，实际高度 " + label.Height +
                        "，安全高度至少 " + required + "。");
                }
            }

            var textBox = control as TextBox;
            if (textBox != null && !textBox.Multiline && !textBox.AutoSize)
            {
                throw new InvalidOperationException(
                    formName + " 存在强制固定高度的单行 TextBox。");
            }

            var button = control as Button;
            if (button != null && button.Visible)
            {
                var required = button.Font.Height + 12;
                if (button.Height > 0 && button.Height < required)
                {
                    throw new InvalidOperationException(
                        formName + " 存在高度不足的按钮：" + button.Text + "。");
                }
            }

            var table = control as TableLayoutPanel;
            if (table != null)
            {
                for (var row = 0; row < table.RowStyles.Count; row++)
                {
                    var style = table.RowStyles[row];
                    if (style.SizeType == SizeType.Absolute &&
                        style.Height > 0 &&
                        style.Height < 80F)
                    {
                        throw new InvalidOperationException(
                            formName + " 仍存在小于 80px 的固定 TableLayout 行。");
                    }
                }

                for (var column = 0; column < table.ColumnStyles.Count; column++)
                {
                    var style = table.ColumnStyles[column];
                    if (style.SizeType != SizeType.Absolute || style.Width <= 0)
                        continue;

                    foreach (Control child in table.Controls)
                    {
                        if (!child.Visible ||
                            table.GetColumn(child) != column ||
                            table.GetColumnSpan(child) != 1)
                        {
                            continue;
                        }

                        if (!(child is Label) &&
                            !(child is Button) &&
                            !(child is CheckBox) &&
                            !(child is ComboBox))
                        {
                            continue;
                        }

                        var preferred = child.GetPreferredSize(Size.Empty).Width + child.Margin.Horizontal;
                        if (preferred > 0 && style.Width + 4 < preferred)
                        {
                            throw new InvalidOperationException(
                                formName + " 存在可能横向裁字的固定 TableLayout 列：" +
                                style.Width + "px < " + preferred + "px，控件：" + child.Text);
                        }
                    }
                }
            }

            foreach (Control child in control.Controls)
                VerifyControlTree(child, formName);
        }

        private static void VerifyLegacyBookSchemaUpgrade(string root)
        {
            var legacyPath = Path.Combine(root, "legacy-v3.db");
            SQLiteConnection.CreateFile(legacyPath);

            using (var connection = new SQLiteConnection("Data Source=" + legacyPath + ";Version=3;"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE schema_info (version INTEGER NOT NULL);
INSERT INTO schema_info(version) VALUES(3);

CREATE TABLE books (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    isbn TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL,
    author TEXT NOT NULL DEFAULT '',
    publisher TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL DEFAULT '',
    list_price_cent INTEGER NOT NULL DEFAULT 0,
    sale_price_cent INTEGER NOT NULL DEFAULT 0,
    stock_quantity INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

INSERT INTO books
(isbn, title, author, publisher, category, list_price_cent, sale_price_cent,
 stock_quantity, is_active, created_at, updated_at)
VALUES
('9780000000002', '旧库升级图书', 'Legacy', '', '', 3000, 2000, 2, 1,
 '2026-01-01 00:00:00', '2026-01-01 00:00:00');";
                    command.ExecuteNonQuery();
                }
            }

            var upgraded = new ApplicationServices(legacyPath);
            var legacyBook = upgraded.Books.FindByExactIsbn("9780000000002");
            if (legacyBook == null ||
                legacyBook.Title != "旧库升级图书" ||
                legacyBook.DefaultPurchasePriceCent != 0 ||
                legacyBook.ShelfCode != "")
                throw new InvalidOperationException("V3 图书数据库前向升级自检失败。");

            legacyBook.ShelfCode = "LEGACY-01";
            upgraded.Books.Update(legacyBook);
            if (upgraded.Books.GetById(legacyBook.Id).ShelfCode != "LEGACY-01")
                throw new InvalidOperationException("升级后新增字段写入自检失败。");
        }
    }
}
