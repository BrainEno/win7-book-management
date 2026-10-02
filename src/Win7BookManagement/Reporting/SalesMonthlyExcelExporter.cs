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
    public sealed class SalesMonthlyExcelExporter
    {
        private const int MaxCalendarDays = 31;
        private const int FirstDayColumn = 4;
        private const int ColumnsPerDay = 4;

        private static readonly BusinessLine[] BusinessLines =
        {
            new BusinessLine("书籍", "零售"),
            new BusinessLine("", "亲爱的陌生人"),
            new BusinessLine("", "独立出版书籍"),
            new BusinessLine("饮品", ""),
            new BusinessLine("文创", "2024圣诞礼包49元"),
            new BusinessLine("", "2024圣诞礼包59元"),
            new BusinessLine("", "笔记本/日历"),
            new BusinessLine("", "其他文创品"),
            new BusinessLine("", "目田帆布包"),
            new BusinessLine("", "【漫滩】帆布包"),
            new BusinessLine("", "目田明信片"),
            new BusinessLine("", "纸袋/徽章"),
            new BusinessLine("", "小画片"),
            new BusinessLine("书架", "充值"),
            new BusinessLine("活动", "一锅乱炖"),
            new BusinessLine("", "目田福袋"),
            new BusinessLine("", "木刻活动"),
            new BusinessLine("", "佛里灯唱片"),
            new BusinessLine("", "放映册子"),
            new BusinessLine("", "樊懿画作"),
            new BusinessLine("", "电影茶水费"),
            new BusinessLine("其他", "赠送/CD/快递")
        };

        private static readonly string[] TemplateDetailHeaders =
        {
            "序号", "单号", "商品编码", "商品名称", "数量", "库存", "售价", "折让价",
            "折扣", "码洋", "实洋", "出版日期", "价款(财务成本)", "税款", "财务实洋",
            "进货折扣(参考值)", "标记", "批准退货人", "预定折扣", "操作时间",
            "明细业务员", "成本(参考值)", "毛利(参考值)", "部门编码", "用户名称",
            "折扣人员", "供 应 商", "出版社号", "出版社", "作者",
            "收款方式", "应收金额", "实收金额", "找零金额", "单据备注", "系统分类"
        };

        private static readonly int[] DetailColumnWidths =
        {
            6, 20, 16, 28, 8, 8, 10, 10, 8, 10, 10, 12, 14, 10, 12, 15, 9, 12,
            10, 20, 12, 14, 14, 10, 12, 12, 16, 12, 18, 20, 10, 12, 12, 10, 24, 14
        };

        public void Export(
            DataTable detail,
            string path,
            DateTime month,
            string storeName,
            int nightShiftStartHour)
        {
            if (detail == null) throw new ArgumentNullException("detail");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("导出路径不能为空。", "path");
            if (nightShiftStartHour < 0 || nightShiftStartHour > 23)
                throw new ArgumentOutOfRangeException("nightShiftStartHour");

            var normalizedMonth = new DateTime(month.Year, month.Month, 1);
            var normalizedStoreName = string.IsNullOrWhiteSpace(storeName)
                ? "目田书店"
                : storeName.Trim();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var rows = ReadRows(detail);
            IWorkbook workbook = new XSSFWorkbook();
            try
            {
                var styles = new StyleFactory(workbook);
                CreateMonthlySummary(
                    workbook,
                    styles,
                    rows,
                    normalizedMonth,
                    normalizedStoreName,
                    nightShiftStartHour);

                var days = DateTime.DaysInMonth(normalizedMonth.Year, normalizedMonth.Month);
                for (var day = 1; day <= days; day++)
                {
                    CreateDailyDetail(
                        workbook,
                        styles,
                        rows,
                        new DateTime(normalizedMonth.Year, normalizedMonth.Month, day));
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
            IList<MonthlySalesRow> rows,
            DateTime month,
            string storeName,
            int nightShiftStartHour)
        {
            var sheet = workbook.CreateSheet("销售月报表");
            sheet.CreateFreezePane(4, 0);

            sheet.SetColumnWidth(0, Width(8.43));
            sheet.SetColumnWidth(1, Width(13.875));
            sheet.SetColumnWidth(2, Width(8.43));
            sheet.SetColumnWidth(3, Width(8.43));

            for (var day = 0; day < MaxCalendarDays; day++)
            {
                var start = FirstDayColumn + day * ColumnsPerDay;
                sheet.SetColumnWidth(start, Width(4.25));
                sheet.SetColumnWidth(start + 1, Width(5.0));
                sheet.SetColumnWidth(start + 2, Width(4.25));
                sheet.SetColumnWidth(start + 3, Width(5.0));
            }

            var titleRow = GetRow(sheet, 0);
            titleRow.HeightInPoints = 29.1F;
            MergeAndStyle(
                sheet,
                styles,
                0,
                0,
                0,
                3,
                storeName + " " + month.Month + "月份销售报表",
                FillTone.Yellow,
                true,
                12,
                false,
                HorizontalAlignment.Center,
                BorderStyle.None,
                BorderStyle.None,
                BorderStyle.None,
                BorderStyle.None);

            GetRow(sheet, 1).HeightInPoints = 17.1F;
            GetRow(sheet, 2).HeightInPoints = 17.1F;
            GetRow(sheet, 3).HeightInPoints = 33F;

            MergeAndStyle(
                sheet, styles, 1, 1, 0, 1, "日期",
                FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin);
            MergeAndStyle(
                sheet, styles, 1, 1, 2, 3, " 月份小计",
                FillTone.Yellow, true, 9, false, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin);
            MergeAndStyle(
                sheet, styles, 2, 3, 0, 1, "项  目",
                FillTone.Yellow, false, 9, true, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);

            WriteSummaryCell(
                sheet, styles, 3, 2, "数量", FillTone.Yellow, true, true,
                BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium);
            WriteSummaryCell(
                sheet, styles, 3, 3, "实收金额", FillTone.Yellow, true, true,
                BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);

            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            for (var day = 1; day <= MaxCalendarDays; day++)
            {
                var start = FirstDayColumn + (day - 1) * ColumnsPerDay;
                var dayLabel = day <= daysInMonth ? day + "日" : "";

                MergeAndStyle(
                    sheet, styles, 1, 1, start, start + 3, dayLabel,
                    FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                    BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin);

                MergeAndStyle(
                    sheet, styles, 2, 2, start, start + 1, day <= daysInMonth ? "白班" : "",
                    FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                MergeAndStyle(
                    sheet, styles, 2, 2, start + 2, start + 3, day <= daysInMonth ? "晚班" : "",
                    FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                WriteSummaryCell(
                    sheet, styles, 3, start, day <= daysInMonth ? "数量" : "",
                    FillTone.Yellow, false, true,
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium);
                WriteSummaryCell(
                    sheet, styles, 3, start + 1, day <= daysInMonth ? "实收金额" : "",
                    FillTone.Yellow, false, true,
                    BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium);
                WriteSummaryCell(
                    sheet, styles, 3, start + 2, day <= daysInMonth ? "数量" : "",
                    FillTone.Yellow, false, true,
                    BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Medium);
                WriteSummaryCell(
                    sheet, styles, 3, start + 3, day <= daysInMonth ? "实收金额" : "",
                    FillTone.Yellow, false, true,
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);
            }

            var quantity = new long[BusinessLines.Length, MaxCalendarDays, 2];
            var amount = new long[BusinessLines.Length, MaxCalendarDays, 2];
            var orderMap = new Dictionary<long, MonthlyOrder>();
            var notesByDay = new List<string>[MaxCalendarDays];

            for (var i = 0; i < notesByDay.Length; i++)
                notesByDay[i] = new List<string>();

            foreach (var row in rows)
            {
                var lineIndex = ResolveBusinessLine(row.Category);
                var dayIndex = row.SoldAt.Day - 1;
                var shift = row.SoldAt.Hour >= nightShiftStartHour ? 1 : 0;

                quantity[lineIndex, dayIndex, shift] += row.Quantity;
                amount[lineIndex, dayIndex, shift] += row.LineTotalCent;

                MonthlyOrder order;
                if (!orderMap.TryGetValue(row.OrderId, out order))
                {
                    order = new MonthlyOrder
                    {
                        OrderId = row.OrderId,
                        OrderNo = row.OrderNo,
                        SoldAt = row.SoldAt,
                        PaymentMethod = row.PaymentMethod,
                        TotalCent = row.OrderTotalCent,
                        AmountReceivedCent = row.AmountReceivedCent,
                        ChangeCent = row.ChangeCent,
                        Note = row.OrderNote
                    };
                    orderMap.Add(row.OrderId, order);

                    if (!string.IsNullOrWhiteSpace(row.OrderNote))
                    {
                        notesByDay[dayIndex].Add(
                            row.SoldAt.ToString("HH:mm") + " " + row.OrderNo + "：" + row.OrderNote.Trim());
                    }
                }
            }

            for (var i = 0; i < BusinessLines.Length; i++)
            {
                var sheetRowIndex = 4 + i;
                var row = GetRow(sheet, sheetRowIndex);
                row.HeightInPoints = 17.1F;

                var definition = BusinessLines[i];

                WriteSummaryCell(
                    sheet, styles, sheetRowIndex, 0, definition.Group,
                    FillTone.Yellow, false, false,
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryCell(
                    sheet, styles, sheetRowIndex, 1, definition.Label,
                    FillTone.Yellow, false, false,
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                long monthQuantity = 0;
                long monthAmount = 0;
                for (var day = 0; day < MaxCalendarDays; day++)
                {
                    for (var shift = 0; shift < 2; shift++)
                    {
                        monthQuantity += quantity[i, day, shift];
                        monthAmount += amount[i, day, shift];
                    }
                }

                WriteSummaryNumber(
                    sheet, styles, sheetRowIndex, 2, monthQuantity,
                    FillTone.Yellow, true, "0",
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryNumber(
                    sheet, styles, sheetRowIndex, 3, monthAmount / 100.0,
                    FillTone.Yellow, true, "0.00",
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                for (var day = 0; day < MaxCalendarDays; day++)
                {
                    var start = FirstDayColumn + day * ColumnsPerDay;
                    for (var shift = 0; shift < 2; shift++)
                    {
                        var qtyCol = start + shift * 2;
                        var amountCol = qtyCol + 1;
                        var left = shift == 0 ? BorderStyle.Medium : BorderStyle.Thin;
                        var right = shift == 1 ? BorderStyle.Medium : BorderStyle.Thin;

                        if (day >= daysInMonth)
                        {
                            WriteSummaryCell(
                                sheet, styles, sheetRowIndex, qtyCol, "",
                                FillTone.None, false, false,
                                left, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                            WriteSummaryCell(
                                sheet, styles, sheetRowIndex, amountCol, "",
                                FillTone.Blue, false, false,
                                BorderStyle.Thin, right, BorderStyle.Thin, BorderStyle.Thin);
                            continue;
                        }

                        WriteSummaryNumber(
                            sheet, styles, sheetRowIndex, qtyCol, quantity[i, day, shift],
                            FillTone.None, false, "0",
                            left, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                        WriteSummaryNumber(
                            sheet, styles, sheetRowIndex, amountCol, amount[i, day, shift] / 100.0,
                            FillTone.Blue, false, "0.00",
                            BorderStyle.Thin, right, BorderStyle.Thin, BorderStyle.Thin);
                    }
                }
            }

            sheet.AddMergedRegion(new CellRangeAddress(4, 6, 0, 0));
            sheet.AddMergedRegion(new CellRangeAddress(8, 16, 0, 0));
            sheet.AddMergedRegion(new CellRangeAddress(18, 24, 0, 0));

            var totalRowIndex = 26;
            GetRow(sheet, totalRowIndex).HeightInPoints = 17.1F;
            MergeAndStyle(
                sheet, styles, totalRowIndex, totalRowIndex, 0, 1, "合计",
                FillTone.Yellow, true, 9, false, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium);

            long grandQuantity = 0;
            long grandAmount = 0;
            for (var i = 0; i < BusinessLines.Length; i++)
            {
                for (var day = 0; day < MaxCalendarDays; day++)
                {
                    for (var shift = 0; shift < 2; shift++)
                    {
                        grandQuantity += quantity[i, day, shift];
                        grandAmount += amount[i, day, shift];
                    }
                }
            }

            WriteSummaryNumber(
                sheet, styles, totalRowIndex, 2, grandQuantity,
                FillTone.Yellow, true, "0",
                BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Medium);
            WriteSummaryNumber(
                sheet, styles, totalRowIndex, 3, grandAmount / 100.0,
                FillTone.Yellow, true, "0.00",
                BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Medium);

            for (var day = 0; day < MaxCalendarDays; day++)
            {
                var start = FirstDayColumn + day * ColumnsPerDay;
                for (var shift = 0; shift < 2; shift++)
                {
                    long dayQty = 0;
                    long dayAmount = 0;
                    for (var i = 0; i < BusinessLines.Length; i++)
                    {
                        dayQty += quantity[i, day, shift];
                        dayAmount += amount[i, day, shift];
                    }

                    var qtyCol = start + shift * 2;
                    var amountCol = qtyCol + 1;
                    WriteSummaryNumber(
                        sheet, styles, totalRowIndex, qtyCol, day < daysInMonth ? dayQty : 0,
                        FillTone.Yellow, true, "0",
                        shift == 0 ? BorderStyle.Medium : BorderStyle.Thin,
                        BorderStyle.Thin,
                        BorderStyle.Medium,
                        BorderStyle.Medium);
                    WriteSummaryNumber(
                        sheet, styles, totalRowIndex, amountCol, day < daysInMonth ? dayAmount / 100.0 : 0,
                        FillTone.Yellow, true, "0.00",
                        BorderStyle.Thin,
                        shift == 1 ? BorderStyle.Medium : BorderStyle.Thin,
                        BorderStyle.Medium,
                        BorderStyle.Medium);
                }
            }

            var paymentMethods = BuildPaymentMethods(orderMap.Values);
            var paymentStartRow = 27;
            for (var paymentIndex = 0; paymentIndex < paymentMethods.Count; paymentIndex++)
            {
                var paymentRow = paymentStartRow + paymentIndex;
                GetRow(sheet, paymentRow).HeightInPoints = 17.1F;
                var paymentMethod = paymentMethods[paymentIndex];

                WriteSummaryCell(
                    sheet, styles, paymentRow, 0, "",
                    FillTone.Yellow, false, false,
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryCell(
                    sheet, styles, paymentRow, 1, paymentMethod,
                    FillTone.Yellow, false, false,
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                long paymentMonthAmount = 0;
                var paymentByDayShift = new long[MaxCalendarDays, 2];
                foreach (var order in orderMap.Values)
                {
                    if (!string.Equals(order.PaymentMethod, paymentMethod, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dayIndex = order.SoldAt.Day - 1;
                    var shift = order.SoldAt.Hour >= nightShiftStartHour ? 1 : 0;
                    paymentByDayShift[dayIndex, shift] += order.TotalCent;
                    paymentMonthAmount += order.TotalCent;
                }

                WriteSummaryNumber(
                    sheet, styles, paymentRow, 2, 0,
                    FillTone.Yellow, true, "0",
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryNumber(
                    sheet, styles, paymentRow, 3, paymentMonthAmount / 100.0,
                    FillTone.Yellow, true, "0.00",
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                for (var day = 0; day < MaxCalendarDays; day++)
                {
                    var start = FirstDayColumn + day * ColumnsPerDay;
                    for (var shift = 0; shift < 2; shift++)
                    {
                        var qtyCol = start + shift * 2;
                        var amountCol = qtyCol + 1;
                        WriteSummaryNumber(
                            sheet, styles, paymentRow, qtyCol, 0,
                            FillTone.None, false, "0",
                            shift == 0 ? BorderStyle.Medium : BorderStyle.Thin,
                            BorderStyle.Thin,
                            BorderStyle.Thin,
                            BorderStyle.Thin);
                        WriteSummaryNumber(
                            sheet, styles, paymentRow, amountCol,
                            day < daysInMonth ? paymentByDayShift[day, shift] / 100.0 : 0,
                            FillTone.Blue, false, "0.00",
                            BorderStyle.Thin,
                            shift == 1 ? BorderStyle.Medium : BorderStyle.Thin,
                            BorderStyle.Thin,
                            BorderStyle.Thin);
                    }
                }
            }

            var reservedLabels = new[] { "微店收入", "刷会员卡小计", "未收款小计" };
            var reservedStartRow = paymentStartRow + paymentMethods.Count;
            for (var reservedIndex = 0; reservedIndex < reservedLabels.Length; reservedIndex++)
            {
                var rowIndex = reservedStartRow + reservedIndex;
                GetRow(sheet, rowIndex).HeightInPoints = 17.1F;
                MergeAndStyle(
                    sheet, styles, rowIndex, rowIndex, 0, 1, reservedLabels[reservedIndex],
                    FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                    BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryNumber(
                    sheet, styles, rowIndex, 2, 0,
                    FillTone.Yellow, true, "0",
                    BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                WriteSummaryNumber(
                    sheet, styles, rowIndex, 3, 0,
                    FillTone.Yellow, true, "0.00",
                    BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

                for (var day = 0; day < MaxCalendarDays; day++)
                {
                    var start = FirstDayColumn + day * ColumnsPerDay;
                    for (var shift = 0; shift < 2; shift++)
                    {
                        var qtyCol = start + shift * 2;
                        var amountCol = qtyCol + 1;
                        WriteSummaryNumber(
                            sheet, styles, rowIndex, qtyCol, 0,
                            FillTone.None, false, "0",
                            shift == 0 ? BorderStyle.Medium : BorderStyle.Thin,
                            BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
                        WriteSummaryNumber(
                            sheet, styles, rowIndex, amountCol, 0,
                            FillTone.Blue, false, "0.00",
                            BorderStyle.Thin,
                            shift == 1 ? BorderStyle.Medium : BorderStyle.Thin,
                            BorderStyle.Thin, BorderStyle.Thin);
                    }
                }
            }

            var staffRowIndex = reservedStartRow + reservedLabels.Length;
            GetRow(sheet, staffRowIndex).HeightInPoints = 30.75F;
            MergeAndStyle(
                sheet, styles, staffRowIndex, staffRowIndex, 0, 1, "值班人员",
                FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);
            WriteSummaryCell(
                sheet, styles, staffRowIndex, 2, "",
                FillTone.Yellow, false, false,
                BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin, BorderStyle.Thin);
            WriteSummaryCell(
                sheet, styles, staffRowIndex, 3, "",
                FillTone.Yellow, false, false,
                BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);

            for (var day = 0; day < MaxCalendarDays; day++)
            {
                var start = FirstDayColumn + day * ColumnsPerDay;
                MergeAndStyle(
                    sheet, styles, staffRowIndex, staffRowIndex, start, start + 3, "",
                    FillTone.None, false, 9, false, HorizontalAlignment.Center,
                    BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Thin);
            }

            var noteRowIndex = staffRowIndex + 1;
            GetRow(sheet, noteRowIndex).HeightInPoints = 348F;
            MergeAndStyle(
                sheet, styles, noteRowIndex, noteRowIndex, 0, 1, "备注说明",
                FillTone.Yellow, false, 9, false, HorizontalAlignment.Center,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);
            MergeAndStyle(
                sheet, styles, noteRowIndex, noteRowIndex, 2, 3,
                "班次：00:00–" + (nightShiftStartHour - 1 + 24) % 24 + ":59 白班；" +
                nightShiftStartHour.ToString("00") + ":00 起晚班。",
                FillTone.Yellow, false, 8, true, HorizontalAlignment.Left,
                BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);

            for (var day = 0; day < MaxCalendarDays; day++)
            {
                var start = FirstDayColumn + day * ColumnsPerDay;
                var noteText = day < daysInMonth
                    ? string.Join(Environment.NewLine, notesByDay[day].ToArray())
                    : "";
                MergeAndStyle(
                    sheet, styles, noteRowIndex, noteRowIndex, start, start + 3, noteText,
                    FillTone.None, false, 9, true, HorizontalAlignment.Left,
                    BorderStyle.Medium, BorderStyle.Medium, BorderStyle.Thin, BorderStyle.Medium);
            }
        }

        private static void CreateDailyDetail(
            IWorkbook workbook,
            StyleFactory styles,
            IList<MonthlySalesRow> rows,
            DateTime date)
        {
            var sheet = workbook.CreateSheet(date.ToString("MMdd", CultureInfo.InvariantCulture));
            sheet.CreateFreezePane(0, 2);

            for (var i = 0; i < DetailColumnWidths.Length; i++)
                sheet.SetColumnWidth(i, Width(DetailColumnWidths[i]));

            GetRow(sheet, 0).HeightInPoints = 8F;
            var headerRow = GetRow(sheet, 1);
            headerRow.HeightInPoints = 34F;

            for (var i = 0; i < TemplateDetailHeaders.Length; i++)
            {
                var cell = GetCell(sheet, 1, i);
                cell.SetCellValue(TemplateDetailHeaders[i]);
                cell.CellStyle = styles.Get(
                    FillTone.Yellow,
                    true,
                    9,
                    true,
                    HorizontalAlignment.Center,
                    BorderStyle.Medium,
                    BorderStyle.Medium,
                    BorderStyle.Medium,
                    BorderStyle.Medium,
                    null);
            }

            var dayRows = new List<MonthlySalesRow>();
            foreach (var row in rows)
            {
                if (row.SoldAt.Date == date.Date)
                    dayRows.Add(row);
            }

            dayRows.Sort(delegate(MonthlySalesRow left, MonthlySalesRow right)
            {
                var time = left.SoldAt.CompareTo(right.SoldAt);
                if (time != 0) return time;
                var order = left.OrderId.CompareTo(right.OrderId);
                if (order != 0) return order;
                return left.ItemId.CompareTo(right.ItemId);
            });

            var currentExcelRow = 2;
            var orderSequence = 0;
            var index = 0;
            while (index < dayRows.Count)
            {
                var orderId = dayRows[index].OrderId;
                var start = index;
                var end = index;
                while (end + 1 < dayRows.Count && dayRows[end + 1].OrderId == orderId)
                    end++;

                orderSequence++;
                var groupFirstExcelRow = currentExcelRow;
                var groupLastExcelRow = currentExcelRow + (end - start);

                for (var i = start; i <= end; i++)
                {
                    var source = dayRows[i];
                    var rowIndex = currentExcelRow++;
                    var firstInOrder = i == start;
                    var lastInOrder = i == end;
                    var topBorder = firstInOrder ? BorderStyle.Medium : BorderStyle.Thin;
                    var bottomBorder = lastInOrder ? BorderStyle.Medium : BorderStyle.Thin;
                    GetRow(sheet, rowIndex).HeightInPoints = 22F;

                    var listPriceCent = source.ListPriceCent > 0
                        ? source.ListPriceCent
                        : source.BaseUnitPriceCent;
                    var costTotalCent = checked(source.CostRefCent * source.Quantity);
                    var grossCent = checked(listPriceCent * source.Quantity);
                    var purchaseDiscount = listPriceCent > 0
                        ? source.CostRefCent * 100.0 / listPriceCent
                        : 0.0;
                    var marker = source.ReturnedQuantity <= 0
                        ? "no"
                        : source.ReturnedQuantity >= source.Quantity ? "已退" : "部分退";

                    WriteDetailValue(sheet, styles, rowIndex, 0, firstInOrder ? (object)orderSequence : null, "0", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 1, firstInOrder ? source.OrderNo : null, "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 2, source.SelfCode, "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 3, source.Title, "@", topBorder, bottomBorder, HorizontalAlignment.Left);
                    WriteDetailValue(sheet, styles, rowIndex, 4, source.Quantity, "0", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 5, source.StockAtSale, "0", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 6, source.BaseUnitPriceCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 7, source.FinalUnitPriceCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 8, source.LineDiscountBasisPoints / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 9, grossCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 10, source.LineTotalCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 11, source.PublicationYear, "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 12, costTotalCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 13, 0.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 14, source.LineTotalCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 15, purchaseDiscount, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 16, marker, "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 17, "", "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 18, firstInOrder ? (object)(source.OrderDiscountBasisPoints / 100.0) : null, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 19, firstInOrder ? (object)source.SoldAt : null, "yyyy-mm-dd hh:mm:ss", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 20, "", "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 21, costTotalCent / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 22, (source.LineTotalCent - costTotalCent) / 100.0, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 23, 9999, "0", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 24, "", "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 25, "", "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 26, string.IsNullOrWhiteSpace(source.SupplierName) ? "不区分" : source.SupplierName, "@", topBorder, bottomBorder, HorizontalAlignment.Left);
                    WriteDetailValue(sheet, styles, rowIndex, 27, "", "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 28, source.Publisher, "@", topBorder, bottomBorder, HorizontalAlignment.Left);
                    WriteDetailValue(sheet, styles, rowIndex, 29, source.Author, "@", topBorder, bottomBorder, HorizontalAlignment.Left);
                    WriteDetailValue(sheet, styles, rowIndex, 30, firstInOrder ? source.PaymentMethod : null, "@", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 31, firstInOrder ? (object)(source.OrderTotalCent / 100.0) : null, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 32, firstInOrder ? (object)(source.AmountReceivedCent / 100.0) : null, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 33, firstInOrder ? (object)(source.ChangeCent / 100.0) : null, "0.00", topBorder, bottomBorder);
                    WriteDetailValue(sheet, styles, rowIndex, 34, firstInOrder ? source.OrderNote : null, "@", topBorder, bottomBorder, HorizontalAlignment.Left, true);
                    WriteDetailValue(sheet, styles, rowIndex, 35, source.Category, "@", topBorder, bottomBorder, HorizontalAlignment.Left);
                }

                if (groupLastExcelRow > groupFirstExcelRow)
                {
                    foreach (var column in new[] { 0, 1, 18, 19, 30, 31, 32, 33, 34 })
                    {
                        sheet.AddMergedRegion(
                            new CellRangeAddress(
                                groupFirstExcelRow,
                                groupLastExcelRow,
                                column,
                                column));
                    }
                }

                index = end + 1;
            }

            if (currentExcelRow == 2)
            {
                var empty = GetRow(sheet, 2);
                empty.HeightInPoints = 24F;
                var cell = GetCell(sheet, 2, 0);
                cell.SetCellValue("当日无销售记录");
                cell.CellStyle = styles.Get(
                    FillTone.None,
                    false,
                    9,
                    false,
                    HorizontalAlignment.Left,
                    BorderStyle.Thin,
                    BorderStyle.Thin,
                    BorderStyle.Thin,
                    BorderStyle.Thin,
                    null);
                sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, TemplateDetailHeaders.Length - 1));
            }

            sheet.SetAutoFilter(
                new CellRangeAddress(
                    1,
                    Math.Max(1, currentExcelRow - 1),
                    0,
                    TemplateDetailHeaders.Length - 1));
        }

        private static void WriteDetailValue(
            ISheet sheet,
            StyleFactory styles,
            int rowIndex,
            int columnIndex,
            object value,
            string numberFormat,
            BorderStyle topBorder,
            BorderStyle bottomBorder,
            HorizontalAlignment alignment = HorizontalAlignment.Center,
            bool wrap = false)
        {
            var cell = GetCell(sheet, rowIndex, columnIndex);
            SetCellValue(cell, value);
            cell.CellStyle = styles.Get(
                FillTone.None,
                false,
                9,
                wrap,
                alignment,
                BorderStyle.Thin,
                BorderStyle.Thin,
                topBorder,
                bottomBorder,
                numberFormat);
        }

        private static IList<MonthlySalesRow> ReadRows(DataTable table)
        {
            var result = new List<MonthlySalesRow>();
            foreach (DataRow row in table.Rows)
            {
                result.Add(new MonthlySalesRow
                {
                    OrderId = ToLong(row["order_id"]),
                    ItemId = ToLong(row["item_id"]),
                    SoldAt = ToDateTime(row["sold_at"]),
                    OrderNo = ToStringValue(row["order_no"]),
                    SelfCode = ToStringValue(row["self_code"]),
                    Isbn = ToStringValue(row["isbn"]),
                    Title = ToStringValue(row["title"]),
                    Author = ToStringValue(row["author"]),
                    Publisher = ToStringValue(row["publisher"]),
                    Category = ToStringValue(row["category"]),
                    PublicationYear = ToStringValue(row["publication_year"]),
                    Quantity = ToInt(row["quantity"]),
                    BaseUnitPriceCent = ToLong(row["base_unit_price_cent"]),
                    LineDiscountBasisPoints = ToInt(row["line_discount_basis_points"]),
                    LineDiscountedUnitPriceCent = ToLong(row["line_discounted_unit_price_cent"]),
                    OrderDiscountBasisPoints = ToInt(row["order_discount_basis_points"]),
                    FinalUnitPriceCent = ToLong(row["final_unit_price_cent"]),
                    LineTotalCent = ToLong(row["line_total_cent"]),
                    OrderTotalCent = ToLong(row["order_total_cent"]),
                    PaymentMethod = ToStringValue(row["payment_method"]),
                    AmountReceivedCent = ToLong(row["amount_received_cent"]),
                    ChangeCent = ToLong(row["change_cent"]),
                    OrderNote = ToStringValue(row["order_note"]),
                    ListPriceCent = ToLong(row["list_price_cent"]),
                    CostRefCent = ToLong(row["cost_ref_cent"]),
                    SupplierName = ToStringValue(row["supplier_name"]),
                    StockAtSale = ToInt(row["stock_at_sale"]),
                    ReturnedQuantity = ToInt(row["returned_quantity"])
                });
            }
            return result;
        }

        private static int ResolveBusinessLine(string category)
        {
            var value = (category ?? "").Trim();
            if (value.Length == 0)
                return 0;

            for (var i = 0; i < BusinessLines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(BusinessLines[i].Label) &&
                    string.Equals(value, BusinessLines[i].Label, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                if (string.IsNullOrWhiteSpace(BusinessLines[i].Label) &&
                    !string.IsNullOrWhiteSpace(BusinessLines[i].Group) &&
                    string.Equals(value, BusinessLines[i].Group, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            if (value.IndexOf("独立", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (value.IndexOf("饮品", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (value.IndexOf("文创", StringComparison.OrdinalIgnoreCase) >= 0)
                return 7;
            if (value.IndexOf("活动", StringComparison.OrdinalIgnoreCase) >= 0)
                return 14;
            return 0;
        }

        private static List<string> BuildPaymentMethods(ICollection<MonthlyOrder> orders)
        {
            var result = new List<string> { "现金", "微信", "支付宝" };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in result) seen.Add(value);

            foreach (var order in orders)
            {
                var value = string.IsNullOrWhiteSpace(order.PaymentMethod)
                    ? "未记录"
                    : order.PaymentMethod.Trim();
                if (seen.Add(value))
                    result.Add(value);
            }
            return result;
        }

        private static void WriteSummaryCell(
            ISheet sheet,
            StyleFactory styles,
            int rowIndex,
            int columnIndex,
            string value,
            FillTone fill,
            bool bold,
            bool wrap,
            BorderStyle left,
            BorderStyle right,
            BorderStyle top,
            BorderStyle bottom)
        {
            var cell = GetCell(sheet, rowIndex, columnIndex);
            cell.SetCellValue(value ?? "");
            cell.CellStyle = styles.Get(
                fill,
                bold,
                9,
                wrap,
                HorizontalAlignment.Center,
                left,
                right,
                top,
                bottom,
                null);
        }

        private static void WriteSummaryNumber(
            ISheet sheet,
            StyleFactory styles,
            int rowIndex,
            int columnIndex,
            double value,
            FillTone fill,
            bool bold,
            string numberFormat,
            BorderStyle left,
            BorderStyle right,
            BorderStyle top,
            BorderStyle bottom)
        {
            var cell = GetCell(sheet, rowIndex, columnIndex);
            cell.SetCellValue(value);
            cell.CellStyle = styles.Get(
                fill,
                bold,
                9,
                false,
                HorizontalAlignment.Center,
                left,
                right,
                top,
                bottom,
                numberFormat);
        }

        private static void MergeAndStyle(
            ISheet sheet,
            StyleFactory styles,
            int firstRow,
            int lastRow,
            int firstColumn,
            int lastColumn,
            string value,
            FillTone fill,
            bool bold,
            short fontSize,
            bool wrap,
            HorizontalAlignment alignment,
            BorderStyle left,
            BorderStyle right,
            BorderStyle top,
            BorderStyle bottom)
        {
            for (var rowIndex = firstRow; rowIndex <= lastRow; rowIndex++)
            {
                for (var columnIndex = firstColumn; columnIndex <= lastColumn; columnIndex++)
                {
                    var cell = GetCell(sheet, rowIndex, columnIndex);
                    if (rowIndex == firstRow && columnIndex == firstColumn)
                        cell.SetCellValue(value ?? "");
                    cell.CellStyle = styles.Get(
                        fill,
                        bold,
                        fontSize,
                        wrap,
                        alignment,
                        columnIndex == firstColumn ? left : BorderStyle.Thin,
                        columnIndex == lastColumn ? right : BorderStyle.Thin,
                        rowIndex == firstRow ? top : BorderStyle.Thin,
                        rowIndex == lastRow ? bottom : BorderStyle.Thin,
                        null);
                }
            }

            if (firstRow != lastRow || firstColumn != lastColumn)
                sheet.AddMergedRegion(new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn));
        }

        private static IRow GetRow(ISheet sheet, int rowIndex)
        {
            return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        }

        private static ICell GetCell(ISheet sheet, int rowIndex, int columnIndex)
        {
            var row = GetRow(sheet, rowIndex);
            return row.GetCell(columnIndex) ?? row.CreateCell(columnIndex);
        }

        private static void SetCellValue(ICell cell, object value)
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

            if (value is byte || value is short || value is int || value is long ||
                value is float || value is double || value is decimal)
            {
                cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return;
            }

            cell.SetCellValue(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static int Width(double characters)
        {
            return Math.Min(255 * 256, Math.Max(1, (int)Math.Round(characters * 256)));
        }

        private static long ToLong(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string ToStringValue(object value)
        {
            return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
        }

        private static DateTime ToDateTime(object value)
        {
            if (value is DateTime)
                return (DateTime)value;

            var text = ToStringValue(value);
            DateTime parsed;
            if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(text, out parsed))
                return parsed;

            throw new InvalidOperationException("销售月报存在无法识别的销售时间：" + text);
        }

        private sealed class StyleFactory
        {
            private readonly IWorkbook _workbook;
            private readonly Dictionary<string, ICellStyle> _styles =
                new Dictionary<string, ICellStyle>();
            private readonly Dictionary<string, IFont> _fonts =
                new Dictionary<string, IFont>();
            private readonly IDataFormat _dataFormat;

            public StyleFactory(IWorkbook workbook)
            {
                _workbook = workbook;
                _dataFormat = workbook.CreateDataFormat();
            }

            public ICellStyle Get(
                FillTone fill,
                bool bold,
                short fontSize,
                bool wrap,
                HorizontalAlignment alignment,
                BorderStyle left,
                BorderStyle right,
                BorderStyle top,
                BorderStyle bottom,
                string numberFormat)
            {
                var key =
                    fill + "|" + bold + "|" + fontSize + "|" + wrap + "|" + alignment +
                    "|" + left + "|" + right + "|" + top + "|" + bottom + "|" + numberFormat;

                ICellStyle style;
                if (_styles.TryGetValue(key, out style))
                    return style;

                style = _workbook.CreateCellStyle();
                style.Alignment = alignment;
                style.VerticalAlignment = VerticalAlignment.Center;
                style.WrapText = wrap;
                style.BorderLeft = left;
                style.BorderRight = right;
                style.BorderTop = top;
                style.BorderBottom = bottom;
                style.SetFont(GetFont(bold, fontSize));

                if (fill == FillTone.Yellow)
                {
                    style.FillForegroundColor = IndexedColors.Yellow.Index;
                    style.FillPattern = FillPattern.SolidForeground;
                }
                else if (fill == FillTone.Blue)
                {
                    style.FillForegroundColor = IndexedColors.LightBlue.Index;
                    style.FillPattern = FillPattern.SolidForeground;
                }

                if (!string.IsNullOrWhiteSpace(numberFormat))
                    style.DataFormat = _dataFormat.GetFormat(numberFormat);

                _styles[key] = style;
                return style;
            }

            private IFont GetFont(bool bold, short size)
            {
                var key = bold + "|" + size;
                IFont font;
                if (_fonts.TryGetValue(key, out font))
                    return font;

                font = _workbook.CreateFont();
                font.FontName = "宋体";
                font.FontHeightInPoints = size;
                font.IsBold = bold;
                _fonts[key] = font;
                return font;
            }
        }

        private enum FillTone
        {
            None,
            Yellow,
            Blue
        }

        private sealed class BusinessLine
        {
            public BusinessLine(string group, string label)
            {
                Group = group;
                Label = label;
            }

            public string Group { get; private set; }
            public string Label { get; private set; }
        }

        private sealed class MonthlyOrder
        {
            public long OrderId { get; set; }
            public string OrderNo { get; set; }
            public DateTime SoldAt { get; set; }
            public string PaymentMethod { get; set; }
            public long TotalCent { get; set; }
            public long AmountReceivedCent { get; set; }
            public long ChangeCent { get; set; }
            public string Note { get; set; }

            public MonthlyOrder()
            {
                OrderNo = "";
                PaymentMethod = "";
                Note = "";
            }
        }

        private sealed class MonthlySalesRow
        {
            public long OrderId { get; set; }
            public long ItemId { get; set; }
            public DateTime SoldAt { get; set; }
            public string OrderNo { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public string Publisher { get; set; }
            public string Category { get; set; }
            public string PublicationYear { get; set; }
            public int Quantity { get; set; }
            public long BaseUnitPriceCent { get; set; }
            public int LineDiscountBasisPoints { get; set; }
            public long LineDiscountedUnitPriceCent { get; set; }
            public int OrderDiscountBasisPoints { get; set; }
            public long FinalUnitPriceCent { get; set; }
            public long LineTotalCent { get; set; }
            public long OrderTotalCent { get; set; }
            public string PaymentMethod { get; set; }
            public long AmountReceivedCent { get; set; }
            public long ChangeCent { get; set; }
            public string OrderNote { get; set; }
            public long ListPriceCent { get; set; }
            public long CostRefCent { get; set; }
            public string SupplierName { get; set; }
            public int StockAtSale { get; set; }
            public int ReturnedQuantity { get; set; }

            public MonthlySalesRow()
            {
                OrderNo = "";
                SelfCode = "";
                Isbn = "";
                Title = "";
                Author = "";
                Publisher = "";
                Category = "";
                PublicationYear = "";
                PaymentMethod = "";
                OrderNote = "";
                SupplierName = "";
            }
        }
    }
}
