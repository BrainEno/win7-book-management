using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Win7BookManagement.Database;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Tests
{
    internal sealed class TemporaryDatabase : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "BookDesk-Tests-" + Guid.NewGuid().ToString("N"));
        public readonly ApplicationServices Services;
        public TemporaryDatabase() { Services = new ApplicationServices(Path.Combine(Root, "data", "test.db")); }
        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            try { Directory.Delete(Root, true); } catch (IOException) { }
        }
    }

    [TestClass]
    public class DatabaseMigrationTests
    {
        [TestMethod]
        public void FreshDatabase_RecordsRealVersion_AndIsIdempotent()
        {
            using (var db = new TemporaryDatabase())
            {
                Assert.AreEqual(DatabaseInitializer.SupportedVersion, DatabaseInitializer.GetDatabaseVersion(db.Services.Database));
                new DatabaseInitializer(db.Services.Database).EnsureCreated();
                using (var c = db.Services.Database.Open())
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA user_version;";
                    Assert.AreEqual(DatabaseInitializer.SupportedVersion, Convert.ToInt32(cmd.ExecuteScalar()));
                    cmd.CommandText = "SELECT COUNT(*) FROM schema_migration_history;";
                    Assert.AreEqual(1, Convert.ToInt32(cmd.ExecuteScalar()));
                }
            }
        }

        [TestMethod]
        public void FutureDatabase_IsRejectedBeforeWriting()
        {
            using (var db = new TemporaryDatabase())
            {
                using (var c = db.Services.Database.Open())
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "UPDATE schema_info SET version=999; PRAGMA user_version=999;";
                    cmd.ExecuteNonQuery();
                }
                SQLiteConnection.ClearAllPools();
                var path = db.Services.Database.DatabasePath;
                byte[] before;
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) before = sha.ComputeHash(stream);
                Assert.ThrowsException<InvalidOperationException>(() => new DatabaseInitializer(db.Services.Database).EnsureCreated());
                using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                    CollectionAssert.AreEqual(before, sha.ComputeHash(stream));
                Assert.IsFalse(Directory.Exists(Path.Combine(db.Root, "data", "migration-safety")));
            }
        }

        [TestMethod]
        public void FailedLegacyMigration_RollsBackAndKeepsSafetySnapshot()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = db.Services.Database.DatabasePath;
                SQLiteConnection.ClearAllPools();
                File.Delete(path);
                using (var c = new SQLiteConnection("Data Source=" + path + ";Version=3;Pooling=False;"))
                {
                    c.Open();
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = @"CREATE TABLE schema_info(version INTEGER NOT NULL); INSERT INTO schema_info VALUES(3);
CREATE TABLE books(id INTEGER PRIMARY KEY, isbn TEXT NOT NULL DEFAULT '', title TEXT NOT NULL,
 author TEXT NOT NULL DEFAULT '', publisher TEXT NOT NULL DEFAULT '', category TEXT NOT NULL DEFAULT '',
 list_price_cent INTEGER NOT NULL DEFAULT 0, sale_price_cent INTEGER NOT NULL DEFAULT 0,
 stock_quantity INTEGER NOT NULL DEFAULT 0, is_active INTEGER NOT NULL DEFAULT 1,
 created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
INSERT INTO books(isbn,title,created_at,updated_at) VALUES('duplicate','甲','2020-01-01','2020-01-01');
INSERT INTO books(isbn,title,created_at,updated_at) VALUES('duplicate','乙','2020-01-01','2020-01-01');";
                        cmd.ExecuteNonQuery();
                    }
                }
                var initializer = new DatabaseInitializer(db.Services.Database);
                Assert.ThrowsException<InvalidOperationException>(() => initializer.EnsureCreated());
                Assert.AreEqual(3, DatabaseInitializer.GetDatabaseVersion(db.Services.Database));
                Assert.IsTrue(File.Exists(initializer.LastMigrationBackupPath));
                using (var c = db.Services.Database.OpenReadOnly())
                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('books') WHERE name='self_code';";
                    Assert.AreEqual(0, Convert.ToInt32(cmd.ExecuteScalar()));
                }
            }
        }
    }

    [TestClass]
    public class BackupSafetyTests
    {
        [TestMethod]
        public void EveryPooledConnection_UsesFullSynchronous()
        {
            using (var db = new TemporaryDatabase())
            {
                var connections = Enumerable.Range(0, 4).Select(i => db.Services.Database.Open()).ToArray();
                try
                {
                    foreach (var c in connections)
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA synchronous;";
                        Assert.AreEqual(2, Convert.ToInt32(cmd.ExecuteScalar()));
                    }
                }
                finally { foreach (var c in connections) c.Dispose(); }
            }
        }

        [TestMethod]
        public void AutomaticBackup_PublishesValidatedSecondCopyAndPrunesBoth()
        {
            using (var db = new TemporaryDatabase())
            {
                db.Services.Books.Insert(new Book { Title = "备份测试", SalePriceCent = 1000 });
                var primary = Path.Combine(db.Root, "primary");
                var secondary = Path.Combine(db.Root, "secondary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, secondary);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                for (var i = 0; i < 5; i++) { db.Services.AutoBackup.CreateNow("test"); Thread.Sleep(5); }
                Assert.AreEqual(3, Directory.GetFiles(primary, "bookstore_*.db").Length);
                Assert.AreEqual(3, Directory.GetFiles(secondary, "bookstore_*.db").Length);
                Assert.IsTrue(db.Services.AutoBackup.VerifyLatestBackup().Passed);
                Assert.AreEqual(BackupHealthLevel.Ok, db.Services.AutoBackup.GetStatus().Level);
            }
        }

        [TestMethod]
        public void SecondaryFailure_IsVisibleAndDoesNotUndoPrimaryBackup()
        {
            using (var db = new TemporaryDatabase())
            {
                var blocker = Path.Combine(db.Root, "not-a-directory"); File.WriteAllText(blocker, "x");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, Path.Combine(db.Root, "primary"));
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, blocker);
                var path = db.Services.AutoBackup.CreateNow("test");
                Assert.IsTrue(File.Exists(path));
                Assert.AreEqual(BackupHealthLevel.Warning, db.Services.AutoBackup.GetStatus().Level);
            }
        }

        [TestMethod]
        public void CorruptBackup_IsNotReportedAsUsable()
        {
            using (var db = new TemporaryDatabase())
            {
                var directory = Path.Combine(db.Root, "primary"); Directory.CreateDirectory(directory);
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, directory);
                File.WriteAllBytes(Path.Combine(directory, "bookstore_corrupt.db"), new byte[] { 1, 2, 3 });
                Assert.IsFalse(db.Services.AutoBackup.VerifyLatestBackup().Passed);
            }
        }
    }
}
