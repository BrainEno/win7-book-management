using Win7BookManagement.Database;
using Win7BookManagement.Reporting;
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
            Dictionaries = new DictionaryRepository(Database);
            Purchases = new PurchaseService(Database);
            Sales = new SalesService(Database);
            Returns = new ReturnService(Database);
            Inventory = new InventoryService(Database);
            Reports = new ReportRepository(Database);
            Documents = new DocumentRepository(Database);
            Dashboard = new DashboardRepository(Database);
            Settings = new SettingsRepository(Database);
            Excel = new ExcelReportExporter();
            Backup = new BackupService(Database);
        }

        public DatabaseConnectionFactory Database { get; private set; }
        public BookRepository Books { get; private set; }
        public SupplierRepository Suppliers { get; private set; }
        public DictionaryRepository Dictionaries { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public SalesService Sales { get; private set; }
        public ReturnService Returns { get; private set; }
        public InventoryService Inventory { get; private set; }
        public ReportRepository Reports { get; private set; }
        public DocumentRepository Documents { get; private set; }
        public DashboardRepository Dashboard { get; private set; }
        public SettingsRepository Settings { get; private set; }
        public ExcelReportExporter Excel { get; private set; }
        public BackupService Backup { get; private set; }
    }
}
