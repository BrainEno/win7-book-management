using System;
using System.Collections.Generic;
using Win7BookManagement.Database;
using Win7BookManagement.Models;

namespace Win7BookManagement.Repositories
{
    public sealed class SupplierRepository
    {
        private readonly DatabaseConnectionFactory _factory;

        public SupplierRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public IList<Supplier> GetAll(bool includeInactive)
        {
            var result = new List<Supplier>();
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT id, name, contact_name, phone, note, is_active
FROM suppliers
WHERE (@all = 1 OR is_active = 1)
ORDER BY name;";
                command.Parameters.AddWithValue("@all", includeInactive ? 1 : 0);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new Supplier
                        {
                            Id = Convert.ToInt64(reader["id"]),
                            Name = Convert.ToString(reader["name"]),
                            ContactName = Convert.ToString(reader["contact_name"]),
                            Phone = Convert.ToString(reader["phone"]),
                            Note = Convert.ToString(reader["note"]),
                            IsActive = Convert.ToInt32(reader["is_active"]) == 1
                        });
                    }
                }
            }
            return result;
        }

        public long Insert(Supplier supplier)
        {
            Validate(supplier);
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO suppliers(name, contact_name, phone, note, is_active, created_at, updated_at)
VALUES(@name, @contact, @phone, @note, @active, @now, @now);
SELECT last_insert_rowid();";
                AddParameters(command, supplier);
                command.Parameters.AddWithValue("@now", now);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        public void Update(Supplier supplier)
        {
            Validate(supplier);
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE suppliers SET
 name=@name, contact_name=@contact, phone=@phone, note=@note,
 is_active=@active, updated_at=@updated
WHERE id=@id;";
                AddParameters(command, supplier);
                command.Parameters.AddWithValue("@updated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@id", supplier.Id);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("未找到要修改的供应商。");
            }
        }

        private static void Validate(Supplier supplier)
        {
            if (supplier == null) throw new ArgumentNullException("supplier");
            if (string.IsNullOrWhiteSpace(supplier.Name)) throw new InvalidOperationException("供应商名称不能为空。");
        }

        private static void AddParameters(System.Data.SQLite.SQLiteCommand command, Supplier supplier)
        {
            command.Parameters.AddWithValue("@name", (supplier.Name ?? "").Trim());
            command.Parameters.AddWithValue("@contact", (supplier.ContactName ?? "").Trim());
            command.Parameters.AddWithValue("@phone", (supplier.Phone ?? "").Trim());
            command.Parameters.AddWithValue("@note", (supplier.Note ?? "").Trim());
            command.Parameters.AddWithValue("@active", supplier.IsActive ? 1 : 0);
        }
    }
}
