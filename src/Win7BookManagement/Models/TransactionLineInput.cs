namespace Win7BookManagement.Models
{
    public sealed class TransactionLineInput
    {
        public long BookId { get; set; }
        public int Quantity { get; set; }

        // Backward-compatible operator-entered unit amount. New purchase and
        // sales flows also preserve BaseUnitPriceCent plus discount metadata.
        public long UnitPriceCent { get; set; }

        // Shared discount metadata. 10000 basis points = 100.00%.
        // Purchase treats BaseUnitPriceCent as pre-discount unit cost; sales
        // treats it as pre-discount selling price.
        public long BaseUnitPriceCent { get; set; }
        public int DiscountBasisPoints { get; set; }

        public TransactionLineInput()
        {
            DiscountBasisPoints = 10000;
        }
    }
}
