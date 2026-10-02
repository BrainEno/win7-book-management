namespace Win7BookManagement.Models
{
    public sealed class TransactionLineInput
    {
        public long BookId { get; set; }
        public int Quantity { get; set; }

        // Purchase uses UnitPriceCent as the unit cost. Sales uses it as the
        // operator-entered pre-discount selling price for backward compatibility.
        public long UnitPriceCent { get; set; }

        // Sales-only discount metadata. Purchase services intentionally ignore
        // these fields.
        public long BaseUnitPriceCent { get; set; }
        public int DiscountBasisPoints { get; set; }

        public TransactionLineInput()
        {
            DiscountBasisPoints = 10000;
        }
    }
}
