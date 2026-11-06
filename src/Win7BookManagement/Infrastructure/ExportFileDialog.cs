using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>Keep the selected export type and the actual filename consistent.</summary>
    public static class ExportFileDialog
    {
        public static SaveFileDialog Create(string suggestedName, params string[] formats)
        {
            if (formats == null || formats.Length == 0)
                throw new ArgumentException("至少需要一种导出格式。", "formats");

            var extensions = new List<string>();
            var filters = new List<string>();
            foreach (var format in formats)
            {
                var extension = (format ?? "").ToLowerInvariant();
                string title;
                switch (extension)
                {
                    case ".xlsx": title = "Excel 工作簿 (*.xlsx)"; break;
                    case ".xls": title = "旧版 Excel (*.xls)"; break;
                    case ".csv": title = "CSV 文本 (*.csv)"; break;
                    default: throw new ArgumentException("不支持的导出格式。", "formats");
                }
                if (extensions.Contains(extension)) throw new ArgumentException("导出格式重复。", "formats");
                extensions.Add(extension);
                filters.Add(title);
                filters.Add("*" + extension);
            }

            var dialog = new SaveFileDialog
            {
                Filter = string.Join("|", filters.ToArray()),
                // Do not force xlsx: WinForms chooses the selected filter's
                // extension when the user has not entered one.
                DefaultExt = "",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = suggestedName
            };
            dialog.FileOk += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (MatchesSelection(dialog.FileName, dialog.FilterIndex, extensions.ToArray())) return;
                e.Cancel = true;
                var expected = dialog.FilterIndex > 0 && dialog.FilterIndex <= extensions.Count
                    ? extensions[dialog.FilterIndex - 1] : "所选格式对应的扩展名";
                MessageBox.Show("文件名扩展名与选择的保存类型不一致。请选择对应的保存类型，或将文件名改为以 " + expected + " 结尾。",
                    "请核对导出格式", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            return dialog;
        }

        public static bool MatchesSelection(string path, int filterIndex, params string[] formats)
        {
            if (string.IsNullOrWhiteSpace(path) || formats == null || filterIndex < 1 || filterIndex > formats.Length)
                return false;
            return string.Equals(Path.GetExtension(path), formats[filterIndex - 1], StringComparison.OrdinalIgnoreCase);
        }
    }
}
