using System;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;

namespace Win7BookManagement.Repositories
{
    public sealed class ReportRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public ReportRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public DataTable SalesDetail(DateTime fromDate, DateTime toDate)
        {
            return Fill(@"
SELECT so.sold_at AS 日期,
       so.order_no AS 销售单号,
       si.isbn_snapshot AS ISBN,
       si.title_snapshot AS 书名,
       si.author_snapshot AS 作者,
       si.publisher_snapshot AS 出版社,
       si.category_snapshot AS 分类,
       si.quantity AS 数量,
       ROUND(si.base_unit_price_cent / 100.0, 2) AS 原单价,
       ROUND(si.line_discount_basis_points / 100.0, 2) AS [单品折扣%],
       ROUND(so.order_discount_basis_points / 100.0, 2) AS [整单折扣%],
       ROUND(si.unit_price_cent / 100.0, 2) AS 实收单价,
       ROUND(si.line_total_cent / 100.0, 2) AS 金额,
       CASE WHEN TRIM(so.payment_method)='' THEN '未记录' ELSE so.payment_method END AS 收款方式
FROM sales_orders so
JOIN sales_order_items si ON si.sales_order_id = so.id
WHERE so.sold_at >= @from AND so.sold_at < @to
ORDER BY so.sold_at, so.id, si.id;", fromDate, toDate);
        }

