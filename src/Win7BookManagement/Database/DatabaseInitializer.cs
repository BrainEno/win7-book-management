using System;
using System.IO;
using System.Data.SQLite;

namespace Win7BookManagement.Database
{
    public sealed class DatabaseInitializer
    {
        private readonly DatabaseConnectionFactory _factory;

        public DatabaseInitializer(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public const int SupportedVersion = 11;
        public string LastMigrationBackupPath { get; private set; }

        private sealed class SchemaMigration
        {
            public readonly int Version;
            public readonly string Name;
            public readonly Action<SQLiteConnection, SQLiteTransaction> Apply;
            public SchemaMigration(int version, string name, Action<SQLiteConnection, SQLiteTransaction> apply)
            { Version = version; Name = name; Apply = apply; }
        }

        // Append-only: never edit a released migration. Each step and its version
        // update share one transaction. The legacy baseline is deliberately
        // idempotent because historical releases recorded 10 before v4-v9 too.
        private static readonly SchemaMigration[] Migrations =
        {
            new SchemaMigration(10, "legacy-baseline", ApplyBaseline),
            new SchemaMigration(11, "stocktake-documents", ApplyStocktake)
        };

        public void EnsureCreated()
        {
            var current = GetDatabaseVersion(_factory);
            if (current > SupportedVersion)
                throw new InvalidOperationException(
                    "这个数据库由较新版本的 BOOK DESK 创建（数据库版本 " + current +
                    "，本程序支持 " + SupportedVersion + "）。请先升级软件，勿用旧版本继续写入。");

            LastMigrationBackupPath = null;
            if (current < SupportedVersion && File.Exists(_factory.DatabasePath) &&
                new FileInfo(_factory.DatabasePath).Length > 0)
            {
                var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_factory.DatabasePath)), "migration-safety");
                LastMigrationBackupPath = new Services.BackupService(_factory).CreateMigrationBackup(directory);
            }

            try
            {
                using (var connection = _factory.Open())
                {
                    // A second process may have upgraded the file while we waited.
                    current = ReadVersion(connection);
                    if (current > SupportedVersion)
                        throw new InvalidOperationException("数据库版本高于本程序支持版本，请升级 BOOK DESK。");
                    foreach (var migration in Migrations)
                    {
                        if (migration.Version <= current) continue;
                        using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                        {
                            try
                            {
                                migration.Apply(connection, transaction);
                                WriteVersion(connection, transaction, migration.Version);
                                using (var history = connection.CreateCommand())
                                {
                                    history.Transaction = transaction;
                                    history.CommandText = @"INSERT INTO schema_migration_history(version, name, applied_at)
VALUES(@version, @name, @at);";
                                    history.Parameters.AddWithValue("@version", migration.Version);
                                    history.Parameters.AddWithValue("@name", migration.Name);
                                    history.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("o"));
                                    history.ExecuteNonQuery();
                                }
                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var safety = string.IsNullOrEmpty(LastMigrationBackupPath)
                    ? "数据库尚未修改，或本次事务已回滚。"
                    : "本次迁移事务已回滚；迁移前快照保存在：" + LastMigrationBackupPath;
                throw new InvalidOperationException("数据库升级未完成，请停止录入并联系维护人员。" + safety, ex);
            }
        }

        public static int GetDatabaseVersion(DatabaseConnectionFactory factory)
        {
            if (!File.Exists(factory.DatabasePath) || new FileInfo(factory.DatabasePath).Length == 0) return 0;
            using (var connection = factory.OpenReadOnly())
                return ReadVersion(connection);
        }

        private static int ReadVersion(SQLiteConnection connection)
        {
            int schema = 0;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_info';";
                if (Convert.ToInt64(command.ExecuteScalar()) > 0)
                {
                    command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_info;";
                    schema = Convert.ToInt32(command.ExecuteScalar());
                }
                command.CommandText = "PRAGMA user_version;";
                return Math.Max(schema, Convert.ToInt32(command.ExecuteScalar()));
            }
        }

        private static void WriteVersion(SQLiteConnection connection, SQLiteTransaction transaction, int version)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"CREATE TABLE IF NOT EXISTS schema_migration_history(
    version INTEGER PRIMARY KEY, name TEXT NOT NULL, applied_at TEXT NOT NULL);
UPDATE schema_info SET version=@version;
INSERT INTO schema_info(version) SELECT @version WHERE NOT EXISTS(SELECT 1 FROM schema_info);";
                command.Parameters.AddWithValue("@version", version);
                command.ExecuteNonQuery();
                // user_version is part of SQLite's transactional database header.
                command.CommandText = "PRAGMA user_version=" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
                command.ExecuteNonQuery();
            }
        }

        private static void ApplyBaseline(SQLiteConnection connection, SQLiteTransaction transaction)
        {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_info (
    version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS books (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    self_code TEXT NOT NULL DEFAULT '',
    isbn TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL,
    author TEXT NOT NULL DEFAULT '',
    publisher TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL DEFAULT '',
    publication_year TEXT NOT NULL DEFAULT '',
    edition TEXT NOT NULL DEFAULT '',
    binding TEXT NOT NULL DEFAULT '',
    shelf_code TEXT NOT NULL DEFAULT '',
    note TEXT NOT NULL DEFAULT '',
    list_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(list_price_cent >= 0),
    default_purchase_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(default_purchase_price_cent >= 0),
    sale_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(sale_price_cent >= 0),
    stock_quantity INTEGER NOT NULL DEFAULT 0 CHECK(stock_quantity >= 0),
    is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0, 1)),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_books_isbn_nonempty
ON books(isbn) WHERE isbn <> '';

CREATE TABLE IF NOT EXISTS suppliers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    contact_name TEXT NOT NULL DEFAULT '',
    phone TEXT NOT NULL DEFAULT '',
    note TEXT NOT NULL DEFAULT '',
    is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0, 1)),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_suppliers_name
