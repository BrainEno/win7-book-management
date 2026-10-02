using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Services
{
    public sealed class ReturnService
    {
        private readonly DatabaseConnectionFactory _factory;

        public ReturnService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string CreateSalesReturn(long sourceSalesOrderId, IList<ReturnLineInput> lines, string note)
        {
            return CreateSalesReturn(sourceSalesOrderId, lines, note, null);
        }

        public string CreateSalesReturn(
            long sourceSalesOrderId,
            IList<ReturnLineInput> lines,
            string note,
            string refundMethod)
        {
            ValidateLines(lines);

            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var returnNo = "SR" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string sourceOrderNo;
                    string sourcePaymentMethod;
                    using (var sourceCommand = connection.CreateCommand())
                    {
                        sourceCommand.Transaction = transaction;
                        sourceCommand.CommandText =
                            "SELECT order_no, payment_method FROM sales_orders WHERE id=@id;";
                        sourceCommand.Parameters.AddWithValue("@id", sourceSalesOrderId);
                        using (var reader = sourceCommand.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("原销售单不存在。");
                            sourceOrderNo = Convert.ToString(reader["order_no"]);
                            sourcePaymentMethod = Convert.ToString(reader["payment_method"]);
                        }
                    }

                    var normalizedRefundMethod = string.IsNullOrWhiteSpace(refundMethod)
                        ? (sourcePaymentMethod ?? "").Trim()
                        : refundMethod.Trim();
                    if (normalizedRefundMethod.Length == 0)
                        normalizedRefundMethod = "未记录";

                    var prepared = new List<PreparedLine>();
                    long totalCent = 0;

                    foreach (var line in lines)
                    {
                        var item = LoadSalesItem(connection, transaction, sourceSalesOrderId, line.SourceItemId);
                        var alreadyReturned = GetReturnedQuantity(
                            connection, transaction,
                            "SELECT COALESCE(SUM(quantity),0) FROM sales_return_items WHERE source_sales_order_item_id=@id;",
                            line.SourceItemId);

                        var remaining = item.OriginalQuantity - alreadyReturned;
                        if (line.Quantity > remaining)
                            throw new InvalidOperationException(item.Title + " 最多还可退 " + remaining + " 册。");

                        var lineTotal = checked((long)line.Quantity * item.UnitPriceCent);
                        totalCent = checked(totalCent + lineTotal);
                        item.ReturnQuantity = line.Quantity;
                        item.LineTotalCent = lineTotal;
                        prepared.Add(item);
                    }

                    long returnId;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO sales_returns
(return_no, source_sales_order_id, source_order_no_snapshot, refund_method,
 returned_at, total_cent, note, created_at)
VALUES(@no, @sourceId, @sourceNo, @refundMethod, @at, @total, @note, @at);
SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", returnNo);
                        command.Parameters.AddWithValue("@sourceId", sourceSalesOrderId);
                        command.Parameters.AddWithValue("@sourceNo", sourceOrderNo);
                        command.Parameters.AddWithValue("@refundMethod", normalizedRefundMethod);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@total", totalCent);
                        command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                        returnId = Convert.ToInt64(command.ExecuteScalar());
                    }

                    foreach (var item in prepared)
                    {
                        using (var itemCommand = connection.CreateCommand())
                        {
                            itemCommand.Transaction = transaction;
                            itemCommand.CommandText = @"
INSERT INTO sales_return_items
(sales_return_id, source_sales_order_item_id, book_id, isbn_snapshot, title_snapshot,
 quantity, unit_price_cent, line_total_cent)
VALUES(@returnId, @sourceItemId, @bookId, @isbn, @title, @qty, @unit, @total);";
                            itemCommand.Parameters.AddWithValue("@returnId", returnId);
                            itemCommand.Parameters.AddWithValue("@sourceItemId", item.SourceItemId);
                            itemCommand.Parameters.AddWithValue("@bookId", item.BookId);
                            itemCommand.Parameters.AddWithValue("@isbn", item.Isbn);
                            itemCommand.Parameters.AddWithValue("@title", item.Title);
                            itemCommand.Parameters.AddWithValue("@qty", item.ReturnQuantity);
                            itemCommand.Parameters.AddWithValue("@unit", item.UnitPriceCent);
                            itemCommand.Parameters.AddWithValue("@total", item.LineTotalCent);
                            itemCommand.ExecuteNonQuery();
                        }

                        using (var stockCommand = connection.CreateCommand())
                        {
                            stockCommand.Transaction = transaction;
                            stockCommand.CommandText =
                                "UPDATE books SET stock_quantity=stock_quantity+@qty, updated_at=@at WHERE id=@id;";
                            stockCommand.Parameters.AddWithValue("@qty", item.ReturnQuantity);
                            stockCommand.Parameters.AddWithValue("@at", timestamp);
                            stockCommand.Parameters.AddWithValue("@id", item.BookId);
                            if (stockCommand.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(item.Title + " 对应图书资料不存在。");
                        }

                        InsertLedger(connection, transaction, item, "SALE_RETURN", item.ReturnQuantity,
                            "SALE_RETURN", returnId, returnNo, timestamp, note);
                    }

                    transaction.Commit();
                    return returnNo;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public string GetSuggestedRefundMethod(long sourceSalesOrderId)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT payment_method FROM sales_orders WHERE id=@id LIMIT 1;";
                command.Parameters.AddWithValue("@id", sourceSalesOrderId);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value
                    ? ""
                    : Convert.ToString(value).Trim();
            }
        }

        public string CreatePurchaseReturn(long sourcePurchaseOrderId, IList<ReturnLineInput> lines, string note)
        {
            ValidateLines(lines);

            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var returnNo = "PR" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string sourceOrderNo;
                    string supplierName;
                    using (var sourceCommand = connection.CreateCommand())
                    {
                        sourceCommand.Transaction = transaction;
                        sourceCommand.CommandText =
                            "SELECT order_no, supplier_name_snapshot FROM purchase_orders WHERE id=@id AND status='reviewed';";
                        sourceCommand.Parameters.AddWithValue("@id", sourcePurchaseOrderId);
                        using (var reader = sourceCommand.ExecuteReader())
                        {
                            if (!reader.Read()) throw new InvalidOperationException("原采购单不存在或尚未复核。");
                            sourceOrderNo = Convert.ToString(reader["order_no"]);
                            supplierName = Convert.ToString(reader["supplier_name_snapshot"]);
                        }
                    }

                    var prepared = new List<PreparedLine>();
                    long totalCent = 0;

                    foreach (var line in lines)
                    {
                        var item = LoadPurchaseItem(connection, transaction, sourcePurchaseOrderId, line.SourceItemId);
                        var alreadyReturned = GetReturnedQuantity(
                            connection, transaction,
                            "SELECT COALESCE(SUM(quantity),0) FROM purchase_return_items WHERE source_purchase_order_item_id=@id;",
                            line.SourceItemId);

                        var remaining = item.OriginalQuantity - alreadyReturned;
                        if (line.Quantity > remaining)
                            throw new InvalidOperationException(item.Title + " 最多还可退 " + remaining + " 册。");

                        var stock = GetCurrentStock(connection, transaction, item.BookId);
                        if (stock < line.Quantity)
                            throw new InvalidOperationException(item.Title + " 当前库存只有 " + stock + " 册，无法退回 " + line.Quantity + " 册。");

                        var lineTotal = checked((long)line.Quantity * item.UnitPriceCent);
                        totalCent = checked(totalCent + lineTotal);
                        item.ReturnQuantity = line.Quantity;
                        item.LineTotalCent = lineTotal;
                        prepared.Add(item);
                    }

                    long returnId;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO purchase_returns
(return_no, source_purchase_order_id, source_order_no_snapshot, supplier_name_snapshot,
 returned_at, total_cent, note, created_at)
VALUES(@no, @sourceId, @sourceNo, @supplierName, @at, @total, @note, @at);
SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", returnNo);
                        command.Parameters.AddWithValue("@sourceId", sourcePurchaseOrderId);
                        command.Parameters.AddWithValue("@sourceNo", sourceOrderNo);
                        command.Parameters.AddWithValue("@supplierName", supplierName);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@total", totalCent);
                        command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                        returnId = Convert.ToInt64(command.ExecuteScalar());
                    }

                    foreach (var item in prepared)
                    {
                        using (var itemCommand = connection.CreateCommand())
                        {
                            itemCommand.Transaction = transaction;
                            itemCommand.CommandText = @"
INSERT INTO purchase_return_items
(purchase_return_id, source_purchase_order_item_id, book_id, isbn_snapshot, title_snapshot,
 quantity, unit_cost_cent, line_total_cent)
VALUES(@returnId, @sourceItemId, @bookId, @isbn, @title, @qty, @unit, @total);";
                            itemCommand.Parameters.AddWithValue("@returnId", returnId);
                            itemCommand.Parameters.AddWithValue("@sourceItemId", item.SourceItemId);
                            itemCommand.Parameters.AddWithValue("@bookId", item.BookId);
                            itemCommand.Parameters.AddWithValue("@isbn", item.Isbn);
                            itemCommand.Parameters.AddWithValue("@title", item.Title);
                            itemCommand.Parameters.AddWithValue("@qty", item.ReturnQuantity);
                            itemCommand.Parameters.AddWithValue("@unit", item.UnitPriceCent);
                            itemCommand.Parameters.AddWithValue("@total", item.LineTotalCent);
                            itemCommand.ExecuteNonQuery();
                        }

                        using (var stockCommand = connection.CreateCommand())
                        {
                            stockCommand.Transaction = transaction;
                            stockCommand.CommandText = @"
UPDATE books
SET stock_quantity=stock_quantity-@qty, updated_at=@at
WHERE id=@id AND stock_quantity>=@qty;";
                            stockCommand.Parameters.AddWithValue("@qty", item.ReturnQuantity);
                            stockCommand.Parameters.AddWithValue("@at", timestamp);
                            stockCommand.Parameters.AddWithValue("@id", item.BookId);
                            if (stockCommand.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(item.Title + " 库存不足，采购退货已取消。");
                        }

                        InsertLedger(connection, transaction, item, "PURCHASE_RETURN", -item.ReturnQuantity,
                            "PURCHASE_RETURN", returnId, returnNo, timestamp, note);
                    }

                    transaction.Commit();
                    return returnNo;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private static void ValidateLines(IList<ReturnLineInput> lines)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("退货单至少需要一项图书。");

            var seen = new HashSet<long>();
            foreach (var line in lines)
            {
                if (line.SourceItemId <= 0) throw new InvalidOperationException("退货明细来源无效。");
                if (line.Quantity <= 0) throw new InvalidOperationException("退货数量必须大于 0。");
                if (!seen.Add(line.SourceItemId))
                    throw new InvalidOperationException("同一原单明细不能重复添加。");
            }
        }

        private static PreparedLine LoadSalesItem(
            SQLiteConnection connection, SQLiteTransaction transaction,
            long sourceOrderId, long sourceItemId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT id, book_id, isbn_snapshot, title_snapshot, quantity, unit_price_cent
FROM sales_order_items
WHERE id=@itemId AND sales_order_id=@orderId;";
                command.Parameters.AddWithValue("@itemId", sourceItemId);
                command.Parameters.AddWithValue("@orderId", sourceOrderId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("销售退货明细不属于所选原销售单。");
                    return new PreparedLine
                    {
                        SourceItemId = Convert.ToInt64(reader["id"]),
                        BookId = Convert.ToInt64(reader["book_id"]),
                        Isbn = Convert.ToString(reader["isbn_snapshot"]),
                        Title = Convert.ToString(reader["title_snapshot"]),
                        OriginalQuantity = Convert.ToInt32(reader["quantity"]),
                        UnitPriceCent = Convert.ToInt64(reader["unit_price_cent"])
                    };
                }
            }
        }

        private static PreparedLine LoadPurchaseItem(
            SQLiteConnection connection, SQLiteTransaction transaction,
            long sourceOrderId, long sourceItemId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT id, book_id, isbn_snapshot, title_snapshot, quantity, unit_cost_cent
FROM purchase_order_items
WHERE id=@itemId AND purchase_order_id=@orderId;";
                command.Parameters.AddWithValue("@itemId", sourceItemId);
                command.Parameters.AddWithValue("@orderId", sourceOrderId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("采购退货明细不属于所选原采购单。");
                    return new PreparedLine
                    {
                        SourceItemId = Convert.ToInt64(reader["id"]),
                        BookId = Convert.ToInt64(reader["book_id"]),
                        Isbn = Convert.ToString(reader["isbn_snapshot"]),
                        Title = Convert.ToString(reader["title_snapshot"]),
                        OriginalQuantity = Convert.ToInt32(reader["quantity"]),
                        UnitPriceCent = Convert.ToInt64(reader["unit_cost_cent"])
                    };
                }
            }
        }

        private static int GetReturnedQuantity(
            SQLiteConnection connection, SQLiteTransaction transaction,
            string sql, long sourceItemId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", sourceItemId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static int GetCurrentStock(
            SQLiteConnection connection, SQLiteTransaction transaction, long bookId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT stock_quantity FROM books WHERE id=@id;";
                command.Parameters.AddWithValue("@id", bookId);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("图书资料不存在。");
                return Convert.ToInt32(value);
            }
        }

        private static string GetRequiredString(
            SQLiteConnection connection, SQLiteTransaction transaction,
            string sql, long id, string error)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", id);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException(error);
                return Convert.ToString(value);
            }
        }

        private static void InsertLedger(
            SQLiteConnection connection, SQLiteTransaction transaction,
            PreparedLine item, string type, int quantity, string referenceType,
            long referenceId, string referenceNo, string timestamp, string note)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO inventory_transactions
(book_id, isbn_snapshot, title_snapshot, type, quantity, reference_type,
 reference_id, reference_no, occurred_at, note)
VALUES(@bookId, @isbn, @title, @type, @qty, @refType, @refId, @refNo, @at, @note);";
                command.Parameters.AddWithValue("@bookId", item.BookId);
                command.Parameters.AddWithValue("@isbn", item.Isbn);
                command.Parameters.AddWithValue("@title", item.Title);
                command.Parameters.AddWithValue("@type", type);
                command.Parameters.AddWithValue("@qty", quantity);
                command.Parameters.AddWithValue("@refType", referenceType);
                command.Parameters.AddWithValue("@refId", referenceId);
                command.Parameters.AddWithValue("@refNo", referenceNo);
                command.Parameters.AddWithValue("@at", timestamp);
                command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                command.ExecuteNonQuery();
            }
        }

        private sealed class PreparedLine
        {
            public long SourceItemId { get; set; }
            public long BookId { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public int OriginalQuantity { get; set; }
            public long UnitPriceCent { get; set; }
            public int ReturnQuantity { get; set; }
            public long LineTotalCent { get; set; }
        }
    }
}
