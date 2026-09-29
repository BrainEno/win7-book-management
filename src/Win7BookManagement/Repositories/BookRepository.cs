using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Repositories
{
    public sealed class BookRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public BookRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public IList<Book> Search(string keyword, bool includeInactive)
        {
            var result = new List<Book>();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                var term = (keyword ?? "").Trim();
                command.CommandText = @"
SELECT id, isbn, title, author, publisher, category,
       list_price_cent, sale_price_cent, stock_quantity, is_active
FROM books
WHERE (@includeInactive = 1 OR is_active = 1)
  AND (
       @term = ''
       OR isbn LIKE @like
       OR title LIKE @like
       OR author LIKE @like
       OR publisher LIKE @like
       OR category LIKE @like
  )
ORDER BY title, id;";
                command.Parameters.AddWithValue("@includeInactive", includeInactive ? 1 : 0);
                command.Parameters.AddWithValue("@term", term);
                command.Parameters.AddWithValue("@like", "%" + term + "%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(ReadBook(reader));
                }
            }
            return result;
        }

        public Book GetById(long id)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, isbn, title, author, publisher, category,
       list_price_cent, sale_price_cent, stock_quantity, is_active
FROM books WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? ReadBook(reader) : null;
            }
        }

        public Book FindByExactIsbn(string isbn)
        {
            var normalized = (isbn ?? "").Trim();
            if (normalized.Length == 0) return null;

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, isbn, title, author, publisher, category,
       list_price_cent, sale_price_cent, stock_quantity, is_active
FROM books WHERE isbn = @isbn AND is_active = 1 LIMIT 1;";
                command.Parameters.AddWithValue("@isbn", normalized);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? ReadBook(reader) : null;
            }
        }

        public long Insert(Book book)
        {
            Validate(book);
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO books
(isbn, title, author, publisher, category, list_price_cent, sale_price_cent,
 stock_quantity, is_active, created_at, updated_at)
VALUES
(@isbn, @title, @author, @publisher, @category, @listPrice, @salePrice,
 0, @isActive, @now, @now);
SELECT last_insert_rowid();";
                AddBookParameters(command, book);
                command.Parameters.AddWithValue("@now", now);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public void Update(Book book)
        {
            Validate(book);
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE books SET
    isbn=@isbn, title=@title, author=@author, publisher=@publisher,
    category=@category, list_price_cent=@listPrice, sale_price_cent=@salePrice,
    is_active=@isActive, updated_at=@updatedAt
WHERE id=@id;";
                AddBookParameters(command, book);
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@id", book.Id);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("未找到要修改的图书。");
            }
        }

        private static void Validate(Book book)
        {
            if (book == null) throw new ArgumentNullException("book");
            if (string.IsNullOrWhiteSpace(book.Title)) throw new InvalidOperationException("书名不能为空。");
            if (book.ListPriceCent < 0 || book.SalePriceCent < 0) throw new InvalidOperationException("价格不能为负数。");
        }

        private static void AddBookParameters(SQLiteCommand command, Book book)
        {
            command.Parameters.AddWithValue("@isbn", (book.Isbn ?? "").Trim());
            command.Parameters.AddWithValue("@title", (book.Title ?? "").Trim());
            command.Parameters.AddWithValue("@author", (book.Author ?? "").Trim());
            command.Parameters.AddWithValue("@publisher", (book.Publisher ?? "").Trim());
            command.Parameters.AddWithValue("@category", (book.Category ?? "").Trim());
            command.Parameters.AddWithValue("@listPrice", book.ListPriceCent);
            command.Parameters.AddWithValue("@salePrice", book.SalePriceCent);
            command.Parameters.AddWithValue("@isActive", book.IsActive ? 1 : 0);
        }

        private static Book ReadBook(SQLiteDataReader reader)
        {
            return new Book
            {
                Id = Convert.ToInt64(reader["id"]),
                Isbn = Convert.ToString(reader["isbn"]),
                Title = Convert.ToString(reader["title"]),
                Author = Convert.ToString(reader["author"]),
                Publisher = Convert.ToString(reader["publisher"]),
                Category = Convert.ToString(reader["category"]),
                ListPriceCent = Convert.ToInt64(reader["list_price_cent"]),
                SalePriceCent = Convert.ToInt64(reader["sale_price_cent"]),
                StockQuantity = Convert.ToInt32(reader["stock_quantity"]),
                IsActive = Convert.ToInt32(reader["is_active"]) == 1
            };
        }
    }
}
