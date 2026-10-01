using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Models
{
    public sealed class Book
    {
        public long Id { get; set; }
        public string SelfCode { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string Category { get; set; }
        public string PublicationYear { get; set; }
        public string Edition { get; set; }
        public string Binding { get; set; }
        public string ShelfCode { get; set; }
        public string Note { get; set; }
        public long ListPriceCent { get; set; }
        public long DefaultPurchasePriceCent { get; set; }
        public long SalePriceCent { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }

        public decimal ListPriceYuan { get { return Money.ToYuan(ListPriceCent); } }
        public decimal DefaultPurchasePriceYuan { get { return Money.ToYuan(DefaultPurchasePriceCent); } }
        public decimal SalePriceYuan { get { return Money.ToYuan(SalePriceCent); } }

        public Book()
        {
            SelfCode = "";
            Isbn = "";
            Title = "";
            Author = "";
            Publisher = "";
            Category = "";
            PublicationYear = "";
            Edition = "";
            Binding = "";
            ShelfCode = "";
            Note = "";
            IsActive = true;
        }
    }
    public sealed class BookSearchCriteria
    {
        public string SelfCode { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string Category { get; set; }

        public BookSearchCriteria()
        {
            SelfCode = "";
            Isbn = "";
            Title = "";
            Author = "";
            Publisher = "";
            Category = "";
        }
    }

}
