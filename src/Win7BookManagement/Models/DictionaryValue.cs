namespace Win7BookManagement.Models
{
    public static class DictionaryKeys
    {
        public const string BookCategory = "book_category";\n        public const string PaymentMethod = "payment_method";
        public const string PaymentMethod = "payment_method";
    }

    public sealed class DictionaryValue
    {
        public long Id { get; set; }
        public string DictionaryKey { get; set; }
        public string Value { get; set; }
        public int SortOrder { get; set; }
        public string Note { get; set; }
        public bool IsActive { get; set; }

        public DictionaryValue()
        {
            DictionaryKey = "";
            Value = "";
            Note = "";
            IsActive = true;
        }
    }
}
