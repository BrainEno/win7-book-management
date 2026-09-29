namespace Win7BookManagement.Models
{
    public sealed class DashboardSummary
    {
        public int ActiveTitles { get; set; }
        public int StockUnits { get; set; }
        public int LowStockTitles { get; set; }
        public int TodaySalesOrders { get; set; }
        public int TodaySalesQuantity { get; set; }
        public long TodaySalesCent { get; set; }
        public long MonthSalesCent { get; set; }
    }
}
