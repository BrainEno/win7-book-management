using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NPOI.SS.UserModel;
using Win7BookManagement.Database;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Repositories;

namespace Win7BookManagement.Services
{
    public sealed class BookImportRow
    {
        public int RowNumber { get; set; }
        public Book Book { get; set; }
        public bool IsUpdate { get; set; }
        public string Error { get; set; }
        public string[] Raw { get; set; }
        public string Status { get { return Error != null ? "错误" : IsUpdate ? "更新" : "新增"; } }
    }

    public sealed class BookImportService
    {
        public static readonly string[] Headers = { "店内编码", "ISBN", "书名", "作者", "出版社", "分类", "出版年", "版次", "装帧", "货架位", "售价", "默认进价", "备注" };
        private readonly DatabaseConnectionFactory _factory;
        private readonly BookRepository _books;

        public BookImportService(DatabaseConnectionFactory factory, BookRepository books)
        { _factory = factory; _books = books; }

        public static DataTable CreateTemplate()
        {
            var table = new DataTable("图书导入模板");
            foreach (var header in Headers) table.Columns.Add(header, typeof(string));
            table.Rows.Add("", "9787111213826", "示例图书（导入前请删除本行）", "作者", "出版社", "文学", "2020", "1", "平装", "A-01", "39.80", "20.00", "库存不从模板导入");
            return table;
        }

        public IList<BookImportRow> Preview(string path)
        {
            var rows = ReadRows(path);
            var existing = _books.Search("", true);
            var byCode = existing.Where(b => !string.IsNullOrEmpty(b.SelfCode)).ToDictionary(b => b.SelfCode, b => b, StringComparer.OrdinalIgnoreCase);
            var byIsbn = existing.Where(b => !string.IsNullOrEmpty(b.Isbn)).ToDictionary(b => b.Isbn, b => b);
            var seenCode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIsbn = new HashSet<string>();
            var result = new List<BookImportRow>();
            foreach (var cells in rows)
            {
                var row = new BookImportRow { RowNumber = cells.Item1, Book = new Book(), Raw = cells.Item2 };
                result.Add(row);
                try
                {
                    var b = row.Book;
                    b.SelfCode = Get(cells.Item2, 0); b.Isbn = Isbn.Normalize(Get(cells.Item2, 1));
                    b.Title = Get(cells.Item2, 2); b.Author = Get(cells.Item2, 3); b.Publisher = Get(cells.Item2, 4);
                    b.Category = Get(cells.Item2, 5); b.PublicationYear = Get(cells.Item2, 6); b.Edition = Get(cells.Item2, 7);
                    b.Binding = Get(cells.Item2, 8); b.ShelfCode = Get(cells.Item2, 9);
                    b.SalePriceCent = ParsePrice(Get(cells.Item2, 10), "售价"); b.ListPriceCent = b.SalePriceCent;
                    b.DefaultPurchasePriceCent = ParsePrice(Get(cells.Item2, 11), "默认进价"); b.Note = Get(cells.Item2, 12);
                    if (string.IsNullOrWhiteSpace(b.Title)) throw new InvalidOperationException("书名不能为空。");
                    Book match = null;
                    if (b.SelfCode.Length > 0 && byCode.ContainsKey(b.SelfCode)) match = byCode[b.SelfCode];
                    if (b.Isbn.Length > 0 && byIsbn.ContainsKey(b.Isbn))
                    {
                        if (match != null && match.Id != byIsbn[b.Isbn].Id) throw new InvalidOperationException("店内编码与 ISBN 指向两本不同图书。");
                        match = byIsbn[b.Isbn];
                    }
                    if (b.SelfCode.Length > 0 && !seenCode.Add(b.SelfCode)) throw new InvalidOperationException("文件内店内编码重复。");
                    if (b.Isbn.Length > 0 && !seenIsbn.Add(b.Isbn)) throw new InvalidOperationException("文件内 ISBN 重复。");
                    if (match != null)
                    {
                        row.IsUpdate = true; b.Id = match.Id; b.StockQuantity = match.StockQuantity; b.IsActive = match.IsActive;
                        if (string.IsNullOrEmpty(b.SelfCode)) b.SelfCode = match.SelfCode;
                    }
                }
                catch (Exception ex) { row.Error = ex.Message; }
            }
            if (result.Count == 0) throw new InvalidOperationException("文件中没有可导入的数据行。");
            return result;
        }

