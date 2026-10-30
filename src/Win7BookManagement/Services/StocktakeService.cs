using System;
using System.Collections.Generic;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Services
{
    public sealed class StocktakeService
    {
        private readonly DatabaseConnectionFactory _factory;
        public StocktakeService(DatabaseConnectionFactory factory) { _factory = factory; }

        public StocktakeDocument Create(string shelfCode, string note)
        {
            var shelf = (shelfCode ?? "").Trim();
            var now = DateTime.Now;
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    long id;
                    var countNo = "SC" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"INSERT INTO stock_counts(count_no,shelf_code,status,note,created_at)
VALUES(@no,@shelf,'draft',@note,@at); SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", countNo); command.Parameters.AddWithValue("@shelf", shelf);
                        command.Parameters.AddWithValue("@note", (note ?? "").Trim()); command.Parameters.AddWithValue("@at", Timestamp(now));
                        id = Convert.ToInt64(command.ExecuteScalar());
                    }
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"INSERT INTO stock_count_items(count_id,book_id,expected_quantity,actual_quantity)
SELECT @id,id,stock_quantity,stock_quantity FROM books WHERE is_active=1 AND (@shelf='' OR shelf_code=@shelf);";
                        command.Parameters.AddWithValue("@id", id); command.Parameters.AddWithValue("@shelf", shelf);
                        if (command.ExecuteNonQuery() == 0) throw new InvalidOperationException("该货架位没有可盘点的启用图书。");
                    }
                    transaction.Commit();
                    return Load(id);
                }
                catch { transaction.Rollback(); throw; }
            }
        }

        public void SaveActual(long countId, IDictionary<long, int> actual)
        {
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    EnsureDraft(connection, transaction, countId);
                    foreach (var pair in actual)
                    {
                        if (pair.Value < 0) throw new InvalidOperationException("实盘数量不能为负数。");
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE stock_count_items SET actual_quantity=@actual WHERE count_id=@id AND book_id=@book;";
                            command.Parameters.AddWithValue("@actual", pair.Value); command.Parameters.AddWithValue("@id", countId); command.Parameters.AddWithValue("@book", pair.Key);
                            if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("盘点单明细已变化，请重新打开。");
                        }
                    }
                    transaction.Commit();
                }
                catch { transaction.Rollback(); throw; }
            }
        }

        public int Post(long countId)
        {
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var document = EnsureDraft(connection, transaction, countId);
                    var adjustments = 0;
                    foreach (var item in document.Items)
                    {
                        if (!item.ActualQuantity.HasValue) throw new InvalidOperationException("还有图书未填写实盘数量。");
                        int current; string isbn; string title;
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "SELECT stock_quantity,isbn,title FROM books WHERE id=@id;";
                            command.Parameters.AddWithValue("@id", item.BookId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("盘点图书不存在。");
                                current = Convert.ToInt32(reader["stock_quantity"]); isbn = Convert.ToString(reader["isbn"]); title = Convert.ToString(reader["title"]);
                            }
                        }
                        if (current != item.ExpectedQuantity)
                            throw new InvalidOperationException("《" + item.Title + "》在盘点期间库存已变化，请重新生成盘点单，避免覆盖正常业务。");
                        var delta = item.ActualQuantity.Value - current;
                        if (delta == 0) continue;
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "UPDATE books SET stock_quantity=@actual,updated_at=@now WHERE id=@id;";
                            command.Parameters.AddWithValue("@actual", item.ActualQuantity.Value); command.Parameters.AddWithValue("@now", Timestamp(DateTime.Now)); command.Parameters.AddWithValue("@id", item.BookId);
                            command.ExecuteNonQuery();
                        }
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"INSERT INTO inventory_transactions
(book_id,isbn_snapshot,title_snapshot,type,quantity,reference_type,reference_id,reference_no,occurred_at,note)
VALUES(@book,@isbn,@title,'ADJUSTMENT',@delta,'STOCKTAKE',@id,@no,@at,@note);";
                            command.Parameters.AddWithValue("@book", item.BookId); command.Parameters.AddWithValue("@isbn", isbn); command.Parameters.AddWithValue("@title", title);
                            command.Parameters.AddWithValue("@delta", delta); command.Parameters.AddWithValue("@id", countId); command.Parameters.AddWithValue("@no", document.CountNo);
                            command.Parameters.AddWithValue("@at", Timestamp(DateTime.Now));
                            command.Parameters.AddWithValue("@note", "盘点单 " + document.CountNo + "：计划 " + item.ExpectedQuantity + "，实盘 " + item.ActualQuantity.Value + "。" + document.Note);
                            command.ExecuteNonQuery();
                        }
                        adjustments++;
                    }
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "UPDATE stock_counts SET status='posted',posted_at=@at WHERE id=@id;";
                        command.Parameters.AddWithValue("@at", Timestamp(DateTime.Now)); command.Parameters.AddWithValue("@id", countId);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                    return adjustments;
                }
                catch { transaction.Rollback(); throw; }
            }
        }

        public StocktakeDocument Load(long id)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id,count_no,shelf_code,status,note,created_at FROM stock_counts WHERE id=@id;";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("盘点单不存在。");
                    var document = new StocktakeDocument { Id = reader.GetInt64(0), CountNo = reader.GetString(1), ShelfCode = reader.GetString(2), Status = reader.GetString(3), Note = reader.GetString(4), CreatedAt = DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture) };
                    reader.Close();
                    using (var items = connection.CreateCommand())
                    {
                        items.CommandText = @"SELECT i.book_id,b.self_code,b.title,b.shelf_code,i.expected_quantity,i.actual_quantity
FROM stock_count_items i JOIN books b ON b.id=i.book_id WHERE i.count_id=@id ORDER BY b.shelf_code,b.title;";
                        items.Parameters.AddWithValue("@id", id);
                        using (var r = items.ExecuteReader())
                            while (r.Read()) document.Items.Add(new StocktakeItem { BookId = r.GetInt64(0), SelfCode = r.GetString(1), Title = r.GetString(2), ShelfCode = r.GetString(3), ExpectedQuantity = r.GetInt32(4), ActualQuantity = r.IsDBNull(5) ? (int?)null : r.GetInt32(5) });
                    }
                    return document;
                }
            }
        }

        private StocktakeDocument EnsureDraft(System.Data.SQLite.SQLiteConnection connection, System.Data.SQLite.SQLiteTransaction transaction, long id)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction; command.CommandText = "SELECT status FROM stock_counts WHERE id=@id;";
                command.Parameters.AddWithValue("@id", id);
                var status = Convert.ToString(command.ExecuteScalar());
                if (status != "draft") throw new InvalidOperationException("盘点单不存在或已过账，不能重复调整库存。");
            }
            return Load(id);
        }
        private static string Timestamp(DateTime value) { return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }
    }
}
