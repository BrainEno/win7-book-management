namespace Win7BookManagement.Models
{
    public sealed class Book
    {
        public long Id { get; set; }
        public string Isbn { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string Category { get; set; }
        public long ListPriceCent { get; set; }
        public long SalePriceCent { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }

        public Book()
        {
            Isbn = "";
            Title = "";
            Author = "";
            Publisher = "";
            Category = "";
            IsActive = true;
        }
    }
}
