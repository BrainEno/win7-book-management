using Win7BookManagement.Database;
using Win7BookManagement.Repositories;

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
        }

        public DatabaseConnectionFactory Database { get; private set; }
        public BookRepository Books { get; private set; }
        public SupplierRepository Suppliers { get; private set; }
    }
}
