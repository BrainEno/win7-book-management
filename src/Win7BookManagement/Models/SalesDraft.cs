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
        public IList<SalesDraftLine> Lines { get; private set; }

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
        public int OrderDiscountBasisPoints { get; set; }
        public int ItemCount { get; set; }
        public int QuantityTotal { get; set; }
        public long TotalCent { get; set; }
        public DateTime UpdatedAt { get; set; }

        public SalesDraftSummary()
        {
            DraftNo = "";
            Note = "";
            OrderDiscountBasisPoints = 10000;
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
            DiscountBasisPoints = 10000;
            IsActive = true;
        }
    }
}
