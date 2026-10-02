using System;
using System.Data;
using System.Globalization;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Win7BookManagement.Reporting
{
    public sealed class ExcelReportExporter
    {
        public void Export(DataTable table, string path, string sheetName)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("导出路径不能为空。", "path");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            IWorkbook workbook = new XSSFWorkbook();
            try
            {
                var sheet = workbook.CreateSheet(SanitizeSheetName(sheetName));
                var titleStyle = CreateStyle(
                    workbook, true, 12, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Center, null, false);
                var headerStyle = CreateStyle(
                    workbook, true, 9, IndexedColors.Yellow.Index,
                    BorderStyle.Medium, BorderStyle.Medium,
                    BorderStyle.Medium, BorderStyle.Medium,
                    HorizontalAlignment.Center, null, true);
                var textStyle = CreateStyle(
                    workbook, false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Center, null, false);
                var leftTextStyle = CreateStyle(
                    workbook, false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Left, null, true);
                var moneyStyle = CreateStyle(
                    workbook, false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Right, "0.00", false);
                var integerStyle = CreateStyle(
                    workbook, false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Right, "0", false);
                var dateStyle = CreateStyle(
                    workbook, false, 9, IndexedColors.White.Index,
                    BorderStyle.Thin, BorderStyle.Thin,
                    BorderStyle.Thin, BorderStyle.Thin,
                    HorizontalAlignment.Center, "yyyy-mm-dd hh:mm:ss", false);

                var title = sheet.CreateRow(0);
                title.HeightInPoints = 28F;
                for (var columnIndex = 0; columnIndex < Math.Max(1, table.Columns.Count); columnIndex++)
                {
                    var cell = title.CreateCell(columnIndex);
                    cell.CellStyle = titleStyle;
                    if (columnIndex == 0)
                        cell.SetCellValue(string.IsNullOrWhiteSpace(sheetName) ? "报表" : sheetName.Trim());
                }
                if (table.Columns.Count > 1)
                    sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, table.Columns.Count - 1));

                var header = sheet.CreateRow(1);
                header.HeightInPoints = 30F;
                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    var cell = header.CreateCell(columnIndex);
                    cell.SetCellValue(table.Columns[columnIndex].ColumnName);
                    cell.CellStyle = headerStyle;
                    sheet.SetColumnWidth(
                        columnIndex,
                        WidthForColumn(table.Columns[columnIndex].ColumnName));
                }

                for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var source = table.Rows[rowIndex];
                    var row = sheet.CreateRow(rowIndex + 2);
                    row.HeightInPoints = 21F;

                    for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                    {
                        var columnName = table.Columns[columnIndex].ColumnName;
                        var value = source[columnIndex];
                        var cell = row.CreateCell(columnIndex);

                        if (IsDateColumn(columnName) && TryWriteDate(cell, value))
                        {
                            cell.CellStyle = dateStyle;
                        }
                        else if (IsMoneyColumn(columnName))
                        {
                            WriteValue(cell, value);
                            cell.CellStyle = moneyStyle;
                        }
                        else if (IsIntegerColumn(columnName))
                        {
                            WriteValue(cell, value);
                            cell.CellStyle = integerStyle;
                        }
                        else
                        {
                            WriteValue(cell, value);
                            cell.CellStyle = IsLongTextColumn(columnName)
                                ? leftTextStyle
                                : textStyle;
                        }
                    }
                }

                if (table.Rows.Count > 0)
                    ApplyBottomBorder(workbook, sheet, table.Rows.Count + 1, table.Columns.Count);

                sheet.CreateFreezePane(0, 2);
                if (table.Columns.Count > 0)
                {
                    sheet.SetAutoFilter(
                        new CellRangeAddress(
                            1,
                            Math.Max(1, table.Rows.Count + 1),
                            0,
                            table.Columns.Count - 1));
                }

                sheet.FitToPage = true;
                sheet.PrintSetup.Landscape = table.Columns.Count >= 7;
                sheet.PrintSetup.FitWidth = 1;
                sheet.PrintSetup.FitHeight = 0;

                using (var stream = File.Create(path))
                    workbook.Write(stream);
            }
            finally
            {
                workbook.Close();
            }
        }

        private static ICellStyle CreateStyle(
            IWorkbook workbook,
            bool bold,
            short fontSize,
            short fillColor,
            BorderStyle left,
            BorderStyle right,
            BorderStyle top,
            BorderStyle bottom,
            HorizontalAlignment alignment,
            string numberFormat,
            bool wrap)
        {
            var style = workbook.CreateCellStyle();
            style.Alignment = alignment;
            style.VerticalAlignment = VerticalAlignment.Center;
            style.WrapText = wrap;
            style.BorderLeft = left;
            style.BorderRight = right;
            style.BorderTop = top;
            style.BorderBottom = bottom;

            var font = workbook.CreateFont();
            font.FontName = "宋体";
            font.FontHeightInPoints = fontSize;
            font.IsBold = bold;
            style.SetFont(font);

            if (fillColor != IndexedColors.White.Index)
            {
                style.FillForegroundColor = fillColor;
                style.FillPattern = FillPattern.SolidForeground;
            }

            if (!string.IsNullOrWhiteSpace(numberFormat))
                style.DataFormat = workbook.CreateDataFormat().GetFormat(numberFormat);

            return style;
        }

        private static void ApplyBottomBorder(
            IWorkbook workbook,
            ISheet sheet,
            int rowIndex,
            int columnCount)
        {
            var row = sheet.GetRow(rowIndex);
            if (row == null) return;

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = row.GetCell(columnIndex);
                if (cell == null) continue;

                var clone = workbook.CreateCellStyle();
                clone.CloneStyleFrom(cell.CellStyle);
                clone.BorderBottom = BorderStyle.Medium;
                cell.CellStyle = clone;
            }
        }

        private static bool TryWriteDate(ICell cell, object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            if (value is DateTime)
            {
                cell.SetCellValue((DateTime)value);
                return true;
            }

            DateTime parsed;
            var text = Convert.ToString(value);
            if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed) ||
                DateTime.TryParse(text, out parsed))
            {
                cell.SetCellValue(parsed);
                return true;
            }

            cell.SetCellValue(text);
            return false;
        }

        private static void WriteValue(ICell cell, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cell.SetCellValue("");
                return;
            }

            if (value is byte || value is short || value is int || value is long ||
                value is float || value is double || value is decimal)
            {
                cell.SetCellValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is DateTime)
            {
                cell.SetCellValue((DateTime)value);
                return;
            }

            cell.SetCellValue(Convert.ToString(value));
        }

        private static int WidthForColumn(string name)
        {
            double width;
            if (name == "日期") width = 20;
            else if (name.Contains("单号")) width = 22;
            else if (name == "ISBN") width = 18;
            else if (name == "店内编码") width = 16;
            else if (name == "书名") width = 34;
            else if (name == "作者") width = 18;
            else if (name == "出版社") width = 20;
            else if (name == "分类") width = 14;
            else if (name == "供应商") width = 18;
            else if (name.Contains("方式")) width = 12;
            else if (name.Contains("备注")) width = 30;
            else if (name.Contains("数量") || name == "已退" || name == "可退") width = 11;
            else if (IsMoneyColumn(name)) width = 14;
            else width = 15;

            return Math.Min(255 * 256, Math.Max(1, (int)Math.Round(width * 256)));
        }

        private static bool IsMoneyColumn(string name)
        {
            return name.Contains("金额") ||
                   name.Contains("单价") ||
                   name.Contains("进价") ||
                   name.Contains("成本") ||
                   name.Contains("售价") ||
                   name.Contains("退款");
        }

        private static bool IsIntegerColumn(string name)
        {
            return name.Contains("数量") ||
                   name == "库存" ||
                   name == "库存数量" ||
                   name == "数量变化";
        }

        private static bool IsDateColumn(string name)
        {
            return name == "日期" || name.Contains("时间");
        }

        private static bool IsLongTextColumn(string name)
        {
            return name == "书名" ||
                   name == "备注" ||
                   name.Contains("供应商") ||
                   name == "作者" ||
                   name == "出版社";
        }

        private static string SanitizeSheetName(string name)
        {
            var value = string.IsNullOrWhiteSpace(name) ? "报表" : name.Trim();
            foreach (var invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                value = value.Replace(invalid, '_');
            if (value.Length > 31) value = value.Substring(0, 31);
            return value;
        }
    }
}
