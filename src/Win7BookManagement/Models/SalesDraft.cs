using System;
using System.Collections.Generic;

namespace Win7BookManagement.Models
{
    public sealed class SalesDraft
    {
        public long Id { get; set; }
        public string DraftNo { get; set; }
        public string Note { get; set; }
        public int OrderDiscountBasisPoints { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IList<SalesDraftLine> Lines { get; set; }

        public SalesDraft()
        {
            DraftNo = "";
            Note = "";
            OrderDiscountBasisPoints = 10000;
            Lines = new List<SalesDraftLine>();
        }
    }

    public sealed class SalesDraftSummary
    {
        public long Id { get; set; }
        public string DraftNo { get; set; }
        public string Note { get; set; }
        public int ItemCount { get; set; }
        public int QuantityTotal { get; set; }
        public DateTime UpdatedAt { get; set; }

        public SalesDraftSummary()
        {
            DraftNo = "";
            Note = "";
        }
    }

    public sealed class SalesDraftLine
    {
        public long BookId { get; set; }
        public string SelfCode { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public int Quantity { get; set; }
        public long BaseUnitPriceCent { get; set; }
        public int DiscountBasisPoints { get; set; }

        public SalesDraftLine()
        {
            SelfCode = "";
            Isbn = "";
            Title = "";
            Author = "";
            IsActive = true;
            DiscountBasisPoints = 10000;
        }
    }
}
