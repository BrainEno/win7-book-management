using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Win7BookManagement.Reporting
{
    public static class CsvReportExporter
    {
        public static void Export(DataTable table, string path, IProgress<int> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (table == null) throw new ArgumentNullException("table");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径不能为空。", "path");
            var full = Path.GetFullPath(path);
            var temporary = full + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Stream instead of retaining the entire CSV in memory on 2 GB tills.
                using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(true)))
                {
                    writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName)).ToArray()));
                    for (var i = 0; i < table.Rows.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteLine(string.Join(",", table.Rows[i].ItemArray.Select(v => Escape(Convert.ToString(v, CultureInfo.InvariantCulture))).ToArray()));
                        if (progress != null && (i % 100 == 0 || i == table.Rows.Count - 1))
                            progress.Report((int)((long)(i + 1) * 90 / Math.Max(1, table.Rows.Count)));
                    }
                }
                // Do not replace an existing export after a cancellation request.
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
                if (progress != null) progress.Report(100);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static string Escape(string value)
        {
            var text = value ?? "";
            if (text.StartsWith("=") || text.StartsWith("+") || text.StartsWith("-") || text.StartsWith("@")) text = "\t" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
