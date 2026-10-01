using System;
using System.Collections.Generic;

namespace Win7BookManagement.Models
{
    public sealed class PurchaseDocument
    {
        public PurchaseDocument()
        {
            Lines = new List<PurchaseDocumentLine>();
            OrderNo = "";
            SupplierName = "";
            Status = "draft";
            Note = "";
        }

        public long Id { get; set; }
        public string OrderNo { get; set; }
        public long? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public DateTime PurchasedAt { get; set; }
        public long TotalCent { get; set; }
        public string Note { get; set; }
        public string Status { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IList<PurchaseDocumentLine> Lines { get; set; }

        public bool IsReviewed
        {
            get { return string.Equals(Status, "reviewed", StringComparison.OrdinalIgnoreCase); }
        }

        public string StatusText
        {
            get { return IsReviewed ? "已复核" : "草稿"; }
        }
    }

    public sealed class PurchaseDocumentLine
    {
        public long Id { get; set; }
        public long BookId { get; set; }
        public string SelfCode { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string ShelfCode { get; set; }
        public int CurrentStock { get; set; }
        public int Quantity { get; set; }
        public long UnitCostCent { get; set; }
        public long LineTotalCent { get; set; }
    }

    public sealed class PurchaseDocumentSummary
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }
        public string SupplierName { get; set; }
        public DateTime PurchasedAt { get; set; }
        public int Quantity { get; set; }
        public long TotalCent { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string PurchaseDate
        {
            get { return PurchasedAt.ToString("yyyy-MM-dd"); }
        }

        public decimal TotalYuan
        {
            get { return TotalCent / 100m; }
        }

        public string StatusText
        {
            get { return string.Equals(Status, "reviewed", StringComparison.OrdinalIgnoreCase) ? "已复核" : "草稿"; }
        }

        public string UpdatedAtText
        {
            get { return UpdatedAt.ToString("yyyy-MM-dd HH:mm"); }
        }
    }
}
