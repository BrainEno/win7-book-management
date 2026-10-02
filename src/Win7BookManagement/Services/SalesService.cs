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
        public const string DefaultPaymentMethod = "微信";
        public const string CashPaymentMethod = "现金";

        private readonly DatabaseConnectionFactory _factory;

        public SalesService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string Checkout(IList<TransactionLineInput> lines, string note)
        {
            return Checkout(lines, note, 10000, DefaultPaymentMethod, null);
        }

        public string Checkout(
            IList<TransactionLineInput> lines,
            string note,
            int orderDiscountBasisPoints)
        {
            return Checkout(
                lines,
                note,
                orderDiscountBasisPoints,
                DefaultPaymentMethod,
                null);
        }

        public string Checkout(
            IList<TransactionLineInput> lines,
            string note,
            int orderDiscountBasisPoints,
            string paymentMethod,
            long? amountReceivedCent)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("销售单至少需要一项图书。");
            if (orderDiscountBasisPoints < 0 || orderDiscountBasisPoints > 10000)
                throw new InvalidOperationException("整单折扣必须在 0% 到 100% 之间。");

            var normalizedPayment = (paymentMethod ?? "").Trim();
            if (normalizedPayment.Length == 0)
                throw new InvalidOperationException("请选择收款方式。");

            long subtotalCent = 0;
            long lineDiscountCent = 0;
            long orderDiscountCent = 0;
            long totalCent = 0;

            foreach (var line in lines)
            {
                ValidateLine(line);

                var baseUnitPriceCent = ResolveBaseUnitPrice(line);
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

            long receivedCent;
            long changeCent;
            if (string.Equals(
                normalizedPayment,
                CashPaymentMethod,
                StringComparison.OrdinalIgnoreCase))
            {
                receivedCent = amountReceivedCent ?? 0;
                if (receivedCent < totalCent)
                    throw new InvalidOperationException(
                        "现金实收不能小于应收金额，还差 ¥" +
                        Money.ToYuan(totalCent - receivedCent).ToString("0.00") + "。");
                changeCent = checked(receivedCent - totalCent);
            }
            else
            {
                receivedCent = totalCent;
                changeCent = 0;
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
 total_cent, payment_method, amount_received_cent, change_cent,
 note, created_at)
VALUES
(@no, @at, @subtotal, @lineDiscount,
 @orderDiscountBasisPoints, @orderDiscount,
 @total, @paymentMethod, @received, @change,
 @note, @at);
SELECT last_insert_rowid();";
                        command.Parameters.AddWithValue("@no", orderNo);
                        command.Parameters.AddWithValue("@at", timestamp);
                        command.Parameters.AddWithValue("@subtotal", subtotalCent);
                        command.Parameters.AddWithValue("@lineDiscount", lineDiscountCent);
                        command.Parameters.AddWithValue("@orderDiscountBasisPoints", orderDiscountBasisPoints);
                        command.Parameters.AddWithValue("@orderDiscount", orderDiscountCent);
                        command.Parameters.AddWithValue("@total", totalCent);
                        command.Parameters.AddWithValue("@paymentMethod", normalizedPayment);
                        command.Parameters.AddWithValue("@received", receivedCent);
                        command.Parameters.AddWithValue("@change", changeCent);
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

        public SalesDraft SaveDraft(
            long? draftId,
            IList<TransactionLineInput> lines,
            string note,
            int orderDiscountBasisPoints)
        {
            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("当前销售单没有商品，无法挂单。");
            if (orderDiscountBasisPoints < 0 || orderDiscountBasisPoints > 10000)
                throw new InvalidOperationException("整单折扣必须在 0% 到 100% 之间。");

            foreach (var line in lines)
                ValidateLine(line);

            var now = DateTime.Now;
            var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var id = draftId ?? 0;

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    if (id <= 0)
                    {
                        var draftNo = "H" + now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = @"
INSERT INTO sales_drafts
(draft_no, note, order_discount_basis_points, created_at, updated_at)
VALUES(@no, @note, @discount, @at, @at);
SELECT last_insert_rowid();";
                            insert.Parameters.AddWithValue("@no", draftNo);
                            insert.Parameters.AddWithValue("@note", (note ?? "").Trim());
                            insert.Parameters.AddWithValue("@discount", orderDiscountBasisPoints);
                            insert.Parameters.AddWithValue("@at", timestamp);
                            id = Convert.ToInt64(insert.ExecuteScalar());
                        }
                    }
                    else
                    {
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = @"
UPDATE sales_drafts
SET note=@note,
    order_discount_basis_points=@discount,
    updated_at=@at
WHERE id=@id;";
                            update.Parameters.AddWithValue("@note", (note ?? "").Trim());
                            update.Parameters.AddWithValue("@discount", orderDiscountBasisPoints);
                            update.Parameters.AddWithValue("@at", timestamp);
                            update.Parameters.AddWithValue("@id", id);
                            if (update.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("要更新的挂单已不存在。");
                        }

                        using (var clear = connection.CreateCommand())
                        {
                            clear.Transaction = transaction;
                            clear.CommandText = "DELETE FROM sales_draft_items WHERE sales_draft_id=@id;";
                            clear.Parameters.AddWithValue("@id", id);
                            clear.ExecuteNonQuery();
                        }
                    }

                    foreach (var line in lines)
                    {
                        string selfCode;
                        string isbn;
                        string title;
                        string author;
                        using (var book = connection.CreateCommand())
                        {
                            book.Transaction = transaction;
                            book.CommandText = @"
SELECT self_code, isbn, title, author
FROM books
WHERE id=@id
LIMIT 1;";
                            book.Parameters.AddWithValue("@id", line.BookId);
                            using (var reader = book.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("挂单中的图书资料已不存在。");
                                selfCode = Convert.ToString(reader["self_code"]);
                                isbn = Convert.ToString(reader["isbn"]);
                                title = Convert.ToString(reader["title"]);
                                author = Convert.ToString(reader["author"]);
                            }
                        }

                        using (var item = connection.CreateCommand())
                        {
                            item.Transaction = transaction;
                            item.CommandText = @"
INSERT INTO sales_draft_items
(sales_draft_id, book_id, self_code_snapshot, isbn_snapshot,
 title_snapshot, author_snapshot, quantity, base_unit_price_cent,
 line_discount_basis_points)
VALUES
(@draftId, @bookId, @selfCode, @isbn,
 @title, @author, @quantity, @price, @discount);";
                            item.Parameters.AddWithValue("@draftId", id);
                            item.Parameters.AddWithValue("@bookId", line.BookId);
                            item.Parameters.AddWithValue("@selfCode", selfCode);
                            item.Parameters.AddWithValue("@isbn", isbn);
                            item.Parameters.AddWithValue("@title", title);
                            item.Parameters.AddWithValue("@author", author);
                            item.Parameters.AddWithValue("@quantity", line.Quantity);
                            item.Parameters.AddWithValue("@price", ResolveBaseUnitPrice(line));
                            item.Parameters.AddWithValue("@discount", line.DiscountBasisPoints);
                            item.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return GetDraft(id);
        }

        public IList<SalesDraftSummary> GetDraftSummaries()
        {
            var drafts = new Dictionary<long, SalesDraftSummary>();
            var order = new List<long>();

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT d.id,
       d.draft_no,
       d.updated_at,
       d.note,
       d.order_discount_basis_points,
       i.quantity,
       i.base_unit_price_cent,
       i.line_discount_basis_points
FROM sales_drafts d
LEFT JOIN sales_draft_items i ON i.sales_draft_id=d.id
ORDER BY d.updated_at DESC, d.id DESC, i.id;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = Convert.ToInt64(reader["id"]);
                        SalesDraftSummary summary;
                        if (!drafts.TryGetValue(id, out summary))
                        {
                            summary = new SalesDraftSummary
                            {
                                Id = id,
                                DraftNo = Convert.ToString(reader["draft_no"]),
                                UpdatedAt = ParseTimestamp(Convert.ToString(reader["updated_at"])),
                                Note = Convert.ToString(reader["note"]),
                                OrderDiscountBasisPoints = Convert.ToInt32(reader["order_discount_basis_points"])
                            };
                            drafts[id] = summary;
                            order.Add(id);
                        }

                        if (reader["quantity"] == DBNull.Value)
                            continue;

                        var quantity = Convert.ToInt32(reader["quantity"]);
                        var baseCent = Convert.ToInt64(reader["base_unit_price_cent"]);
                        var lineDiscount = Convert.ToInt32(reader["line_discount_basis_points"]);
                        var lineCent = ApplyBasisPoints(baseCent, lineDiscount);
                        var finalCent = ApplyBasisPoints(lineCent, summary.OrderDiscountBasisPoints);

                        summary.ItemCount += 1;
                        summary.QuantityTotal += quantity;
                        summary.TotalCent = checked(
                            summary.TotalCent + checked((long)quantity * finalCent));
                    }
                }
            }

            var result = new List<SalesDraftSummary>();
            foreach (var id in order)
                result.Add(drafts[id]);
            return result;
        }

        public SalesDraft GetDraft(long id)
        {
            SalesDraft draft = null;
            using (var connection = _factory.Open())
            {
                using (var header = connection.CreateCommand())
                {
                    header.CommandText = @"
SELECT id, draft_no, note, order_discount_basis_points, created_at, updated_at
FROM sales_drafts
WHERE id=@id
LIMIT 1;";
                    header.Parameters.AddWithValue("@id", id);
                    using (var reader = header.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        draft = new SalesDraft
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            DraftNo = Convert.ToString(reader["draft_no"]),
                            Note = Convert.ToString(reader["note"]),
                            OrderDiscountBasisPoints = Convert.ToInt32(reader["order_discount_basis_points"]),
                            CreatedAt = ParseTimestamp(Convert.ToString(reader["created_at"])),
                            UpdatedAt = ParseTimestamp(Convert.ToString(reader["updated_at"]))
                        };
                    }
                }

                using (var items = connection.CreateCommand())
                {
                    items.CommandText = @"
SELECT i.book_id,
       i.self_code_snapshot,
       i.isbn_snapshot,
       i.title_snapshot,
       i.author_snapshot,
       i.quantity,
       i.base_unit_price_cent,
       i.line_discount_basis_points,
       COALESCE(b.stock_quantity, 0) AS current_stock,
       COALESCE(b.is_active, 0) AS is_active
FROM sales_draft_items i
LEFT JOIN books b ON b.id=i.book_id
WHERE i.sales_draft_id=@id
ORDER BY i.id;";
                    items.Parameters.AddWithValue("@id", id);
                    using (var reader = items.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            draft.Lines.Add(new SalesDraftLine
                            {
                                BookId = Convert.ToInt64(reader["book_id"]),
                                SelfCode = Convert.ToString(reader["self_code_snapshot"]),
                                Isbn = Convert.ToString(reader["isbn_snapshot"]),
                                Title = Convert.ToString(reader["title_snapshot"]),
                                Author = Convert.ToString(reader["author_snapshot"]),
                                Quantity = Convert.ToInt32(reader["quantity"]),
                                BaseUnitPriceCent = Convert.ToInt64(reader["base_unit_price_cent"]),
                                DiscountBasisPoints = Convert.ToInt32(reader["line_discount_basis_points"]),
                                CurrentStock = Convert.ToInt32(reader["current_stock"]),
                                IsActive = Convert.ToInt32(reader["is_active"]) == 1
                            });
                        }
                    }
                }
            }

            return draft;
        }

        public void DeleteDraft(long id)
        {
            if (id <= 0)
                return;

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var items = connection.CreateCommand())
                    {
                        items.Transaction = transaction;
                        items.CommandText = "DELETE FROM sales_draft_items WHERE sales_draft_id=@id;";
                        items.Parameters.AddWithValue("@id", id);
                        items.ExecuteNonQuery();
                    }

                    using (var header = connection.CreateCommand())
                    {
                        header.Transaction = transaction;
                        header.CommandText = "DELETE FROM sales_drafts WHERE id=@id;";
                        header.Parameters.AddWithValue("@id", id);
                        header.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private static void ValidateLine(TransactionLineInput line)
        {
            if (line == null)
                throw new InvalidOperationException("销售明细不能为空。");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("销售数量必须大于 0。");

            var baseUnitPriceCent = ResolveBaseUnitPrice(line);
            if (baseUnitPriceCent < 0)
                throw new InvalidOperationException("售价不能为负数。");
            if (line.DiscountBasisPoints < 0 || line.DiscountBasisPoints > 10000)
                throw new InvalidOperationException("单品折扣必须在 0% 到 100% 之间。");
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

        private static DateTime ParseTimestamp(string value)
        {
            DateTime parsed;
            if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
            {
                return parsed;
            }

            return DateTime.TryParse(value, out parsed) ? parsed : DateTime.MinValue;
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

        public sealed class SalesDraft
        {
            public long Id { get; set; }
            public string DraftNo { get; set; }
            public string Note { get; set; }
            public int OrderDiscountBasisPoints { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public IList<SalesDraftLine> Lines { get; private set; }

            public SalesDraft()
            {
                DraftNo = "";
                Note = "";
                Lines = new List<SalesDraftLine>();
            }
        }

        public sealed class SalesDraftLine
        {
            public long BookId { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public int Quantity { get; set; }
            public long BaseUnitPriceCent { get; set; }
            public int DiscountBasisPoints { get; set; }
            public int CurrentStock { get; set; }
            public bool IsActive { get; set; }

            public SalesDraftLine()
            {
                SelfCode = "";
                Isbn = "";
                Title = "";
                Author = "";
            }
        }

        public sealed class SalesDraftSummary
        {
            public long Id { get; set; }
            public string DraftNo { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string Note { get; set; }
            public int OrderDiscountBasisPoints { get; set; }
            public int ItemCount { get; set; }
            public int QuantityTotal { get; set; }
            public long TotalCent { get; set; }

            public SalesDraftSummary()
            {
                DraftNo = "";
                Note = "";
            }
        }
    }
}
