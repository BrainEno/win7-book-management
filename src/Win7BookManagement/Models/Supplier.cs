namespace Win7BookManagement.Models
{
    public sealed class Supplier
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string Note { get; set; }
        public bool IsActive { get; set; }

        public Supplier()
        {
            Name = "";
            ContactName = "";
            Phone = "";
            Note = "";
            IsActive = true;
        }
    }
}