ON suppliers(name);

CREATE TABLE IF NOT EXISTS purchase_orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_no TEXT NOT NULL UNIQUE,
    supplier_id INTEGER NULL,
    supplier_name_snapshot TEXT NOT NULL DEFAULT '',
    purchased_at TEXT NOT NULL,
    subtotal_cent INTEGER NOT NULL DEFAULT 0 CHECK(subtotal_cent >= 0),
    line_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discount_cent >= 0),
    order_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000),
    order_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(order_discount_cent >= 0),
    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'reviewed',
    reviewed_at TEXT NULL,
    updated_at TEXT NOT NULL DEFAULT '',
    FOREIGN KEY(supplier_id) REFERENCES suppliers(id)
);

CREATE TABLE IF NOT EXISTS purchase_order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    purchase_order_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    base_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_cost_cent >= 0),
    line_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000),
    line_discounted_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discounted_unit_cost_cent >= 0),
    unit_cost_cent INTEGER NOT NULL CHECK(unit_cost_cent >= 0),
    line_total_cent INTEGER NOT NULL CHECK(line_total_cent >= 0),
    FOREIGN KEY(purchase_order_id) REFERENCES purchase_orders(id),
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS sales_orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_no TEXT NOT NULL UNIQUE,
    sold_at TEXT NOT NULL,
    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),
    payment_method TEXT NOT NULL DEFAULT '',
    amount_received_cent INTEGER NOT NULL DEFAULT 0 CHECK(amount_received_cent >= 0),
    change_cent INTEGER NOT NULL DEFAULT 0 CHECK(change_cent >= 0),
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales_order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_order_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    self_code_snapshot TEXT NOT NULL DEFAULT '',
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    author_snapshot TEXT NOT NULL DEFAULT '',
    publisher_snapshot TEXT NOT NULL DEFAULT '',
    category_snapshot TEXT NOT NULL DEFAULT '',
    publication_year_snapshot TEXT NOT NULL DEFAULT '',
    list_price_snapshot_cent INTEGER NOT NULL DEFAULT 0 CHECK(list_price_snapshot_cent >= 0),
    cost_ref_snapshot_cent INTEGER NOT NULL DEFAULT 0 CHECK(cost_ref_snapshot_cent >= 0),
    cost_ref_source_snapshot TEXT NOT NULL DEFAULT '',
    supplier_snapshot TEXT NOT NULL DEFAULT '',
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    unit_price_cent INTEGER NOT NULL CHECK(unit_price_cent >= 0),
    line_total_cent INTEGER NOT NULL CHECK(line_total_cent >= 0),
    FOREIGN KEY(sales_order_id) REFERENCES sales_orders(id),
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS sales_drafts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    draft_no TEXT NOT NULL UNIQUE,
    note TEXT NOT NULL DEFAULT '',
    order_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales_draft_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_draft_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    self_code_snapshot TEXT NOT NULL DEFAULT '',
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    author_snapshot TEXT NOT NULL DEFAULT '',
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    base_unit_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_price_cent >= 0),
    line_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000),
    FOREIGN KEY(sales_draft_id) REFERENCES sales_drafts(id) ON DELETE CASCADE,
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS sales_returns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    return_no TEXT NOT NULL UNIQUE,
    source_sales_order_id INTEGER NOT NULL,
    source_order_no_snapshot TEXT NOT NULL,
    refund_method TEXT NOT NULL DEFAULT '',
    returned_at TEXT NOT NULL,
    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    FOREIGN KEY(source_sales_order_id) REFERENCES sales_orders(id)
);

