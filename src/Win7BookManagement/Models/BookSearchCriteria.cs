namespace Win7BookManagement.Models
{
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
