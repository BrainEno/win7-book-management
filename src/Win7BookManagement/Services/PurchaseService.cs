using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Services
{
    public sealed class PurchaseService
    {
        public const string DraftStatus = "draft";
        public const string ReviewedStatus = "reviewed";

        private readonly DatabaseConnectionFactory _factory;

        public PurchaseService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        // Backward-compatible entry point used by existing callers and snapshot
        // fixtures. New UI code saves a draft first and reviews it explicitly.
        public string Receive(long? supplierId, IList<TransactionLineInput> lines, string note)
        {
            var draft = SaveDraft(null, "", DateTime.Now, supplierId, lines, note);
            Review(draft.Id);
            return draft.OrderNo;
        }

        public PurchaseDocument SaveDraft(
            long? documentId,
            string requestedOrderNo,
            DateTime purchaseDate,
            long? supplierId,
            IList<TransactionLineInput> lines,
            string note)
        {
            return SaveDraft(
                documentId, requestedOrderNo, purchaseDate, supplierId, lines, note, 10000);
        }

        public PurchaseDocument SaveDraft(
            long? documentId,
            string requestedOrderNo,
            DateTime purchaseDate,
            long? supplierId,
            IList<TransactionLineInput> lines,
            string note,
            int orderDiscountBasisPoints)
        {
            var normalizedLines = lines ?? new List<TransactionLineInput>();
            ValidateDraftLines(normalizedLines);
            if (orderDiscountBasisPoints < 0 || orderDiscountBasisPoints > 10000)
                throw new InvalidOperationException("整单折扣必须在 0% 到 100% 之间。");

            var now = DateTime.Now;
            var timestamp = Format(now);
            var purchasedAt = Format(purchaseDate.Date);
            var totals = CalculateTotals(normalizedLines, orderDiscountBasisPoints);

            long id;
            string orderNo;

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var supplierName = LoadSupplierName(connection, transaction, supplierId);

                    string currentOrderNo = "";
                    if (documentId.HasValue)
                    {
                        using (var current = connection.CreateCommand())
                        {
                            current.Transaction = transaction;
                            current.CommandText = "SELECT order_no, status FROM purchase_orders WHERE id=@id;";
                            current.Parameters.AddWithValue("@id", documentId.Value);
                            using (var reader = current.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("要保存的采购草稿不存在。");
                                if (string.Equals(Convert.ToString(reader["status"]), ReviewedStatus, StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidOperationException("已复核采购单不能直接修改，请先反复核。");
                                currentOrderNo = Convert.ToString(reader["order_no"]);
                            }
                        }
                    }

                    orderNo = (requestedOrderNo ?? "").Trim();
                    if (orderNo.Length == 0)
                        orderNo = documentId.HasValue && currentOrderNo.Length > 0
                            ? currentOrderNo
                            : GenerateOrderNo(connection, transaction, purchaseDate.Date);

                    EnsureUniqueOrderNo(connection, transaction, orderNo, documentId);

                    if (documentId.HasValue)
                    {
                        id = documentId.Value;
                        using (var update = connection.CreateCommand())
                        {
                            update.Transaction = transaction;
                            update.CommandText = @"
UPDATE purchase_orders
SET order_no=@no,
    supplier_id=@supplierId,
    supplier_name_snapshot=@supplierName,
    purchased_at=@purchasedAt,
    subtotal_cent=@subtotal,
    line_discount_cent=@lineDiscount,
    order_discount_basis_points=@orderDiscountBasisPoints,
    order_discount_cent=@orderDiscount,
    total_cent=@total,
    note=@note,
    status=@status,
    reviewed_at=NULL,
    updated_at=@updatedAt
WHERE id=@id AND status=@draft;";
                            update.Parameters.AddWithValue("@no", orderNo);
                            update.Parameters.AddWithValue("@supplierId", supplierId.HasValue ? (object)supplierId.Value : DBNull.Value);
                            update.Parameters.AddWithValue("@supplierName", supplierName);
                            update.Parameters.AddWithValue("@purchasedAt", purchasedAt);
                            update.Parameters.AddWithValue("@subtotal", totals.SubtotalCent);
                            update.Parameters.AddWithValue("@lineDiscount", totals.LineDiscountCent);
                            update.Parameters.AddWithValue("@orderDiscountBasisPoints", orderDiscountBasisPoints);
                            update.Parameters.AddWithValue("@orderDiscount", totals.OrderDiscountCent);
                            update.Parameters.AddWithValue("@total", totals.TotalCent);
                            update.Parameters.AddWithValue("@note", (note ?? "").Trim());
                            update.Parameters.AddWithValue("@status", DraftStatus);
                            update.Parameters.AddWithValue("@updatedAt", timestamp);
                            update.Parameters.AddWithValue("@id", id);
                            update.Parameters.AddWithValue("@draft", DraftStatus);
                            if (update.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("采购草稿状态已变化，请重新打开后再保存。");
                        }

                        using (var deleteItems = connection.CreateCommand())
                        {
                            deleteItems.Transaction = transaction;
                            deleteItems.CommandText = "DELETE FROM purchase_order_items WHERE purchase_order_id=@id;";
                            deleteItems.Parameters.AddWithValue("@id", id);
                            deleteItems.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var insert = connection.CreateCommand())
                        {
                            insert.Transaction = transaction;
                            insert.CommandText = @"
INSERT INTO purchase_orders
(order_no, supplier_id, supplier_name_snapshot, purchased_at,
 subtotal_cent, line_discount_cent, order_discount_basis_points, order_discount_cent,
 total_cent, note, created_at, status, reviewed_at, updated_at)
VALUES
(@no, @supplierId, @supplierName, @purchasedAt,
 @subtotal, @lineDiscount, @orderDiscountBasisPoints, @orderDiscount,
 @total, @note, @createdAt, @status, NULL, @updatedAt);
SELECT last_insert_rowid();";
                            insert.Parameters.AddWithValue("@no", orderNo);
                            insert.Parameters.AddWithValue("@supplierId", supplierId.HasValue ? (object)supplierId.Value : DBNull.Value);
                            insert.Parameters.AddWithValue("@supplierName", supplierName);
                            insert.Parameters.AddWithValue("@purchasedAt", purchasedAt);
                            insert.Parameters.AddWithValue("@subtotal", totals.SubtotalCent);
                            insert.Parameters.AddWithValue("@lineDiscount", totals.LineDiscountCent);
                            insert.Parameters.AddWithValue("@orderDiscountBasisPoints", orderDiscountBasisPoints);
                            insert.Parameters.AddWithValue("@orderDiscount", totals.OrderDiscountCent);
                            insert.Parameters.AddWithValue("@total", totals.TotalCent);
                            insert.Parameters.AddWithValue("@note", (note ?? "").Trim());
                            insert.Parameters.AddWithValue("@createdAt", timestamp);
                            insert.Parameters.AddWithValue("@status", DraftStatus);
                            insert.Parameters.AddWithValue("@updatedAt", timestamp);
                            id = Convert.ToInt64(insert.ExecuteScalar());
                        }
                    }

                    InsertItems(connection, transaction, id, normalizedLines, orderDiscountBasisPoints);
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return GetDocument(id);
        }

        public string Review(long documentId)
        {
            var now = DateTime.Now;
            var timestamp = Format(now);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string orderNo;
                    string note;
                    string status;
                    using (var header = connection.CreateCommand())
                    {
                        header.Transaction = transaction;
                        header.CommandText = "SELECT order_no, note, status FROM purchase_orders WHERE id=@id;";
                        header.Parameters.AddWithValue("@id", documentId);
                        using (var reader = header.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("采购单不存在。");
                            orderNo = Convert.ToString(reader["order_no"]);
                            note = Convert.ToString(reader["note"]);
                            status = Convert.ToString(reader["status"]);
                        }
                    }

                    if (string.Equals(status, ReviewedStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        transaction.Commit();
                        return orderNo;
                    }

                    var lines = LoadPostingLines(connection, transaction, documentId, true);
                    if (lines.Count == 0)
                        throw new InvalidOperationException("采购草稿至少需要一项图书才能复核。");

                    foreach (var line in lines)
                    {
                        using (var stock = connection.CreateCommand())
                        {
                            stock.Transaction = transaction;
                            stock.CommandText = @"
UPDATE books
SET stock_quantity=stock_quantity+@qty, updated_at=@at
WHERE id=@id AND is_active=1;";
                            stock.Parameters.AddWithValue("@qty", line.Quantity);
                            stock.Parameters.AddWithValue("@at", timestamp);
                            stock.Parameters.AddWithValue("@id", line.BookId);
                            if (stock.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("《" + line.Title + "》不存在或已停用，无法复核入库。");
                        }

                        InsertLedger(
                            connection,
                            transaction,
                            line.BookId,
                            line.Isbn,
                            line.Title,
                            "PURCHASE",
                            line.Quantity,
                            "PURCHASE",
                            documentId,
                            orderNo,
                            timestamp,
                            note);
                    }

                    using (var update = connection.CreateCommand())
                    {
                        update.Transaction = transaction;
                        update.CommandText = @"
UPDATE purchase_orders
SET status=@reviewed, reviewed_at=@at, updated_at=@at
WHERE id=@id AND status=@draft;";
                        update.Parameters.AddWithValue("@reviewed", ReviewedStatus);
                        update.Parameters.AddWithValue("@at", timestamp);
                        update.Parameters.AddWithValue("@id", documentId);
                        update.Parameters.AddWithValue("@draft", DraftStatus);
                        if (update.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("采购单状态已变化，请刷新后再复核。");
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

        public string Unreview(long documentId)
        {
            var now = DateTime.Now;
            var timestamp = Format(now);

            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string orderNo;
                    string status;
                    using (var header = connection.CreateCommand())
                    {
                        header.Transaction = transaction;
                        header.CommandText = "SELECT order_no, status FROM purchase_orders WHERE id=@id;";
                        header.Parameters.AddWithValue("@id", documentId);
                        using (var reader = header.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("采购单不存在。");
                            orderNo = Convert.ToString(reader["order_no"]);
                            status = Convert.ToString(reader["status"]);
                        }
                    }

                    if (!string.Equals(status, ReviewedStatus, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("当前采购单尚未复核，无需反复核。");

                    using (var returnCheck = connection.CreateCommand())
                    {
                        returnCheck.Transaction = transaction;
                        returnCheck.CommandText = "SELECT COUNT(1) FROM purchase_returns WHERE source_purchase_order_id=@id;";
                        returnCheck.Parameters.AddWithValue("@id", documentId);
                        if (Convert.ToInt32(returnCheck.ExecuteScalar()) > 0)
                            throw new InvalidOperationException("该采购单已经发生采购退货，不能直接反复核。请保留原单和退货单历史。");
                    }

                    var lines = LoadPostingLines(connection, transaction, documentId, false);
                    foreach (var line in lines)
                    {
                        using (var stock = connection.CreateCommand())
                        {
                            stock.Transaction = transaction;
                            stock.CommandText = @"
UPDATE books
SET stock_quantity=stock_quantity-@qty, updated_at=@at
WHERE id=@id AND stock_quantity>=@qty;";
                            stock.Parameters.AddWithValue("@qty", line.Quantity);
                            stock.Parameters.AddWithValue("@at", timestamp);
                            stock.Parameters.AddWithValue("@id", line.BookId);
                            if (stock.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException(
                                    "《" + line.Title + "》当前库存不足以撤销本次入库。可能已有后续销售或调整，请先处理后续库存业务。");
                        }

                        InsertLedger(
                            connection,
                            transaction,
                            line.BookId,
                            line.Isbn,
                            line.Title,
                            "PURCHASE_UNREVIEW",
                            -line.Quantity,
                            "PURCHASE_UNREVIEW",
                            documentId,
                            orderNo,
                            timestamp,
                            "反复核采购单 " + orderNo);
                    }

                    using (var update = connection.CreateCommand())
                    {
                        update.Transaction = transaction;
                        update.CommandText = @"
UPDATE purchase_orders
SET status=@draft, reviewed_at=NULL, updated_at=@at
WHERE id=@id AND status=@reviewed;";
                        update.Parameters.AddWithValue("@draft", DraftStatus);
                        update.Parameters.AddWithValue("@at", timestamp);
                        update.Parameters.AddWithValue("@id", documentId);
                        update.Parameters.AddWithValue("@reviewed", ReviewedStatus);
                        if (update.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("采购单状态已变化，请刷新后再反复核。");
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

        public string DeleteDraft(long documentId)
        {
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string orderNo;
                    string status;
                    using (var header = connection.CreateCommand())
                    {
                        header.Transaction = transaction;
                        header.CommandText = "SELECT order_no, status FROM purchase_orders WHERE id=@id;";
                        header.Parameters.AddWithValue("@id", documentId);
                        using (var reader = header.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("采购草稿不存在或已经被删除。");

                            orderNo = Convert.ToString(reader["order_no"]);
                            status = Convert.ToString(reader["status"]);
                        }
                    }

                    if (!string.Equals(status, DraftStatus, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("已复核采购单不能删除。需要纠错时请使用退货或反复核流程。");

                    using (var ledger = connection.CreateCommand())
                    {
                        ledger.Transaction = transaction;
                        ledger.CommandText = @"
SELECT COUNT(1)
FROM inventory_transactions
WHERE reference_id=@id
  AND reference_type IN ('PURCHASE', 'PURCHASE_UNREVIEW');";
                        ledger.Parameters.AddWithValue("@id", documentId);
                        if (Convert.ToInt32(ledger.ExecuteScalar()) > 0)
                            throw new InvalidOperationException(
                                "这张采购单曾经复核并产生库存流水，即使当前已反复核也必须保留历史，不能删除。");
                    }

                    using (var returns = connection.CreateCommand())
                    {
                        returns.Transaction = transaction;
                        returns.CommandText = "SELECT COUNT(1) FROM purchase_returns WHERE source_purchase_order_id=@id;";
                        returns.Parameters.AddWithValue("@id", documentId);
                        if (Convert.ToInt32(returns.ExecuteScalar()) > 0)
                            throw new InvalidOperationException("这张采购单已有采购退货记录，不能删除。");
                    }

                    using (var items = connection.CreateCommand())
                    {
                        items.Transaction = transaction;
                        items.CommandText = "DELETE FROM purchase_order_items WHERE purchase_order_id=@id;";
                        items.Parameters.AddWithValue("@id", documentId);
                        items.ExecuteNonQuery();
                    }

                    using (var order = connection.CreateCommand())
                    {
                        order.Transaction = transaction;
                        order.CommandText = "DELETE FROM purchase_orders WHERE id=@id AND status=@draft;";
                        order.Parameters.AddWithValue("@id", documentId);
                        order.Parameters.AddWithValue("@draft", DraftStatus);
                        if (order.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("采购草稿状态已变化，请刷新后重试。");
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

        public PurchaseDocument GetDocument(long documentId)
        {
            using (var connection = _factory.Open())
            {
                PurchaseDocument document;
                using (var header = connection.CreateCommand())
                {
                    header.CommandText = @"
SELECT id, order_no, supplier_id, supplier_name_snapshot, purchased_at,
       subtotal_cent, line_discount_cent, order_discount_basis_points,
       order_discount_cent, total_cent, note, status, reviewed_at, updated_at
FROM purchase_orders
WHERE id=@id;";
                    header.Parameters.AddWithValue("@id", documentId);
                    using (var reader = header.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        document = new PurchaseDocument
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            OrderNo = Convert.ToString(reader["order_no"]),
                            SupplierId = reader["supplier_id"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(reader["supplier_id"]),
                            SupplierName = Convert.ToString(reader["supplier_name_snapshot"]),
                            PurchasedAt = ParseDate(Convert.ToString(reader["purchased_at"])),
                            SubtotalCent = Convert.ToInt64(reader["subtotal_cent"]),
                            LineDiscountCent = Convert.ToInt64(reader["line_discount_cent"]),
                            OrderDiscountBasisPoints = Convert.ToInt32(reader["order_discount_basis_points"]),
                            OrderDiscountCent = Convert.ToInt64(reader["order_discount_cent"]),
                            TotalCent = Convert.ToInt64(reader["total_cent"]),
                            Note = Convert.ToString(reader["note"]),
                            Status = Convert.ToString(reader["status"]),
                            ReviewedAt = reader["reviewed_at"] == DBNull.Value ||
                                         string.IsNullOrWhiteSpace(Convert.ToString(reader["reviewed_at"]))
                                ? (DateTime?)null
                                : ParseDate(Convert.ToString(reader["reviewed_at"])),
                            UpdatedAt = ParseDate(Convert.ToString(reader["updated_at"]))
                        };
                    }
                }

                using (var items = connection.CreateCommand())
                {
                    items.CommandText = @"
SELECT pi.id,
       pi.book_id,
       b.self_code,
       pi.isbn_snapshot,
       pi.title_snapshot,
       b.author,
       b.publisher,
       b.shelf_code,
       b.stock_quantity,
       pi.quantity,
       pi.base_unit_cost_cent,
       pi.line_discount_basis_points,
       pi.line_discounted_unit_cost_cent,
       pi.unit_cost_cent,
       pi.line_total_cent
FROM purchase_order_items pi
JOIN books b ON b.id=pi.book_id
WHERE pi.purchase_order_id=@id
ORDER BY pi.id;";
                    items.Parameters.AddWithValue("@id", documentId);
                    using (var reader = items.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            document.Lines.Add(new PurchaseDocumentLine
                            {
                                Id = Convert.ToInt64(reader["id"]),
                                BookId = Convert.ToInt64(reader["book_id"]),
                                SelfCode = Convert.ToString(reader["self_code"]),
                                Isbn = Convert.ToString(reader["isbn_snapshot"]),
                                Title = Convert.ToString(reader["title_snapshot"]),
                                Author = Convert.ToString(reader["author"]),
                                Publisher = Convert.ToString(reader["publisher"]),
                                ShelfCode = Convert.ToString(reader["shelf_code"]),
                                CurrentStock = Convert.ToInt32(reader["stock_quantity"]),
                                Quantity = Convert.ToInt32(reader["quantity"]),
                                BaseUnitCostCent = Convert.ToInt64(reader["base_unit_cost_cent"]),
                                LineDiscountBasisPoints = Convert.ToInt32(reader["line_discount_basis_points"]),
                                LineDiscountedUnitCostCent = Convert.ToInt64(reader["line_discounted_unit_cost_cent"]),
                                UnitCostCent = Convert.ToInt64(reader["unit_cost_cent"]),
                                LineTotalCent = Convert.ToInt64(reader["line_total_cent"])
                            });
                        }
                    }
                }

                return document;
            }
        }

        public IList<PurchaseDocumentSummary> GetHistory()
        {
            return SearchHistory("", "", 500);
        }

        public IList<PurchaseDocumentSummary> SearchHistory(
            string keyword,
            string status,
            int limit)
        {
            if (limit <= 0) limit = 500;
            if (limit > 2000) limit = 2000;

            keyword = (keyword ?? "").Trim();
            status = (status ?? "").Trim();

            var result = new List<PurchaseDocumentSummary>();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT po.id,
       po.order_no,
       po.supplier_name_snapshot,
       po.purchased_at,
       COALESCE(SUM(pi.quantity),0) AS quantity,
       po.total_cent,
       po.status,
       po.updated_at
FROM purchase_orders po
LEFT JOIN purchase_order_items pi ON pi.purchase_order_id=po.id
WHERE (@status='' OR po.status=@status)
  AND (
        @keyword='' OR
        po.order_no LIKE @pattern OR
        po.supplier_name_snapshot LIKE @pattern OR
        EXISTS (
            SELECT 1
            FROM purchase_order_items search_item
            WHERE search_item.purchase_order_id=po.id
              AND (
                  search_item.isbn_snapshot LIKE @pattern OR
                  search_item.title_snapshot LIKE @pattern
              )
        )
      )
GROUP BY po.id, po.order_no, po.supplier_name_snapshot, po.purchased_at,
         po.total_cent, po.status, po.updated_at
ORDER BY po.purchased_at DESC, po.id DESC
LIMIT @limit;";
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@keyword", keyword);
                command.Parameters.AddWithValue("@pattern", "%" + keyword + "%");
                command.Parameters.AddWithValue("@limit", limit);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new PurchaseDocumentSummary
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            OrderNo = Convert.ToString(reader["order_no"]),
                            SupplierName = Convert.ToString(reader["supplier_name_snapshot"]),
                            PurchasedAt = ParseDate(Convert.ToString(reader["purchased_at"])),
                            Quantity = Convert.ToInt32(reader["quantity"]),
                            TotalCent = Convert.ToInt64(reader["total_cent"]),
                            Status = Convert.ToString(reader["status"]),
                            UpdatedAt = ParseDate(Convert.ToString(reader["updated_at"]))
                        });
                    }
                }
            }
            return result;
        }

        public PurchaseDocument CopyToNewDraft(long sourceDocumentId, DateTime purchaseDate)
        {
            var source = GetDocument(sourceDocumentId);
            if (source == null)
                throw new InvalidOperationException("要复制的采购单不存在。");

            var lines = new List<TransactionLineInput>();
            foreach (var line in source.Lines)
            {
                lines.Add(new TransactionLineInput
                {
                    BookId = line.BookId,
                    Quantity = line.Quantity,
                    UnitPriceCent = line.BaseUnitCostCent,
                    BaseUnitPriceCent = line.BaseUnitCostCent,
                    DiscountBasisPoints = line.LineDiscountBasisPoints
                });
            }

            return SaveDraft(
                null,
                "",
                purchaseDate.Date,
                source.SupplierId,
                lines,
                "",
                source.OrderDiscountBasisPoints);
        }

        public PurchaseNavigationState GetNavigationState(long currentId)
        {
            using (var connection = _factory.Open())
            {
                string purchasedAt;
                long id;
                using (var current = connection.CreateCommand())
                {
                    current.CommandText = "SELECT id, purchased_at FROM purchase_orders WHERE id=@id;";
                    current.Parameters.AddWithValue("@id", currentId);
                    using (var reader = current.ExecuteReader())
                    {
                        if (!reader.Read())
                            return new PurchaseNavigationState { CurrentId = currentId };

                        id = Convert.ToInt64(reader["id"]);
                        purchasedAt = Convert.ToString(reader["purchased_at"]);
                    }
                }

                var state = new PurchaseNavigationState { CurrentId = id };

                using (var count = connection.CreateCommand())
                {
                    count.CommandText = "SELECT COUNT(1) FROM purchase_orders;";
                    state.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                }

                using (var position = connection.CreateCommand())
                {
                    position.CommandText = @"
SELECT COUNT(1)
FROM purchase_orders
WHERE purchased_at<@at OR (purchased_at=@at AND id<=@id);";
                    position.Parameters.AddWithValue("@at", purchasedAt);
                    position.Parameters.AddWithValue("@id", id);
                    state.Position = Convert.ToInt32(position.ExecuteScalar());
                }

                using (var previous = connection.CreateCommand())
                {
                    previous.CommandText = @"
SELECT id
FROM purchase_orders
WHERE purchased_at<@at OR (purchased_at=@at AND id<@id)
ORDER BY purchased_at DESC, id DESC
LIMIT 1;";
                    previous.Parameters.AddWithValue("@at", purchasedAt);
                    previous.Parameters.AddWithValue("@id", id);
                    var value = previous.ExecuteScalar();
                    state.PreviousId = value == null || value == DBNull.Value
                        ? (long?)null
                        : Convert.ToInt64(value);
                }

                using (var next = connection.CreateCommand())
                {
                    next.CommandText = @"
SELECT id
FROM purchase_orders
WHERE purchased_at>@at OR (purchased_at=@at AND id>@id)
ORDER BY purchased_at ASC, id ASC
LIMIT 1;";
                    next.Parameters.AddWithValue("@at", purchasedAt);
                    next.Parameters.AddWithValue("@id", id);
                    var value = next.ExecuteScalar();
                    state.NextId = value == null || value == DBNull.Value
                        ? (long?)null
                        : Convert.ToInt64(value);
                }

                return state;
            }
        }

        public long? GetAdjacentDocumentId(long currentId, bool next)
        {
            var state = GetNavigationState(currentId);
            return next ? state.NextId : state.PreviousId;
        }

        private static void ValidateDraftLines(IList<TransactionLineInput> lines)
        {
            var seen = new HashSet<long>();
            foreach (var line in lines)
            {
                if (line.BookId <= 0)
                    throw new InvalidOperationException("采购明细包含无效图书。");
                if (line.Quantity <= 0)
                    throw new InvalidOperationException("入库数量必须大于 0。");
                if (ResolveBaseUnitCost(line) < 0)
                    throw new InvalidOperationException("进价不能为负数。");
                if (line.DiscountBasisPoints < 0 || line.DiscountBasisPoints > 10000)
                    throw new InvalidOperationException("单品折扣必须在 0% 到 100% 之间。");
                if (!seen.Add(line.BookId))
                    throw new InvalidOperationException("同一本图书不能在采购明细中重复出现。");
            }
        }

        private static PurchaseTotals CalculateTotals(
            IList<TransactionLineInput> lines,
            int orderDiscountBasisPoints)
        {
            var result = new PurchaseTotals();
            foreach (var line in lines)
            {
                var baseUnitCostCent = ResolveBaseUnitCost(line);
                var lineDiscountedUnitCostCent =
                    ApplyBasisPoints(baseUnitCostCent, line.DiscountBasisPoints);
                var finalUnitCostCent =
                    ApplyBasisPoints(lineDiscountedUnitCostCent, orderDiscountBasisPoints);

                result.SubtotalCent = checked(
                    result.SubtotalCent + checked((long)line.Quantity * baseUnitCostCent));
                result.LineDiscountCent = checked(
                    result.LineDiscountCent + checked(
                        (long)line.Quantity * (baseUnitCostCent - lineDiscountedUnitCostCent)));
                result.OrderDiscountCent = checked(
                    result.OrderDiscountCent + checked(
                        (long)line.Quantity * (lineDiscountedUnitCostCent - finalUnitCostCent)));
                result.TotalCent = checked(
                    result.TotalCent + checked((long)line.Quantity * finalUnitCostCent));
            }
            return result;
        }

        private static long ResolveBaseUnitCost(TransactionLineInput line)
        {
            return line.BaseUnitPriceCent > 0 || line.UnitPriceCent == 0
                ? line.BaseUnitPriceCent
                : line.UnitPriceCent;
        }

        private static long ApplyBasisPoints(long amountCent, int basisPoints)
        {
            if (amountCent <= 0 || basisPoints <= 0) return 0;
            if (basisPoints >= 10000) return amountCent;

            return decimal.ToInt64(decimal.Round(
                amountCent * (basisPoints / 10000m),
                0,
                MidpointRounding.AwayFromZero));
        }

        private static string LoadSupplierName(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long? supplierId)
        {
            if (!supplierId.HasValue) return "";

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT name FROM suppliers WHERE id=@id;";
                command.Parameters.AddWithValue("@id", supplierId.Value);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("供应商不存在。");
                return Convert.ToString(value);
            }
        }

        private static string GenerateOrderNo(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            DateTime purchaseDate)
        {
            var dateText = purchaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var prefix = purchaseDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            int sequence;

            using (var count = connection.CreateCommand())
            {
                count.Transaction = transaction;
                count.CommandText = "SELECT COUNT(1) FROM purchase_orders WHERE substr(purchased_at,1,10)=@date;";
                count.Parameters.AddWithValue("@date", dateText);
                sequence = Convert.ToInt32(count.ExecuteScalar()) + 1;
            }

            while (true)
            {
                var candidate = prefix + sequence.ToString("D2", CultureInfo.InvariantCulture);
                using (var exists = connection.CreateCommand())
                {
                    exists.Transaction = transaction;
                    exists.CommandText = "SELECT COUNT(1) FROM purchase_orders WHERE order_no=@no;";
                    exists.Parameters.AddWithValue("@no", candidate);
                    if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
                        return candidate;
                }
                sequence += 1;
            }
        }

        private static void EnsureUniqueOrderNo(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string orderNo,
            long? currentId)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                throw new InvalidOperationException("采购单号不能为空。");

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(1) FROM purchase_orders WHERE order_no=@no AND id<>@id;";
                command.Parameters.AddWithValue("@no", orderNo.Trim());
                command.Parameters.AddWithValue("@id", currentId.HasValue ? currentId.Value : 0L);
                if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("采购单号“" + orderNo + "”已存在，请更换后再保存。");
            }
        }

        private static void InsertItems(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long orderId,
            IList<TransactionLineInput> lines,
            int orderDiscountBasisPoints)
        {
            foreach (var line in lines)
            {
                string isbn;
                string title;
                using (var book = connection.CreateCommand())
                {
                    book.Transaction = transaction;
                    book.CommandText = "SELECT isbn, title FROM books WHERE id=@id;";
                    book.Parameters.AddWithValue("@id", line.BookId);
                    using (var reader = book.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("采购图书不存在。");
                        isbn = Convert.ToString(reader["isbn"]);
                        title = Convert.ToString(reader["title"]);
                    }
                }

                var baseUnitCostCent = ResolveBaseUnitCost(line);
                var lineDiscountedUnitCostCent =
                    ApplyBasisPoints(baseUnitCostCent, line.DiscountBasisPoints);
                var finalUnitCostCent =
                    ApplyBasisPoints(lineDiscountedUnitCostCent, orderDiscountBasisPoints);
                var lineTotal = checked((long)line.Quantity * finalUnitCostCent);
                using (var item = connection.CreateCommand())
                {
                    item.Transaction = transaction;
                    item.CommandText = @"
INSERT INTO purchase_order_items
(purchase_order_id, book_id, isbn_snapshot, title_snapshot, quantity,
 base_unit_cost_cent, line_discount_basis_points, line_discounted_unit_cost_cent,
 unit_cost_cent, line_total_cent)
VALUES(@orderId, @bookId, @isbn, @title, @qty,
 @baseUnit, @lineDiscountBasisPoints, @lineDiscountedUnit, @unit, @total);";
                    item.Parameters.AddWithValue("@orderId", orderId);
                    item.Parameters.AddWithValue("@bookId", line.BookId);
                    item.Parameters.AddWithValue("@isbn", isbn);
                    item.Parameters.AddWithValue("@title", title);
                    item.Parameters.AddWithValue("@qty", line.Quantity);
                    item.Parameters.AddWithValue("@baseUnit", baseUnitCostCent);
                    item.Parameters.AddWithValue("@lineDiscountBasisPoints", line.DiscountBasisPoints);
                    item.Parameters.AddWithValue("@lineDiscountedUnit", lineDiscountedUnitCostCent);
                    item.Parameters.AddWithValue("@unit", finalUnitCostCent);
                    item.Parameters.AddWithValue("@total", lineTotal);
                    item.ExecuteNonQuery();
                }
            }
        }

        private static IList<PostingLine> LoadPostingLines(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long documentId,
            bool requireActive)
        {
            var result = new List<PostingLine>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT pi.book_id,
       pi.isbn_snapshot,
       pi.title_snapshot,
       pi.quantity,
       b.is_active
FROM purchase_order_items pi
JOIN books b ON b.id=pi.book_id
WHERE pi.purchase_order_id=@id
ORDER BY pi.id;";
                command.Parameters.AddWithValue("@id", documentId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (requireActive && Convert.ToInt32(reader["is_active"]) != 1)
                            throw new InvalidOperationException("《" + Convert.ToString(reader["title_snapshot"]) + "》已停用，无法复核入库。");

                        result.Add(new PostingLine
                        {
                            BookId = Convert.ToInt64(reader["book_id"]),
                            Isbn = Convert.ToString(reader["isbn_snapshot"]),
                            Title = Convert.ToString(reader["title_snapshot"]),
                            Quantity = Convert.ToInt32(reader["quantity"])
                        });
                    }
                }
            }
            return result;
        }

        private static void InsertLedger(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            long bookId,
            string isbn,
            string title,
            string type,
            int quantity,
            string referenceType,
            long referenceId,
            string referenceNo,
            string timestamp,
            string note)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO inventory_transactions
(book_id, isbn_snapshot, title_snapshot, type, quantity, reference_type,
 reference_id, reference_no, occurred_at, note)
VALUES(@bookId, @isbn, @title, @type, @qty, @refType, @refId, @refNo, @at, @note);";
                command.Parameters.AddWithValue("@bookId", bookId);
                command.Parameters.AddWithValue("@isbn", isbn ?? "");
                command.Parameters.AddWithValue("@title", title ?? "");
                command.Parameters.AddWithValue("@type", type);
                command.Parameters.AddWithValue("@qty", quantity);
                command.Parameters.AddWithValue("@refType", referenceType);
                command.Parameters.AddWithValue("@refId", referenceId);
                command.Parameters.AddWithValue("@refNo", referenceNo ?? "");
                command.Parameters.AddWithValue("@at", timestamp);
                command.Parameters.AddWithValue("@note", (note ?? "").Trim());
                command.ExecuteNonQuery();
            }
        }

        private static DateTime ParseDate(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out parsed))
                return parsed;
            if (DateTime.TryParse(value, out parsed))
                return parsed;
            return DateTime.MinValue;
        }

        private static string Format(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private sealed class PurchaseTotals
        {
            public long SubtotalCent { get; set; }
            public long LineDiscountCent { get; set; }
            public long OrderDiscountCent { get; set; }
            public long TotalCent { get; set; }
        }

        private sealed class PostingLine
        {
            public long BookId { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public int Quantity { get; set; }
        }
    }
}