CREATE TABLE IF NOT EXISTS sales_return_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_return_id INTEGER NOT NULL,
    source_sales_order_item_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    unit_price_cent INTEGER NOT NULL CHECK(unit_price_cent >= 0),
    line_total_cent INTEGER NOT NULL CHECK(line_total_cent >= 0),
    FOREIGN KEY(sales_return_id) REFERENCES sales_returns(id),
    FOREIGN KEY(source_sales_order_item_id) REFERENCES sales_order_items(id),
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS purchase_returns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    return_no TEXT NOT NULL UNIQUE,
    source_purchase_order_id INTEGER NOT NULL,
    source_order_no_snapshot TEXT NOT NULL,
    supplier_name_snapshot TEXT NOT NULL DEFAULT '',
    returned_at TEXT NOT NULL,
    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    FOREIGN KEY(source_purchase_order_id) REFERENCES purchase_orders(id)
);

CREATE TABLE IF NOT EXISTS purchase_return_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    purchase_return_id INTEGER NOT NULL,
    source_purchase_order_item_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    unit_cost_cent INTEGER NOT NULL CHECK(unit_cost_cent >= 0),
    line_total_cent INTEGER NOT NULL CHECK(line_total_cent >= 0),
    FOREIGN KEY(purchase_return_id) REFERENCES purchase_returns(id),
    FOREIGN KEY(source_purchase_order_item_id) REFERENCES purchase_order_items(id),
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS inventory_transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    book_id INTEGER NOT NULL,
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    type TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity <> 0),
    reference_type TEXT NOT NULL DEFAULT '',
    reference_id INTEGER NULL,
    reference_no TEXT NOT NULL DEFAULT '',
    occurred_at TEXT NOT NULL,
    note TEXT NOT NULL DEFAULT '',
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS dictionary_values (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    dictionary_key TEXT NOT NULL,
    value TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    note TEXT NOT NULL DEFAULT '',
    is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0, 1)),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_dictionary_values_key_value