        public DataTable SalesMonthlyExportDetail(DateTime month)
        {
            var fromDate = new DateTime(month.Year, month.Month, 1);
            var toDate = fromDate.AddMonths(1);

            return Fill(@"
SELECT so.id AS order_id,
       si.id AS item_id,
       so.sold_at AS sold_at,
       so.order_no AS order_no,
       COALESCE(NULLIF(si.self_code_snapshot, ''), si.isbn_snapshot) AS self_code,
       si.isbn_snapshot AS isbn,
       si.title_snapshot AS title,
       si.author_snapshot AS author,
       si.publisher_snapshot AS publisher,
       si.category_snapshot AS category,
       si.publication_year_snapshot AS publication_year,
       si.quantity AS quantity,
       si.base_unit_price_cent AS base_unit_price_cent,
       si.line_discount_basis_points AS line_discount_basis_points,
       si.line_discounted_unit_price_cent AS line_discounted_unit_price_cent,
       so.order_discount_basis_points AS order_discount_basis_points,
       si.unit_price_cent AS final_unit_price_cent,
       si.line_total_cent AS line_total_cent,
       so.total_cent AS order_total_cent,
       CASE WHEN TRIM(so.payment_method)='' THEN '未记录' ELSE so.payment_method END AS payment_method,
       so.amount_received_cent AS amount_received_cent,
       so.change_cent AS change_cent,
       so.note AS order_note,
       si.list_price_snapshot_cent AS list_price_cent,
       si.cost_ref_snapshot_cent AS cost_ref_cent,
       si.cost_ref_source_snapshot AS cost_ref_source,
       si.supplier_snapshot AS supplier_name,
       COALESCE(
         (
           SELECT SUM(it.quantity)
           FROM inventory_transactions it
           WHERE it.book_id=si.book_id
             AND it.occurred_at<=so.sold_at
         ),
         0
       ) AS stock_after_sale,
       COALESCE(
         (
           SELECT SUM(sri.quantity)
           FROM sales_return_items sri
           JOIN sales_returns sr ON sr.id=sri.sales_return_id
           WHERE sri.source_sales_order_item_id=si.id
             AND sr.returned_at>=@from AND sr.returned_at<@to
         ),
         0
       ) AS returned_quantity_in_month,
       COALESCE(
         (
           SELECT SUM(sri.quantity)
           FROM sales_return_items sri
           JOIN sales_returns sr ON sr.id=sri.sales_return_id
           WHERE sri.source_sales_order_item_id=si.id
             AND sr.returned_at<@to
         ),
         0
       ) AS returned_quantity_to_month_end
FROM sales_orders so
JOIN sales_order_items si ON si.sales_order_id=so.id
WHERE so.sold_at>=@from AND so.sold_at<@to
ORDER BY so.sold_at, so.id, si.id;", fromDate, toDate.AddDays(-1));
        }

        public DataTable SalesMonthlyReturnExportDetail(DateTime month)
        {
            var fromDate = new DateTime(month.Year, month.Month, 1);
            var toDate = fromDate.AddMonths(1);

            return Fill(@"
SELECT sr.id AS return_id,
       sri.id AS return_item_id,
       sr.returned_at AS returned_at,
       sr.return_no AS return_no,
       sr.source_order_no_snapshot AS source_order_no,
       sri.source_sales_order_item_id AS source_item_id,
       sri.book_id AS book_id,
       sri.isbn_snapshot AS isbn,
       sri.title_snapshot AS title,
       sri.quantity AS quantity,
       sri.unit_price_cent AS unit_price_cent,
       sri.line_total_cent AS line_total_cent,
       CASE WHEN TRIM(sr.refund_method)='' THEN '未记录' ELSE sr.refund_method END AS refund_method,
       sr.note AS return_note
FROM sales_returns sr
JOIN sales_return_items sri ON sri.sales_return_id=sr.id
WHERE sr.returned_at>=@from AND sr.returned_at<@to
ORDER BY sr.returned_at, sr.id, sri.id;", fromDate, toDate.AddDays(-1));
        }

        public DataTable SalesMonthlySummary(DateTime month)
        {
            var fromDate = new DateTime(month.Year, month.Month, 1);
            var toDate = fromDate.AddMonths(1);

            return Fill(@"
SELECT
  COALESCE((SELECT SUM(total_cent) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to), 0)
    AS gross_sales_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_returns WHERE returned_at>=@from AND returned_at<@to), 0)
    AS return_amount_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to), 0)
    - COALESCE((SELECT SUM(total_cent) FROM sales_returns WHERE returned_at>=@from AND returned_at<@to), 0)
    AS net_sales_cent,
  COALESCE((
    SELECT SUM(si.quantity)
    FROM sales_order_items si
    JOIN sales_orders so ON so.id=si.sales_order_id
    WHERE so.sold_at>=@from AND so.sold_at<@to
  ), 0) AS sales_quantity,
  COALESCE((
    SELECT SUM(sri.quantity)
    FROM sales_return_items sri
    JOIN sales_returns sr ON sr.id=sri.sales_return_id
    WHERE sr.returned_at>=@from AND sr.returned_at<@to
  ), 0) AS return_quantity,
  COALESCE((
    SELECT SUM(si.quantity)
    FROM sales_order_items si
    JOIN sales_orders so ON so.id=si.sales_order_id
    WHERE so.sold_at>=@from AND so.sold_at<@to
  ), 0)
  - COALESCE((
    SELECT SUM(sri.quantity)
    FROM sales_return_items sri
    JOIN sales_returns sr ON sr.id=sri.sales_return_id
    WHERE sr.returned_at>=@from AND sr.returned_at<@to
  ), 0) AS net_quantity,
  COALESCE((SELECT COUNT(*) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to), 0)
    AS order_count,
  CASE
    WHEN COALESCE((SELECT COUNT(*) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to), 0)=0 THEN 0
    ELSE ROUND(
      COALESCE((SELECT SUM(total_cent) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to), 0) * 1.0 /
      (SELECT COUNT(*) FROM sales_orders WHERE sold_at>=@from AND sold_at<@to)
    )
  END AS average_order_cent;", fromDate, toDate.AddDays(-1));
        }

        public DataTable OperatingDailySummary(DateTime date)
        {
            var table = CreateOperatingSummaryTable();
            using (var connection = _factory.Open())
            {
                AppendOperatingSummaryRow(
                    connection,
                    table,
                    date.Date,
                    date.Date.AddDays(1),
                    date.ToString("yyyy-MM-dd"));
            }
            return table;
        }

