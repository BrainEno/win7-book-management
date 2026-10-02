using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Repositories
{
    public sealed class DocumentRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public DocumentRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public DataTable Search(string kind, DateTime fromDate, DateTime toDate, string keyword)
        {
            if (toDate.Date < fromDate.Date)
                throw new InvalidOperationException("结束日期不能早于开始日期。");

            var sql = SqlForList(kind);
            var table = new DataTable();

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@from", Format(fromDate.Date));
                command.Parameters.AddWithValue("@to", Format(toDate.Date.AddDays(1)));
                command.Parameters.AddWithValue("@keyword", (keyword ?? "").Trim());
                command.Parameters.AddWithValue("@like", "%" + (keyword ?? "").Trim() + "%");

                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }

            return table;
        }

        public DataTable GetItems(string kind, long documentId)
        {
            var table = new DataTable();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = SqlForItems(kind);
                command.Parameters.AddWithValue("@id", documentId);
                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }
            return table;
        }

        public IList<ReturnableDocumentLine> GetReturnableLines(string kind, long sourceDocumentId)
        {
            if (kind != "sale" && kind != "purchase")
                throw new InvalidOperationException("只有销售单和采购单可以发起退货。");

            var result = new List<ReturnableDocumentLine>();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = kind == "sale" ? @"
SELECT si.id AS source_item_id,
       si.book_id,
       si.isbn_snapshot,
       si.title_snapshot,
       si.quantity AS original_quantity,
       COALESCE((
         SELECT SUM(sri.quantity)
         FROM sales_return_items sri
         WHERE sri.source_sales_order_item_id = si.id
       ), 0) AS returned_quantity,
       b.stock_quantity AS current_stock,
       si.unit_price_cent AS unit_price_cent
FROM sales_order_items si
JOIN books b ON b.id = si.book_id
WHERE si.sales_order_id = @id
ORDER BY si.id;" : @"
SELECT pi.id AS source_item_id,
       pi.book_id,
       pi.isbn_snapshot,
       pi.title_snapshot,
       pi.quantity AS original_quantity,
       COALESCE((
         SELECT SUM(pri.quantity)
         FROM purchase_return_items pri
         WHERE pri.source_purchase_order_item_id = pi.id
       ), 0) AS returned_quantity,
       b.stock_quantity AS current_stock,
       pi.unit_cost_cent AS unit_price_cent
FROM purchase_order_items pi
JOIN purchase_orders po ON po.id = pi.purchase_order_id
JOIN books b ON b.id = pi.book_id
WHERE pi.purchase_order_id = @id
  AND po.status = 'reviewed'
ORDER BY pi.id;";
                command.Parameters.AddWithValue("@id", sourceDocumentId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var original = Convert.ToInt32(reader["original_quantity"]);
                        var returned = Convert.ToInt32(reader["returned_quantity"]);
                        result.Add(new ReturnableDocumentLine
                        {
                            SourceItemId = Convert.ToInt64(reader["source_item_id"]),
                            BookId = Convert.ToInt64(reader["book_id"]),
                            Isbn = Convert.ToString(reader["isbn_snapshot"]),
                            Title = Convert.ToString(reader["title_snapshot"]),
                            OriginalQuantity = original,
                            ReturnedQuantity = returned,
                            ReturnableQuantity = Math.Max(0, original - returned),
                            CurrentStock = Convert.ToInt32(reader["current_stock"]),
                            UnitPriceCent = Convert.ToInt64(reader["unit_price_cent"]),
                            ReturnQuantity = 0
                        });
                    }
                }
            }
            return result;
        }

        public string GetDocumentNo(string kind, long documentId)
        {
            string table;
            string column;
            switch (kind)
            {
                case "sale": table = "sales_orders"; column = "order_no"; break;
                case "purchase": table = "purchase_orders"; column = "order_no"; break;
                case "sale_return": table = "sales_returns"; column = "return_no"; break;
                case "purchase_return": table = "purchase_returns"; column = "return_no"; break;
                default: throw new InvalidOperationException("未知单据类型。");
            }

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + column + " FROM " + table + " WHERE id=@id;";
                command.Parameters.AddWithValue("@id", documentId);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? "" : Convert.ToString(value);
            }
        }

        private static string SqlForList(string kind)
        {
            switch (kind)
            {
                case "sale":
                    return @"
SELECT so.id AS Id,
       so.sold_at AS 日期,
       so.order_no AS 单号,
       COALESCE(SUM(si.quantity),0) AS 数量,
       ROUND(so.total_cent / 100.0, 2) AS 金额,
       CASE WHEN TRIM(so.payment_method)='' THEN '未记录' ELSE so.payment_method END AS 收款方式,
       ROUND(so.amount_received_cent / 100.0, 2) AS 实收金额,
       ROUND(so.change_cent / 100.0, 2) AS 找零金额,
       CASE
         WHEN COALESCE(SUM(r.returned_qty),0)=0 THEN '未退货'
         WHEN COALESCE(SUM(r.returned_qty),0)<COALESCE(SUM(si.quantity),0) THEN '部分退货'
         ELSE '已退完'
       END AS 状态,
       so.note AS 备注
FROM sales_orders so
JOIN sales_order_items si ON si.sales_order_id=so.id
LEFT JOIN (
  SELECT source_sales_order_item_id, SUM(quantity) AS returned_qty
  FROM sales_return_items
  GROUP BY source_sales_order_item_id
) r ON r.source_sales_order_item_id=si.id
WHERE so.sold_at>=@from AND so.sold_at<@to
  AND (@keyword='' OR so.order_no LIKE @like OR so.note LIKE @like
       OR EXISTS (
         SELECT 1 FROM sales_order_items sx
         WHERE sx.sales_order_id=so.id
           AND (sx.isbn_snapshot LIKE @like OR sx.title_snapshot LIKE @like)
       ))
GROUP BY so.id, so.sold_at, so.order_no, so.total_cent, so.note
ORDER BY so.sold_at DESC, so.id DESC;";
                case "purchase":
                    return @"
SELECT po.id AS Id,
       po.purchased_at AS 日期,
       po.order_no AS 单号,
       po.supplier_name_snapshot AS 供应商,
       COALESCE(SUM(pi.quantity),0) AS 数量,
       ROUND(po.total_cent / 100.0, 2) AS 金额,
       CASE
         WHEN COALESCE(SUM(r.returned_qty),0)=0 THEN '未退货'
         WHEN COALESCE(SUM(r.returned_qty),0)<COALESCE(SUM(pi.quantity),0) THEN '部分退货'
         ELSE '已退完'
       END AS 状态,
       po.note AS 备注
FROM purchase_orders po
JOIN purchase_order_items pi ON pi.purchase_order_id=po.id
LEFT JOIN (
  SELECT source_purchase_order_item_id, SUM(quantity) AS returned_qty
  FROM purchase_return_items
  GROUP BY source_purchase_order_item_id
) r ON r.source_purchase_order_item_id=pi.id
WHERE po.purchased_at>=@from AND po.purchased_at<@to
  AND po.status='reviewed'
  AND (@keyword='' OR po.order_no LIKE @like OR po.supplier_name_snapshot LIKE @like OR po.note LIKE @like
       OR EXISTS (
         SELECT 1 FROM purchase_order_items px
         WHERE px.purchase_order_id=po.id
           AND (px.isbn_snapshot LIKE @like OR px.title_snapshot LIKE @like)
       ))
GROUP BY po.id, po.purchased_at, po.order_no, po.supplier_name_snapshot, po.total_cent, po.note
ORDER BY po.purchased_at DESC, po.id DESC;";
                case "sale_return":
                    return @"
SELECT sr.id AS Id,
       sr.returned_at AS 日期,
       sr.return_no AS 退货单号,
       sr.source_order_no_snapshot AS 原销售单号,
       COALESCE(SUM(sri.quantity),0) AS 数量,
       ROUND(sr.total_cent / 100.0, 2) AS 退款金额,
       sr.note AS 备注
FROM sales_returns sr
JOIN sales_return_items sri ON sri.sales_return_id=sr.id
WHERE sr.returned_at>=@from AND sr.returned_at<@to
  AND (@keyword='' OR sr.return_no LIKE @like OR sr.source_order_no_snapshot LIKE @like OR sr.note LIKE @like
       OR EXISTS (
         SELECT 1 FROM sales_return_items sx
         WHERE sx.sales_return_id=sr.id
           AND (sx.isbn_snapshot LIKE @like OR sx.title_snapshot LIKE @like)
       ))
GROUP BY sr.id, sr.returned_at, sr.return_no, sr.source_order_no_snapshot, sr.total_cent, sr.note
ORDER BY sr.returned_at DESC, sr.id DESC;";
                case "purchase_return":
                    return @"
SELECT pr.id AS Id,
       pr.returned_at AS 日期,
       pr.return_no AS 退货单号,
       pr.source_order_no_snapshot AS 原采购单号,
       pr.supplier_name_snapshot AS 供应商,
       COALESCE(SUM(pri.quantity),0) AS 数量,
       ROUND(pr.total_cent / 100.0, 2) AS 退货金额,
       pr.note AS 备注
FROM purchase_returns pr
JOIN purchase_return_items pri ON pri.purchase_return_id=pr.id
WHERE pr.returned_at>=@from AND pr.returned_at<@to
  AND (@keyword='' OR pr.return_no LIKE @like OR pr.source_order_no_snapshot LIKE @like
       OR pr.supplier_name_snapshot LIKE @like OR pr.note LIKE @like
       OR EXISTS (
         SELECT 1 FROM purchase_return_items px
         WHERE px.purchase_return_id=pr.id
           AND (px.isbn_snapshot LIKE @like OR px.title_snapshot LIKE @like)
       ))
GROUP BY pr.id, pr.returned_at, pr.return_no, pr.source_order_no_snapshot, pr.supplier_name_snapshot, pr.total_cent, pr.note
ORDER BY pr.returned_at DESC, pr.id DESC;";
                default:
                    throw new InvalidOperationException("未知单据类型。");
            }
        }

        private static string SqlForItems(string kind)
        {
            switch (kind)
            {
                case "sale":
                    return @"
SELECT si.isbn_snapshot AS ISBN,
       si.title_snapshot AS 书名,
       si.quantity AS 原数量,
       COALESCE((SELECT SUM(sri.quantity) FROM sales_return_items sri WHERE sri.source_sales_order_item_id=si.id),0) AS 已退,
       si.quantity-COALESCE((SELECT SUM(sri.quantity) FROM sales_return_items sri WHERE sri.source_sales_order_item_id=si.id),0) AS 可退,
       ROUND(si.base_unit_price_cent/100.0,2) AS 原价,
       ROUND(si.line_discount_basis_points/100.0,2) AS [单品折扣%],
       ROUND(so.order_discount_basis_points/100.0,2) AS [整单折扣%],
       ROUND(si.unit_price_cent/100.0,2) AS 实收单价,
       ROUND(si.line_total_cent/100.0,2) AS 金额
FROM sales_order_items si
JOIN sales_orders so ON so.id=si.sales_order_id
WHERE si.sales_order_id=@id
ORDER BY si.id;";
                case "purchase":
                    return @"
SELECT pi.isbn_snapshot AS ISBN,
       pi.title_snapshot AS 书名,
       pi.quantity AS 原数量,
       COALESCE((SELECT SUM(pri.quantity) FROM purchase_return_items pri WHERE pri.source_purchase_order_item_id=pi.id),0) AS 已退,
       pi.quantity-COALESCE((SELECT SUM(pri.quantity) FROM purchase_return_items pri WHERE pri.source_purchase_order_item_id=pi.id),0) AS 可退,
       ROUND(pi.unit_cost_cent/100.0,2) AS 进价,
       ROUND(pi.line_total_cent/100.0,2) AS 金额
FROM purchase_order_items pi
WHERE pi.purchase_order_id=@id
ORDER BY pi.id;";
                case "sale_return":
                    return @"
SELECT sri.isbn_snapshot AS ISBN,
       sri.title_snapshot AS 书名,
       sri.quantity AS 退货数量,
       ROUND(sri.unit_price_cent/100.0,2) AS 成交单价,
       ROUND(sri.line_total_cent/100.0,2) AS 退款金额
FROM sales_return_items sri
WHERE sri.sales_return_id=@id
ORDER BY sri.id;";
                case "purchase_return":
                    return @"
SELECT pri.isbn_snapshot AS ISBN,
       pri.title_snapshot AS 书名,
       pri.quantity AS 退货数量,
       ROUND(pri.unit_cost_cent/100.0,2) AS 原进价,
       ROUND(pri.line_total_cent/100.0,2) AS 退货金额
FROM purchase_return_items pri
WHERE pri.purchase_return_id=@id
ORDER BY pri.id;";
                default:
                    throw new InvalidOperationException("未知单据类型。");
            }
        }

        private static string Format(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
