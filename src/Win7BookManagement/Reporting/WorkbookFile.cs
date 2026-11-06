using System;
using System.IO;
using NPOI.SS.UserModel;

namespace Win7BookManagement.Reporting
{
    internal static class WorkbookFile
    {
        public static string ValidateExtension(string path, bool allowCsv)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径不能为空。", "path");
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase) ||
                (allowCsv && string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)))
                return extension;
            throw new InvalidOperationException(allowCsv
                ? "文件扩展名不受支持。请选择 .xlsx、.xls 或 .csv 格式。"
                : "月报只支持 .xlsx 或 .xls，请检查文件扩展名。");
        }

        public static void Publish(IWorkbook workbook, string path)
        {
            var full = Path.GetFullPath(path);
            var temporary = full + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = File.Create(temporary)) workbook.Write(stream);
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