        public DataTable OperatingMonthlySummary(DateTime month)
        {
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var table = CreateOperatingSummaryTable();

            using (var connection = _factory.Open())
            {
                for (var date = monthStart; date < monthEnd; date = date.AddDays(1))
                {
                    AppendOperatingSummaryRow(
                        connection,
                        table,
                        date,
                        date.AddDays(1),
                        date.ToString("yyyy-MM-dd"));
                }

                AppendOperatingSummaryRow(
                    connection,
                    table,
                    monthStart,
                    monthEnd,
                    "本月合计");
            }

            return table;
        }

        private static DataTable CreateOperatingSummaryTable()
        {
            var table = new DataTable();
            table.Columns.Add("日期", typeof(string));
            table.Columns.Add("订单数", typeof(long));
            table.Columns.Add("销售册数", typeof(long));
            table.Columns.Add("退货册数", typeof(long));
            table.Columns.Add("净销售册数", typeof(long));
            table.Columns.Add("原金额", typeof(decimal));
            table.Columns.Add("优惠额", typeof(decimal));
            table.Columns.Add("销售额", typeof(decimal));
            table.Columns.Add("退货额", typeof(decimal));
            table.Columns.Add("净销售", typeof(decimal));
            table.Columns.Add("客单价", typeof(decimal));
            table.Columns.Add("平均成交折扣%", typeof(decimal));
            table.Columns.Add("参考成本", typeof(decimal));
            table.Columns.Add("参考毛利", typeof(decimal));
            table.Columns.Add("参考毛利率%", typeof(decimal));
            table.Columns.Add("微信净额", typeof(decimal));
            table.Columns.Add("支付宝净额", typeof(decimal));
            table.Columns.Add("现金净额", typeof(decimal));
            table.Columns.Add("其他净额", typeof(decimal));
            return table;
        }

