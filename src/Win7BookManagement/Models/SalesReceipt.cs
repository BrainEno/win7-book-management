using System;
using System.Collections.Generic;

namespace Win7BookManagement.Models
{
    public sealed class SalesReceipt
    {
        public string StoreName { get; set; }
        public string OrderNo { get; set; }
        public DateTime SoldAt { get; set; }
        public string PaymentMethod { get; set; }
        public long SubtotalCent { get; set; }
        public long DiscountCent { get; set; }
        public long TotalCent { get; set; }
        public long ReceivedCent { get; set; }
        public long ChangeCent { get; set; }
        public string Note { get; set; }
        public IList<SalesReceiptLine> Lines { get; set; }
        public SalesReceipt() { Lines = new List<SalesReceiptLine>(); StoreName = "BOOK DESK"; OrderNo = ""; PaymentMethod = ""; Note = ""; }
    }
    public sealed class SalesReceiptLine
    {
        public string Title { get; set; }
        public int Quantity { get; set; }
        public long UnitPriceCent { get; set; }
        public long TotalCent { get; set; }
    }
}
