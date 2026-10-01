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

        public void EnsureCreated()
        {
            var isNew = !File.Exists(_factory.DatabasePath);

            using (var connection = _factory.Open())
            {
                using (var pragma = connection.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    pragma.ExecuteNonQuery();
                }

                using (var transaction = connection.BeginTransaction())
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
    note TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sales_order_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_order_id INTEGER NOT NULL,
    book_id INTEGER NOT NULL,
    isbn_snapshot TEXT NOT NULL DEFAULT '',
    title_snapshot TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK(quantity > 0),
    unit_price_cent INTEGER NOT NULL CHECK(unit_price_cent >= 0),
    line_total_cent INTEGER NOT NULL CHECK(line_total_cent >= 0),
    FOREIGN KEY(sales_order_id) REFERENCES sales_orders(id),
    FOREIGN KEY(book_id) REFERENCES books(id)
);

CREATE TABLE IF NOT EXISTS sales_returns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    return_no TEXT NOT NULL UNIQUE,
    source_sales_order_id INTEGER NOT NULL,
    source_order_no_snapshot TEXT NOT NULL,
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

CREATE INDEX IF NOT EXISTS ix_inventory_book_time
ON inventory_transactions(book_id, occurred_at);

CREATE INDEX IF NOT EXISTS ix_sales_orders_sold_at
ON sales_orders(sold_at);

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

                    using (var version = connection.CreateCommand())
                    {
                        version.Transaction = transaction;
                        version.CommandText = @"
INSERT INTO schema_info(version)
SELECT 5 WHERE NOT EXISTS (SELECT 1 FROM schema_info);

UPDATE schema_info
SET version = 5
WHERE version < 5;

INSERT OR IGNORE INTO app_settings(key, value, updated_at)
VALUES('low_stock_threshold', '3', @now);";
                        version.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        version.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            if (isNew && !File.Exists(_factory.DatabasePath))
                throw new InvalidOperationException("数据库创建失败。");
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
