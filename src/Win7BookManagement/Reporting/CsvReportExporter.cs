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
                    writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => EscapeText(c.ColumnName)).ToArray()));
                    for (var i = 0; i < table.Rows.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteLine(string.Join(",", table.Rows[i].ItemArray.Select(EscapeValue).ToArray()));
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
        private static string EscapeValue(object value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            // Only actual numeric CLR values bypass formula-prefix protection.
            // A string such as "-1+cmd" is untrusted text, not a number.
            return IsNumeric(value) ? Quote(text) : EscapeText(text);
        }

        private static bool IsNumeric(object value)
        {
            if (value == null || value == DBNull.Value || value.GetType().IsEnum) return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte: case TypeCode.SByte:
                case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Single:
                    var single = (float)value;
                    return !float.IsNaN(single) && !float.IsInfinity(single);
                case TypeCode.Double:
                    var number = (double)value;
                    return !double.IsNaN(number) && !double.IsInfinity(number);
                case TypeCode.Decimal: return true;
                default: return false;
            }
        }

        internal static string EscapeText(string value)
        {
            var text = value ?? "";
            var firstNonWhitespace = 0;
            while (firstNonWhitespace < text.Length && char.IsWhiteSpace(text[firstNonWhitespace]))
                firstNonWhitespace++;
            var leadingControl = text.Length > 0 && (text[0] == '\t' || text[0] == '\r' || text[0] == '\n');
            var formula = firstNonWhitespace < text.Length &&
                (text[firstNonWhitespace] == '=' || text[firstNonWhitespace] == '+' ||
                 text[firstNonWhitespace] == '-' || text[firstNonWhitespace] == '@');
            if (leadingControl || formula) text = "\t" + text;
            return Quote(text);
        }

        private static string Quote(string text)
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
