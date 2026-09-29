using System;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Repositories
{
    public sealed class DashboardRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public DashboardRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public DashboardSummary GetSummary(int lowStockThreshold)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
  (SELECT COUNT(*) FROM books WHERE is_active=1) AS active_titles,
  (SELECT COALESCE(SUM(stock_quantity),0) FROM books WHERE is_active=1) AS stock_units,
  (SELECT COUNT(*) FROM books WHERE is_active=1 AND stock_quantity<=@threshold) AS low_stock_titles,
  (SELECT COUNT(*) FROM sales_orders WHERE sold_at>=@today AND sold_at<@tomorrow) AS today_orders,
  (
    (SELECT COALESCE(SUM(si.quantity),0)
       FROM sales_order_items si JOIN sales_orders so ON so.id=si.sales_order_id
      WHERE so.sold_at>=@today AND so.sold_at<@tomorrow)
    -
    (SELECT COALESCE(SUM(sri.quantity),0)
       FROM sales_return_items sri JOIN sales_returns sr ON sr.id=sri.sales_return_id
      WHERE sr.returned_at>=@today AND sr.returned_at<@tomorrow)
  ) AS today_quantity,
  (
    (SELECT COALESCE(SUM(total_cent),0) FROM sales_orders WHERE sold_at>=@today AND sold_at<@tomorrow)
    -
    (SELECT COALESCE(SUM(total_cent),0) FROM sales_returns WHERE returned_at>=@today AND returned_at<@tomorrow)
  ) AS today_sales_cent,
  (
    (SELECT COALESCE(SUM(total_cent),0) FROM sales_orders WHERE sold_at>=@monthStart AND sold_at<@tomorrow)
    -
    (SELECT COALESCE(SUM(total_cent),0) FROM sales_returns WHERE returned_at>=@monthStart AND returned_at<@tomorrow)
  ) AS month_sales_cent;";
                command.Parameters.AddWithValue("@threshold", lowStockThreshold);
                command.Parameters.AddWithValue("@today", Format(today));
                command.Parameters.AddWithValue("@tomorrow", Format(tomorrow));
                command.Parameters.AddWithValue("@monthStart", Format(monthStart));

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return new DashboardSummary();
                    return new DashboardSummary
                    {
                        ActiveTitles = Convert.ToInt32(reader["active_titles"]),
                        StockUnits = Convert.ToInt32(reader["stock_units"]),
                        LowStockTitles = Convert.ToInt32(reader["low_stock_titles"]),
                        TodaySalesOrders = Convert.ToInt32(reader["today_orders"]),
                        TodaySalesQuantity = Convert.ToInt32(reader["today_quantity"]),
                        TodaySalesCent = Convert.ToInt64(reader["today_sales_cent"]),
                        MonthSalesCent = Convert.ToInt64(reader["month_sales_cent"])
                    };
                }
            }
        }

        public DataTable RecentSales(int limit)
        {
            var table = new DataTable();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT so.sold_at AS 时间,
       so.order_no AS 销售单号,
       COALESCE(SUM(si.quantity),0) AS 册数,
       ROUND(so.total_cent / 100.0, 2) AS 金额
FROM sales_orders so
LEFT JOIN sales_order_items si ON si.sales_order_id=so.id
GROUP BY so.id, so.sold_at, so.order_no, so.total_cent
ORDER BY so.sold_at DESC, so.id DESC
LIMIT @limit;";
                command.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(limit, 100)));
                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }
            return table;
        }

        public DataTable LowStock(int threshold, int limit)
        {
            var table = new DataTable();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT isbn AS ISBN,
       title AS 书名,
       author AS 作者,
       stock_quantity AS 当前库存
FROM books
WHERE is_active=1 AND stock_quantity<=@threshold
ORDER BY stock_quantity ASC, title ASC
LIMIT @limit;";
                command.Parameters.AddWithValue("@threshold", threshold);
                command.Parameters.AddWithValue("@limit", Math.Max(1, Math.Min(limit, 200)));
                using (var adapter = new SQLiteDataAdapter(command))
                    adapter.Fill(table);
            }
            return table;
        }

        private static string Format(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