ON dictionary_values(dictionary_key, value COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS ix_dictionary_values_key_active_sort
ON dictionary_values(dictionary_key, is_active, sort_order, value);

CREATE INDEX IF NOT EXISTS ix_inventory_book_time
ON inventory_transactions(book_id, occurred_at);

CREATE INDEX IF NOT EXISTS ix_sales_orders_sold_at
ON sales_orders(sold_at);

CREATE INDEX IF NOT EXISTS ix_sales_drafts_updated_at
ON sales_drafts(updated_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS ix_sales_draft_items_draft
ON sales_draft_items(sales_draft_id, id);

CREATE INDEX IF NOT EXISTS ix_purchase_orders_purchased_at
ON purchase_orders(purchased_at);

CREATE INDEX IF NOT EXISTS ix_sales_returns_returned_at
ON sales_returns(returned_at);

CREATE INDEX IF NOT EXISTS ix_sales_return_items_source
ON sales_return_items(source_sales_order_item_id);

CREATE INDEX IF NOT EXISTS ix_purchase_returns_returned_at
ON purchase_returns(returned_at);

CREATE INDEX IF NOT EXISTS ix_purchase_return_items_source
ON purchase_return_items(source_purchase_order_item_id);
";
                    command.ExecuteNonQuery();

                    EnsureBookMetadataColumns(connection, transaction);
                    EnsurePurchaseWorkflowColumns(connection, transaction);
                    EnsurePurchaseDiscountColumns(connection, transaction);
                    EnsureSalesDiscountColumns(connection, transaction);
                    EnsureSalesPaymentColumns(connection, transaction);
                    EnsureSalesReportingColumns(connection, transaction);
                    EnsureSalesDraftSchema(connection, transaction);
                    EnsureDictionarySchema(connection, transaction);

                    using (var bookIndexes = connection.CreateCommand())
                    {
                        bookIndexes.Transaction = transaction;
                        bookIndexes.CommandText = @"
CREATE UNIQUE INDEX IF NOT EXISTS ux_books_self_code_nonempty
ON books(self_code) WHERE self_code <> '';

CREATE INDEX IF NOT EXISTS ix_purchase_orders_status_date
ON purchase_orders(status, purchased_at, id);";
                        bookIndexes.ExecuteNonQuery();
                    }

                    using (var normalizePurchases = connection.CreateCommand())
                    {
                        normalizePurchases.Transaction = transaction;
                        normalizePurchases.CommandText = @"
UPDATE purchase_orders
SET status='reviewed'
WHERE status IS NULL OR trim(status)='';

UPDATE purchase_orders
SET reviewed_at=purchased_at
WHERE status='reviewed'
  AND (reviewed_at IS NULL OR trim(reviewed_at)='');

UPDATE purchase_orders
SET updated_at=created_at
WHERE updated_at IS NULL OR trim(updated_at)='';";
                        normalizePurchases.ExecuteNonQuery();
                    }
                }
            using (var defaults = connection.CreateCommand())
            {
                defaults.Transaction = transaction;
                defaults.CommandText = @"INSERT OR IGNORE INTO app_settings(key, value, updated_at)
VALUES('low_stock_threshold', '3', @now);";
                defaults.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                defaults.ExecuteNonQuery();
            }
        }

        private static void ApplyStocktake(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"CREATE TABLE stock_counts(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    count_no TEXT NOT NULL UNIQUE,
    shelf_code TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL CHECK(status IN ('draft','posted')),
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    posted_at TEXT NULL);
CREATE TABLE stock_count_items(
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    count_id INTEGER NOT NULL REFERENCES stock_counts(id),
    book_id INTEGER NOT NULL REFERENCES books(id),
    expected_quantity INTEGER NOT NULL CHECK(expected_quantity >= 0),
    actual_quantity INTEGER NULL CHECK(actual_quantity >= 0),
    UNIQUE(count_id, book_id));
