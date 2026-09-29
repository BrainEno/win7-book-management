namespace Win7BookManagement.Models
{
    public sealed class TransactionLineInput
    {
        public long BookId { get; set; }
        public int Quantity { get; set; }
        public long UnitPriceCent { get; set; }
    }
}
