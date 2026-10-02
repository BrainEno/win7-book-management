using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Repositories
{
    public sealed class DictionaryRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public DictionaryRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public IList<DictionaryValue> Search(string dictionaryKey, string keyword, bool includeInactive)
        {
            var result = new List<DictionaryValue>();
            var key = NormalizeKey(dictionaryKey);
            var term = (keyword ?? "").Trim();

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, dictionary_key, value, sort_order, note, is_active
FROM dictionary_values
WHERE dictionary_key=@key
  AND (@includeInactive=1 OR is_active=1)
  AND (@term='' OR value LIKE @like OR note LIKE @like)
ORDER BY sort_order, value COLLATE NOCASE, id;";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@includeInactive", includeInactive ? 1 : 0);
                command.Parameters.AddWithValue("@term", term);
                command.Parameters.AddWithValue("@like", "%" + term + "%");

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Read(reader));
                }
            }

            return result;
        }

        public IList<string> GetActiveValues(string dictionaryKey)
        {
            var rows = Search(dictionaryKey, "", false);
            var result = new List<string>();
            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Value))
                    result.Add(row.Value);
            }
            return result;
        }

        public DictionaryValue GetById(long id)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, dictionary_key, value, sort_order, note, is_active
FROM dictionary_values
WHERE id=@id
LIMIT 1;";
                command.Parameters.AddWithValue("@id", id);
                using (var reader = command.ExecuteReader())
                    return reader.Read() ? Read(reader) : null;
            }
        }

        public long Insert(DictionaryValue item)
        {
            Validate(item);
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO dictionary_values
(dictionary_key, value, sort_order, note, is_active, created_at, updated_at)
VALUES(@key, @value, @sort, @note, @active, @now, @now);
SELECT last_insert_rowid();";
                AddParameters(command, item);
                command.Parameters.AddWithValue("@now", now);

                try
                {
                    return Convert.ToInt64(command.ExecuteScalar());
                }
                catch (SQLiteException ex)
                {
                    throw TranslateUniqueError(item, ex);
                }
            }
        }

        public void Update(DictionaryValue item)
        {
            if (item == null || item.Id <= 0)
                throw new InvalidOperationException("未选择要修改的字典值。");

            Validate(item);
            using (var connection = _factory.Open())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    string oldKey;
                    string oldValue;
                    using (var current = connection.CreateCommand())
                    {
                        current.Transaction = transaction;
                        current.CommandText =
                            "SELECT dictionary_key, value FROM dictionary_values WHERE id=@id LIMIT 1;";
                        current.Parameters.AddWithValue("@id", item.Id);
                        using (var reader = current.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("未找到要修改的字典值.");
                            oldKey = Convert.ToString(reader["dictionary_key"]);
                            oldValue = Convert.ToString(reader["value"]);
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
UPDATE dictionary_values
SET dictionary_key=@key,
    value=@value,
    sort_order=@sort,
    note=@note,
    is_active=@active,
    updated_at=@now
WHERE id=@id;";
                        AddParameters(command, item);
                        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        command.Parameters.AddWithValue("@id", item.Id);

                        try
                        {
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("未找到要修改的字典值。");
                        }
                        catch (SQLiteException ex)
                        {
                            throw TranslateUniqueError(item, ex);
                        }
                    }

                    if (string.Equals(oldKey, DictionaryKeys.BookCategory, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.DictionaryKey, DictionaryKeys.BookCategory, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(oldValue, item.Value, StringComparison.Ordinal))
                    {
                        using (var books = connection.CreateCommand())
                        {
                            books.Transaction = transaction;
                            books.CommandText = @"
UPDATE books
SET category=@newValue,
    updated_at=@now
WHERE category=@oldValue COLLATE NOCASE;";
                            books.Parameters.AddWithValue("@newValue", item.Value);
                            books.Parameters.AddWithValue("@oldValue", oldValue);
                            books.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            books.ExecuteNonQuery();
                        }
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

        public DictionaryValue EnsureValue(string dictionaryKey, string value)
        {
            var key = NormalizeKey(dictionaryKey);
            var normalized = (value ?? "").Trim();
            if (normalized.Length == 0)
                return null;

            using (var connection = _factory.Open())
            {
                using (var find = connection.CreateCommand())
                {
                    find.CommandText = @"
SELECT id, dictionary_key, value, sort_order, note, is_active
FROM dictionary_values
WHERE dictionary_key=@key AND value=@value COLLATE NOCASE
LIMIT 1;";
                    find.Parameters.AddWithValue("@key", key);
                    find.Parameters.AddWithValue("@value", normalized);
                    using (var reader = find.ExecuteReader())
                    {
                        if (reader.Read())
                            return Read(reader);
                    }
                }

                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                using (var insert = connection.CreateCommand())
                {
                    insert.CommandText = @"
INSERT INTO dictionary_values
(dictionary_key, value, sort_order, note, is_active, created_at, updated_at)
VALUES(@key, @value, 0, '', 1, @now, @now);
SELECT last_insert_rowid();";
                    insert.Parameters.AddWithValue("@key", key);
                    insert.Parameters.AddWithValue("@value", normalized);
                    insert.Parameters.AddWithValue("@now", now);
                    var id = Convert.ToInt64(insert.ExecuteScalar());
                    return new DictionaryValue
                    {
                        Id = id,
                        DictionaryKey = key,
                        Value = normalized,
                        IsActive = true
                    };
                }
            }
        }

        private static void Validate(DictionaryValue item)
        {
            if (item == null) throw new ArgumentNullException("item");
            item.DictionaryKey = NormalizeKey(item.DictionaryKey);
            item.Value = (item.Value ?? "").Trim();
            item.Note = (item.Note ?? "").Trim();

            if (item.Value.Length == 0)
                throw new InvalidOperationException("字典值不能为空。");
            if (item.Value.Length > 100)
                throw new InvalidOperationException("字典值不能超过 100 个字符。");
            if (item.Note.Length > 500)
                throw new InvalidOperationException("备注不能超过 500 个字符。");
        }

        private static string NormalizeKey(string dictionaryKey)
        {
            var key = (dictionaryKey ?? "").Trim();
            if (key.Length == 0)
                throw new InvalidOperationException("字典类型不能为空。");
            return key;
        }

        private static void AddParameters(SQLiteCommand command, DictionaryValue item)
        {
            command.Parameters.AddWithValue("@key", item.DictionaryKey);
            command.Parameters.AddWithValue("@value", item.Value);
            command.Parameters.AddWithValue("@sort", item.SortOrder);
            command.Parameters.AddWithValue("@note", item.Note);
            command.Parameters.AddWithValue("@active", item.IsActive ? 1 : 0);
        }

        private static Exception TranslateUniqueError(DictionaryValue item, SQLiteException ex)
        {
            if (ex != null && ex.Message != null &&
                ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new InvalidOperationException(
                    "字典值“" + item.Value + "”已经存在，请直接编辑原有条目。");
            }
            return ex;
        }

        private static DictionaryValue Read(SQLiteDataReader reader)
        {
            return new DictionaryValue
            {
                Id = Convert.ToInt64(reader["id"]),
                DictionaryKey = Convert.ToString(reader["dictionary_key"]),
                Value = Convert.ToString(reader["value"]),
                SortOrder = Convert.ToInt32(reader["sort_order"]),
                Note = Convert.ToString(reader["note"]),
                IsActive = Convert.ToInt32(reader["is_active"]) == 1
            };
        }
    }
}