        public int Commit(string path)
        {
            var rows = Preview(path);
            var errors = rows.Where(r => r.Error != null).ToList();
            if (errors.Count > 0)
                throw new InvalidOperationException("有 " + errors.Count + " 行错误，未导入任何数据。第一处：第 " + errors[0].RowNumber + " 行，" + errors[0].Error + " 请导出错误行修正后重试。");
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var row in rows)
                    {
                        var b = row.Book;
                        if (!row.IsUpdate && string.IsNullOrEmpty(b.SelfCode)) b.SelfCode = NextCode(connection, transaction);
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = row.IsUpdate ? @"UPDATE books SET self_code=@code,isbn=@isbn,title=@title,author=@author,
publisher=@publisher,category=@category,publication_year=@year,edition=@edition,binding=@binding,shelf_code=@shelf,
note=@note,list_price_cent=@price,default_purchase_price_cent=@cost,sale_price_cent=@price,updated_at=@now WHERE id=@id;" :
@"INSERT INTO books(self_code,isbn,title,author,publisher,category,publication_year,edition,binding,shelf_code,note,
list_price_cent,default_purchase_price_cent,sale_price_cent,stock_quantity,is_active,created_at,updated_at)
VALUES(@code,@isbn,@title,@author,@publisher,@category,@year,@edition,@binding,@shelf,@note,@price,@cost,@price,0,1,@now,@now);";
                            command.Parameters.AddWithValue("@id", b.Id);
                            command.Parameters.AddWithValue("@code", b.SelfCode); command.Parameters.AddWithValue("@isbn", b.Isbn);
                            command.Parameters.AddWithValue("@title", b.Title); command.Parameters.AddWithValue("@author", b.Author);
                            command.Parameters.AddWithValue("@publisher", b.Publisher); command.Parameters.AddWithValue("@category", b.Category);
                            command.Parameters.AddWithValue("@year", b.PublicationYear); command.Parameters.AddWithValue("@edition", b.Edition);
                            command.Parameters.AddWithValue("@binding", b.Binding); command.Parameters.AddWithValue("@shelf", b.ShelfCode);
                            command.Parameters.AddWithValue("@note", b.Note); command.Parameters.AddWithValue("@price", b.SalePriceCent);
                            command.Parameters.AddWithValue("@cost", b.DefaultPurchasePriceCent);
                            command.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("第 " + row.RowNumber + " 行写入失败。");
                        }
                        if (b.Category.Length > 0)
                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = @"INSERT OR IGNORE INTO dictionary_values(dictionary_key,value,sort_order,note,is_active,created_at,updated_at)
VALUES('book_category',@value,0,'批量导入',1,@now,@now);";
                                command.Parameters.AddWithValue("@value", b.Category);
                                command.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                command.ExecuteNonQuery();
                            }
                    }
                    transaction.Commit();
                    return rows.Count;
                }
                catch { transaction.Rollback(); throw; }
            }
        }

        private static string NextCode(System.Data.SQLite.SQLiteConnection connection, System.Data.SQLite.SQLiteTransaction transaction)
        {
            long next;
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction; command.CommandText = "SELECT COALESCE(MAX(id),0)+1 FROM books;";
                next = Convert.ToInt64(command.ExecuteScalar());
            }
            while (true)
            {
                var candidate = "BK-" + next.ToString("D6");
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction; command.CommandText = "SELECT COUNT(*) FROM books WHERE self_code=@code;";
                    command.Parameters.AddWithValue("@code", candidate);
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0) return candidate;
                }
                next++;
            }
        }

        private static string Get(string[] cells, int index) { return index < cells.Length ? (cells[index] ?? "").Trim() : ""; }
        private static long ParsePrice(string text, string label)
        {
            if (text.Length == 0) return 0;
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) &&
                !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
                throw new InvalidOperationException(label + "必须是金额，例如 39.80。");
            if (value < 0) throw new InvalidOperationException(label + "不能为负数。");
            return Money.FromYuan(value);
        }

        private static IList<Tuple<int, string[]>> ReadRows(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("请选择导入文件。", path);
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var result = new List<Tuple<int, string[]>>();
            if (extension == ".csv")
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                var headerSeen = false;
                foreach (var record in ParseCsv(text))
                {
                    if (!headerSeen) { ValidateHeader(record); headerSeen = true; continue; }
                    if (record.All(string.IsNullOrWhiteSpace)) continue;
                    result.Add(Tuple.Create(result.Count + 2, record));
                }
            }
            else if (extension == ".xlsx" || extension == ".xls")
            {
                using (var stream = File.OpenRead(path))
                {
                    var workbook = WorkbookFactory.Create(stream);
                    try
                    {
                        var sheet = workbook.GetSheetAt(0); var formatter = new DataFormatter(CultureInfo.InvariantCulture);
                        var header = sheet.GetRow(sheet.FirstRowNum);
                        if (header == null) throw new InvalidOperationException("模板缺少表头。");
                        ValidateHeader(Enumerable.Range(0, Headers.Length).Select(i => formatter.FormatCellValue(header.GetCell(i))).ToArray());
                        for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
                        {
                            var row = sheet.GetRow(r); if (row == null) continue;
                            var cells = Enumerable.Range(0, Headers.Length).Select(i => formatter.FormatCellValue(row.GetCell(i)).Trim()).ToArray();
                            if (cells.All(string.IsNullOrWhiteSpace)) continue;
                            result.Add(Tuple.Create(r + 1, cells));
                        }
                    }
                    finally { workbook.Close(); }
                }
            }
            else throw new InvalidOperationException("只支持 .xlsx、.xls 或 UTF-8 .csv 模板。");
            if (result.Count > 10000) throw new InvalidOperationException("单次最多导入 10000 行，请拆分文件。");
            return result;
        }

        private static void ValidateHeader(string[] cells)
        {
            if (cells.Length < Headers.Length || !Headers.SequenceEqual(cells.Take(Headers.Length).Select(c => (c ?? "").Trim().TrimStart('\uFEFF'))))
                throw new InvalidOperationException("表头与模板不一致，请下载模板后填写。必需列：" + string.Join("、", Headers) + "；实际列：" + string.Join("|", cells));
        }

        public static IEnumerable<string[]> ParseCsv(string text)
        {
            var fields = new List<string>(); var field = new StringBuilder(); var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else field.Append(ch);
                }
                else if (ch == '"' && field.Length == 0) quoted = true;
                else if (ch == ',') { fields.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    fields.Add(field.ToString()); field.Clear(); yield return fields.ToArray(); fields.Clear();
                }
                else field.Append(ch);
            }
            if (quoted) throw new InvalidOperationException("CSV 引号未闭合。");
            if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); yield return fields.ToArray(); }
        }

        public static string ToCsv(IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            foreach (var row in rows)
                builder.AppendLine(string.Join(",", row.Select(EscapeCsv).ToArray()));
            return builder.ToString();
        }
        private static string EscapeCsv(string value)
        {
            var text = value ?? "";
            if (text.StartsWith("=") || text.StartsWith("+") || text.StartsWith("-") || text.StartsWith("@")) text = "\t" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
