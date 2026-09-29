using System;
using System.IO;

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
    isbn TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL,
    author TEXT NOT NULL DEFAULT '',
    publisher TEXT NOT NULL DEFAULT '',
    category TEXT NOT NULL DEFAULT '',
    list_price_cent INTEGER NOT NULL DEFAULT 0 CHECK(list_price_cent >= 0),
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
";
                    command.ExecuteNonQuery();

                    using (var version = connection.CreateCommand())
                    {
                        version.Transaction = transaction;
                        version.CommandText = @"
INSERT INTO schema_info(version)
SELECT 2 WHERE NOT EXISTS (SELECT 1 FROM schema_info);

UPDATE schema_info
SET version = 2
WHERE version < 2;

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
    }
}
