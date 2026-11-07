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
                    Assert.IsTrue(Convert.ToInt32(cmd.ExecuteScalar()) >= 1);
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
        public void Backups_AreStandaloneCleanAndRestorableAfterRepeatedOverwrites()
        {
            using (var db = new TemporaryDatabase())
            {
                var id = db.Services.Books.Insert(new Book { Title = "独立备份测试" });
                db.Services.Inventory.Adjust(id, 7, "备份前库存");
                var path = Path.Combine(db.Root, "snapshot.db");
                for (var i = 0; i < 12; i++)
                {
                    db.Services.Backup.CreateBackup(path);
                    Assert.AreEqual(0, Directory.GetFiles(db.Root, "snapshot.db.writing-*").Length,
                        "第 " + i + " 次备份留下了临时文件。");
                    Assert.IsFalse(File.Exists(path + "-wal"));
                    Assert.IsFalse(File.Exists(path + "-shm"));
                }
                var isolatedDir = Path.Combine(db.Root, "isolated");
                Directory.CreateDirectory(isolatedDir);
                var isolated = Path.Combine(isolatedDir, "copy.db");
                File.Copy(path, isolated); // deliberately copy only the database
                BackupService.ValidateBackup(isolated);
                using (var c = new SQLiteConnection("Data Source=" + isolated + ";Version=3;Read Only=True;Pooling=False;"))
                {
                    c.Open();
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA journal_mode;";
                        Assert.AreEqual("delete", Convert.ToString(cmd.ExecuteScalar()).ToLowerInvariant());
                        cmd.CommandText = "SELECT stock_quantity FROM books WHERE title='独立备份测试';";
                        Assert.AreEqual(7, Convert.ToInt32(cmd.ExecuteScalar()));
                    }
                }
                Assert.IsFalse(File.Exists(isolated + "-wal"));
                Assert.IsFalse(File.Exists(isolated + "-shm"));
                db.Services.Inventory.Adjust(id, 4, "备份后库存");
                var safety = db.Services.Backup.Restore(isolated);
                Assert.IsTrue(File.Exists(safety));
                Assert.AreEqual(7, db.Services.Books.GetById(id).StockQuantity);
                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(db.Root, "data"), "test.db.restore-stage-*").Length);
            }
        }

        private static BackupService WithPublishedValidation(DatabaseConnectionFactory factory, Action<string> validation)
        {
            return (BackupService)Activator.CreateInstance(typeof(BackupService),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { factory, validation }, null);
        }

        [TestMethod]
        public void FailedFinalValidation_RestoresPreviousBackup()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                db.Services.Backup.CreateBackup(path);
                var original = File.ReadAllBytes(path);
                db.Services.Books.Insert(new Book { Title = "修改后数据" });
                var service = WithPublishedValidation(db.Services.Database, p => { throw new InvalidOperationException("模拟校验失败"); });
                var error = Assert.ThrowsException<InvalidOperationException>(() => service.CreateBackup(path));
                StringAssert.Contains(error.Message, "模拟校验失败");
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                BackupService.ValidateBackup(path);
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "target.db.previous-*").Length);
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "target.db.writing-*").Length);
            }
        }

        [TestMethod]
        public void FailedRollback_PreservesOldBackupAndReportsItsPath()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                db.Services.Backup.CreateBackup(path);
                var original = File.ReadAllBytes(path);
                db.Services.Books.Insert(new Book { Title = "新数据" });
                FileStream locked = null;
                try
                {
                    var service = WithPublishedValidation(db.Services.Database, p =>
                    {
                        // On Windows, this prevents the failed target from being deleted.
                        locked = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read);
                        throw new InvalidOperationException("模拟校验失败");
                    });
                    var error = Assert.ThrowsException<InvalidOperationException>(() => service.CreateBackup(path));
                    var previous = Directory.GetFiles(db.Root, "target.db.previous-*");
                    Assert.AreEqual(1, previous.Length);
                    StringAssert.Contains(error.Message, previous[0]);
                    StringAssert.Contains(error.Message, "请勿删除");
                    Assert.IsInstanceOfType(error.InnerException, typeof(AggregateException));
                    CollectionAssert.AreEqual(original, File.ReadAllBytes(previous[0]));
                    BackupService.ValidateBackup(previous[0]);
                    Assert.AreEqual(0, Directory.GetFiles(db.Root, "target.db.writing-*").Length);
                }
                finally { if (locked != null) locked.Dispose(); }
            }
        }

        [TestMethod]
        public void FailedFinalValidationOfNewBackup_RemovesUnverifiedTarget()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "new.db");
                var service = WithPublishedValidation(db.Services.Database, p => { throw new InvalidOperationException("模拟校验失败"); });
                Assert.ThrowsException<InvalidOperationException>(() => service.CreateBackup(path));
                Assert.IsFalse(File.Exists(path));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "new.db.writing-*").Length);
            }
        }

        [TestMethod]
        public void PublishFailure_RestoresPreviousTarget()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                db.Services.Backup.CreateBackup(path);
                var original = File.ReadAllBytes(path);
                var publish = typeof(BackupService).GetMethod("PublishValidatedBackup",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var error = Assert.ThrowsException<System.Reflection.TargetInvocationException>(() =>
                    publish.Invoke(null, new object[] { Path.Combine(db.Root, "missing-staging"), path }));
                Assert.IsInstanceOfType(error.InnerException, typeof(IOException));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "target.db.previous-*").Length);
            }
        }

        [TestMethod]
        public void PublishCollision_PreservesBothPreviousAndNewlyAppearedTarget()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                var staging = Path.Combine(db.Root, "staging.db");
                db.Services.Backup.CreateBackup(path);
                var original = File.ReadAllBytes(path);
                File.WriteAllText(staging, "staged");
                Action<string, string> move = (from, to) =>
                {
                    if (from == staging)
                    {
                        File.WriteAllText(path, "other process file");
                        throw new IOException("模拟发布冲突");
                    }
                    File.Move(from, to);
                };
                var publish = typeof(BackupService).GetMethod("PublishValidatedBackupCore",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var wrapper = Assert.ThrowsException<System.Reflection.TargetInvocationException>(() =>
                    publish.Invoke(null, new object[] { staging, path, move }));
                var error = wrapper.InnerException as InvalidOperationException;
                Assert.IsNotNull(error);
                var previous = Directory.GetFiles(db.Root, "target.db.previous-*");
                Assert.AreEqual(1, previous.Length);
                StringAssert.Contains(error.Message, previous[0]);
                Assert.IsInstanceOfType(error.InnerException, typeof(AggregateException));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(previous[0]));
                BackupService.ValidateBackup(previous[0]);
                Assert.AreEqual("other process file", File.ReadAllText(path));
                Assert.AreEqual("staged", File.ReadAllText(staging));
            }
        }

        [TestMethod]
        public void BackupWithExistingTargetSidecars_RefusesOverwriteAndPreservesEverything()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                db.Services.Backup.CreateBackup(path);
                var original = File.ReadAllBytes(path);
                var wal = path + "-wal";
                var journal = new byte[] { 1, 2, 3, 4 };
                File.WriteAllBytes(wal, journal);
                var error = Assert.ThrowsException<InvalidOperationException>(() => db.Services.Backup.CreateBackup(path));
                StringAssert.Contains(error.Message, "日志文件");
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                CollectionAssert.AreEqual(journal, File.ReadAllBytes(wal));
                Assert.AreEqual(0, Directory.GetFiles(db.Root, "target.db.writing-*").Length);
                Assert.ThrowsException<InvalidOperationException>(() => db.Services.Backup.CreateBackup(db.Services.Database.DatabasePath + "-wal"));
            }
        }

        [TestMethod]
        public void Backup_RefusesOrphanedOrEmptyTargetSidecarsWithoutDeletingThem()
        {
            using (var db = new TemporaryDatabase())
            {
                foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
                {
                    var path = Path.Combine(db.Root, "missing" + suffix.Substring(1) + ".db");
                    File.WriteAllBytes(path + suffix, new byte[0]);
                    Assert.ThrowsException<InvalidOperationException>(() => db.Services.Backup.CreateBackup(path));
                    Assert.IsFalse(File.Exists(path));
                    Assert.IsTrue(File.Exists(path + suffix));
                    Assert.AreEqual(0, Directory.GetFiles(db.Root, Path.GetFileName(path) + ".writing-*").Length);
                }
                foreach (var suffix in new[] { "-shm", "-journal" })
                    Assert.ThrowsException<InvalidOperationException>(() => db.Services.Backup.CreateBackup(db.Services.Database.DatabasePath + suffix));
            }
        }

        [TestMethod]
        public void Backup_DoesNotTouchOlderOrUnrelatedWritingFiles()
        {
            using (var db = new TemporaryDatabase())
            {
                var path = Path.Combine(db.Root, "target.db");
                var old = path + ".writing-legacy-wal";
                File.WriteAllText(old, "unverified history");
                db.Services.Backup.CreateBackup(path);
                Assert.AreEqual("unverified history", File.ReadAllText(old));
                CollectionAssert.AreEqual(new[] { old }, Directory.GetFiles(db.Root, "target.db.writing-*"));
            }
        }

        [TestMethod]
        public void CancelledBackup_PreservesExistingTargetAndCreatesNoAdditionalTemporaryFiles()
        {
            using (var db = new TemporaryDatabase())
            using (var cancellation = new CancellationTokenSource())
            {
                var path = Path.Combine(db.Root, "existing.db");
                db.Services.Backup.CreateBackup(path);
                var before = File.ReadAllBytes(path);
                var temporaryBefore = Directory.GetFiles(db.Root, "existing.db.writing-*").OrderBy(x => x).ToArray();
                Assert.AreEqual(0, temporaryBefore.Length);
                cancellation.Cancel();
                Assert.ThrowsException<OperationCanceledException>(
                    () => db.Services.Backup.CreateBackup(path, null, cancellation.Token));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                BackupService.ValidateBackup(path);
                CollectionAssert.AreEqual(temporaryBefore,
                    Directory.GetFiles(db.Root, "existing.db.writing-*").OrderBy(x => x).ToArray());
            }
        }

        private sealed class CancelBackupOnProgress : IProgress<int>
        {
            private readonly CancellationTokenSource _source;
            public CancelBackupOnProgress(CancellationTokenSource source) { _source = source; }
            public void Report(int percent) { _source.Cancel(); }
        }

        [TestMethod]
        public void BackupCancelledDuringSnapshot_PreservesPreviousTarget()
        {
            using (var db = new TemporaryDatabase())
            using (var source = new CancellationTokenSource())
            {
                // SQLite reports intermediate progress only when the snapshot
                // spans more than the 64-page backup step.
                using (var connection = db.Services.Database.Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE backup_test_padding(data BLOB); INSERT INTO backup_test_padding VALUES(randomblob(1048576));";
                    command.ExecuteNonQuery();
                }
                var path = Path.Combine(db.Root, "existing.db");
                db.Services.Backup.CreateBackup(path);
                var before = File.ReadAllBytes(path);
                var temporaryBefore = Directory.GetFiles(db.Root, "existing.db.writing-*").OrderBy(x => x).ToArray();
                Assert.AreEqual(0, temporaryBefore.Length);
                Assert.ThrowsException<OperationCanceledException>(() =>
                    db.Services.Backup.CreateBackup(path, new CancelBackupOnProgress(source), source.Token));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                BackupService.ValidateBackup(path);
                CollectionAssert.AreEqual(temporaryBefore,
                    Directory.GetFiles(db.Root, "existing.db.writing-*").OrderBy(x => x).ToArray());
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
        public void AutomaticBackup_CancelBeforePublicationDoesNotPublishOrCopy()
        {
            using (var db = new TemporaryDatabase())
            using (var source = new CancellationTokenSource())
            {
                var primary = Path.Combine(db.Root, "primary");
                var secondary = Path.Combine(db.Root, "secondary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, secondary);
                db.Services.Settings.SetString(AutoBackupService.LastSecondaryErrorKey, "旧状态");
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                source.Cancel();
                Assert.ThrowsException<OperationCanceledException>(() => service.CreateNow("cancel", null, source.Token));
                Assert.AreEqual(0, Directory.GetFiles(primary).Length);
                Assert.IsFalse(Directory.Exists(secondary));
                Assert.AreEqual("旧状态", db.Services.Settings.GetString(AutoBackupService.LastSecondaryErrorKey, ""));
                Assert.AreEqual("", db.Services.Settings.GetString(AutoBackupService.LastBackupUtcKey, ""));
            }
        }

        [TestMethod]
        public void AutomaticBackup_CancelAfterPrimaryPublicationStillCopiesAndPrunes()
        {
            using (var db = new TemporaryDatabase())
            using (var source = new CancellationTokenSource())
            {
                var primary = Path.Combine(db.Root, "primary");
                var secondary = Path.Combine(db.Root, "secondary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, secondary);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                db.Services.Books.Insert(new Book { Title = "取消边界备份", SalePriceCent = 1000 });
                for (var i = 0; i < 4; i++)
                {
                    var older = db.Services.Backup.CreateTimestampedBackup(primary);
                    File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-1));
                }
                Func<string, IProgress<int>, CancellationToken, string> publishThenCancel = (dir, progress, token) =>
                {
                    var path = db.Services.Backup.CreateTimestampedBackup(dir, progress, token);
                    source.Cancel();
                    return path;
                };
                var core = typeof(AutoBackupService).GetMethod("CreateNowCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var result = (string)core.Invoke(service, new object[] { "late cancellation", null, source.Token, publishThenCancel });
                Assert.IsTrue(source.IsCancellationRequested);
                Assert.IsTrue(File.Exists(result));
                BackupService.ValidateBackup(result);
                var copy = Path.Combine(secondary, Path.GetFileName(result));
                Assert.IsTrue(File.Exists(copy));
                BackupService.ValidateBackup(copy);
                Assert.AreEqual(3, Directory.GetFiles(primary, "bookstore_auto_*.db").Length);
                Assert.AreEqual(0, Directory.GetFiles(primary, "*.writing-*").Length);
                Assert.AreEqual(0, Directory.GetFiles(secondary, "*.writing-*").Length);
                Assert.AreEqual(BackupHealthLevel.Ok, service.GetStatus().Level);
                Assert.AreNotEqual("", db.Services.Settings.GetString(AutoBackupService.LastBackupUtcKey, ""));
            }
        }

        [TestMethod]
        public void AutomaticBackup_CancelDuringSecondCopyFinishesOtherCopiesAndReportsFailure()
        {
            using (var db = new TemporaryDatabase())
            using (var source = new CancellationTokenSource())
            {
                var primary = Path.Combine(db.Root, "primary");
                var configured = Path.Combine(db.Root, "configured");
                var removable = Path.Combine(db.Root, "removable");
                var blocker = Path.Combine(db.Root, "blocker");
                File.WriteAllText(blocker, "not a directory");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, configured);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () =>
                {
                    source.Cancel(); // configured copy has already completed
                    return new[] { removable, blocker };
                });
                var result = service.CreateNow("late cancellation", null, source.Token);
                Assert.IsTrue(source.IsCancellationRequested);
                BackupService.ValidateBackup(result);
                BackupService.ValidateBackup(Path.Combine(configured, Path.GetFileName(result)));
                BackupService.ValidateBackup(Path.Combine(removable, Path.GetFileName(result)));
                Assert.AreEqual(BackupHealthLevel.Warning, service.GetStatus().Level);
                StringAssert.Contains(service.GetStatus().Message, "第二份备份未完成");
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
        public void MissingOrMovedBackup_IsNotHiddenByRecentStoredTimestamp()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var path = db.Services.AutoBackup.CreateNow("test");
                File.Delete(path);
                Assert.AreEqual(BackupHealthLevel.Critical, db.Services.AutoBackup.GetStatus().Level);
                Assert.IsNull(db.Services.AutoBackup.GetStatus().LastBackupUtc);
                var replacement = db.Services.AutoBackup.RunStartupBackupIfNeeded();
                Assert.IsNotNull(replacement);
                Assert.IsTrue(File.Exists(replacement));
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, Path.Combine(db.Root, "new-directory"));
                Assert.AreEqual(BackupHealthLevel.Critical, db.Services.AutoBackup.GetStatus().Level);
                Assert.IsNotNull(db.Services.AutoBackup.RunStartupBackupIfNeeded());
            }
        }

        [TestMethod]
        public void OldBackupFile_IsNotHiddenByRecentStoredTimestamp()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var path = db.Services.AutoBackup.CreateNow("test");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-8));
                Assert.AreEqual(BackupHealthLevel.Critical, db.Services.AutoBackup.GetStatus().Level);
                Assert.IsNotNull(db.Services.AutoBackup.RunStartupBackupIfNeeded());
            }
        }

        [TestMethod]
        public void Startup_ReplacesCorruptTodaysBackupWithValidatedSnapshot()
        {
            using (var db = new TemporaryDatabase())
            {
                var dir = Path.Combine(db.Root, "primary");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, dir);
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                var previous = service.CreateNow("test");
                File.WriteAllBytes(previous, new byte[] { 1, 2, 3 });
                Assert.IsFalse(service.VerifyLatestBackup().Passed);
                Thread.Sleep(25);
                var replacement = service.RunStartupBackupIfNeeded();
                Assert.IsNotNull(replacement);
                Assert.AreNotEqual(previous, replacement);
                Assert.IsTrue(File.Exists(previous)); // do not delete the damaged original as a side effect
                BackupService.ValidateBackup(replacement);
                Assert.IsTrue(service.VerifyLatestBackup().Passed);
                Assert.IsNull(service.RunStartupBackupIfNeeded());
            }
        }

        [TestMethod]
        public void StartupFailure_IsVisibleAndCanBeRetried()
        {
            using (var db = new TemporaryDatabase())
            {
                var blocker = Path.Combine(db.Root, "bad-directory");
                File.WriteAllText(blocker, "not a directory");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, blocker);
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                Assert.IsNull(service.RunStartupBackupIfNeeded());
                Assert.AreEqual(BackupHealthLevel.Critical, service.GetStatus().Level);
                StringAssert.Contains(service.GetStatus().Message, "启动自动备份失败");
                File.Delete(blocker);
                var replacement = service.RunStartupBackupIfNeeded();
                Assert.IsNotNull(replacement);
                Assert.IsTrue(File.Exists(replacement));
                Assert.AreNotEqual(BackupHealthLevel.Critical, service.GetStatus().Level);
            }
        }

        [TestMethod]
        public void Retention_LeavesManualAndLegacyFilesAloneInBothDirectories()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary"); var removable = Path.Combine(db.Root, "usb");
                Directory.CreateDirectory(primary); Directory.CreateDirectory(removable);
                var manual = "bookstore_20200101_120000.db";
                var legacy = "bookstore_20200101_120000123.db";
                foreach (var dir in new[] { primary, removable })
                {
                    File.WriteAllText(Path.Combine(dir, manual), "manual");
                    File.WriteAllText(Path.Combine(dir, legacy), "legacy");
                    File.SetLastWriteTimeUtc(Path.Combine(dir, manual), DateTime.UtcNow.AddYears(-1));
                    File.SetLastWriteTimeUtc(Path.Combine(dir, legacy), DateTime.UtcNow.AddYears(-1));
                }
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new[] { removable });
                for (var i = 0; i < 5; i++) service.CreateNow("test");
                foreach (var dir in new[] { primary, removable })
                {
                    Assert.AreEqual("manual", File.ReadAllText(Path.Combine(dir, manual)));
                    Assert.AreEqual("legacy", File.ReadAllText(Path.Combine(dir, legacy)));
                    Assert.AreEqual(3, Directory.GetFiles(dir, "bookstore_auto_*.db").Length);
                }
                Assert.AreEqual(BackupHealthLevel.Ok, service.GetStatus().Level);
            }
        }

        [TestMethod]
        public void Retention_PreservesManagedBackupsWithSidecarsInBothLocations()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary");
                var secondary = Path.Combine(db.Root, "secondary");
                Directory.CreateDirectory(primary); Directory.CreateDirectory(secondary);
                var sidecars = new[] { "-wal", "-shm", "-journal" };
                foreach (var directory in new[] { primary, secondary })
                {
                    foreach (var sidecar in sidecars)
                    {
                        var path = Path.Combine(directory, "bookstore_auto_20200101_120000123_" + Guid.NewGuid().ToString("N") + ".db");
                        File.WriteAllText(path, "可能需要日志的旧备份");
                        File.WriteAllText(path + sidecar, "不要删除");
                        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddYears(-1));
                    }
                }
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, secondary);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                string latest = null;
                for (var i = 0; i < 5; i++) latest = service.CreateNow("retention test");
                service.Prune();
                foreach (var directory in new[] { primary, secondary })
                {
                    Assert.AreEqual(6, Directory.GetFiles(directory, "bookstore_auto_*.db").Length);
                    foreach (var sidecar in sidecars)
                    {
                        var matches = Directory.GetFiles(directory, "*.db" + sidecar);
                        Assert.AreEqual(1, matches.Length);
                        Assert.AreEqual("不要删除", File.ReadAllText(matches[0]));
                        Assert.AreEqual("可能需要日志的旧备份", File.ReadAllText(matches[0].Substring(0, matches[0].Length - sidecar.Length)));
                    }
                    BackupService.ValidateBackup(Path.Combine(directory, Path.GetFileName(latest)));
                    Assert.AreEqual(0, Directory.GetFiles(directory, "*.writing-*").Length);
                }
            }
        }

        [TestMethod]
        public void Retention_DoesNotDeleteBackupWithLiveUncheckpointedWal()
        {
            using (var db = new TemporaryDatabase())
            {
                var directory = Path.Combine(db.Root, "primary");
                Directory.CreateDirectory(directory);
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, directory);
                db.Services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var path = Path.Combine(directory, "bookstore_auto_20200101_120000123_" + Guid.NewGuid().ToString("N") + ".db");
                db.Services.Backup.CreateBackup(path);
                using (var connection = new SQLiteConnection("Data Source=" + path + ";Version=3;Pooling=False;"))
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA journal_mode=WAL;";
                        Assert.AreEqual("wal", Convert.ToString(command.ExecuteScalar()).ToLowerInvariant());
                        command.CommandText = "PRAGMA wal_autocheckpoint=0; CREATE TABLE wal_test(value TEXT); INSERT INTO wal_test VALUES('未合并数据');";
                        command.ExecuteNonQuery();
                        Assert.IsTrue(File.Exists(path + "-wal"));
                        Assert.IsTrue(new FileInfo(path + "-wal").Length > 0);
                        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddYears(-1));
                        var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                        for (var i = 0; i < 4; i++) service.CreateNow("wal retention test");
                        service.Prune();
                        Assert.IsTrue(File.Exists(path));
                        Assert.IsTrue(File.Exists(path + "-wal"));
                        Assert.IsTrue(new FileInfo(path + "-wal").Length > 0);
                        command.CommandText = "SELECT value FROM wal_test;";
                        Assert.AreEqual("未合并数据", Convert.ToString(command.ExecuteScalar()));
                    }
                }
            }
        }

        [TestMethod]
        public void SecondaryCopy_RefusesOrphanedTargetSidecarsWithoutTouchingThem()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary");
                var secondary = Path.Combine(db.Root, "secondary");
                Directory.CreateDirectory(primary); Directory.CreateDirectory(secondary);
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                var source = db.Services.Backup.CreateTimestampedBackup(primary);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                var copy = typeof(AutoBackupService).GetMethod("TryCopy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
                {
                    var name = "candidate" + suffix.Substring(1) + ".db";
                    var target = Path.Combine(secondary, name);
                    var bytes = new byte[] { 0, 1, 2, 3 };
                    File.WriteAllBytes(target + suffix, bytes);
                    Assert.IsFalse((bool)copy.Invoke(service, new object[] { source, secondary, name }));
                    Assert.IsFalse(File.Exists(target));
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(target + suffix));
                    Assert.AreEqual(0, Directory.GetFiles(secondary, name + ".writing-*").Length);
                }
                Assert.IsTrue((bool)copy.Invoke(service, new object[] { source, secondary, "safe.db" }));
                BackupService.ValidateBackup(Path.Combine(secondary, "safe.db"));
                BackupService.ValidateBackup(source);
            }
        }

        [TestMethod]
        public void StartupAndExit_BackUpAcrossSimulatedDaysAndRemovableDestination()
        {
            using (var db = new TemporaryDatabase())
            {
                var primary = Path.Combine(db.Root, "primary"); var usb = Path.Combine(db.Root, "usb");
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                db.Services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, usb);
                // Same configured location and removable drive must not be copied twice.
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new[] { usb });
                var dayOne = service.RunStartupBackupIfNeeded();
                Assert.IsNotNull(dayOne);
                Assert.IsTrue(File.Exists(Path.Combine(usb, Path.GetFileName(dayOne))));
                Assert.IsNull(service.RunStartupBackupIfNeeded());
                File.SetLastWriteTimeUtc(dayOne, DateTime.UtcNow.AddDays(-2));
                var dayTwo = service.RunStartupBackupIfNeeded();
                Assert.IsNotNull(dayTwo);
                var exit = service.RunExitBackup();
                Assert.IsNotNull(exit);
                Assert.AreNotEqual(dayTwo, exit);
                foreach (var item in new[] { dayOne, dayTwo, exit })
                    BackupService.ValidateBackup(Path.Combine(usb, Path.GetFileName(item)));
                Assert.AreEqual(BackupHealthLevel.Ok, service.GetStatus().Level);
            }
        }

        [TestMethod]
        public void BackupAge_TransitionsFromWarningToCritical()
        {
            using (var db = new TemporaryDatabase())
            {
                db.Services.Settings.SetString(AutoBackupService.BackupDirectoryKey, Path.Combine(db.Root, "primary"));
                db.Services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var service = new AutoBackupService(db.Services.Database, db.Services.Settings, () => new string[0]);
                var path = service.CreateNow("test");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-4));
                Assert.AreEqual(BackupHealthLevel.Warning, service.GetStatus().Level);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-8));
                Assert.AreEqual(BackupHealthLevel.Critical, service.GetStatus().Level);
            }
        }

        [TestMethod]
        public void RetentionNeverDeletesOrCountsLiveDatabaseAsBackup()
        {
            var root = Path.Combine(Path.GetTempPath(), "BookDesk-LiveName-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var live = Path.Combine(root, "bookstore_auto_20200101_120000123_" + Guid.NewGuid().ToString("N") + ".db");
                var services = new ApplicationServices(live);
                services.Settings.SetString(AutoBackupService.BackupDirectoryKey, root);
                services.Settings.SetInt(AutoBackupService.BackupRetentionKey, 3);
                services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, false);
                var service = new AutoBackupService(services.Database, services.Settings, () => new string[0]);
                Assert.IsNull(service.GetStatus().LastBackupUtc);
                for (var i = 0; i < 4; i++) service.CreateNow("test");
                File.SetLastWriteTimeUtc(live, DateTime.UtcNow.AddYears(-1));
                service.Prune();
                Assert.IsTrue(File.Exists(live));
                Assert.AreEqual(4, Directory.GetFiles(root, "bookstore_auto_*.db").Length);
                Assert.IsFalse(service.VerifyLatestBackup().Path == live);
            }
            finally { SQLiteConnection.ClearAllPools(); try { Directory.Delete(root, true); } catch (IOException) { } }
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
