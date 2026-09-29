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

        private const string SelectColumns = @"
id, self_code, isbn, title, author, publisher, category,
publication_year, edition, binding, shelf_code, note,
list_price_cent, default_purchase_price_cent, sale_price_cent,
stock_quantity, is_active";

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
SELECT " + SelectColumns + @"
FROM books
WHERE (@includeInactive = 1 OR is_active = 1)
  AND (
       @term = ''
       OR self_code LIKE @like
       OR isbn LIKE @like
       OR title LIKE @like
       OR author LIKE @like
       OR publisher LIKE @like
       OR category LIKE @like
       OR publication_year LIKE @like
       OR edition LIKE @like
       OR binding LIKE @like
       OR shelf_code LIKE @like
       OR note LIKE @like
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
                command.CommandText = "SELECT " + SelectColumns + " FROM books WHERE id = @id;";
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
                command.CommandText = "SELECT " + SelectColumns + " FROM books WHERE isbn = @isbn AND is_active = 1 LIMIT 1;";
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
            {
                EnsureUniqueIdentifiers(connection, book, 0);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO books
(self_code, isbn, title, author, publisher, category,
 publication_year, edition, binding, shelf_code, note,
 list_price_cent, default_purchase_price_cent, sale_price_cent,
 stock_quantity, is_active, created_at, updated_at)
VALUES
(@selfCode, @isbn, @title, @author, @publisher, @category,
 @publicationYear, @edition, @binding, @shelfCode, @note,
 @listPrice, @defaultPurchasePrice, @salePrice,
 0, @isActive, @now, @now);
SELECT last_insert_rowid();";
                    AddBookParameters(command, book);
                    command.Parameters.AddWithValue("@now", now);
                    return Convert.ToInt64(command.ExecuteScalar());
                }
            }
        }

        public void Update(Book book)
        {
            Validate(book);
            using (var connection = _factory.Open())
            {
                EnsureUniqueIdentifiers(connection, book, book.Id);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE books SET
    self_code=@selfCode,
    isbn=@isbn,
    title=@title,
    author=@author,
    publisher=@publisher,
    category=@category,
    publication_year=@publicationYear,
    edition=@edition,
    binding=@binding,
    shelf_code=@shelfCode,
    note=@note,
    list_price_cent=@listPrice,
    default_purchase_price_cent=@defaultPurchasePrice,
    sale_price_cent=@salePrice,
    is_active=@isActive,
    updated_at=@updatedAt
WHERE id=@id;";
                    AddBookParameters(command, book);
                    command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@id", book.Id);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("未找到要修改的图书。");
                }
            }
        }

        private static void Validate(Book book)
        {
            if (book == null) throw new ArgumentNullException("book");
            if (string.IsNullOrWhiteSpace(book.Title))
                throw new InvalidOperationException("书名不能为空。");
            if (book.ListPriceCent < 0 || book.DefaultPurchasePriceCent < 0 || book.SalePriceCent < 0)
                throw new InvalidOperationException("价格不能为负数。");
        }

        private static void EnsureUniqueIdentifiers(SQLiteConnection connection, Book book, long currentId)
        {
            CheckUnique(connection, "isbn", (book.Isbn ?? "").Trim(), currentId, "ISBN");
            CheckUnique(connection, "self_code", (book.SelfCode ?? "").Trim(), currentId, "店内编码");
        }

        private static void CheckUnique(SQLiteConnection connection, string column, string value, long currentId, string label)
        {
            if (value.Length == 0) return;

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(1) FROM books WHERE " + column + "=@value AND id<>@id;";
                command.Parameters.AddWithValue("@value", value);
                command.Parameters.AddWithValue("@id", currentId);
                if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                    throw new InvalidOperationException(label + "“" + value + "”已被其他图书使用，请核对后再保存。");
            }
        }

        private static void AddBookParameters(SQLiteCommand command, Book book)
        {
            command.Parameters.AddWithValue("@selfCode", (book.SelfCode ?? "").Trim());
            command.Parameters.AddWithValue("@isbn", (book.Isbn ?? "").Trim());
            command.Parameters.AddWithValue("@title", (book.Title ?? "").Trim());
            command.Parameters.AddWithValue("@author", (book.Author ?? "").Trim());
            command.Parameters.AddWithValue("@publisher", (book.Publisher ?? "").Trim());
            command.Parameters.AddWithValue("@category", (book.Category ?? "").Trim());
            command.Parameters.AddWithValue("@publicationYear", (book.PublicationYear ?? "").Trim());
            command.Parameters.AddWithValue("@edition", (book.Edition ?? "").Trim());
            command.Parameters.AddWithValue("@binding", (book.Binding ?? "").Trim());
            command.Parameters.AddWithValue("@shelfCode", (book.ShelfCode ?? "").Trim());
            command.Parameters.AddWithValue("@note", (book.Note ?? "").Trim());
            command.Parameters.AddWithValue("@listPrice", book.ListPriceCent);
            command.Parameters.AddWithValue("@defaultPurchasePrice", book.DefaultPurchasePriceCent);
            command.Parameters.AddWithValue("@salePrice", book.SalePriceCent);
            command.Parameters.AddWithValue("@isActive", book.IsActive ? 1 : 0);
        }

        private static Book ReadBook(SQLiteDataReader reader)
        {
            return new Book
            {
                Id = Convert.ToInt64(reader["id"]),
                SelfCode = Convert.ToString(reader["self_code"]),
                Isbn = Convert.ToString(reader["isbn"]),
                Title = Convert.ToString(reader["title"]),
                Author = Convert.ToString(reader["author"]),
                Publisher = Convert.ToString(reader["publisher"]),
                Category = Convert.ToString(reader["category"]),
                PublicationYear = Convert.ToString(reader["publication_year"]),
                Edition = Convert.ToString(reader["edition"]),
                Binding = Convert.ToString(reader["binding"]),
                ShelfCode = Convert.ToString(reader["shelf_code"]),
                Note = Convert.ToString(reader["note"]),
                ListPriceCent = Convert.ToInt64(reader["list_price_cent"]),
                DefaultPurchasePriceCent = Convert.ToInt64(reader["default_purchase_price_cent"]),
                SalePriceCent = Convert.ToInt64(reader["sale_price_cent"]),
                StockQuantity = Convert.ToInt32(reader["stock_quantity"]),
                IsActive = Convert.ToInt32(reader["is_active"]) == 1
            };
        }
    }
}
