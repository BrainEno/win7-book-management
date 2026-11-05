using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
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
            Win7BookManagement.Forms.MainForm.SuppressStartupPrompts = true;
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
                    Isbn = "9780000000002",
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

                // Exercise the actual sales entry handler, not only repository searches.
                using (var salesLookup = new SalesForm(services))
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var input = (AntdUI.Input)typeof(SalesForm).GetField("_isbn", flags).GetValue(salesLookup);
                    var cart = (System.Collections.IList)typeof(SalesForm).GetField("_rows", flags).GetValue(salesLookup);
                    var add = typeof(SalesForm).GetMethod("AddByIsbn", flags);
                    foreach (var term in new[] { selfPublished.SelfCode, "自出版物", "独立作者" })
                    {
                        input.Text = term;
                        add.Invoke(salesLookup, null);
                        if (cart.Count != 1 || (long)cart[0].GetType().GetProperty("BookId").GetValue(cart[0], null) != selfPublishedId || input.Text != "")
                            throw new InvalidOperationException("销售开单店内编码 / 书名 / 作者查找自检失败。");
                    }
                }

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

                var exactByIsbn = services.Books.FindByExactIdentifier("9780000000002");
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
                        new TransactionLineInput
                        {
                            BookId = bookId,
                            Quantity = 4,
                            UnitPriceCent = 2000,
                            BaseUnitPriceCent = 2000,
                            DiscountBasisPoints = 8000
                        }
                    },
                    "purchase",
                    6250);
                if (services.Books.GetById(bookId).StockQuantity != 5 ||
                    purchaseDraft.IsReviewed ||
                    purchaseDraft.SubtotalCent != 8000 ||
                    purchaseDraft.LineDiscountCent != 1600 ||
                    purchaseDraft.OrderDiscountBasisPoints != 6250 ||
                    purchaseDraft.OrderDiscountCent != 2400 ||
                    purchaseDraft.TotalCent != 4000 ||
                    !purchaseDraft.OrderNo.StartsWith(DateTime.Today.ToString("yyyyMMdd"), StringComparison.Ordinal))
                    throw new InvalidOperationException("采购折扣草稿保存或自动单号自检失败。");

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
                    copiedPurchaseDraft.OrderDiscountBasisPoints != 6250 ||
                    copiedPurchaseDraft.Lines[0].BaseUnitCostCent != 2000 ||
                    copiedPurchaseDraft.Lines[0].LineDiscountBasisPoints != 8000 ||
                    copiedPurchaseDraft.Lines[0].LineDiscountedUnitCostCent != 1600 ||
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
                    "sale return",
                    "微信");

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
                if (saleReturnDocs.Rows.Count != 1 ||
                    purchaseReturnDocs.Rows.Count != 1 ||
                    Convert.ToString(saleReturnDocs.Rows[0]["退款方式"]) != "微信")
                    throw new InvalidOperationException("退货单据 / 退款方式查询自检失败。");

                var saleItems = services.Documents.GetItems("sale", saleId);
                if (saleItems.Rows.Count != 1 ||
                    Convert.ToInt32(saleItems.Rows[0]["已退"]) != 1 ||
                    Convert.ToDecimal(saleItems.Rows[0]["原价"]) != 20.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["单品折扣%"]) != 90.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["整单折扣%"]) != 80.00m ||
                    Convert.ToDecimal(saleItems.Rows[0]["实收单价"]) != 14.40m)
                    throw new InvalidOperationException("原销售单退货状态 / 折扣快照自检失败。");

                var purchaseItems = services.Documents.GetItems("purchase", purchaseId);
                if (purchaseItems.Rows.Count != 1 ||
                    Convert.ToDecimal(purchaseItems.Rows[0]["原进价"]) != 20.00m ||
                    Convert.ToDecimal(purchaseItems.Rows[0]["单品折扣%"]) != 80.00m ||
                    Convert.ToDecimal(purchaseItems.Rows[0]["折后进价"]) != 16.00m ||
                    Convert.ToDecimal(purchaseItems.Rows[0]["整单折扣%"]) != 62.50m ||
                    Convert.ToDecimal(purchaseItems.Rows[0]["实际进价"]) != 10.00m)
                    throw new InvalidOperationException("采购折扣快照 / 单据中心展示自检失败。");

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
                    Convert.ToString(sales.Rows[0]["收款方式"]) != "现金" ||
                    Convert.ToString(sales.Rows[0]["作者"]) != "Test" ||
                    Convert.ToString(sales.Rows[0]["出版社"]) != "测试出版社" ||
                    Convert.ToString(sales.Rows[0]["分类"]) != "测试分类" ||
                    Convert.ToString(saleReturns.Rows[0]["退款方式"]) != "微信")
                    throw new InvalidOperationException("销售折扣 / 历史快照 / 收退款方式报表自检失败。");

                var operatingDaily = services.Reports.OperatingDailySummary(DateTime.Today);
                var operatingMonthly = services.Reports.OperatingMonthlySummary(DateTime.Today);
                if (operatingDaily.Rows.Count != 1 ||
                    Convert.ToInt64(operatingDaily.Rows[0]["订单数"]) != 1 ||
                    Convert.ToInt64(operatingDaily.Rows[0]["销售册数"]) != 2 ||
                    Convert.ToInt64(operatingDaily.Rows[0]["退货册数"]) != 1 ||
                    Convert.ToInt64(operatingDaily.Rows[0]["净销售册数"]) != 1 ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["原金额"]) != 40.00m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["优惠额"]) != 11.20m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["销售额"]) != 28.80m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["退货额"]) != 14.40m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["净销售"]) != 14.40m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["客单价"]) != 28.80m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["平均成交折扣%"]) != 72.00m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["参考成本"]) != 10.00m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["参考毛利"]) != 4.40m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["参考毛利率%"]) != 30.56m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["微信净额"]) != -14.40m ||
                    Convert.ToDecimal(operatingDaily.Rows[0]["现金净额"]) != 28.80m)
                    throw new InvalidOperationException("经营日报口径自检失败。");

                var expectedOperatingRows =
                    DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) + 1;
                var operatingMonthTotal =
                    operatingMonthly.Rows[operatingMonthly.Rows.Count - 1];
                if (operatingMonthly.Rows.Count != expectedOperatingRows ||
                    Convert.ToString(operatingMonthTotal["日期"]) != "本月合计" ||
                    Convert.ToDecimal(operatingMonthTotal["净销售"]) != 14.40m ||
                    Convert.ToDecimal(operatingMonthTotal["参考毛利"]) != 4.40m)
                    throw new InvalidOperationException("月度经营摘要自检失败。");

                var purchaseMonthlySummary =
                    services.Reports.PurchaseMonthlySummary(DateTime.Today);
                var purchaseMonthTotal =
                    purchaseMonthlySummary.Rows[purchaseMonthlySummary.Rows.Count - 1];
                if (purchaseMonthlySummary.Rows.Count != expectedOperatingRows ||
                    Convert.ToString(purchaseMonthTotal["日期"]) != "本月合计" ||
                    Convert.ToInt64(purchaseMonthTotal["采购单数"]) != 1 ||
                    Convert.ToInt64(purchaseMonthTotal["入库册数"]) != 4 ||
                    Convert.ToDecimal(purchaseMonthTotal["采购金额"]) != 40.00m ||
                    Convert.ToInt64(purchaseMonthTotal["采购退货单数"]) != 1 ||
                    Convert.ToInt64(purchaseMonthTotal["退货册数"]) != 1 ||
                    Convert.ToDecimal(purchaseMonthTotal["退货金额"]) != 10.00m ||
                    Convert.ToInt64(purchaseMonthTotal["净入库册数"]) != 3 ||
                    Convert.ToDecimal(purchaseMonthTotal["净采购金额"]) != 30.00m ||
                    Convert.ToInt64(purchaseMonthTotal["供应商数"]) != 1)
                    throw new InvalidOperationException("采购月度摘要口径自检失败。");

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

                using (var genericStream = File.OpenRead(excelPath))
                {
                    var genericWorkbook = new XSSFWorkbook(genericStream);
                    try
                    {
                        var genericSheet = genericWorkbook.GetSheet("销售退货明细");
                        if (genericSheet == null ||
                            genericSheet.GetRow(0).GetCell(0).StringCellValue != "销售退货明细" ||
                            genericSheet.GetRow(1).GetCell(0).StringCellValue != "日期" ||
                            genericSheet.GetColumnWidth(0) < 4000 ||
                            genericSheet.GetRow(1).HeightInPoints < 28F ||
                            genericSheet.PrintSetup.PaperSize != 9 ||
                            genericSheet.RepeatingRows == null ||
                            genericSheet.RepeatingRows.FormatAsString() != "2:2" ||
                            string.IsNullOrWhiteSpace(genericWorkbook.GetPrintArea(0)) ||
                            genericSheet.Footer.Right.IndexOf("&P", StringComparison.Ordinal) < 0)
                            throw new InvalidOperationException("通用 Excel 标题 / 表头 / 列宽 / 打印设置自检失败。");
                    }
                    finally
                    {
                        genericWorkbook.Close();
                    }
                }

                services.Settings.SetReportStoreName("目田书店");
                services.Settings.SetReportNightShiftStartHour(14);
                services.Settings.SetReportCategoryMapping("测试分类=独立出版书籍");

                var bookForSnapshotMutation = services.Books.GetById(bookId);
                bookForSnapshotMutation.Author = "后改作者";
                bookForSnapshotMutation.Publisher = "后改出版社";
                bookForSnapshotMutation.Category = "后改分类";
                services.Books.Update(bookForSnapshotMutation);

                var monthlySummary = services.Reports.SalesMonthlySummary(DateTime.Today);
                if (monthlySummary.Rows.Count != 1 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["gross_sales_cent"]) != 2880 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["return_amount_cent"]) != 1440 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["net_sales_cent"]) != 1440 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["sales_quantity"]) != 2 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["return_quantity"]) != 1 ||
                    Convert.ToInt64(monthlySummary.Rows[0]["order_count"]) != 1)
                    throw new InvalidOperationException("销售月报净销售口径自检失败。");

                var monthlyDetail = services.Reports.SalesMonthlyExportDetail(DateTime.Today);
                if (monthlyDetail.Rows.Count != 1 ||
                    Convert.ToString(monthlyDetail.Rows[0]["author"]) != "Test" ||
                    Convert.ToString(monthlyDetail.Rows[0]["publisher"]) != "测试出版社" ||
                    Convert.ToString(monthlyDetail.Rows[0]["category"]) != "测试分类" ||
                    Convert.ToString(monthlyDetail.Rows[0]["cost_ref_source"]) != "最近采购价" ||
                    Convert.ToInt32(monthlyDetail.Rows[0]["returned_quantity_in_month"]) != 1 ||
                    Convert.ToInt32(monthlyDetail.Rows[0]["returned_quantity_to_month_end"]) != 1)
                    throw new InvalidOperationException("销售历史快照 / 参考成本 / 月内退货口径自检失败。");

                var monthlyReturns = services.Reports.SalesMonthlyReturnExportDetail(DateTime.Today);
                if (monthlyReturns.Rows.Count != 1 ||
                    Convert.ToString(monthlyReturns.Rows[0]["refund_method"]) != "微信" ||
                    Convert.ToInt64(monthlyReturns.Rows[0]["line_total_cent"]) != 1440)
                    throw new InvalidOperationException("销售月报退款明细自检失败。");

                var monthlyExcelPath = Path.Combine(root, "sales-monthly.xlsx");
                services.SalesMonthlyExcel.Export(
                    monthlyDetail,
                    monthlyReturns,
                    monthlyExcelPath,
                    DateTime.Today,
                    services.Settings.GetReportStoreName(),
                    services.Settings.GetReportNightShiftStartHour(),
                    services.Settings.GetReportCategoryMapping());

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
                            summarySheet.GetColumnWidth(1) < 3000 ||
                            summarySheet.GetRow(26).GetCell(0).StringCellValue != "销售合计" ||
                            Math.Abs(summarySheet.GetRow(26).GetCell(3).NumericCellValue - 28.80) > 0.001 ||
                            summarySheet.GetRow(27).GetCell(0).StringCellValue != "销售退货" ||
                            Math.Abs(summarySheet.GetRow(27).GetCell(3).NumericCellValue - 14.40) > 0.001 ||
                            summarySheet.GetRow(28).GetCell(0).StringCellValue != "净销售" ||
                            Math.Abs(summarySheet.GetRow(28).GetCell(3).NumericCellValue - 14.40) > 0.001 ||
                            summarySheet.GetRow(6).GetCell(1).StringCellValue != "独立出版书籍" ||
                            summarySheet.GetRow(6).GetCell(2).NumericCellValue != 2 ||
                            summarySheet.PrintSetup.PaperSize != 9 ||
                            summarySheet.RepeatingRows == null ||
                            summarySheet.RepeatingRows.FormatAsString() != "1:4" ||
                            string.IsNullOrWhiteSpace(monthlyWorkbook.GetPrintArea(
                                monthlyWorkbook.GetSheetIndex(summarySheet))))
                            throw new InvalidOperationException("销售月报主表结构 / 净销售 / 分类映射 / 合并 / 列宽 / 打印设置自检失败。");

                        var daySheet = monthlyWorkbook.GetSheet(DateTime.Today.ToString("MMdd"));
                        if (daySheet == null ||
                            daySheet.GetRow(1).GetCell(0).StringCellValue != "序号" ||
                            daySheet.GetRow(1).GetCell(29).StringCellValue != "作者" ||
                            daySheet.GetRow(1).GetCell(30).StringCellValue != "收款方式" ||
                            daySheet.GetRow(2).GetCell(30).StringCellValue != "现金" ||
                            Math.Abs(daySheet.GetRow(2).GetCell(31).NumericCellValue - 28.80) > 0.001 ||
                            Math.Abs(daySheet.GetRow(2).GetCell(32).NumericCellValue - 30.00) > 0.001 ||
                            Math.Abs(daySheet.GetRow(2).GetCell(33).NumericCellValue - 1.20) > 0.001 ||
                            daySheet.GetRow(1).GetCell(36).StringCellValue != "当月退货数量" ||
                            daySheet.GetRow(2).GetCell(36).NumericCellValue != 1 ||
                            daySheet.GetRow(2).GetCell(37).NumericCellValue != 1 ||
                            daySheet.GetRow(2).GetCell(38).StringCellValue != "最近采购价")
                            throw new InvalidOperationException("销售月报每日明细 / 收款 / 退货 / 参考成本快照自检失败。");
                    }
                    finally
                    {
                        monthlyWorkbook.Close();
                    }
                }

                var purchaseMonthlyDetail =
                    services.Reports.PurchaseMonthlyExportDetail(DateTime.Today);
                var purchaseMonthlyReturns =
                    services.Reports.PurchaseMonthlyReturnExportDetail(DateTime.Today);
                if (purchaseMonthlyDetail.Rows.Count != 1 ||
                    purchaseMonthlyReturns.Rows.Count != 1 ||
                    Convert.ToInt32(
                        purchaseMonthlyDetail.Rows[0]["returned_quantity_in_month"]) != 1 ||
                    Convert.ToInt32(
                        purchaseMonthlyDetail.Rows[0]["returned_quantity_to_month_end"]) != 1 ||
                    Convert.ToInt64(
                        purchaseMonthlyReturns.Rows[0]["line_total_cent"]) != 1000)
                    throw new InvalidOperationException("采购月报明细查询自检失败。");

                var purchaseMonthlyExcelPath =
                    Path.Combine(root, "purchase-monthly.xlsx");
                services.PurchaseMonthlyExcel.Export(
                    purchaseMonthlyDetail,
                    purchaseMonthlyReturns,
                    purchaseMonthlyExcelPath,
                    DateTime.Today,
                    services.Settings.GetReportStoreName());

                if (!File.Exists(purchaseMonthlyExcelPath) ||
                    new FileInfo(purchaseMonthlyExcelPath).Length == 0)
                    throw new InvalidOperationException("采购月报 Excel 导出自检失败。");

                using (var purchaseMonthlyStream =
                    File.OpenRead(purchaseMonthlyExcelPath))
                {
                    var purchaseWorkbook =
                        new XSSFWorkbook(purchaseMonthlyStream);
                    try
                    {
                        var expectedSheets =
                            DateTime.DaysInMonth(
                                DateTime.Today.Year,
                                DateTime.Today.Month) + 2;
                        if (purchaseWorkbook.NumberOfSheets != expectedSheets)
                            throw new InvalidOperationException(
                                "采购月报 Sheet 数量自检失败。");

                        var purchaseSummary =
                            purchaseWorkbook.GetSheet("采购月报表");
                        if (purchaseSummary == null)
                            throw new InvalidOperationException(
                                "采购月报汇总 Sheet 缺失。");

                        var todaySummaryRow =
                            purchaseSummary.GetRow(DateTime.Today.Day + 1);
                        var totalRow =
                            purchaseSummary.GetRow(
                                DateTime.DaysInMonth(
                                    DateTime.Today.Year,
                                    DateTime.Today.Month) + 2);
                        if (todaySummaryRow == null ||
                            Math.Abs(todaySummaryRow.GetCell(3).NumericCellValue - 40.00) > 0.001 ||
                            Math.Abs(todaySummaryRow.GetCell(6).NumericCellValue - 10.00) > 0.001 ||
                            Math.Abs(todaySummaryRow.GetCell(8).NumericCellValue - 30.00) > 0.001 ||
                            totalRow == null ||
                            totalRow.GetCell(0).StringCellValue != "本月合计" ||
                            purchaseSummary.PrintSetup.PaperSize != 9 ||
                            purchaseSummary.RepeatingRows == null ||
                            purchaseSummary.RepeatingRows.FormatAsString() != "2:2")
                            throw new InvalidOperationException(
                                "采购月报汇总 / 打印设置自检失败。");

                        var supplierSheet =
                            purchaseWorkbook.GetSheet("供应商汇总");
                        if (supplierSheet == null ||
                            supplierSheet.GetRow(2).GetCell(0).StringCellValue != "测试供应商" ||
                            Math.Abs(supplierSheet.GetRow(2).GetCell(3).NumericCellValue - 40.00) > 0.001 ||
                            Math.Abs(supplierSheet.GetRow(2).GetCell(5).NumericCellValue - 10.00) > 0.001 ||
                            Math.Abs(supplierSheet.GetRow(2).GetCell(6).NumericCellValue - 30.00) > 0.001)
                            throw new InvalidOperationException(
                                "采购月报供应商汇总自检失败。");

                        var purchaseDay =
                            purchaseWorkbook.GetSheet(
                                DateTime.Today.ToString("MMdd"));
                        if (purchaseDay == null ||
                            purchaseDay.GetRow(1).GetCell(0).StringCellValue != "采购入库明细" ||
                            purchaseDay.GetRow(2).GetCell(2).StringCellValue != "采购单号" ||
                            purchaseDay.GetRow(3).GetCell(3).StringCellValue != "测试供应商" ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(8).NumericCellValue - 40.00) > 0.001 ||
                            purchaseDay.GetRow(2).GetCell(14).StringCellValue != "原进价" ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(14).NumericCellValue - 20.00) > 0.001 ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(15).NumericCellValue - 80.00) > 0.001 ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(16).NumericCellValue - 16.00) > 0.001 ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(17).NumericCellValue - 62.50) > 0.001 ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(18).NumericCellValue - 80.00) > 0.001 ||
                            Math.Abs(purchaseDay.GetRow(3).GetCell(20).NumericCellValue - 24.00) > 0.001 ||
                            purchaseDay.GetRow(5).GetCell(0).StringCellValue != "采购退货明细" ||
                            purchaseDay.GetRow(6).GetCell(2).StringCellValue != "退货单号" ||
                            Math.Abs(purchaseDay.GetRow(7).GetCell(9).NumericCellValue - 10.00) > 0.001)
                            throw new InvalidOperationException(
                                "采购月报逐日采购 / 退货明细自检失败。");
                    }
                    finally
                    {
                        purchaseWorkbook.Close();
                    }
                }

                // Verify real BIFF8 workbooks by reopening both legacy monthly
                // exports, including totals and print settings, not just file size.
                var salesXls = Path.Combine(root, "sales-monthly.xls");
                services.SalesMonthlyExcel.Export(monthlyDetail, monthlyReturns, salesXls, DateTime.Today,
                    services.Settings.GetReportStoreName(), services.Settings.GetReportNightShiftStartHour(),
                    services.Settings.GetReportCategoryMapping());
                using (var stream = File.OpenRead(salesXls))
                {
                    var workbook = WorkbookFactory.Create(stream);
                    try
                    {
                        var sheet = workbook.GetSheet("销售月报表");
                        if (!(workbook is NPOI.HSSF.UserModel.HSSFWorkbook) || sheet == null ||
                            workbook.NumberOfSheets != DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) + 1 ||
                            Math.Abs(sheet.GetRow(28).GetCell(3).NumericCellValue - 14.40) > 0.001 ||
                            sheet.PrintSetup.PaperSize != 9 || sheet.RepeatingRows == null ||
                            string.IsNullOrEmpty(workbook.GetPrintArea(workbook.GetSheetIndex(sheet))))
                            throw new InvalidOperationException("旧版 XLS 销售月报结构 / 净销售 / 打印设置自检失败。");
                    }
                    finally { workbook.Close(); }
                }
                var purchaseXls = Path.Combine(root, "purchase-monthly.xls");
                services.PurchaseMonthlyExcel.Export(purchaseMonthlyDetail, purchaseMonthlyReturns, purchaseXls,
                    DateTime.Today, services.Settings.GetReportStoreName());
                using (var stream = File.OpenRead(purchaseXls))
                {
                    var workbook = WorkbookFactory.Create(stream);
                    try
                    {
                        var sheet = workbook.GetSheet("采购月报表");
                        if (!(workbook is NPOI.HSSF.UserModel.HSSFWorkbook) || sheet == null ||
                            workbook.NumberOfSheets != DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month) + 2 ||
                            Math.Abs(sheet.GetRow(DateTime.Today.Day + 1).GetCell(8).NumericCellValue - 30.00) > 0.001 ||
                            sheet.PrintSetup.PaperSize != 9 || sheet.RepeatingRows == null ||
                            string.IsNullOrEmpty(workbook.GetPrintArea(workbook.GetSheetIndex(sheet))))
                            throw new InvalidOperationException("旧版 XLS 采购月报结构 / 净采购 / 打印设置自检失败。");
                    }
                    finally { workbook.Close(); }
                }

                var backupSnapshotStock = services.Books.GetById(bookId).StockQuantity;
                var backupPath = Path.Combine(root, "backup.db");
                services.Backup.CreateBackup(backupPath);
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
                    throw new InvalidOperationException("数据库备份自检失败。");

                var liveOverwriteRejected = false;
                try
                {
                    services.Backup.CreateBackup(dbPath);
                }
                catch (InvalidOperationException)
                {
                    liveOverwriteRejected = true;
                }
                if (!liveOverwriteRejected)
                    throw new InvalidOperationException("备份不应允许覆盖当前数据库文件。");

                services.Inventory.Adjust(bookId, 2, "backup restore mutation");
                if (services.Books.GetById(bookId).StockQuantity != backupSnapshotStock + 2)
                    throw new InvalidOperationException("恢复往返测试准备数据失败。");

                var safetyBackupPath = services.Backup.Restore(backupPath);
                var restoredServices = new ApplicationServices(dbPath);
                if (restoredServices.Books.GetById(bookId).StockQuantity != backupSnapshotStock)
                    throw new InvalidOperationException("数据库恢复没有还原到备份时的数据。");

                if (string.IsNullOrWhiteSpace(safetyBackupPath) || !File.Exists(safetyBackupPath))
                    throw new InvalidOperationException("恢复前安全备份没有生成。");

                using (var safetyConnection = new SQLiteConnection(
                    "Data Source=" + safetyBackupPath + ";Version=3;Read Only=True;Pooling=False;"))
                {
                    safetyConnection.Open();
                    using (var command = safetyConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT stock_quantity FROM books WHERE id=@id;";
                        command.Parameters.AddWithValue("@id", bookId);
                        if (Convert.ToInt32(command.ExecuteScalar()) != backupSnapshotStock + 2)
                            throw new InvalidOperationException("恢复前安全备份没有保存恢复操作前的最新数据。");
                    }
                }

                var corruptBackupPath = Path.Combine(root, "corrupt-backup.db");
                File.WriteAllText(corruptBackupPath, "not a sqlite database");
                var corruptBackupRejected = false;
                try
                {
                    restoredServices.Backup.Restore(corruptBackupPath);
                }
                catch (InvalidOperationException)
                {
                    corruptBackupRejected = true;
                }
                if (!corruptBackupRejected ||
                    new ApplicationServices(dbPath).Books.GetById(bookId).StockQuantity != backupSnapshotStock)
                    throw new InvalidOperationException("损坏备份拒绝或原数据库保护自检失败。");

                VerifyLegacyBookSchemaUpgrade(root);
                VerifyPersistentTableWidths(services);
                VerifyWindowPlacementContracts();
                VerifyHostedShellSmallScreen(services);
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

        // Separate process: unavailable SimSun must be reported as skipped (2),
        // never silently substituted by GDI and reported as a passed font test.
        public static int RunSimSunLayout()
        {
            var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simsun-test-result.txt");
            var root = Path.Combine(Path.GetTempPath(), "BookDesk-SimSun-" + Guid.NewGuid().ToString("N"));
            var field = typeof(UiTheme).GetField("_fontFamilyOverrideForTests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object previous = null;
            var originalAntdFont = AntdUI.Config.Font;
            try
            {
                if (!System.Drawing.FontFamily.Families.Any(f => string.Equals(f.Name, "SimSun", StringComparison.OrdinalIgnoreCase)))
                {
                    File.WriteAllText(output, "SKIPPED: SimSun is not installed. This is not a pass.");
                    return 2;
                }
                if (field == null) throw new InvalidOperationException("宋体自检无法设置字体。");
                previous = field.GetValue(null);
                field.SetValue(null, "SimSun");
                using (var actual = UiTheme.Font(9F))
                    if (!string.Equals(actual.FontFamily.Name, "SimSun", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("宋体被系统替换，自检不能算通过。");
                AntdUI.Config.Font = UiTheme.Font(9F);
                MainForm.SuppressStartupPrompts = true;
                var services = new ApplicationServices(Path.Combine(root, "test.db"));
                services.Settings.SetOnboardingCompleted(true);
                var id = services.Books.Insert(new Book { Title = "宋体自检图书" });
                var book = services.Books.GetById(id);
                VerifyHostedShellSmallScreen(services);
                var forms = new Form[] { new SalesForm(services), new PurchaseForm(services), new BookEditForm(services, book), new BookLabelForm(services, book) };
                try
                {
                    foreach (var form in forms)
                    {
                        form.ShowInTaskbar = false;
                        form.Show();
                        Application.DoEvents();
                        form.Size = new Size(1024, 680);
                        var profile = form as IUiSpecPage;
                        if (profile != null)
                        {
                            // MainForm applies the UI spec profile to hosted pages.
                            profile.ApplyUiSpecProfile(BookDeskUiSpec.Resolve(1024, 680));
                        }
                        form.PerformLayout();
                        Application.DoEvents();
                        if (VerifySimSunText(form, form.GetType().Name) == 0)
                            throw new InvalidOperationException(form.GetType().Name + " 没有可见的宋体文本控件。");
                    }
                }
                finally { foreach (var form in forms) form.Dispose(); }
                File.WriteAllText(output, "PASSED: SimSun was installed and used for visible layout checks at 1024x680 logical size.");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(output, "FAILED: " + ex);
                return 1;
            }
            finally
            {
                if (field != null) field.SetValue(null, previous);
                AntdUI.Config.Font = originalAntdFont;
                SQLiteConnection.ClearAllPools();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static int VerifySimSunText(Control control, string formName)
        {
            var count = 0;
            var label = control as Label;
            var button = control as AntdUI.Button;
            if (control.Visible && (label != null || button != null) &&
                !string.IsNullOrWhiteSpace(control.Text) &&
                string.Equals(control.Font.FontFamily.Name, "SimSun", StringComparison.OrdinalIgnoreCase))
            {
                count++;
                // Single-line controls must be tall enough for their actual font.
                // Tight-profile controls intentionally use 32px, not the generic 34px density.
                if (!control.Text.Contains("\n"))
                {
                    var size = TextRenderer.MeasureText(control.Text, control.Font, Size.Empty,
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    if (control.Height < size.Height + control.Padding.Vertical ||
                        (button != null && control.Width < size.Width + control.Padding.Horizontal))
                        throw new InvalidOperationException(formName + " 宋体文本空间不足：" + control.Text +
                            " 控件=" + control.Size + " 文字=" + size);
                }
            }
            foreach (Control child in control.Controls) count += VerifySimSunText(child, formName);
            return count;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDlgItem(IntPtr window, int id);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        // Runs in a separate STA process with a CI timeout: a broken modal close
        // must fail the test instead of hanging the entire build indefinitely.
        public static int RunUiTaskRegression()
        {
            var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-task-test-result.txt");
            var root = Path.Combine(Path.GetTempPath(), "BookDesk-UiTask-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(output, "START\r\n");
                var services = new ApplicationServices(Path.Combine(root, "test.db"));
                using (var owner = new Form { Text = "UiTask regression owner", ShowInTaskbar = false })
                {
                    owner.Show();
                    Application.DoEvents();
                    var backupPath = Path.Combine(root, "backup.db");
                    MarkUiTask("backup starting");
                    var backup = ExerciseUiTask(owner, "测试备份", (progress, token) =>
                    {
                        System.Threading.Thread.Sleep(350);
                        return services.Backup.CreateBackup(backupPath, progress, token);
                    }, true, false, null);
                    if (!backup.Completed || backup.Value != backupPath || !File.Exists(backupPath) || backup.Ticks == 0)
                        throw new InvalidOperationException("后台备份未正确结束或 UI 未响应。");
                    BackupService.ValidateBackup(backupPath);

                    var exportPath = Path.Combine(root, "report.xlsx");
                    var table = new System.Data.DataTable();
                    table.Columns.Add("金额", typeof(int));
                    table.Rows.Add(123);
                    MarkUiTask("export starting");
                    var export = ExerciseUiTask(owner, "测试不可取消导出", (progress, token) =>
                    {
                        System.Threading.Thread.Sleep(350);
                        services.Excel.Export(table, exportPath, "测试", progress, token);
                        return exportPath;
                    }, false, true, null);
                    if (!export.AttemptedClose || !export.Completed || !File.Exists(exportPath))
                        throw new InvalidOperationException("不可取消任务被提前关闭或未完成。");
                    using (var stream = File.OpenRead(exportPath))
                    {
                        var workbook = WorkbookFactory.Create(stream);
                        try { if (workbook.GetSheetAt(0).GetRow(2).GetCell(0).NumericCellValue != 123) throw new InvalidOperationException("导出内容不正确。"); }
                        finally { workbook.Close(); }
                    }

                    MarkUiTask("cancellation starting");
                    var cancelled = ExerciseUiTask(owner, "测试取消", (progress, token) =>
                    {
                        if (!token.WaitHandle.WaitOne(4000)) throw new InvalidOperationException("没有收到取消请求。");
                        progress.Report(50);
                        token.ThrowIfCancellationRequested();
                        return "unexpected";
                    }, true, true, "已取消");
                    if (!cancelled.AttemptedClose || cancelled.Completed || !cancelled.MessageClosed)
                        throw new InvalidOperationException("取消任务没有正确结束或提示。");

                    MarkUiTask("failure starting");
                    var failure = ExerciseUiTask(owner, "测试失败", (progress, token) =>
                    {
                        System.Threading.Thread.Sleep(250);
                        return FailUiTaskForTest();
                    }, true, false, "BOOK DESK 提示");
                    if (failure.Completed || !failure.MessageClosed)
                        throw new InvalidOperationException("任务失败没有显示错误提示。");

                    MarkUiTask("late result starting");
                    var late = ExerciseUiTask(owner, "测试取消后已完成", (progress, token) =>
                    {
                        if (!token.WaitHandle.WaitOne(4000)) throw new InvalidOperationException("没有收到取消请求。");
                        // Once a result is published, success must not be reported as cancellation.
                        return "published";
                    }, true, true, null);
                    if (!late.AttemptedClose || !late.Completed || late.Value != "published")
                        throw new InvalidOperationException("已发布的结果没有报告成功。");
                    owner.Close();
                }
                File.WriteAllText(output, "PASSED: responsive modal backup/export, close prevention, cancellation, failure and completed result.");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(output, "FAILED: " + ex);
                return 1;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static readonly object UiTaskTraceLock = new object();
        private static void MarkUiTask(string message)
        {
            lock (UiTaskTraceLock) File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-task-test-result.txt"), message + Environment.NewLine);
        }

        private static string FailUiTaskForTest()
        {
            throw new InvalidOperationException("测试失败提示");
        }

        private sealed class UiTaskObservation
        {
            public bool Completed, AttemptedClose, MessageClosed;
            public string Value;
            public int Ticks;
        }

        private static UiTaskObservation ExerciseUiTask(Form owner, string title,
            Func<IProgress<int>, System.Threading.CancellationToken, string> work,
            bool canCancel, bool attemptClose, string messageTitle)
        {
            var observation = new UiTaskObservation();
            using (var timer = new System.Windows.Forms.Timer { Interval = 30 })
            {
                var attached = false;
                timer.Tick += delegate
                {
                    observation.Ticks++;
                    if (!attached)
                    {
                        var observedDialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == title && f.Visible);
                        if (observedDialog != null)
                        {
                            attached = true;
                            observedDialog.FormClosing += (sender, e) => MarkUiTask(title + ": FormClosing cancel=" + e.Cancel + " reason=" + e.CloseReason);
                            observedDialog.FormClosed += (sender, e) => MarkUiTask(title + ": FormClosed");
                        }
                    }
                    if (attemptClose && !observation.AttemptedClose)
                    {
                        var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Text == title && f.Visible);
                        if (dialog != null)
                        {
                            observation.AttemptedClose = true;
                            MarkUiTask(title + ": attempting close");
                            // Simulate an actual window-manager close request;
                            // do not re-enter Form.Close synchronously inside a Timer tick.
                            try
                            {
                                var posted = PostMessage(dialog.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                                MarkUiTask(title + ": close posted=" + posted);
                            }
                            catch (Exception ex) { MarkUiTask(title + ": close error " + ex); throw; }
                        }
                    }
                    if (messageTitle != null && !observation.MessageClosed)
                    {
                        var window = FindWindow("#32770", messageTitle);
                        if (window != IntPtr.Zero)
                        {
                            // Click the actual standard message-box button: OK for
                            // cancellation, No for errors (never open Explorer).
                            var button = GetDlgItem(window, messageTitle == "已取消" ? 1 : 7);
                            if (button != IntPtr.Zero)
                            {
                                observation.MessageClosed = true;
                                MarkUiTask(title + ": dismissing " + messageTitle);
                                PostMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK
                            }
                        }
                    }
                };
                timer.Start();
                UiTask.Run(owner, title, (progress, token) =>
                {
                    MarkUiTask(title + ": worker started");
                    try
                    {
                        var value = work(progress, token);
                        MarkUiTask(title + ": worker returned");
                        return value;
                    }
                    catch (Exception ex)
                    {
                        MarkUiTask(title + ": worker exception " + ex.GetType().Name);
                        throw;
                    }
                }, value => { observation.Completed = true; observation.Value = value; }, canCancel);
                MarkUiTask(title + ": UiTask returned");
                timer.Stop();
                Application.DoEvents();
            }
            return observation;
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

        // Exercise the real shell and hosted pages, not standalone child forms.
        // These are logical-size checks on the CI monitor, not a DPI simulation.
        private static void VerifyHostedShellSmallScreen(ApplicationServices services)
        {
            using (var shell = new MainForm(services))
            {
                shell.ShowInTaskbar = false;
                shell.Show();
                Application.DoEvents();
                var screen = Screen.FromControl(shell).WorkingArea;
                if (screen.Width < 1024 || screen.Height < 680)
                    throw new InvalidOperationException("CI 工作区过小，无法执行 1024×680 主窗口测试：" + screen);

                foreach (var windowSize in new[] { new Size(1024, 728), new Size(1024, 680) })
                {
                    if (windowSize.Height > screen.Height) continue;
                    shell.MinimumSize = new Size(1, 1);
                    shell.Bounds = WindowPlacement.FitBounds(screen, windowSize, windowSize);
                    WindowPlacement.ClampToWorkingArea(shell);
                    Application.DoEvents();
                    if (!screen.Contains(shell.Bounds) || shell.Size != windowSize)
                        throw new InvalidOperationException("主窗口没有完整位于工作区：" + shell.Bounds + " / " + screen);

                    foreach (var key in new[] { "dashboard", "sales", "books", "purchase", "inventory", "documents", "suppliers", "reports", "backup", "help", "settings" })
                    {
                        var nav = shell.GetNavigationScreenBounds(key);
                        if (nav.IsEmpty || !screen.Contains(nav))
                            throw new InvalidOperationException("小屏侧边导航不可达：" + key + " " + windowSize + " / " + nav);
                    }
                    foreach (var key in new[] { "sales", "purchase" })
                    {
                        shell.Navigate(key);
                        Application.DoEvents();
                        shell.PerformLayout();
                        Application.DoEvents();
                        var target = FindButton(shell, key == "sales" ? "确认结账" : "复核入库");
                        if (target == null || !target.Visible || !target.Enabled)
                            throw new InvalidOperationException("主窗口 " + windowSize + " 中找不到可操作的 " + key + " 主按钮。");
                        var buttonBounds = target.RectangleToScreen(target.ClientRectangle);
                        if (buttonBounds.Width <= 0 || buttonBounds.Height <= 0 || !screen.Contains(buttonBounds))
                            throw new InvalidOperationException("主按钮在工作区外：" + key + " " + buttonBounds);
                        for (Control parent = target.Parent; parent != null; parent = parent.Parent)
                        {
                            if (!parent.Visible || !parent.RectangleToScreen(parent.ClientRectangle).Contains(buttonBounds))
                                throw new InvalidOperationException("主按钮被容器裁切：" + key + " " + windowSize + " / " + parent.GetType().Name + " " + buttonBounds + " / " + parent.RectangleToScreen(parent.ClientRectangle));
                        }
                    }
                }

                // Exercise the compact-to-grouped transition and back without
                // losing the page. This part does not assert screen bounds.
                shell.Navigate("sales");
                Application.DoEvents();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var pageField = typeof(MainForm).GetField("_currentPage", flags);
                var menu = (AntdUI.Menu)typeof(MainForm).GetField("_navigation", flags).GetValue(shell);
                var page = pageField.GetValue(shell);
                var applyNavigation = typeof(MainForm).GetMethod("ApplyNavigationProfile", flags);
                // The CI desktop may be too small to resize a top-level window
                // to 1300px. Exercise the same production profile transition.
                applyNavigation.Invoke(shell, new object[] { false });
                Application.DoEvents();
                if (menu.Collapsed || menu.Items.Count != 4 || !ReferenceEquals(page, pageField.GetValue(shell)))
                    throw new InvalidOperationException("放大后未恢复分组导航或当前页面丢失。shell=" + shell.Size + " client=" + shell.ClientSize + " collapsed=" + menu.Collapsed + " count=" + menu.Items.Count + " same=" + ReferenceEquals(page, pageField.GetValue(shell)));
                applyNavigation.Invoke(shell, new object[] { true });
                Application.DoEvents();
                if (!menu.Collapsed || menu.Items.Count != 11 || !ReferenceEquals(page, pageField.GetValue(shell)))
                    throw new InvalidOperationException("缩小后未恢复小屏导航或当前页面丢失。");
            }
        }

        private static Control FindButton(Control root, string text)
        {
            var button = root as AntdUI.Button;
            if (button != null && button.Text == text) return button;
            foreach (Control child in root.Controls)
            {
                var found = FindButton(child, text);
                if (found != null) return found;
            }
            return null;
        }

        private static void VerifyUiLayoutContracts(ApplicationServices services, Book storedBook)
        {
            var forms = new Form[]
            {
                new MainForm(services),
                new DashboardForm(services, delegate(string key) { }, delegate { }),
                new BookListForm(services),
                new BookEditForm(services, storedBook),
                new BookLabelForm(services, storedBook),
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
                    new Size(1024, 680),
                    new Size(1024, 768),
                    new Size(1280, 1024),
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

        /// <summary>
        /// Pure window-placement checks. These run on any monitor (including
        /// the 1920x1080 GitHub Actions runner) and lock the Issue #54
        /// behaviour: a 1024x768 Win7 working area must contain the whole
        /// shell, and the minimum size must never exceed the working area.
        /// </summary>
        private static void VerifyWindowPlacementContracts()
        {
            var smallWorkingArea = new Rectangle(0, 0, 1024, 728);
            var preferred = new Size(1600, 900);
            var minimum = new Size(
                BookDeskUiSpec.MinimumClientWidth,
                BookDeskUiSpec.MinimumClientHeight);

            var small = WindowPlacement.FitBounds(smallWorkingArea, preferred, minimum);
            if (small != new Rectangle(0, 0, 1024, 728))
                throw new InvalidOperationException(
                    "1024x768 屏幕适配自检失败：期望窗口完全位于 1024x728 工作区，实际为 " + small + "。");

            var smallMinimum = WindowPlacement.ClampMinimum(smallWorkingArea.Size, minimum);
            if (smallMinimum.Width > smallWorkingArea.Width || smallMinimum.Height > smallWorkingArea.Height)
                throw new InvalidOperationException(
                    "最小窗口尺寸超过小屏工作区：" + smallMinimum + "。");

            var hd = WindowPlacement.FitBounds(new Rectangle(0, 0, 1920, 1040), preferred, minimum);
            if (hd != new Rectangle(160, 70, 1600, 900))
                throw new InvalidOperationException(
                    "大屏居中自检失败：期望 (160,70,1600,900)，实际为 " + hd + "。");

            var secondary = WindowPlacement.FitBounds(
                new Rectangle(-1920, 0, 1920, 1040),
                preferred,
                minimum);
            if (secondary.X != -1760 || secondary.Y != 70)
                throw new InvalidOperationException(
                    "负坐标副屏居中自检失败：实际为 " + secondary + "。");

            var tiny = WindowPlacement.FitBounds(new Rectangle(0, 0, 800, 560), preferred, minimum);
            if (tiny.Width != 800 || tiny.Height != 560)
                throw new InvalidOperationException(
                    "极小工作区应收缩到屏幕尺寸，实际为 " + tiny + "。");

            var clampedMinimum = WindowPlacement.ClampMinimum(new Size(1024, 728), new Size(1280, 900));
            if (clampedMinimum != new Size(1024, 728))
                throw new InvalidOperationException(
                    "最小窗口未夹取到工作区：" + clampedMinimum + "。");

            if (BookDeskUiSpec.MinimumClientWidth > 1024 ||
                BookDeskUiSpec.MinimumClientHeight > 680)
                throw new InvalidOperationException(
                    "UI 规格声明的最小客户区大于 1024x768 兼容下限。");

            var tight = BookDeskUiSpec.Resolve(1024, 728);
            if (!tight.IsTight || !tight.IsCompact)
                throw new InvalidOperationException("1024x728 应解析为 tight（且按 compact 处理）。");
            if (tight.SidebarWidth >= BookDeskUiSpec.Compact.SidebarWidth)
                throw new InvalidOperationException("tight 档位应比 compact 更窄。");

            var compact = BookDeskUiSpec.Resolve(1366, 728);
            if (compact.IsTight || !compact.IsCompact)
                throw new InvalidOperationException("1366x768 应解析为 compact 而不是 tight。");

            var standard = BookDeskUiSpec.Resolve(1600, 900);
            if (!string.Equals(standard.Name, "standard", StringComparison.Ordinal))
                throw new InvalidOperationException("1600x900 应解析为 standard。");
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
