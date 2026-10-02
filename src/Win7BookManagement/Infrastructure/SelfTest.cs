using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Win7BookManagement.Forms;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

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
                    Publisher = "测试出版社",
                    Category = "测试分类",
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

                var selfPublishedId = services.Books.Insert(new Book
                {
                    Title = "无 ISBN 自出版物",
                    Author = "独立作者",
                    Publisher = "",
                    Isbn = "",
                    SelfCode = "",
                    ListPriceCent = 4200,
                    SalePriceCent = 4200,
                    DefaultPurchasePriceCent = 1800
                });
                var selfPublished = services.Books.GetById(selfPublishedId);
                if (selfPublished == null ||
                    string.IsNullOrWhiteSpace(selfPublished.SelfCode) ||
                    !selfPublished.SelfCode.StartsWith("BK-", StringComparison.Ordinal) ||
                    selfPublished.Isbn != "")
                    throw new InvalidOperationException("无 ISBN 图书自动店内编码自检失败。");

                var seededCategories = services.Dictionaries.GetActiveValues(DictionaryKeys.BookCategory);
                if (!seededCategories.Contains("测试分类"))
                    throw new InvalidOperationException("图书分类字典自动同步自检失败。");

                var paymentMethods = services.Dictionaries.GetActiveValues(DictionaryKeys.PaymentMethod);
                if (!paymentMethods.Contains("微信") ||
                    !paymentMethods.Contains("支付宝") ||
                    !paymentMethods.Contains("现金"))
                    throw new InvalidOperationException("默认收款方式字典自检失败。");

                selfPublished.Category = "临时分类";
                services.Books.Update(selfPublished);
                var tempCategories = services.Dictionaries.Search(
                    DictionaryKeys.BookCategory,
                    "临时分类",
                    false);
                if (tempCategories.Count != 1)
                    throw new InvalidOperationException("新增图书分类字典值自检失败。");

                var renamedCategory = tempCategories[0];
                renamedCategory.Value = "重命名分类";
                services.Dictionaries.Update(renamedCategory);
                if (services.Books.GetById(selfPublishedId).Category != "重命名分类")
                    throw new InvalidOperationException("图书分类重命名同步自检失败。");

                var selfCodeMatches = services.Books.SearchActiveByIsbnOrTitle(selfPublished.SelfCode);
                var authorMatches = services.Books.SearchActiveByIsbnOrTitle("独立作者");
                if (selfCodeMatches.Count != 1 || selfCodeMatches[0].Id != selfPublishedId ||
                    authorMatches.Count != 1 || authorMatches[0].Id != selfPublishedId)
                    throw new InvalidOperationException("无 ISBN 图书编码 / 作者检索自检失败。");

                var titleMatches = services.Books.SearchActiveByIsbnOrTitle("自检");
                var isbnMatches = services.Books.SearchActiveByIsbnOrTitle("000000");
                if (titleMatches.Count != 1 || titleMatches[0].Id != bookId ||
                    isbnMatches.Count != 1 || isbnMatches[0].Id != bookId)
                    throw new InvalidOperationException("采购入库 ISBN / 书名模糊搜索自检失败。");

                var exactByIsbn = services.Books.FindByExactIdentifier("9780000000001");
                var exactBySelfCode = services.Books.FindByExactIdentifier("BK-TEST-001");
                var advancedMatches = services.Books.SearchAdvanced(new BookSearchCriteria
                {
                    Title = "自检",
                    Publisher = "测试",
                    Category = "测试分类"
                });
                var categories = services.Books.GetActiveCategories();
                if (exactByIsbn == null || exactByIsbn.Id != bookId ||
                    exactBySelfCode == null || exactBySelfCode.Id != bookId ||
                    advancedMatches.Count != 1 || advancedMatches[0].Id != bookId ||
                    !categories.Contains("测试分类"))
                    throw new InvalidOperationException("采购表格扫码精确匹配 / 高级查找自检失败。");

                services.Inventory.Adjust(bookId, 5, "opening");

                var purchaseDraft = services.Purchases.SaveDraft(
                    null,
                    "",
                    DateTime.Today,
                    supplierId,
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput { BookId = bookId, Quantity = 4, UnitPriceCent = 1000 }
                    },
                    "purchase");
                if (services.Books.GetById(bookId).StockQuantity != 5 ||
                    purchaseDraft.IsReviewed ||
                    !purchaseDraft.OrderNo.StartsWith(DateTime.Today.ToString("yyyyMMdd"), StringComparison.Ordinal))
                    throw new InvalidOperationException("采购草稿保存或自动单号自检失败。");

                var olderPurchaseDraft = services.Purchases.SaveDraft(
                    null,
                    "",
                    DateTime.Today.AddDays(-1),
                    null,
                    new List<TransactionLineInput>(),
                    "navigation older");
                var newerPurchaseDraft = services.Purchases.SaveDraft(
                    null,
                    "",
                    DateTime.Today.AddDays(1),
                    null,
                    new List<TransactionLineInput>(),
                    "navigation newer");
                var navigation = services.Purchases.GetNavigationState(purchaseDraft.Id);
                if (navigation.TotalCount != 3 ||
                    navigation.Position != 2 ||
                    navigation.PreviousId != olderPurchaseDraft.Id ||
                    navigation.NextId != newerPurchaseDraft.Id ||
                    services.Purchases.GetAdjacentDocumentId(purchaseDraft.Id, false) != olderPurchaseDraft.Id ||
                    services.Purchases.GetAdjacentDocumentId(purchaseDraft.Id, true) != newerPurchaseDraft.Id)
                    throw new InvalidOperationException("采购单上一张 / 下一张导航顺序自检失败。");

                services.Purchases.DeleteDraft(olderPurchaseDraft.Id);
                services.Purchases.DeleteDraft(newerPurchaseDraft.Id);
                var singleNavigation = services.Purchases.GetNavigationState(purchaseDraft.Id);
                if (singleNavigation.TotalCount != 1 ||
                    singleNavigation.Position != 1 ||
                    singleNavigation.HasPrevious ||
                    singleNavigation.HasNext ||
                    services.Purchases.GetDocument(olderPurchaseDraft.Id) != null ||
                    services.Purchases.GetDocument(newerPurchaseDraft.Id) != null)
                    throw new InvalidOperationException("采购草稿删除或导航边界自检失败。");

                services.Purchases.Review(purchaseDraft.Id);
                if (services.Books.GetById(bookId).StockQuantity != 9)
                    throw new InvalidOperationException("采购复核没有正确增加库存。");

                var purchaseHistoryByTitle = services.Purchases.SearchHistory(
                    "自检图书",
                    PurchaseService.ReviewedStatus,
                    50);
                if (purchaseHistoryByTitle.Count != 1 ||
                    purchaseHistoryByTitle[0].Id != purchaseDraft.Id)
                    throw new InvalidOperationException("采购历史 ISBN / 书名 / 状态筛选自检失败。");

                var copiedPurchaseDraft = services.Purchases.CopyToNewDraft(
                    purchaseDraft.Id,
                    DateTime.Today);
                if (copiedPurchaseDraft.IsReviewed ||
                    copiedPurchaseDraft.Id == purchaseDraft.Id ||
                    copiedPurchaseDraft.SupplierId != purchaseDraft.SupplierId ||
                    copiedPurchaseDraft.Lines.Count != 1 ||
                    copiedPurchaseDraft.Lines[0].BookId != bookId ||
                    copiedPurchaseDraft.Lines[0].Quantity != 4 ||
                    copiedPurchaseDraft.Lines[0].UnitCostCent != 1000 ||
                    services.Books.GetById(bookId).StockQuantity != 9)
                    throw new InvalidOperationException("采购单复制为新草稿自检失败。");
                services.Purchases.DeleteDraft(copiedPurchaseDraft.Id);

                services.Purchases.Unreview(purchaseDraft.Id);
                if (services.Books.GetById(bookId).StockQuantity != 5 ||
                    services.Purchases.GetDocument(purchaseDraft.Id).IsReviewed)
                    throw new InvalidOperationException("采购反复核没有正确撤销库存。");

                var historicalDraftDeleteRejected = false;
                try
                {
                    services.Purchases.DeleteDraft(purchaseDraft.Id);
                }
                catch (InvalidOperationException)
                {
                    historicalDraftDeleteRejected = true;
                }
                if (!historicalDraftDeleteRejected ||
                    services.Purchases.GetDocument(purchaseDraft.Id) == null)
                    throw new InvalidOperationException("曾产生库存流水的采购单不应允许删除。");

                services.Purchases.Review(purchaseDraft.Id);

                var heldDraft = services.Sales.SaveDraft(
                    null,
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput
                        {
                            BookId = bookId,
                            Quantity = 1,
                            UnitPriceCent = 2000,
                            BaseUnitPriceCent = 2000,
                            DiscountBasisPoints = 9500
                        }
                    },
                    "held sale",
                    9000);
                if (heldDraft == null ||
                    heldDraft.Lines.Count != 1 ||
                    heldDraft.Lines[0].BookId != bookId ||
                    services.Books.GetById(bookId).StockQuantity != 9)
                    throw new InvalidOperationException("销售挂单保存不应改变库存。");

                var draftSummaries = services.Sales.GetDraftSummaries();
                if (draftSummaries.Count != 1 ||
                    draftSummaries[0].Id != heldDraft.Id ||
                    draftSummaries[0].QuantityTotal != 1)
                    throw new InvalidOperationException("销售挂单列表自检失败。");

                services.Sales.DeleteDraft(heldDraft.Id);
                if (services.Sales.GetDraftSummaries().Count != 0)
                    throw new InvalidOperationException("销售挂单删除自检失败。");

                services.Sales.Checkout(
                    new List<TransactionLineInput>
                    {
                        new TransactionLineInput
                        {
                            BookId = bookId,
                            Quantity = 2,
                            UnitPriceCent = 2000,
                            BaseUnitPriceCent = 2000,
                            DiscountBasisPoints = 9000
                        }
                    },
                    "sale",
                    8000,
                    "现金",
                    3000);

                if (services.Books.GetById(bookId).StockQuantity != 7)
                    throw new InvalidOperationException("采购/销售库存事务自检失败。");

                var saleDocs = services.Documents.Search("sale", DateTime.Today, DateTime.Today, "");
                var purchaseDocs = services.Documents.Search("purchase", DateTime.Today, DateTime.Today, "");
                if (saleDocs.Rows.Count != 1 || purchaseDocs.Rows.Count != 1 ||
                    Convert.ToString(saleDocs.Rows[0]["收款方式"]) != "现金" ||
                    Convert.ToDecimal(saleDocs.Rows[0]["实收金额"]) != 30.00m ||
                    Convert.ToDecimal(saleDocs.Rows[0]["找零金额"]) != 1.20m)
                    throw new InvalidOperationException("单据中心原单 / 收款快照查询自检失败。");

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
                if (saleItems.Rows.Count != 1 ||
                    Convert.ToInt32(saleItems.Rows[0]["已退"]) != 1 ||
                    Convert.ToDecimal(saleItems.Rows[0]["原价"]) != 20.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["单品折扣%"]) != 90.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["整单折扣%"]) != 80.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["实收单价"]) != 14.40m)
                    throw new InvalidOperationException("原销售单退货状态 / 折扣快照自检失败。");

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
                if (dashboard.ActiveTitles != 2 ||
                    dashboard.StockUnits != 7 ||
                    dashboard.TodaySalesOrders != 1 ||
                    dashboard.TodaySalesQuantity != 1 ||
                    dashboard.TodaySalesCent != 1440)
                    throw new InvalidOperationException("退货后的净销售经营概览自检失败。");

                var sales = services.Reports.SalesDetail(DateTime.Today, DateTime.Today);
                var saleReturns = services.Reports.SalesReturnDetail(DateTime.Today, DateTime.Today);
                var purchaseReturns = services.Reports.PurchaseReturnDetail(DateTime.Today, DateTime.Today);
                if (sales.Rows.Count != 1 ||
                    saleReturns.Rows.Count != 1 ||
                    purchaseReturns.Rows.Count != 1 ||
                    Convert.ToDecimal(sales.Rows[0]["原单价"]) != 20.00m ||
                    Convert.ToDecimal(sales.Rows[0]["单品折扣%"]) != 90.00m ||
                    Convert.ToDecimal(sales.Rows[0]["整单折扣%"]) != 80.00m ||
                    Convert.ToDecimal(sales.Rows[0]["实收单价"]) != 14.40m ||
                    Convert.ToString(sales.Rows[0]["收款方式"]) != "现金")
                    throw new InvalidOperationException("销售折扣 / 收款方式 / 退货报表自检失败。");

                var snapshot = services.Reports.InventorySnapshot(DateTime.Today);
                var selfPublishedSnapshotFound = false;
                foreach (System.Data.DataRow row in snapshot.Rows)
                {
                    if (string.Equals(
                            Convert.ToString(row["店内编码"]),
                            selfPublished.SelfCode,
                            StringComparison.Ordinal) &&
                        Convert.ToInt32(row["库存数量"]) == 0)
                    {
                        selfPublishedSnapshotFound = true;
                        break;
                    }
                }
                if (snapshot.Rows.Count != 2 || !selfPublishedSnapshotFound)
                    throw new InvalidOperationException("库存快照自检失败。");

                var excelPath = Path.Combine(root, "sales-returns.xlsx");
                services.Excel.Export(saleReturns, excelPath, "销售退货明细");
                if (!File.Exists(excelPath) || new FileInfo(excelPath).Length == 0)
                    throw new InvalidOperationException("退货 Excel 导出自检失败。");

                services.Settings.SetReportStoreName("目田书店");
                services.Settings.SetReportNightShiftStartHour(14);
                var monthlyDetail = services.Reports.SalesMonthlyExportDetail(DateTime.Today);
                var monthlyExcelPath = Path.Combine(root, "sales-monthly.xlsx");
                services.SalesMonthlyExcel.Export(
                    monthlyDetail,
                    monthlyExcelPath,
                    DateTime.Today,
                    services.Settings.GetReportStoreName(),
                    services.Settings.GetReportNightShiftStartHour());

                if (!File.Exists(monthlyExcelPath) ||
                    new FileInfo(monthlyExcelPath).Length == 0)
                    throw new InvalidOperationException("销售月报 Excel 导出自检失败。");

                using (var monthlyStream = File.OpenRead(monthlyExcelPath))
                {
                    var monthlyWorkbook = new XSSFWorkbook(monthlyStream);
                    try
                    {
                        var expectedSheets = DateTime.DaysInMonth(
                            DateTime.Today.Year,
                            DateTime.Today.Month) + 1;
                        if (monthlyWorkbook.NumberOfSheets != expectedSheets)
                            throw new InvalidOperationException("销售月报 Sheet 数量自检失败。");

                        var summarySheet = monthlyWorkbook.GetSheet("销售月报表");
                        if (summarySheet == null ||
                            summarySheet.NumMergedRegions < 100 ||
                            summarySheet.GetRow(0).GetCell(0).StringCellValue !=
                                "目田书店 " + DateTime.Today.Month + "月份销售报表" ||
                            summarySheet.GetRow(3).HeightInPoints < 30F ||
                            summarySheet.GetColumnWidth(1) < 3000)
                            throw new InvalidOperationException("销售月报主表结构 / 合并 / 列宽自检失败。");

                        var daySheet = monthlyWorkbook.GetSheet(DateTime.Today.ToString("MMdd"));
                        if (daySheet == null ||
                            daySheet.GetRow(1).GetCell(0).StringCellValue != "序号" ||
                            daySheet.GetRow(1).GetCell(29).StringCellValue != "作者" ||
                            daySheet.GetRow(1).GetCell(30).StringCellValue != "收款方式" ||
                            daySheet.GetRow(2).GetCell(30).StringCellValue != "现金" ||
                            Math.Abs(daySheet.GetRow(2).GetCell(31).NumericCellValue - 28.80) > 0.001 ||
                            Math.Abs(daySheet.GetRow(2).GetCell(32).NumericCellValue - 30.00) > 0.001 ||
                            Math.Abs(daySheet.GetRow(2).GetCell(33).NumericCellValue - 1.20) > 0.001)
                            throw new InvalidOperationException("销售月报每日明细 / 收款快照自检失败。");
                    }
                    finally
                    {
                        monthlyWorkbook.Close();
                    }
                }

                var backupPath = Path.Combine(root, "backup.db");
                services.Backup.CreateBackup(backupPath);
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
                    throw new InvalidOperationException("数据库备份自检失败。");

                VerifyLegacyBookSchemaUpgrade(root);
                VerifyPersistentTableWidths(services);
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

        private static void VerifyPersistentTableWidths(ApplicationServices services)
        {
            const string layoutKey = "self-test";
            const string columnKey = "Title";
            const int expectedWidth = 237;

            services.Settings.SetInt(
                "ui.table.column_width." + layoutKey + "." + columnKey,
                expectedWidth);

            using (var table = new PersistentAntdTable())
            {
                var title = new AntdUI.Column(columnKey, "书名")
                {
                    Width = "120",
                    MinWidth = "96"
                };

                table.Columns = new AntdUI.ColumnCollection { title };
                table.ConfigureColumnPersistence(services.Settings, layoutKey);

                if (!string.Equals(title.Width, expectedWidth.ToString(), StringComparison.Ordinal))
                    throw new InvalidOperationException("AntdUI 表格列宽持久化恢复自检失败。");
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
                new AdvancedBookLookupDialog(services, "", null),
                new PurchaseForm(services),
                new SalesForm(services),
                new InventoryForm(services),
                new DocumentCenterForm(services),
                new ReportsForm(services),
                new DictionaryManagementForm(services),
                new SupplierForm(services),
                new BackupForm(services),
                new SettingsForm(services),
                new HelpForm(services, delegate { }, delegate(string key) { })
            };

            var viewports = new[]
            {
                new Size(1024, 768),
                new Size(1366, 768),
                new Size(1600, 900),
                new Size(1920, 1080),
                new Size(2560, 1440)
            };

            if (!(UiTheme.ResponsiveButtonHeight(820) < UiTheme.ResponsiveButtonHeight(1500)) ||
                !(UiTheme.ResponsiveInputHeight(820) < UiTheme.ResponsiveInputHeight(1500)) ||
                !(UiTheme.ResponsiveControlFontSize(820) < UiTheme.ResponsiveControlFontSize(1500)))
            {
                throw new InvalidOperationException("响应式 UI 密度断点自检失败。");
            }

            foreach (var width in new[] { 820, 1024, 1366, 1920 })
            {
                if (UiTheme.ResponsiveButtonHeight(width) != UiTheme.ResponsiveInputHeight(width))
                    throw new InvalidOperationException(
                        "搜索工具栏按钮与输入框高度不一致：" + width + "px。");
            }

            try
            {
                foreach (var form in forms)
                {
                    form.CreateControl();

                    foreach (var viewport in viewports)
                    {
                        form.Size = viewport;
                        UiTheme.Apply(form);
                        var specPage = form as IUiSpecPage;
                        if (specPage != null)
                            specPage.ApplyUiSpecProfile(
                                BookDeskUiSpec.Resolve(viewport.Width, viewport.Height));
                        else
                            UiTheme.ApplyResponsiveDensity(form, viewport.Width);
                        form.PerformLayout();
                        VerifyControlTree(
                            form,
                            form.GetType().Name + "@" + viewport.Width + "x" + viewport.Height,
                            viewport.Width);
                    }
                }
            }
            finally
            {
                foreach (var form in forms)
                    form.Dispose();
            }
        }

        private static void VerifyControlTree(Control control, string formName, int viewportWidth)
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

            var expectedInputHeight = UiTheme.ResponsiveInputHeight(viewportWidth);
            var antdInput = control as AntdUI.Input;
            if (antdInput != null && antdInput.Visible &&
                antdInput.Height > 0 && antdInput.Height < expectedInputHeight)
            {
                throw new InvalidOperationException(
                    formName + " 存在高度不足的 AntdUI 输入框：" +
                    antdInput.Height + "px < " + expectedInputHeight + "px。");
            }

            var antdSelect = control as AntdUI.Select;
            if (antdSelect != null && antdSelect.Visible &&
                antdSelect.Height > 0 && antdSelect.Height < expectedInputHeight)
            {
                throw new InvalidOperationException(
                    formName + " 存在高度不足的 AntdUI 下拉框：" +
                    antdSelect.Height + "px < " + expectedInputHeight + "px。");
            }

            var antdDate = control as AntdUI.DatePicker;
            if (antdDate != null && antdDate.Visible &&
                antdDate.Height > 0 && antdDate.Height < expectedInputHeight)
            {
                throw new InvalidOperationException(
                    formName + " 存在高度不足的 AntdUI 日期框：" +
                    antdDate.Height + "px < " + expectedInputHeight + "px。");
            }

            var expectedButtonHeight = UiTheme.ResponsiveButtonHeight(viewportWidth);
            var antdButton = control as AntdUI.Button;
            if (antdButton != null && antdButton.Visible &&
                antdButton.Height > 0 && antdButton.Height < expectedButtonHeight)
            {
                throw new InvalidOperationException(
                    formName + " 存在高度不足的 AntdUI 按钮：" +
                    antdButton.Text + "。");
            }

            if (string.Equals(Convert.ToString(control.Tag), "ui-input-frame", StringComparison.Ordinal) &&
                control.Visible &&
                control.Height > 52)
            {
                throw new InvalidOperationException(
                    formName + " 存在异常过高的单行输入容器：" +
                    control.Height + "px。");
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
                VerifyToolbarMidline(table, formName);

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
                VerifyControlTree(child, formName, viewportWidth);
        }

        private static void VerifyToolbarMidline(TableLayoutPanel table, string formName)
        {
            for (var row = 0; row < table.RowCount; row++)
            {
                Control input = null;
                var actions = new List<Control>();

                foreach (Control child in table.Controls)
                {
                    if (!child.Visible || table.GetRow(child) != row)
                        continue;

                    var tag = Convert.ToString(child.Tag);
                    if (string.Equals(tag, "toolbar-input", StringComparison.Ordinal))
                        input = child;
                    else if (string.Equals(tag, "toolbar-action", StringComparison.Ordinal))
                        actions.Add(child);
                }

                if (input == null || actions.Count == 0)
                    continue;

                var inputCenterY = input.Top + (input.Height / 2.0);
                foreach (var action in actions)
                {
                    var actionCenterY = action.Top + (action.Height / 2.0);
                    if (Math.Abs(inputCenterY - actionCenterY) > 2.0)
                    {
                        throw new InvalidOperationException(
                            formName + " 搜索工具栏控件未按中线对齐：" +
                            action.Text + " 与输入框中线相差 " +
                            Math.Abs(inputCenterY - actionCenterY).ToString("0.0") + "px。");
                    }

                    if (action.Height != input.Height)
                    {
                        throw new InvalidOperationException(
                            formName + " 搜索工具栏按钮与输入框高度不一致：" +
                            action.Text + " " + action.Height + "px / 输入框 " +
                            input.Height + "px。");
                    }
                }
            }
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
