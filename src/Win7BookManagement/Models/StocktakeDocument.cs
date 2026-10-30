using System;
using System.Collections.Generic;

namespace Win7BookManagement.Models
{
    public sealed class StocktakeDocument
    {
        public long Id { get; set; }
        public string CountNo { get; set; }
        public string ShelfCode { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public IList<StocktakeItem> Items { get; set; }
        public StocktakeDocument() { Items = new List<StocktakeItem>(); CountNo = ""; ShelfCode = ""; Status = "draft"; Note = ""; }
    }
    public sealed class StocktakeItem
    {
        public long BookId { get; set; }
        public string SelfCode { get; set; }
        public string Title { get; set; }
        public string ShelfCode { get; set; }
        public int ExpectedQuantity { get; set; }
        public int? ActualQuantity { get; set; }
    }
}