        private static void AppendOperatingSummaryRow(
            SQLiteConnection connection,
            DataTable table,
            DateTime from,
            DateTime to,
            string label)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
  COALESCE((SELECT COUNT(*) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to), 0) AS order_count,
  COALESCE((SELECT SUM(si.quantity)
            FROM sales_order_items si
            JOIN sales_orders so ON so.id=si.sales_order_id
            WHERE so.sold_at>=@from AND so.sold_at<@to), 0) AS sales_quantity,
  COALESCE((SELECT SUM(sri.quantity)
            FROM sales_return_items sri
            JOIN sales_returns sr ON sr.id=sri.sales_return_id
            WHERE sr.returned_at>=@from AND sr.returned_at<@to), 0) AS return_quantity,
  COALESCE((SELECT SUM(subtotal_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to), 0) AS subtotal_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to), 0) AS sales_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_returns
            WHERE returned_at>=@from AND returned_at<@to), 0) AS return_cent,
  COALESCE((SELECT SUM(si.cost_ref_snapshot_cent * si.quantity)
            FROM sales_order_items si
            JOIN sales_orders so ON so.id=si.sales_order_id
            WHERE so.sold_at>=@from AND so.sold_at<@to), 0) AS sales_cost_cent,
  COALESCE((SELECT SUM(si.cost_ref_snapshot_cent * sri.quantity)
            FROM sales_return_items sri
            JOIN sales_returns sr ON sr.id=sri.sales_return_id
            JOIN sales_order_items si ON si.id=sri.source_sales_order_item_id
            WHERE sr.returned_at>=@from AND sr.returned_at<@to), 0) AS return_cost_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to AND payment_method='微信'), 0)
    - COALESCE((SELECT SUM(total_cent) FROM sales_returns
                WHERE returned_at>=@from AND returned_at<@to AND refund_method='微信'), 0)
    AS wechat_net_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to AND payment_method='支付宝'), 0)
    - COALESCE((SELECT SUM(total_cent) FROM sales_returns
                WHERE returned_at>=@from AND returned_at<@to AND refund_method='支付宝'), 0)
    AS alipay_net_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to AND payment_method='现金'), 0)
    - COALESCE((SELECT SUM(total_cent) FROM sales_returns
                WHERE returned_at>=@from AND returned_at<@to AND refund_method='现金'), 0)
    AS cash_net_cent,
  COALESCE((SELECT SUM(total_cent) FROM sales_orders
            WHERE sold_at>=@from AND sold_at<@to
              AND payment_method NOT IN ('微信','支付宝','现金')), 0)
    - COALESCE((SELECT SUM(total_cent) FROM sales_returns
                WHERE returned_at>=@from AND returned_at<@to
                  AND refund_method NOT IN ('微信','支付宝','现金')), 0)
    AS other_net_cent;";
                command.Parameters.AddWithValue("@from", FormatDate(from));
                command.Parameters.AddWithValue("@to", FormatDate(to));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return;

                    var orderCount = Convert.ToInt64(reader["order_count"]);
                    var salesQuantity = Convert.ToInt64(reader["sales_quantity"]);
                    var returnQuantity = Convert.ToInt64(reader["return_quantity"]);
                    var subtotalCent = Convert.ToInt64(reader["subtotal_cent"]);
                    var salesCent = Convert.ToInt64(reader["sales_cent"]);
                    var returnCent = Convert.ToInt64(reader["return_cent"]);
                    var salesCostCent = Convert.ToInt64(reader["sales_cost_cent"]);
                    var returnCostCent = Convert.ToInt64(reader["return_cost_cent"]);
                    var netSalesCent = salesCent - returnCent;
                    var netCostCent = salesCostCent - returnCostCent;
                    var referenceProfitCent = netSalesCent - netCostCent;

                    var row = table.NewRow();
                    row["日期"] = label;
                    row["订单数"] = orderCount;
                    row["销售册数"] = salesQuantity;
                    row["退货册数"] = returnQuantity;
                    row["净销售册数"] = salesQuantity - returnQuantity;
                    row["原金额"] = ToYuan(subtotalCent);
                    row["优惠额"] = ToYuan(subtotalCent - salesCent);
                    row["销售额"] = ToYuan(salesCent);
                    row["退货额"] = ToYuan(returnCent);
                    row["净销售"] = ToYuan(netSalesCent);
                    row["客单价"] = orderCount == 0
                        ? 0m
                        : ToYuan((long)Math.Round(
                            salesCent * 1.0 / orderCount,
                            MidpointRounding.AwayFromZero));
                    row["平均成交折扣%"] = subtotalCent <= 0
                        ? 0m
                        : Math.Round(
                            salesCent * 100m / subtotalCent,
                            2,
                            MidpointRounding.AwayFromZero);
                    row["参考成本"] = ToYuan(netCostCent);
                    row["参考毛利"] = ToYuan(referenceProfitCent);
                    row["参考毛利率%"] = netSalesCent == 0
                        ? 0m
                        : Math.Round(
                            referenceProfitCent * 100m / netSalesCent,
                            2,
                            MidpointRounding.AwayFromZero);
                    row["微信净额"] = ToYuan(Convert.ToInt64(reader["wechat_net_cent"]));
                    row["支付宝净额"] = ToYuan(Convert.ToInt64(reader["alipay_net_cent"]));
                    row["现金净额"] = ToYuan(Convert.ToInt64(reader["cash_net_cent"]));
                    row["其他净额"] = ToYuan(Convert.ToInt64(reader["other_net_cent"]));
                    table.Rows.Add(row);
                }
            }
        }

        private static decimal ToYuan(long cent)
        {
            return cent / 100m;
        }

        public DataTable PurchaseDetail(DateTime fromDate, DateTime toDate)
        {
            return Fill(@"
SELECT po.purchased_at AS 日期,
       po.order_no AS 采购单号,
       po.supplier_name_snapshot AS 供应商,
       pi.isbn_snapshot AS ISBN,
       pi.title_snapshot AS 书名,
       pi.quantity AS 数量,
       ROUND(pi.unit_cost_cent / 100.0, 2) AS 进价,
       ROUND(pi.line_total_cent / 100.0, 2) AS 金额
FROM purchase_orders po
JOIN purchase_order_items pi ON pi.purchase_order_id = po.id
WHERE po.purchased_at >= @from AND po.purchased_at < @to
  AND po.status = 'reviewed'
ORDER BY po.purchased_at, po.id, pi.id;", fromDate, toDate);
        }

        public DataTable PurchaseMonthlyExportDetail(DateTime month)
        {
            var fromDate = new DateTime(month.Year, month.Month, 1);
            var toDate = fromDate.AddMonths(1);

            return Fill(@"
SELECT po.id AS purchase_id,
       pi.id AS item_id,
       po.purchased_at AS purchased_at,
       po.reviewed_at AS reviewed_at,
       po.order_no AS order_no,
       po.supplier_name_snapshot AS supplier_name,
       po.note AS order_note,
       pi.book_id AS book_id,
       pi.isbn_snapshot AS isbn,
       pi.title_snapshot AS title,
       pi.quantity AS quantity,
       pi.unit_cost_cent AS unit_cost_cent,
       pi.line_total_cent AS line_total_cent,
       COALESCE((
         SELECT SUM(pri.quantity)
         FROM purchase_return_items pri
         JOIN purchase_returns pr ON pr.id=pri.purchase_return_id
         WHERE pri.source_purchase_order_item_id=pi.id
           AND pr.returned_at>=@from AND pr.returned_at<@to
       ), 0) AS returned_quantity_in_month,
       COALESCE((
         SELECT SUM(pri.quantity)
         FROM purchase_return_items pri
         JOIN purchase_returns pr ON pr.id=pri.purchase_return_id
         WHERE pri.source_purchase_order_item_id=pi.id
           AND pr.returned_at<@to
       ), 0) AS returned_quantity_to_month_end
FROM purchase_orders po
JOIN purchase_order_items pi ON pi.purchase_order_id=po.id
WHERE po.status='reviewed'
  AND po.purchased_at>=@from AND po.purchased_at<@to
ORDER BY po.purchased_at, po.id, pi.id;", fromDate, toDate.AddDays(-1));
        }

        public DataTable PurchaseMonthlyReturnExportDetail(DateTime month)
        {
            var fromDate = new DateTime(month.Year, month.Month, 1);
            var toDate = fromDate.AddMonths(1);

            return Fill(@"
SELECT pr.id AS return_id,
       pri.id AS return_item_id,
       pr.returned_at AS returned_at,
       pr.return_no AS return_no,
       pr.source_order_no_snapshot AS source_order_no,
       pr.supplier_name_snapshot AS supplier_name,
       pr.note AS return_note,
       pri.book_id AS book_id,
       pri.isbn_snapshot AS isbn,
       pri.title_snapshot AS title,
       pri.quantity AS quantity,
       pri.unit_cost_cent AS unit_cost_cent,
       pri.line_total_cent AS line_total_cent
FROM purchase_returns pr
JOIN purchase_return_items pri ON pri.purchase_return_id=pr.id
WHERE pr.returned_at>=@from AND pr.returned_at<@to
ORDER BY pr.returned_at, pr.id, pri.id;", fromDate, toDate.AddDays(-1));
        }

        public DataTable SalesReturnDetail(DateTime fromDate, DateTime toDate)
        {
            return Fill(@"
SELECT sr.returned_at AS 日期,
       sr.return_no AS 销售退货单号,
       sr.source_order_no_snapshot AS 原销售单号,
       sri.isbn_snapshot AS ISBN,
       sri.title_snapshot AS 书名,
       sri.quantity AS 退货数量,
       ROUND(sri.unit_price_cent / 100.0, 2) AS 成交单价,
       ROUND(sri.line_total_cent / 100.0, 2) AS 退款金额,
       CASE WHEN TRIM(sr.refund_method)='' THEN '未记录' ELSE sr.refund_method END AS 退款方式
FROM sales_returns sr
JOIN sales_return_items sri ON sri.sales_return_id = sr.id
WHERE sr.returned_at >= @from AND sr.returned_at < @to
ORDER BY sr.returned_at, sr.id, sri.id;", fromDate, toDate);
        }

        public DataTable PurchaseReturnDetail(DateTime fromDate, DateTime toDate)
        {
            return Fill(@"
SELECT pr.returned_at AS 日期,
       pr.return_no AS 采购退货单号,
       pr.source_order_no_snapshot AS 原采购单号,
       pr.supplier_name_snapshot AS 供应商,
       pri.isbn_snapshot AS ISBN,
       pri.title_snapshot AS 书名,
       pri.quantity AS 退货数量,
       ROUND(pri.unit_cost_cent / 100.0, 2) AS 原进价,
       ROUND(pri.line_total_cent / 100.0, 2) AS 退货金额
FROM purchase_returns pr
JOIN purchase_return_items pri ON pri.purchase_return_id = pr.id
WHERE pr.returned_at >= @from AND pr.returned_at < @to
ORDER BY pr.returned_at, pr.id, pri.id;", fromDate, toDate);
        }

        public DataTable InventoryMovements(DateTime fromDate, DateTime toDate)
        {
            return Fill(@"
SELECT it.occurred_at AS 日期,
       it.reference_no AS 单号,
       CASE it.type
         WHEN 'PURCHASE' THEN '采购入库'
         WHEN 'SALE' THEN '销售出库'
         WHEN 'SALE_RETURN' THEN '销售退货入库'
         WHEN 'PURCHASE_RETURN' THEN '采购退货出库'
         WHEN 'PURCHASE_UNREVIEW' THEN '采购反复核'
         WHEN 'ADJUSTMENT' THEN '库存调整'
         ELSE it.type
       END AS 类型,
       it.isbn_snapshot AS ISBN,
       it.title_snapshot AS 书名,
       it.quantity AS 数量变化,
       it.note AS 备注
FROM inventory_transactions it
WHERE it.occurred_at >= @from AND it.occurred_at < @to
ORDER BY it.occurred_at, it.id;", fromDate, toDate);
        }

        public DataTable InventorySnapshot(DateTime date)
        {
            var endExclusive = date.Date.AddDays(1);
            var table = new DataTable();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT b.self_code AS 店内编码,
       b.isbn AS ISBN,
       b.title AS 书名,
       b.author AS 作者,
       b.publisher AS 出版社,
       b.category AS 分类,
       b.shelf_code AS 货架位,
       COALESCE(SUM(it.quantity), 0) AS 库存数量
FROM books b
LEFT JOIN inventory_transactions it
       ON it.book_id = b.id
      AND it.occurred_at < @endExclusive
WHERE b.created_at < @endExclusive
GROUP BY b.id, b.self_code, b.isbn, b.title, b.author, b.publisher, b.category, b.shelf_code
ORDER BY b.title, b.id;";
                command.Parameters.AddWithValue("@endExclusive", FormatDate(endExclusive));
                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }
            return table;
        }

        private DataTable Fill(string sql, DateTime fromDate, DateTime toDate)
        {
            if (toDate.Date < fromDate.Date)
                throw new InvalidOperationException("结束日期不能早于开始日期。");

            var table = new DataTable();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@from", FormatDate(fromDate.Date));
                command.Parameters.AddWithValue("@to", FormatDate(toDate.Date.AddDays(1)));
                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }
            return table;
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
