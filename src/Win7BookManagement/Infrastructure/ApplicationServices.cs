using Win7BookManagement.Database;
using Win7BookManagement.Repositories;
using Win7BookManagement.Services;

namespace Win7BookManagement.Infrastructure
{
    public sealed class ApplicationServices
    {
        public ApplicationServices(string databasePath)
        {
            Database = new DatabaseConnectionFactory(databasePath);
            new DatabaseInitializer(Database).EnsureCreated();

            Books = new BookRepository(Database);
            Suppliers = new SupplierRepository(Database);
            Purchases = new PurchaseService(Database);
            Sales = new SalesService(Database);
            Inventory = new InventoryService(Database);
        }

        public DatabaseConnectionFactory Database { get; private set; }
        public BookRepository Books { get; private set; }
        public SupplierRepository Suppliers { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public SalesService Sales { get; private set; }
        public InventoryService Inventory { get; private set; }
    }
}
