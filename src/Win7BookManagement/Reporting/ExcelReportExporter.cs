using System;
using System.Data;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Win7BookManagement.Reporting
{
    public sealed class ExcelReportExporter
    {
        public void Export(DataTable table, string path, string sheetName)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径不能为空。", "path");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            IWorkbook workbook = new XSSFWorkbook();
            try
            {
                var sheet = workbook.CreateSheet(SanitizeSheetName(sheetName));

                var headerStyle = workbook.CreateCellStyle();
                var headerFont = workbook.CreateFont();
                headerFont.IsBold = true;
                headerStyle.SetFont(headerFont);

                var header = sheet.CreateRow(0);
                for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    var cell = header.CreateCell(columnIndex);
                    cell.SetCellValue(table.Columns[columnIndex].ColumnName);
                    cell.CellStyle = headerStyle;
                }

                for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var source = table.Rows[rowIndex];
                    var row = sheet.CreateRow(rowIndex + 1);
                    for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                    {
                        var value = source[columnIndex];
                        var cell = row.CreateCell(columnIndex);
                        WriteValue(cell, value);
                    }
                }

                for (var i = 0; i < table.Columns.Count; i++)
                {
                    sheet.AutoSizeColumn(i);
                    var width = sheet.GetColumnWidth(i);
                    if (width > 12000) sheet.SetColumnWidth(i, 12000);
                }

                using (var stream = File.Create(path))
                    workbook.Write(stream);
            }
            finally
            {
                workbook.Close();
            }
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
                cell.SetCellValue(Convert.ToDouble(value));
                return;
            }

            if (value is DateTime)
            {
                cell.SetCellValue(((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss"));
                return;
            }

            cell.SetCellValue(Convert.ToString(value));
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
