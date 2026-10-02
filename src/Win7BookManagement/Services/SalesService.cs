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
            return Checkout(lines, note, 10000);
        }

        public string Checkout(
            IList<TransactionLineInput> lines,
            string note,
            int orderDiscountBasisPoints)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("销售单至少需要一项图书。");
            if (orderDiscountBasisPoints < 0 || orderDiscountBasisPoints > 10000)
                throw new InvalidOperationException("整单折扣必须在 0% 到 100% 之间。");

            long subtotalCent = 0;
            long lineDiscountCent = 0;
            long orderDiscountCent = 0;
            long totalCent = 0;

            foreach (var line in lines)
            {
                if (line.Quantity <= 0)
                    throw new InvalidOperationException("销售数量必须大于 0。");

                var baseUnitPriceCent = ResolveBaseUnitPrice(line);
                if (baseUnitPriceCent < 0)
                    throw new InvalidOperationException("售价不能为负数。");
                if (line.DiscountBasisPoints < 0 || line.DiscountBasisPoints > 10000)
                    throw new InvalidOperationException("单品折扣必须在 0% 到 100% 之间。");

                var lineDiscountedUnitPriceCent =
                    ApplyBasisPoints(baseUnitPriceCent, line.DiscountBasisPoints);
                var finalUnitPriceCent =
                    ApplyBasisPoints(lineDiscountedUnitPriceCent, orderDiscountBasisPoints);

                subtotalCent = checked(
                    subtotalCent + checked((long)line.Quantity * baseUnitPriceCent));
                lineDiscountCent = checked(
                    lineDiscountCent +
                    checked((long)line.Quantity * (baseUnitPriceCent - lineDiscountedUnitPriceCent)));
                orderDiscountCent = checked(
                    orderDiscountCent +
                    checked((long)line.Quantity * (lineDiscountedUnitPriceCent - finalUnitPriceCent)));
                totalCent = checked(
                    totalCent + checked((long)line.Quantity * finalUnitPriceCent));
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
INSERT INTO sales_orders
(order_no, sold_at, subtotal_cent, line_discount_cent,
 order_discount_basis_points, order_discount_cent,
 total_cent, note, created_at)
VALUES
(@no, @at, @subtotal, @lineDiscount,
 @orderDiscountBasisPoints, @orderDiscount,
 @total, @note, @at);
SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", orderNo);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@subtotal", subtotalCent);
                        command.Parameters.AddWithValue("@lineDiscount", lineDiscountCent);
                        command.Parameters.AddWithValue("@orderDiscountBasisPoints", orderDiscountBasisPoints);
                        command.Parameters.AddWithValue("@orderDiscount", orderDiscountCent);
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

                        var baseUnitPriceCent = ResolveBaseUnitPrice(line);
                        var lineDiscountedUnitPriceCent =
                            ApplyBasisPoints(baseUnitPriceCent, line.DiscountBasisPoints);
                        var finalUnitPriceCent =
                            ApplyBasisPoints(lineDiscountedUnitPriceCent, orderDiscountBasisPoints);
                        var lineTotal = checked((long)line.Quantity * finalUnitPriceCent);

                        using (var itemCommand = connection.CreateCommand())
                        {
                            itemCommand.Transaction = transaction;
                            itemCommand.CommandText = @"
INSERT INTO sales_order_items
(sales_order_id, book_id, isbn_snapshot, title_snapshot, quantity,
 base_unit_price_cent, line_discount_basis_points, line_discounted_unit_price_cent,
 unit_price_cent, line_total_cent)
VALUES
(@orderId, @bookId, @isbn, @title, @qty,
 @baseUnit, @lineDiscountBasisPoints, @lineDiscountedUnit,
 @unit, @total);";
                            itemCommand.Parameters.AddWithValue("@orderId", orderId);
                            itemCommand.Parameters.AddWithValue("@bookId", line.BookId);
                            itemCommand.Parameters.AddWithValue("@isbn", isbn);
                            itemCommand.Parameters.AddWithValue("@title", title);
                            itemCommand.Parameters.AddWithValue("@qty", line.Quantity);
                            itemCommand.Parameters.AddWithValue("@baseUnit", baseUnitPriceCent);
                            itemCommand.Parameters.AddWithValue("@lineDiscountBasisPoints", line.DiscountBasisPoints);
                            itemCommand.Parameters.AddWithValue("@lineDiscountedUnit", lineDiscountedUnitPriceCent);
                            itemCommand.Parameters.AddWithValue("@unit", finalUnitPriceCent);
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

                        InsertLedger(
                            connection,
                            transaction,
                            line.BookId,
                            isbn,
                            title,
                            -line.Quantity,
                            orderId,
                            orderNo,
                            timestamp,
                            note);
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

        private static long ResolveBaseUnitPrice(TransactionLineInput line)
        {
            if (line == null) return 0;
            if (line.BaseUnitPriceCent > 0 || line.UnitPriceCent == 0)
                return line.BaseUnitPriceCent;
            return line.UnitPriceCent;
        }

        private static long ApplyBasisPoints(long amountCent, int basisPoints)
        {
            if (amountCent <= 0 || basisPoints <= 0)
                return 0;
            if (basisPoints >= 10000)
                return amountCent;

            return decimal.ToInt64(
                decimal.Round(
                    amountCent * (basisPoints / 10000m),
                    0,
                    MidpointRounding.AwayFromZero));
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
