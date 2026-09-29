namespace Win7BookManagement.Models
{
    public sealed class ReturnableDocumentLine
    {
        public long SourceItemId { get; set; }
        public long BookId { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public int OriginalQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public int ReturnableQuantity { get; set; }
        public int CurrentStock { get; set; }
        public long UnitPriceCent { get; set; }
        public decimal UnitPriceYuan { get { return UnitPriceCent / 100m; } }
        public int ReturnQuantity { get; set; }

        public ReturnableDocumentLine()
        {
            Isbn = "";
            Title = "";
        }
    }
}
