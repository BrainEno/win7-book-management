using System;
using System.Data.SQLite;
using System.IO;
using Win7BookManagement.Database;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// Deterministic storage and connection invariants for the --verify command.
    /// It creates a new empty database only when the requested path does not yet exist.
    /// Existing database schemas are not upgraded as a side effect of verification.
    /// </summary>
    public static class InvariantChecks
    {
        public static int RunAndPrint(string databasePath)
        {
            var failures = 0;
            Action<bool, string, string> report = delegate(bool ok, string name, string detail)
            {
                Console.WriteLine("[" + (ok ? "PASS" : "FAIL") + "] " + name +
                    (string.IsNullOrEmpty(detail) ? string.Empty : ": " + detail));
                if (!ok) failures++;
            };

            try
            {
                AppPaths.EnsureFolders();
                if (string.IsNullOrWhiteSpace(databasePath))
                    throw new ArgumentException("数据库路径不能为空。", "databasePath");

                var factory = new DatabaseConnectionFactory(databasePath);
                report(factory.ConnectionString.IndexOf("Synchronous=Full", StringComparison.OrdinalIgnoreCase) >= 0,
                    "connection string synchronous=full", factory.ConnectionString);
                report(factory.ConnectionString.IndexOf("Journal Mode=Wal", StringComparison.OrdinalIgnoreCase) >= 0,
                    "connection string journal mode=wal", factory.ConnectionString);
                report(factory.ConnectionString.IndexOf("Foreign Keys=True", StringComparison.OrdinalIgnoreCase) >= 0,
                    "connection string foreign keys", factory.ConnectionString);

                if (!File.Exists(databasePath))
                {
                    new DatabaseInitializer(factory).EnsureCreated();
                    Console.WriteLine("[INFO] 数据库不存在，已创建空数据库用于验证。");
                }

                using (var connection = factory.Open())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA synchronous;";
                        var synchronous = Convert.ToInt32(command.ExecuteScalar());
                        report(synchronous == 2, "PRAGMA synchronous=FULL", synchronous.ToString());
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA foreign_keys;";
                        var foreignKeys = Convert.ToInt32(command.ExecuteScalar());
                        report(foreignKeys == 1, "PRAGMA foreign_keys=ON", foreignKeys.ToString());
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA journal_mode;";
                        var journal = Convert.ToString(command.ExecuteScalar());
                        report(string.Equals(journal, "wal", StringComparison.OrdinalIgnoreCase),
                            "PRAGMA journal_mode=WAL", journal);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA integrity_check;";
                        var integrity = Convert.ToString(command.ExecuteScalar());
                        report(string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase),
                            "PRAGMA integrity_check", integrity);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='books';";
                        var booksTable = Convert.ToInt32(command.ExecuteScalar()) == 1;
                        report(booksTable, "core table books exists", booksTable ? "present" : "missing");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] invariant verification: " + ex.GetType().Name + " - " + ex.Message);
                failures++;
                try { AppLog.Error("verify", "不变量验证失败", ex); }
                catch { }
            }

            Console.WriteLine("Invariant checks: " + (failures == 0 ? "PASS" : "FAIL") +
                "; failures=" + failures);
            return failures == 0 ? 0 : 1;
        }
    }
}