CREATE INDEX ix_stock_counts_status_date ON stock_counts(status, created_at);";
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureBookMetadataColumns(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "books", "self_code",
                "ALTER TABLE books ADD COLUMN self_code TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "publication_year",
                "ALTER TABLE books ADD COLUMN publication_year TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "edition",
                "ALTER TABLE books ADD COLUMN edition TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "binding",
                "ALTER TABLE books ADD COLUMN binding TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "shelf_code",
                "ALTER TABLE books ADD COLUMN shelf_code TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "note",
                "ALTER TABLE books ADD COLUMN note TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "books", "default_purchase_price_cent",
                "ALTER TABLE books ADD COLUMN default_purchase_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(default_purchase_price_cent >= 0);");
        }

        private static void EnsurePurchaseWorkflowColumns(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "purchase_orders", "status",
                "ALTER TABLE purchase_orders ADD COLUMN status TEXT NOT NULL DEFAULT 'reviewed';");
            EnsureColumn(connection, transaction, "purchase_orders", "reviewed_at",
                "ALTER TABLE purchase_orders ADD COLUMN reviewed_at TEXT NULL;");
            EnsureColumn(connection, transaction, "purchase_orders", "updated_at",
                "ALTER TABLE purchase_orders ADD COLUMN updated_at TEXT NOT NULL DEFAULT '';");
        }

        private static void EnsurePurchaseDiscountColumns(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "purchase_orders", "subtotal_cent",
                "ALTER TABLE purchase_orders ADD COLUMN subtotal_cent INTEGER NOT NULL DEFAULT 0 CHECK(subtotal_cent >= 0);");
            EnsureColumn(connection, transaction, "purchase_orders", "line_discount_cent",
                "ALTER TABLE purchase_orders ADD COLUMN line_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discount_cent >= 0);");
            EnsureColumn(connection, transaction, "purchase_orders", "order_discount_basis_points",
                "ALTER TABLE purchase_orders ADD COLUMN order_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000);");
            EnsureColumn(connection, transaction, "purchase_orders", "order_discount_cent",
                "ALTER TABLE purchase_orders ADD COLUMN order_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(order_discount_cent >= 0);");

            EnsureColumn(connection, transaction, "purchase_order_items", "base_unit_cost_cent",
                "ALTER TABLE purchase_order_items ADD COLUMN base_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_cost_cent >= 0);");
            EnsureColumn(connection, transaction, "purchase_order_items", "line_discount_basis_points",
                "ALTER TABLE purchase_order_items ADD COLUMN line_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000);");
            EnsureColumn(connection, transaction, "purchase_order_items", "line_discounted_unit_cost_cent",
                "ALTER TABLE purchase_order_items ADD COLUMN line_discounted_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discounted_unit_cost_cent >= 0);");

            using (var normalize = connection.CreateCommand())
            {
                normalize.Transaction = transaction;
                normalize.CommandText = @"
UPDATE purchase_order_items
SET base_unit_cost_cent = unit_cost_cent
WHERE base_unit_cost_cent = 0 AND unit_cost_cent > 0;

UPDATE purchase_order_items
SET line_discounted_unit_cost_cent = unit_cost_cent
WHERE line_discounted_unit_cost_cent = 0 AND unit_cost_cent > 0;

UPDATE purchase_orders
SET subtotal_cent = total_cent
WHERE subtotal_cent = 0 AND total_cent > 0;";
                normalize.ExecuteNonQuery();
            }
        }

        private static void EnsureSalesDiscountColumns(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "sales_orders", "subtotal_cent",
                "ALTER TABLE sales_orders ADD COLUMN subtotal_cent INTEGER NOT NULL DEFAULT 0 CHECK(subtotal_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_orders", "line_discount_cent",
                "ALTER TABLE sales_orders ADD COLUMN line_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discount_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_orders", "order_discount_basis_points",
                "ALTER TABLE sales_orders ADD COLUMN order_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000);");
            EnsureColumn(connection, transaction, "sales_orders", "order_discount_cent",
                "ALTER TABLE sales_orders ADD COLUMN order_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(order_discount_cent >= 0);");

            EnsureColumn(connection, transaction, "sales_order_items", "base_unit_price_cent",
                "ALTER TABLE sales_order_items ADD COLUMN base_unit_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_price_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_order_items", "line_discount_basis_points",
                "ALTER TABLE sales_order_items ADD COLUMN line_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000);");
            EnsureColumn(connection, transaction, "sales_order_items", "line_discounted_unit_price_cent",
                "ALTER TABLE sales_order_items ADD COLUMN line_discounted_unit_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discounted_unit_price_cent >= 0);");

            using (var normalize = connection.CreateCommand())
            {
                normalize.Transaction = transaction;
                normalize.CommandText = @"
UPDATE sales_order_items
SET base_unit_price_cent = unit_price_cent
WHERE base_unit_price_cent = 0 AND unit_price_cent > 0;

UPDATE sales_order_items
SET line_discounted_unit_price_cent = unit_price_cent
WHERE line_discounted_unit_price_cent = 0 AND unit_price_cent > 0;

UPDATE sales_orders
SET subtotal_cent = total_cent
WHERE subtotal_cent = 0 AND total_cent > 0;";
                normalize.ExecuteNonQuery();
            }
        }

        private static void EnsureSalesPaymentColumns(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "sales_orders", "payment_method",
                "ALTER TABLE sales_orders ADD COLUMN payment_method TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_orders", "amount_received_cent",
                "ALTER TABLE sales_orders ADD COLUMN amount_received_cent INTEGER NOT NULL DEFAULT 0 CHECK(amount_received_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_orders", "change_cent",
                "ALTER TABLE sales_orders ADD COLUMN change_cent INTEGER NOT NULL DEFAULT 0 CHECK(change_cent >= 0);");

            using (var normalize = connection.CreateCommand())
            {
                normalize.Transaction = transaction;
                normalize.CommandText = @"
UPDATE sales_orders
SET amount_received_cent = total_cent
WHERE amount_received_cent = 0 AND total_cent > 0;";
                normalize.ExecuteNonQuery();
            }
        }

        private static void EnsureSalesReportingColumns(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            EnsureColumn(connection, transaction, "sales_order_items", "self_code_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN self_code_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "author_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN author_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "publisher_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN publisher_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "category_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN category_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "publication_year_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN publication_year_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "list_price_snapshot_cent",
                "ALTER TABLE sales_order_items ADD COLUMN list_price_snapshot_cent INTEGER NOT NULL DEFAULT 0 CHECK(list_price_snapshot_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_order_items", "cost_ref_snapshot_cent",
                "ALTER TABLE sales_order_items ADD COLUMN cost_ref_snapshot_cent INTEGER NOT NULL DEFAULT 0 CHECK(cost_ref_snapshot_cent >= 0);");
            EnsureColumn(connection, transaction, "sales_order_items", "cost_ref_source_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN cost_ref_source_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_order_items", "supplier_snapshot",
                "ALTER TABLE sales_order_items ADD COLUMN supplier_snapshot TEXT NOT NULL DEFAULT '';");
            EnsureColumn(connection, transaction, "sales_returns", "refund_method",
                "ALTER TABLE sales_returns ADD COLUMN refund_method TEXT NOT NULL DEFAULT '';");

            using (var normalize = connection.CreateCommand())
            {
                normalize.Transaction = transaction;
                normalize.CommandText = @"
UPDATE sales_order_items
SET self_code_snapshot = COALESCE(
      (SELECT b.self_code FROM books b WHERE b.id=sales_order_items.book_id), ''),
    author_snapshot = COALESCE(
      (SELECT b.author FROM books b WHERE b.id=sales_order_items.book_id), ''),
    publisher_snapshot = COALESCE(
      (SELECT b.publisher FROM books b WHERE b.id=sales_order_items.book_id), ''),
    category_snapshot = COALESCE(
      (SELECT b.category FROM books b WHERE b.id=sales_order_items.book_id), ''),
    publication_year_snapshot = COALESCE(
      (SELECT b.publication_year FROM books b WHERE b.id=sales_order_items.book_id), ''),
    list_price_snapshot_cent = COALESCE(
      (SELECT b.list_price_cent FROM books b WHERE b.id=sales_order_items.book_id), 0)
WHERE cost_ref_source_snapshot='';

UPDATE sales_order_items
SET cost_ref_snapshot_cent = COALESCE(
      (
        SELECT pi.unit_cost_cent
        FROM purchase_order_items pi
        JOIN purchase_orders po ON po.id=pi.purchase_order_id
        JOIN sales_orders so ON so.id=sales_order_items.sales_order_id
        WHERE pi.book_id=sales_order_items.book_id
          AND po.status='reviewed'
          AND po.purchased_at<=so.sold_at
        ORDER BY po.purchased_at DESC, po.id DESC, pi.id DESC
        LIMIT 1
      ),
      (SELECT b.default_purchase_price_cent FROM books b WHERE b.id=sales_order_items.book_id),
      0),
    supplier_snapshot = COALESCE(
      (
        SELECT po.supplier_name_snapshot
        FROM purchase_order_items pi
        JOIN purchase_orders po ON po.id=pi.purchase_order_id
        JOIN sales_orders so ON so.id=sales_order_items.sales_order_id
        WHERE pi.book_id=sales_order_items.book_id
          AND po.status='reviewed'
          AND po.purchased_at<=so.sold_at
        ORDER BY po.purchased_at DESC, po.id DESC, pi.id DESC
        LIMIT 1
      ),
      ''),
    cost_ref_source_snapshot = CASE
      WHEN EXISTS (
        SELECT 1
        FROM purchase_order_items pi
        JOIN purchase_orders po ON po.id=pi.purchase_order_id
        JOIN sales_orders so ON so.id=sales_order_items.sales_order_id
        WHERE pi.book_id=sales_order_items.book_id
          AND po.status='reviewed'
          AND po.purchased_at<=so.sold_at
      ) THEN '最近采购价'
      WHEN COALESCE(
        (SELECT b.default_purchase_price_cent FROM books b WHERE b.id=sales_order_items.book_id), 0
      )>0 THEN '默认进价'
      ELSE '未设置'
    END
WHERE cost_ref_source_snapshot='';

UPDATE sales_returns
SET refund_method = COALESCE(
  (SELECT so.payment_method FROM sales_orders so WHERE so.id=sales_returns.source_sales_order_id),
  '')
WHERE refund_method='';";
                normalize.ExecuteNonQuery();
            }
        }

        private static void EnsureSalesDraftSchema(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            using (var schema = connection.CreateCommand())
            {
                schema.Transaction = transaction;
                schema.CommandText = @"
CREATE TABLE IF NOT EXISTS sales_drafts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    draft_no TEXT NOT NULL UNIQUE,
    note TEXT NOT NULL DEFAULT '',
    order_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales_draft_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_draft_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    self_code_snapshot TEXT NOT NULL DEFAULT '',
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    author_snapshot TEXT NOT NULL DEFAULT '',
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    base_unit_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_price_cent >= 0),
    line_discount_basis_points INTEGER NOT NULL DEFAULT 10000
        CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000),
    FOREIGN KEY(sales_draft_id) REFERENCES sales_drafts(id) ON DELETE CASCADE,
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE INDEX IF NOT EXISTS ix_sales_drafts_updated_at
ON sales_drafts(updated_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS ix_sales_draft_items_draft
ON sales_draft_items(sales_draft_id, id);";
                schema.ExecuteNonQuery();
            }
        }

        private static void EnsureDictionarySchema(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            using (var schema = connection.CreateCommand())
            {
                schema.Transaction = transaction;
                schema.CommandText = @"
CREATE TABLE IF NOT EXISTS dictionary_values (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    dictionary_key TEXT NOT NULL,
    value TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    note TEXT NOT NULL DEFAULT '',
    is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0, 1)),
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_dictionary_values_key_value
ON dictionary_values(dictionary_key, value COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS ix_dictionary_values_key_active_sort
ON dictionary_values(dictionary_key, is_active, sort_order, value);";
                schema.ExecuteNonQuery();
            }

            using (var seed = connection.CreateCommand())
            {
                seed.Transaction = transaction;
                seed.CommandText = @"
INSERT OR IGNORE INTO dictionary_values
(dictionary_key, value, sort_order, note, is_active, created_at, updated_at)
SELECT 'book_category',
       TRIM(category),
       0,
       '由现有图书资料自动导入',
       1,
       @now,
       @now
FROM books
WHERE TRIM(category) <> ''
GROUP BY TRIM(category);

INSERT OR IGNORE INTO dictionary_values
(dictionary_key, value, sort_order, note, is_active, created_at, updated_at)
VALUES
('payment_method', '微信', 10, '默认收款方式', 1, @now, @now),
('payment_method', '支付宝', 20, '默认收款方式', 1, @now, @now),
('payment_method', '现金', 30, '现金收款支持实收与找零', 1, @now, @now);";
                seed.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                seed.ExecuteNonQuery();
            }
        }

        private static void EnsureColumn(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string tableName,
            string columnName,
            string alterSql)
        {
            if (HasColumn(connection, transaction, tableName, columnName))
                return;

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = alterSql;
                command.ExecuteNonQuery();
            }
        }

        private static bool HasColumn(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string tableName,
            string columnName)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "PRAGMA table_info(" + tableName + ");";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(Convert.ToString(reader["name"]), columnName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
