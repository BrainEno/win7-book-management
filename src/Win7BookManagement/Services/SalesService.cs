using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Services
{
    public sealed class SalesService
    {
        private readonly DatabaseConnectionFactory _factory;

        public SalesService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string Checkout(IList<TransactionLineInput> lines, string note)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("销售单至少需要一项图书。");

            long totalCent = 0;
            foreach (var line in lines)
            {
                if (line.Quantity <= 0) throw new InvalidOperationException("销售数量必须大于 0。");
                if (line.UnitPriceCent < 0) throw new InvalidOperationException("售价不能为负数。");
                totalCent = checked(totalCent + checked((long)line.Quantity * line.UnitPriceCent));
            }

            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var orderNo = "S" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    long orderId;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO sales_orders(order_no, sold_at, total_cent, note, created_at)
VALUES(@no, @at, @total, @note, @at);
SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", orderNo);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@total", totalCent);
                        command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                        orderId = Convert.ToInt64(command.ExecuteScalar());
                    }

                    foreach (var line in lines)
                    {
                        string isbn;
                        string title;
                        int stock;
                        using (var bookCommand = connection.CreateCommand())
                        {
                            bookCommand.Transaction = transaction;
                            bookCommand.CommandText =
                                "SELECT isbn, title, stock_quantity FROM books WHERE id=@id AND is_active=1;";
                            bookCommand.Parameters.AddWithValue("@id", line.BookId);
                            using (var reader = bookCommand.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("销售图书不存在或已停用。");
                                isbn = Convert.ToString(reader["isbn"]);
                                title = Convert.ToString(reader["title"]);
                                stock = Convert.ToInt32(reader["stock_quantity"]);
                            }
                        }

                        if (stock < line.Quantity)
                            throw new InvalidOperationException(title + " 库存不足，当前库存：" + stock + "。");

                        var lineTotal = checked((long)line.Quantity * line.UnitPriceCent);
                        using (var itemCommand = connection.CreateCommand())
                        {
                            itemCommand.Transaction = transaction;
                            itemCommand.CommandText = @"
INSERT INTO sales_order_items
(sales_order_id, book_id, isbn_snapshot, title_snapshot, quantity, unit_price_cent, line_total_cent)
VALUES(@orderId, @bookId, @isbn, @title, @qty, @unit, @total);";
                            itemCommand.Parameters.AddWithValue("@orderId", orderId);
                            itemCommand.Parameters.AddWithValue("@bookId", line.BookId);
                            itemCommand.Parameters.AddWithValue("@isbn", isbn);
                            itemCommand.Parameters.AddWithValue("@title", title);
                            itemCommand.Parameters.AddWithValue("@qty", line.Quantity);
                            itemCommand.Parameters.AddWithValue("@unit", line.UnitPriceCent);
                            itemCommand.Parameters.AddWithValue("@total", lineTotal);
                            itemCommand.ExecuteNonQuery();
                        }

                        using (var stockCommand = connection.CreateCommand())
                        {
                            stockCommand.Transaction = transaction;
                            stockCommand.CommandText = @"
UPDATE books
SET stock_quantity=stock_quantity-@qty, updated_at=@at
WHERE id=@id AND stock_quantity>=@qty;";
                            stockCommand.Parameters.AddWithValue("@qty", line.Quantity);
                            stockCommand.Parameters.AddWithValue("@at", timestamp);
                            stockCommand.Parameters.AddWithValue("@id", line.BookId);
                            if (stockCommand.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(title + " 库存不足，销售已取消。");
                        }

                        InsertLedger(connection, transaction, line.BookId, isbn, title, -line.Quantity,
                            orderId, orderNo, timestamp, note);
                    }

                    transaction.Commit();
                    return orderNo;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private static void InsertLedger(
            SQLiteConnection connection, SQLiteTransaction transaction, long bookId,
            string isbn, string title, int quantity, long referenceId,
            string referenceNo, string timestamp, string note)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO inventory_transactions
(book_id, isbn_snapshot, title_snapshot, type, quantity, reference_type,
 reference_id, reference_no, occurred_at, note)
VALUES(@bookId, @isbn, @title, 'SALE', @qty, 'SALE', @refId, @refNo, @at, @note);";
                command.Parameters.AddWithValue("@bookId", bookId);
                command.Parameters.AddWithValue("@isbn", isbn);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@qty", quantity);
                command.Parameters.AddWithValue("@refId", referenceId);
                command.Parameters.AddWithValue("@refNo", referenceNo);
                command.Parameters.AddWithValue("@at", timestamp);
                command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                command.ExecuteNonQuery();
            }
        }
    }
}
