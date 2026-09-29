using System;
using System.Globalization;
using Win7BookManagement.Database;

namespace Win7BookManagement.Services
{
    public sealed class InventoryService
    {
        private readonly DatabaseConnectionFactory _factory;

        public InventoryService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string Adjust(long bookId, int delta, string note)
        {
            if (delta == 0)
                throw new InvalidOperationException("调整数量不能为 0。");

            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var referenceNo = "A" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string isbn;
                    string title;
                    int stock;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT isbn, title, stock_quantity FROM books WHERE id=@id;";
                        command.Parameters.AddWithValue("@id", bookId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("图书不存在。");
                            isbn = Convert.ToString(reader["isbn"]);
                            title = Convert.ToString(reader["title"]);
                            stock = Convert.ToInt32(reader["stock_quantity"]);
                        }
                    }

                    if ((long)stock + delta < 0)
                        throw new InvalidOperationException("调整后库存不能小于 0。");

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText =
                            "UPDATE books SET stock_quantity=stock_quantity+@delta, updated_at=@at WHERE id=@id;";
                        command.Parameters.AddWithValue("@delta", delta);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@id", bookId);
                        command.ExecuteNonQuery();
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO inventory_transactions
(book_id, isbn_snapshot, title_snapshot, type, quantity, reference_type,
 reference_id, reference_no, occurred_at, note)
VALUES(@bookId, @isbn, @title, 'ADJUSTMENT', @qty, 'ADJUSTMENT', NULL, @refNo, @at, @note);";
                        command.Parameters.AddWithValue("@bookId", bookId);
                        command.Parameters.AddWithValue("@isbn", isbn);
                        command.Parameters.AddWithValue("@title", title);
                        command.Parameters.AddWithValue("@qty", delta);
                        command.Parameters.AddWithValue("@refNo", referenceNo);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return referenceNo;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
