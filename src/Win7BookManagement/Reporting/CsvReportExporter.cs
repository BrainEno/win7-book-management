using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Win7BookManagement.Reporting
{
    public static class CsvReportExporter
    {
        public static void Export(DataTable table, string path)
        {
            if (table == null) throw new ArgumentNullException("table");
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName)).ToArray()));
            foreach (DataRow row in table.Rows)
                builder.AppendLine(string.Join(",", row.ItemArray.Select(v => Escape(Convert.ToString(v, CultureInfo.InvariantCulture))).ToArray()));
            var full = Path.GetFullPath(path); var temporary = full + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, builder.ToString(), new UTF8Encoding(true));
                if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
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
