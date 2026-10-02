using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Win7BookManagement.Reporting
{
    public sealed class PurchaseMonthlyExcelExporter
    {
        private static readonly string[] PurchaseHeaders =
        {
            "序号", "采购日期", "采购单号", "供应商", "ISBN", "书名",
            "数量", "进价", "金额", "当月退货数量", "月末累计已退",
            "净入库数量", "复核时间", "单据备注"
        };

        private static readonly int[] PurchaseWidths =
        {
            7, 13, 22, 18, 18, 30, 9, 11, 12, 13, 13, 12, 20, 28
        };

        private static readonly string[] ReturnHeaders =
        {
            "序号", "退货时间", "退货单号", "原采购单号", "供应商",
            "ISBN", "书名", "退货数量", "原进价", "退货金额", "备注"
        };

        private static readonly int[] ReturnWidths =
        {
            7, 20, 22, 22, 18, 18, 30, 10, 11, 12, 28
        };

        public void Export(
            DataTable purchaseDetail,
            DataTable returnDetail,
            string path,
            DateTime month,
            string storeName)
        {
            if (purchaseDetail == null) throw new ArgumentNullException("purchaseDetail");
            if (returnDetail == null) throw new ArgumentNullException("returnDetail");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("导出路径不能为空。", "path");

            var normalizedMonth = new DateTime(month.Year, month.Month, 1);
            var normalizedStoreName = string.IsNullOrWhiteSpace(storeName)
                ? "BOOK DESK"
                : storeName.Trim();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var purchases = ReadPurchases(purchaseDetail);
            var returns = ReadReturns(returnDetail);

            IWorkbook workbook = new XSSFWorkbook();
            try
            {
                var styles = new StyleFactory(workbook);
                CreateMonthlySummary(
                    workbook,
                    styles,
                    purchases,
                    returns,
                    normalizedMonth,
                    normalizedStoreName);
                CreateSupplierSummary(
                    workbook,
                    styles,
                    purchases,
                    returns,
                    normalizedMonth,
                    normalizedStoreName);

                var days = DateTime.DaysInMonth(
                    normalizedMonth.Year,
                    normalizedMonth.Month);
                for (var day = 1; day <= days; day++)
                {
                    CreateDailyDetail(
                        workbook,
                        styles,
                        purchases,
                        returns,
                        new DateTime(
                            normalizedMonth.Year,
                            normalizedMonth.Month,
                            day),
                        normalizedStoreName);
                }

                using (var stream = File.Create(path))
                    workbook.Write(stream);
            }
            finally
            {
                workbook.Close();
            }
        }

        private static void CreateMonthlySummary(
            IWorkbook workbook,
            StyleFactory styles,
            IList<PurchaseRow> purchases,
            IList<PurchaseReturnRow> returns,
            DateTime month,
            string storeName)
        {
            var sheet = workbook.CreateSheet("采购月报表");
            sheet.CreateFreezePane(0, 2);

            var headers = new[]
            {
                "日期", "采购单数", "入库册数", "采购金额",
                "采购退货单数", "退货册数", "退货金额",
                "净入库册数", "净采购金额", "供应商数"
            };
            var widths = new[] { 13, 11, 11, 13, 13, 11, 13, 12, 13, 11 };

            var title = GetRow(sheet, 0);
            title.HeightInPoints = 30F;
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = GetCell(sheet, 0, i);
                if (i == 0)
                {
                    cell.SetCellValue(
                        storeName + " " +
                        month.ToString("yyyy年MM月", CultureInfo.InvariantCulture) +
                        "采购月报");
                }
                cell.CellStyle = styles.Title;
            }
            sheet.AddMergedRegion(
                new CellRangeAddress(0, 0, 0, headers.Length - 1));

            var header = GetRow(sheet, 1);
            header.HeightInPoints = 30F;
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = GetCell(sheet, 1, i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = styles.Header;
                sheet.SetColumnWidth(i, Width(widths[i]));
            }

            var days = DateTime.DaysInMonth(month.Year, month.Month);
            var rowIndex = 2;
            for (var day = 1; day <= days; day++)
            {
                var date = new DateTime(month.Year, month.Month, day);
                var metrics = CalculateDayMetrics(purchases, returns, date);
                WriteSummaryMetricsRow(
                    sheet,
                    styles,
                    rowIndex++,
                    date.ToString("yyyy-MM-dd"),
                    metrics,
                    false);
            }

            var total = CalculatePeriodMetrics(purchases, returns);
            WriteSummaryMetricsRow(
                sheet,
                styles,
                rowIndex,
                "本月合计",
                total,
                true);

            sheet.SetAutoFilter(
                new CellRangeAddress(1, rowIndex, 0, headers.Length - 1));

            ExcelPrintLayout.Apply(
                workbook,
                sheet,
                storeName + " " + month.ToString("yyyy-MM") + " 采购月报",
                true,
                1,
                1,
                headers.Length - 1,
                rowIndex);
        }

        private static void WriteSummaryMetricsRow(
            ISheet sheet,
            StyleFactory styles,
            int rowIndex,
            string label,
            PurchaseMetrics metrics,
            bool total)
        {
            var row = GetRow(sheet, rowIndex);
            row.HeightInPoints = total ? 24F : 21F;

            WriteCell(sheet, rowIndex, 0, label, total ? styles.TotalText : styles.Text);
            WriteCell(sheet, rowIndex, 1, metrics.PurchaseOrderCount, total ? styles.TotalInteger : styles.Integer);
            WriteCell(sheet, rowIndex, 2, metrics.PurchaseQuantity, total ? styles.TotalInteger : styles.Integer);
            WriteCell(sheet, rowIndex, 3, metrics.PurchaseAmountCent / 100.0, total ? styles.TotalMoney : styles.Money);
            WriteCell(sheet, rowIndex, 4, metrics.ReturnOrderCount, total ? styles.TotalInteger : styles.Integer);
            WriteCell(sheet, rowIndex, 5, metrics.ReturnQuantity, total ? styles.TotalInteger : styles.Integer);
            WriteCell(sheet, rowIndex, 6, metrics.ReturnAmountCent / 100.0, total ? styles.TotalMoney : styles.Money);
            WriteCell(sheet, rowIndex, 7, metrics.PurchaseQuantity - metrics.ReturnQuantity, total ? styles.TotalInteger : styles.Integer);
            WriteCell(sheet, rowIndex, 8, (metrics.PurchaseAmountCent - metrics.ReturnAmountCent) / 100.0, total ? styles.TotalMoney : styles.Money);
            WriteCell(sheet, rowIndex, 9, metrics.SupplierCount, total ? styles.TotalInteger : styles.Integer);
        }

        private static void CreateSupplierSummary(
            IWorkbook workbook,
            StyleFactory styles,
            IList<PurchaseRow> purchases,
            IList<PurchaseReturnRow> returns,
            DateTime month,
            string storeName)
        {
            var sheet = workbook.CreateSheet("供应商汇总");
            sheet.CreateFreezePane(0, 2);

            var headers = new[]
            {
                "供应商", "采购单数", "入库册数", "采购金额",
                "退货册数", "退货金额", "净采购金额"
            };
            var widths = new[] { 24, 11, 11, 13, 11, 13, 13 };

            for (var i = 0; i < headers.Length; i++)
            {
                var cell = GetCell(sheet, 0, i);
                if (i == 0)
                {
                    cell.SetCellValue(
                        storeName + " " +
                        month.ToString("yyyy年MM月", CultureInfo.InvariantCulture) +
                        "供应商采购汇总");
                }
                cell.CellStyle = styles.Title;
            }
            sheet.AddMergedRegion(
                new CellRangeAddress(0, 0, 0, headers.Length - 1));

            for (var i = 0; i < headers.Length; i++)
            {
                var cell = GetCell(sheet, 1, i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = styles.Header;
                sheet.SetColumnWidth(i, Width(widths[i]));
            }

            var supplierNames = new SortedSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            foreach (var purchase in purchases)
                supplierNames.Add(NormalizeSupplier(purchase.SupplierName));
            foreach (var returned in returns)
                supplierNames.Add(NormalizeSupplier(returned.SupplierName));

            var rowIndex = 2;
            foreach (var supplier in supplierNames)
            {
                var purchaseOrders = new HashSet<long>();
                long purchaseQty = 0;
                long purchaseCent = 0;
                long returnQty = 0;
                long returnCent = 0;

                foreach (var purchase in purchases)
                {
                    if (!string.Equals(
                        NormalizeSupplier(purchase.SupplierName),
                        supplier,
                        StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    purchaseOrders.Add(purchase.PurchaseId);
                    purchaseQty += purchase.Quantity;
                    purchaseCent += purchase.LineTotalCent;
                }

                foreach (var returned in returns)
                {
                    if (!string.Equals(
                        NormalizeSupplier(returned.SupplierName),
                        supplier,
                        StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    returnQty += returned.Quantity;
                    returnCent += returned.LineTotalCent;
                }

                WriteCell(sheet, rowIndex, 0, supplier, styles.LeftText);
                WriteCell(sheet, rowIndex, 1, purchaseOrders.Count, styles.Integer);
                WriteCell(sheet, rowIndex, 2, purchaseQty, styles.Integer);
                WriteCell(sheet, rowIndex, 3, purchaseCent / 100.0, styles.Money);
                WriteCell(sheet, rowIndex, 4, returnQty, styles.Integer);
                WriteCell(sheet, rowIndex, 5, returnCent / 100.0, styles.Money);
                WriteCell(sheet, rowIndex, 6, (purchaseCent - returnCent) / 100.0, styles.Money);
                rowIndex++;
            }

            if (supplierNames.Count == 0)
            {
                WriteCell(sheet, 2, 0, "本月无采购 / 采购退货记录", styles.LeftText);
                sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, headers.Length - 1));
                rowIndex = 3;
            }

            ExcelPrintLayout.Apply(
                workbook,
                sheet,
                storeName + " " + month.ToString("yyyy-MM") + " 供应商采购汇总",
                false,
                1,
                1,
                headers.Length - 1,
                Math.Max(2, rowIndex - 1));
        }

        private static void CreateDailyDetail(
            IWorkbook workbook,
            StyleFactory styles,
            IList<PurchaseRow> purchases,
            IList<PurchaseReturnRow> returns,
            DateTime date,
            string storeName)
        {
            var sheet = workbook.CreateSheet(
                date.ToString("MMdd", CultureInfo.InvariantCulture));
            sheet.CreateFreezePane(0, 3);

            var maxColumns = Math.Max(
                PurchaseHeaders.Length,
                ReturnHeaders.Length);
            for (var i = 0; i < maxColumns; i++)
            {
                var purchaseWidth = i < PurchaseWidths.Length
                    ? PurchaseWidths[i]
                    : 0;
                var returnWidth = i < ReturnWidths.Length
                    ? ReturnWidths[i]
                    : 0;
                sheet.SetColumnWidth(
                    i,
                    Width(Math.Max(purchaseWidth, returnWidth)));
            }

            var title = GetRow(sheet, 0);
            title.HeightInPoints = 28F;
            for (var i = 0; i < maxColumns; i++)
            {
                var cell = GetCell(sheet, 0, i);
                if (i == 0)
                {
                    cell.SetCellValue(
                        storeName + " " +
                        date.ToString("yyyy-MM-dd") +
                        " 采购明细");
                }
                cell.CellStyle = styles.Title;
            }
            sheet.AddMergedRegion(
                new CellRangeAddress(0, 0, 0, maxColumns - 1));

            var currentRow = 1;
            currentRow = WritePurchaseSection(
                sheet,
                styles,
                purchases,
                date,
                currentRow);

            currentRow += 1;
            currentRow = WriteReturnSection(
                sheet,
                styles,
                returns,
                date,
                currentRow);

            ExcelPrintLayout.Apply(
                workbook,
                sheet,
                storeName + " " + date.ToString("yyyy-MM-dd") + " 采购明细",
                true,
                1,
                2,
                maxColumns - 1,
                Math.Max(2, currentRow - 1));
        }

        private static int WritePurchaseSection(
            ISheet sheet,
            StyleFactory styles,
            IList<PurchaseRow> purchases,
            DateTime date,
            int startRow)
        {
            var sectionTitleRow = startRow;
            var title = GetRow(sheet, sectionTitleRow);
            title.HeightInPoints = 23F;
            for (var i = 0; i < PurchaseHeaders.Length; i++)
            {
                var cell = GetCell(sheet, sectionTitleRow, i);
                if (i == 0) cell.SetCellValue("采购入库明细");
                cell.CellStyle = styles.Section;
            }
            sheet.AddMergedRegion(
                new CellRangeAddress(
                    sectionTitleRow,
                    sectionTitleRow,
                    0,
                    PurchaseHeaders.Length - 1));

            var headerRow = sectionTitleRow + 1;
            for (var i = 0; i < PurchaseHeaders.Length; i++)
            {
                var cell = GetCell(sheet, headerRow, i);
                cell.SetCellValue(PurchaseHeaders[i]);
                cell.CellStyle = styles.Header;
            }

            var dayRows = new List<PurchaseRow>();
            foreach (var purchase in purchases)
            {
                if (purchase.PurchasedAt.Date == date.Date)
                    dayRows.Add(purchase);
            }
            dayRows.Sort(ComparePurchases);

            var excelRow = headerRow + 1;
            var sequence = 0;
            var index = 0;
            while (index < dayRows.Count)
            {
                var purchaseId = dayRows[index].PurchaseId;
                var start = index;
                var end = index;
                while (end + 1 < dayRows.Count &&
                       dayRows[end + 1].PurchaseId == purchaseId)
                    end++;

                sequence++;
                var firstExcelRow = excelRow;
                var lastExcelRow = excelRow + end - start;

                for (var i = start; i <= end; i++)
                {
                    var source = dayRows[i];
                    var first = i == start;
                    var last = i == end;
                    var top = first ? BorderStyle.Medium : BorderStyle.Thin;
                    var bottom = last ? BorderStyle.Medium : BorderStyle.Thin;

                    WriteDetail(
                        sheet, styles, excelRow, 0,
                        first ? (object)sequence : null,
                        "0", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 1,
                        first ? (object)source.PurchasedAt : null,
                        "yyyy-mm-dd", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 2,
                        first ? source.OrderNo : null,
                        "@", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 3,
                        first ? NormalizeSupplier(source.SupplierName) : null,
                        "@", top, bottom, HorizontalAlignment.Left);
                    WriteDetail(
                        sheet, styles, excelRow, 4,
                        source.Isbn, "@", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 5,
                        source.Title, "@", top, bottom, HorizontalAlignment.Left);
                    WriteDetail(
                        sheet, styles, excelRow, 6,
                        source.Quantity, "0", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 7,
                        source.UnitCostCent / 100.0, "0.00", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 8,
                        source.LineTotalCent / 100.0, "0.00", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 9,
                        source.ReturnedQuantityInMonth, "0", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 10,
                        source.ReturnedQuantityToMonthEnd, "0", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 11,
                        source.Quantity - source.ReturnedQuantityToMonthEnd,
                        "0", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 12,
                        first && source.ReviewedAt.HasValue
                            ? (object)source.ReviewedAt.Value
                            : null,
                        "yyyy-mm-dd hh:mm:ss", top, bottom);
                    WriteDetail(
                        sheet, styles, excelRow, 13,
                        first ? source.OrderNote : null,
                        "@", top, bottom, HorizontalAlignment.Left, true);
                    excelRow++;
                }

                if (lastExcelRow > firstExcelRow)
                {
                    foreach (var column in new[] { 0, 1, 2, 3, 12, 13 })
                    {
                        sheet.AddMergedRegion(
                            new CellRangeAddress(
                                firstExcelRow,
                                lastExcelRow,
                                column,
                                column));
                    }
                }

                index = end + 1;
            }

            if (dayRows.Count == 0)
            {
                WriteCell(
                    sheet,
                    excelRow,
                    0,
                    "当日无已复核采购入库记录",
                    styles.LeftText);
                sheet.AddMergedRegion(
                    new CellRangeAddress(
                        excelRow,
                        excelRow,
                        0,
                        PurchaseHeaders.Length - 1));
                excelRow++;
            }

            return excelRow;
        }

        private static int WriteReturnSection(
            ISheet sheet,
            StyleFactory styles,
            IList<PurchaseReturnRow> returns,
            DateTime date,
            int startRow)
        {
            var sectionTitleRow = startRow;
            for (var i = 0; i < ReturnHeaders.Length; i++)
            {
                var cell = GetCell(sheet, sectionTitleRow, i);
                if (i == 0) cell.SetCellValue("采购退货明细");
                cell.CellStyle = styles.Section;
            }
            sheet.AddMergedRegion(
                new CellRangeAddress(
                    sectionTitleRow,
                    sectionTitleRow,
                    0,
                    ReturnHeaders.Length - 1));

            var headerRow = sectionTitleRow + 1;
            for (var i = 0; i < ReturnHeaders.Length; i++)
            {
                var cell = GetCell(sheet, headerRow, i);
                cell.SetCellValue(ReturnHeaders[i]);
                cell.CellStyle = styles.Header;
            }

            var dayRows = new List<PurchaseReturnRow>();
            foreach (var returned in returns)
            {
                if (returned.ReturnedAt.Date == date.Date)
                    dayRows.Add(returned);
            }
            dayRows.Sort(CompareReturns);

            var excelRow = headerRow + 1;
            var sequence = 0;
            var index = 0;
            while (index < dayRows.Count)
            {
                var returnId = dayRows[index].ReturnId;
                var start = index;
                var end = index;
                while (end + 1 < dayRows.Count &&
                       dayRows[end + 1].ReturnId == returnId)
                    end++;

                sequence++;
                var firstExcelRow = excelRow;
                var lastExcelRow = excelRow + end - start;

                for (var i = start; i <= end; i++)
                {
                    var source = dayRows[i];
                    var first = i == start;
                    var last = i == end;
                    var top = first ? BorderStyle.Medium : BorderStyle.Thin;
                    var bottom = last ? BorderStyle.Medium : BorderStyle.Thin;

                    WriteDetail(sheet, styles, excelRow, 0, first ? (object)sequence : null, "0", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 1, first ? (object)source.ReturnedAt : null, "yyyy-mm-dd hh:mm:ss", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 2, first ? source.ReturnNo : null, "@", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 3, first ? source.SourceOrderNo : null, "@", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 4, first ? NormalizeSupplier(source.SupplierName) : null, "@", top, bottom, HorizontalAlignment.Left);
                    WriteDetail(sheet, styles, excelRow, 5, source.Isbn, "@", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 6, source.Title, "@", top, bottom, HorizontalAlignment.Left);
                    WriteDetail(sheet, styles, excelRow, 7, source.Quantity, "0", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 8, source.UnitCostCent / 100.0, "0.00", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 9, source.LineTotalCent / 100.0, "0.00", top, bottom);
                    WriteDetail(sheet, styles, excelRow, 10, first ? source.ReturnNote : null, "@", top, bottom, HorizontalAlignment.Left, true);
                    excelRow++;
                }

                if (lastExcelRow > firstExcelRow)
                {
                    foreach (var column in new[] { 0, 1, 2, 3, 4, 10 })
                    {
                        sheet.AddMergedRegion(
                            new CellRangeAddress(
                                firstExcelRow,
                                lastExcelRow,
                                column,
                                column));
                    }
                }

                index = end + 1;
            }

            if (dayRows.Count == 0)
            {
                WriteCell(
                    sheet,
                    excelRow,
                    0,
                    "当日无采购退货记录",
                    styles.LeftText);
                sheet.AddMergedRegion(
                    new CellRangeAddress(
                        excelRow,
                        excelRow,
                        0,
                        ReturnHeaders.Length - 1));
                excelRow++;
            }

            return excelRow;
        }

        private static PurchaseMetrics CalculateDayMetrics(
            IList<PurchaseRow> purchases,
            IList<PurchaseReturnRow> returns,
            DateTime date)
        {
            var purchaseOrders = new HashSet<long>();
            var returnOrders = new HashSet<long>();
            var suppliers = new HashSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            long purchaseQuantity = 0;
            long purchaseAmountCent = 0;
            long returnQuantity = 0;
            long returnAmountCent = 0;

            foreach (var purchase in purchases)
            {
                if (purchase.PurchasedAt.Date != date.Date) continue;
                purchaseOrders.Add(purchase.PurchaseId);
                suppliers.Add(NormalizeSupplier(purchase.SupplierName));
                purchaseQuantity += purchase.Quantity;
                purchaseAmountCent += purchase.LineTotalCent;
            }

            foreach (var returned in returns)
            {
                if (returned.ReturnedAt.Date != date.Date) continue;
                returnOrders.Add(returned.ReturnId);
                suppliers.Add(NormalizeSupplier(returned.SupplierName));
                returnQuantity += returned.Quantity;
                returnAmountCent += returned.LineTotalCent;
            }

            return new PurchaseMetrics
            {
                PurchaseOrderCount = purchaseOrders.Count,
                PurchaseQuantity = purchaseQuantity,
                PurchaseAmountCent = purchaseAmountCent,
                ReturnOrderCount = returnOrders.Count,
                ReturnQuantity = returnQuantity,
                ReturnAmountCent = returnAmountCent,
                SupplierCount = suppliers.Count
            };
        }

        private static PurchaseMetrics CalculatePeriodMetrics(
            IList<PurchaseRow> purchases,
            IList<PurchaseReturnRow> returns)
        {
            var purchaseOrders = new HashSet<long>();
            var returnOrders = new HashSet<long>();
            var suppliers = new HashSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            var metrics = new PurchaseMetrics();

            foreach (var purchase in purchases)
            {
                purchaseOrders.Add(purchase.PurchaseId);
                suppliers.Add(NormalizeSupplier(purchase.SupplierName));
                metrics.PurchaseQuantity += purchase.Quantity;
                metrics.PurchaseAmountCent += purchase.LineTotalCent;
            }

            foreach (var returned in returns)
            {
                returnOrders.Add(returned.ReturnId);
                suppliers.Add(NormalizeSupplier(returned.SupplierName));
                metrics.ReturnQuantity += returned.Quantity;
                metrics.ReturnAmountCent += returned.LineTotalCent;
            }

            metrics.PurchaseOrderCount = purchaseOrders.Count;
            metrics.ReturnOrderCount = returnOrders.Count;
            metrics.SupplierCount = suppliers.Count;
            return metrics;
        }

        private static int ComparePurchases(PurchaseRow left, PurchaseRow right)
        {
            var date = left.PurchasedAt.CompareTo(right.PurchasedAt);
            if (date != 0) return date;
            var order = left.PurchaseId.CompareTo(right.PurchaseId);
            if (order != 0) return order;
            return left.ItemId.CompareTo(right.ItemId);
        }

        private static int CompareReturns(
            PurchaseReturnRow left,
            PurchaseReturnRow right)
        {
            var date = left.ReturnedAt.CompareTo(right.ReturnedAt);
            if (date != 0) return date;
            var order = left.ReturnId.CompareTo(right.ReturnId);
            if (order != 0) return order;
            return left.ReturnItemId.CompareTo(right.ReturnItemId);
        }

        private static IList<PurchaseRow> ReadPurchases(DataTable table)
        {
            var result = new List<PurchaseRow>();
            foreach (DataRow row in table.Rows)
            {
                result.Add(new PurchaseRow
                {
                    PurchaseId = ToLong(row["purchase_id"]),
                    ItemId = ToLong(row["item_id"]),
                    PurchasedAt = ToDateTime(row["purchased_at"]),
                    ReviewedAt = ToNullableDateTime(row["reviewed_at"]),
                    OrderNo = ToText(row["order_no"]),
                    SupplierName = ToText(row["supplier_name"]),
                    OrderNote = ToText(row["order_note"]),
                    BookId = ToLong(row["book_id"]),
                    Isbn = ToText(row["isbn"]),
                    Title = ToText(row["title"]),
                    Quantity = ToInt(row["quantity"]),
                    UnitCostCent = ToLong(row["unit_cost_cent"]),
                    LineTotalCent = ToLong(row["line_total_cent"]),
                    ReturnedQuantityInMonth = ToInt(row["returned_quantity_in_month"]),
                    ReturnedQuantityToMonthEnd = ToInt(row["returned_quantity_to_month_end"])
                });
            }
            return result;
        }

        private static IList<PurchaseReturnRow> ReadReturns(DataTable table)
        {
            var result = new List<PurchaseReturnRow>();
            foreach (DataRow row in table.Rows)
            {
                result.Add(new PurchaseReturnRow
                {
                    ReturnId = ToLong(row["return_id"]),
                    ReturnItemId = ToLong(row["return_item_id"]),
                    ReturnedAt = ToDateTime(row["returned_at"]),
                    ReturnNo = ToText(row["return_no"]),
                    SourceOrderNo = ToText(row["source_order_no"]),
                    SupplierName = ToText(row["supplier_name"]),
                    ReturnNote = ToText(row["return_note"]),
                    BookId = ToLong(row["book_id"]),
                    Isbn = ToText(row["isbn"]),
                    Title = ToText(row["title"]),
                    Quantity = ToInt(row["quantity"]),
                    UnitCostCent = ToLong(row["unit_cost_cent"]),
                    LineTotalCent = ToLong(row["line_total_cent"])
                });
            }
            return result;
        }

        private static string NormalizeSupplier(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "不区分"
                : value.Trim();
        }

        private static void WriteDetail(
            ISheet sheet,
            StyleFactory styles,
            int rowIndex,
            int columnIndex,
            object value,
            string format,
            BorderStyle top,
            BorderStyle bottom,
            HorizontalAlignment alignment = HorizontalAlignment.Center,
            bool wrap = false)
        {
            var cell = GetCell(sheet, rowIndex, columnIndex);
            SetValue(cell, value);
            cell.CellStyle = styles.GetDetailStyle(
                format,
                alignment,
                wrap,
                top,
                bottom);
            GetRow(sheet, rowIndex).HeightInPoints = wrap ? 30F : 22F;
        }

        private static void WriteCell(
            ISheet sheet,
            int rowIndex,
            int columnIndex,
            object value,
            ICellStyle style)
        {
            var cell = GetCell(sheet, rowIndex, columnIndex);
            SetValue(cell, value);
            cell.CellStyle = style;
        }

        private static void SetValue(ICell cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.SetCellValue("");
                return;
            }

            if (value is DateTime)
            {
                cell.SetCellValue((DateTime)value);
                return;
            }

            if (value is byte || value is short || value is int ||
                value is long || value is float || value is double ||
                value is decimal)
            {
                cell.SetCellValue(
                    Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return;
            }

            cell.SetCellValue(
                Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static IRow GetRow(ISheet sheet, int rowIndex)
        {
            return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        }

        private static ICell GetCell(
            ISheet sheet,
            int rowIndex,
            int columnIndex)
        {
            var row = GetRow(sheet, rowIndex);
            return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
        }

        private static int Width(double characters)
        {
            return Math.Min(
                255 * 256,
                Math.Max(1, (int)Math.Round(characters * 256)));
        }

        private static long ToLong(object value)
        {
            return value == null || value == DBNull.Value
                ? 0
                : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object value)
        {
            return value == null || value == DBNull.Value
                ? 0
                : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string ToText(object value)
        {
            return value == null || value == DBNull.Value
                ? ""
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static DateTime ToDateTime(object value)
        {
            var nullable = ToNullableDateTime(value);
            if (nullable.HasValue) return nullable.Value;
            throw new InvalidOperationException("采购月报存在无法识别的日期。");
        }

        private static DateTime? ToNullableDateTime(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            if (value is DateTime) return (DateTime)value;

            DateTime parsed;
            var text = Convert.ToString(value);
            if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed) ||
                DateTime.TryParse(text, out parsed))
                return parsed;

            return null;
        }

        private sealed class StyleFactory
        {
            private readonly IWorkbook _workbook;
            private readonly IDataFormat _dataFormat;
            private readonly Dictionary<string, ICellStyle> _detailStyles =
                new Dictionary<string, ICellStyle>();

            public StyleFactory(IWorkbook workbook)
            {
                _workbook = workbook;
                _dataFormat = workbook.CreateDataFormat();

                Title = Create(
                    true, 12, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Center, null, false);
                Header = Create(
                    true, 9, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Center, null, true);
                Section = Create(
                    true, 9, IndexedColors.LightBlue.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Left, null, false);
                Text = Create(
                    false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Center, null, false);
                LeftText = Create(
                    false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Left, null, true);
                Integer = Create(
                    false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Right, "0", false);
                Money = Create(
                    false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Right, "0.00", false);
                TotalText = Create(
                    true, 9, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Center, null, false);
                TotalInteger = Create(
                    true, 9, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Right, "0", false);
                TotalMoney = Create(
                    true, 9, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Right, "0.00", false);
            }

            public ICellStyle Title { get; private set; }
            public ICellStyle Header { get; private set; }
            public ICellStyle Section { get; private set; }
            public ICellStyle Text { get; private set; }
            public ICellStyle LeftText { get; private set; }
            public ICellStyle Integer { get; private set; }
            public ICellStyle Money { get; private set; }
            public ICellStyle TotalText { get; private set; }
            public ICellStyle TotalInteger { get; private set; }
            public ICellStyle TotalMoney { get; private set; }

            public ICellStyle GetDetailStyle(
                string format,
                HorizontalAlignment alignment,
                bool wrap,
                BorderStyle top,
                BorderStyle bottom)
            {
                var key =
                    (format ?? "") + "|" + alignment + "|" +
                    wrap + "|" + top + "|" + bottom;
                ICellStyle style;
                if (_detailStyles.TryGetValue(key, out style))
                    return style;

                style = Create(
                    false,
                    9,
                    IndexedColors.White.Index,
                    BorderStyle.Thin,
                    BorderStyle.Thin,
                    top,
                    bottom,
                    alignment,
                    format,
                    wrap);
                _detailStyles[key] = style;
                return style;
            }

            private ICellStyle Create(
                bool bold,
                short fontSize,
                short fill,
                BorderStyle left,
                BorderStyle right,
                BorderStyle top,
                BorderStyle bottom,
                HorizontalAlignment alignment,
                string format,
                bool wrap)
            {
                var style = _workbook.CreateCellStyle();
                style.Alignment = alignment;
                style.VerticalAlignment = VerticalAlignment.Center;
                style.WrapText = wrap;
                style.BorderLeft = left;
                style.BorderRight = right;
                style.BorderTop = top;
                style.BorderBottom = bottom;

                var font = _workbook.CreateFont();
                font.FontName = "宋体";
                font.FontHeightInPoints = fontSize;
                font.IsBold = bold;
                style.SetFont(font);

                if (fill != IndexedColors.White.Index)
                {
                    style.FillForegroundColor = fill;
                    style.FillPattern = FillPattern.SolidForeground;
                }

                if (!string.IsNullOrWhiteSpace(format))
                    style.DataFormat = _dataFormat.GetFormat(format);

                return style;
            }
        }

        private sealed class PurchaseMetrics
        {
            public int PurchaseOrderCount { get; set; }
            public long PurchaseQuantity { get; set; }
            public long PurchaseAmountCent { get; set; }
            public int ReturnOrderCount { get; set; }
            public long ReturnQuantity { get; set; }
            public long ReturnAmountCent { get; set; }
            public int SupplierCount { get; set; }
        }

        private sealed class PurchaseRow
        {
            public long PurchaseId { get; set; }
            public long ItemId { get; set; }
            public DateTime PurchasedAt { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public string OrderNo { get; set; }
            public string SupplierName { get; set; }
            public string OrderNote { get; set; }
            public long BookId { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public int Quantity { get; set; }
            public long UnitCostCent { get; set; }
            public long LineTotalCent { get; set; }
            public int ReturnedQuantityInMonth { get; set; }
            public int ReturnedQuantityToMonthEnd { get; set; }

            public PurchaseRow()
            {
                OrderNo = "";
                SupplierName = "";
                OrderNote = "";
                Isbn = "";
                Title = "";
            }
        }

        private sealed class PurchaseReturnRow
        {
            public long ReturnId { get; set; }
            public long ReturnItemId { get; set; }
            public DateTime ReturnedAt { get; set; }
            public string ReturnNo { get; set; }
            public string SourceOrderNo { get; set; }
            public string SupplierName { get; set; }
            public string ReturnNote { get; set; }
            public long BookId { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public int Quantity { get; set; }
            public long UnitCostCent { get; set; }
            public long LineTotalCent { get; set; }

            public PurchaseReturnRow()
            {
                ReturnNo = "";
                SourceOrderNo = "";
                SupplierName = "";
                ReturnNote = "";
                Isbn = "";
                Title = "";
            }
        }
    }
}
