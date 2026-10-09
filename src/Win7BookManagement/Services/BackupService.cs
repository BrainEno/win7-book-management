using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using Win7BookManagement.Database;

namespace Win7BookManagement.Services
{
    public sealed class BackupService
    {
        private readonly DatabaseConnectionFactory _factory;

        public BackupService(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string LastSafetyBackupPath { get; private set; }

        public string CreateBackup(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("备份路径不能为空。", "targetPath");

            var sourcePath = Path.GetFullPath(_factory.DatabasePath);
            var fullTargetPath = Path.GetFullPath(targetPath);
            if (PathsEqual(sourcePath, fullTargetPath))
                throw new InvalidOperationException("备份文件不能覆盖当前正在使用的数据库。请选择其他位置。");

            var directory = Path.GetDirectoryName(fullTargetPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = fullTargetPath + ".writing-" + Guid.NewGuid().ToString("N");
            string previousBackupPath = null;
            try
            {
                CreateLiveSnapshot(temporaryPath);
                ValidateBackup(temporaryPath);

                previousBackupPath = PublishValidatedBackup(temporaryPath, fullTargetPath);
                ValidateBackup(fullTargetPath);

                TryDelete(previousBackupPath);
                previousBackupPath = null;
                return fullTargetPath;
            }
            catch
            {
                TryDelete(temporaryPath);
                if (!string.IsNullOrWhiteSpace(previousBackupPath) && File.Exists(previousBackupPath))
                {
                    TryDelete(fullTargetPath);
                    File.Move(previousBackupPath, fullTargetPath);
                    previousBackupPath = null;
                }
                throw;
            }
            finally
            {
                TryDelete(temporaryPath);
                TryDelete(previousBackupPath);
            }
        }

        public string CreateTimestampedBackup(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("备份目录不能为空。", "directory");

            Directory.CreateDirectory(directory);
            var file = "bookstore_" +
                       DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) +
                       ".db";
            return CreateBackup(Path.Combine(directory, file));
        }

        public string Restore(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                throw new FileNotFoundException("备份文件不存在。", backupPath);

            var sourceBackupPath = Path.GetFullPath(backupPath);
            var liveDatabasePath = Path.GetFullPath(_factory.DatabasePath);
            if (PathsEqual(sourceBackupPath, liveDatabasePath))
                throw new InvalidOperationException("不能把当前正在使用的数据库当作恢复文件。");

            // Never touch the live database until the selected backup is proven readable and consistent.
            ValidateBackup(sourceBackupPath);

            var stagePath = liveDatabasePath + ".restore-stage-" + Guid.NewGuid().ToString("N");
            string safetyBackupPath = null;
            LastSafetyBackupPath = null;

            try
            {
                if (File.Exists(liveDatabasePath))
                {
                    safetyBackupPath = BuildSafetyBackupPath();
                    CreateBackup(safetyBackupPath);
                    LastSafetyBackupPath = safetyBackupPath;
                }

                // Re-materialize the chosen backup through SQLite's backup API into a standalone staging DB.
                // The original backup file remains untouched throughout the restore operation.
                CreateStandaloneSnapshot(sourceBackupPath, stagePath);
                ValidateBackup(stagePath);

                SQLiteConnection.ClearAllPools();
                DeleteSidecars(liveDatabasePath);
                ReplaceDatabaseFile(stagePath, liveDatabasePath);

                try
                {
                    // Older valid backups may need a forward-only schema upgrade after replacement.
                    new DatabaseInitializer(_factory).EnsureCreated();
                    SQLiteConnection.ClearAllPools();
                    ValidateBackup(liveDatabasePath);
                }
                catch (Exception restoreError)
                {
                    if (string.IsNullOrWhiteSpace(safetyBackupPath) || !File.Exists(safetyBackupPath))
                    {
                        throw new InvalidOperationException(
                            "恢复后的数据库校验失败，且没有可用的恢复前安全备份。原备份文件未被修改。",
                            restoreError);
                    }

                    try
                    {
                        RollBackFromSafetyBackup(safetyBackupPath, liveDatabasePath);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new InvalidOperationException(
                            "恢复失败，并且自动回滚也失败。请不要继续录入数据。恢复前安全备份仍保存在：" +
                            safetyBackupPath + "。恢复错误：" + restoreError.Message +
                            "；回滚错误：" + rollbackError.Message,
                            rollbackError);
                    }

                    throw new InvalidOperationException(
                        "恢复失败，系统已自动回滚到恢复前的数据。安全备份保存在：" + safetyBackupPath,
                        restoreError);
                }

                return safetyBackupPath;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                TryDelete(stagePath);
            }
        }

        private void CreateLiveSnapshot(string destinationPath)
        {
            TryDelete(destinationPath);
            using (var source = _factory.Open())
            {
                CheckpointWal(source);
                using (var destination = OpenStandalone(destinationPath, false))
                {
                    source.BackupDatabase(destination, "main", "main", -1, null, 0);
                }
            }
        }

        private static void CreateStandaloneSnapshot(string sourcePath, string destinationPath)
        {
            TryDelete(destinationPath);
            using (var source = OpenStandalone(sourcePath, true))
            using (var destination = OpenStandalone(destinationPath, false))
            {
                source.BackupDatabase(destination, "main", "main", -1, null, 0);
            }
        }

        private static SQLiteConnection OpenStandalone(string path, bool readOnly)
        {
            var connectionString = "Data Source=" + path + ";Version=3;Pooling=False;";
            if (readOnly)
                connectionString += "Read Only=True;";

            var connection = new SQLiteConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static void CheckpointWal(SQLiteConnection source)
        {
            using (var checkpoint = source.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
                using (var reader = checkpoint.ExecuteReader())
                {
                    if (reader.Read() && Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) != 0)
                    {
                        throw new InvalidOperationException(
                            "数据库当前正在被占用，无法完成一致性检查点。请结束正在进行的操作后再备份。");
                    }
                }
            }
        }

        private string BuildSafetyBackupPath()
        {
            var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(_factory.DatabasePath));
            if (string.IsNullOrWhiteSpace(databaseDirectory))
                throw new InvalidOperationException("无法确定数据库所在目录。");

            var safetyDirectory = Path.Combine(databaseDirectory, "restore-safety");
            Directory.CreateDirectory(safetyDirectory);
            return Path.Combine(
                safetyDirectory,
                "before_restore_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) +
                ".db");
        }

        private void RollBackFromSafetyBackup(string safetyBackupPath, string liveDatabasePath)
        {
            var rollbackStage = liveDatabasePath + ".rollback-stage-" + Guid.NewGuid().ToString("N");
            try
            {
                ValidateBackup(safetyBackupPath);
                CreateStandaloneSnapshot(safetyBackupPath, rollbackStage);
                ValidateBackup(rollbackStage);

                SQLiteConnection.ClearAllPools();
                DeleteSidecars(liveDatabasePath);
                ReplaceDatabaseFile(rollbackStage, liveDatabasePath);
                new DatabaseInitializer(_factory).EnsureCreated();
                SQLiteConnection.ClearAllPools();
                ValidateBackup(liveDatabasePath);
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                TryDelete(rollbackStage);
            }
        }

        private static string PublishValidatedBackup(string temporaryPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(temporaryPath, targetPath);
                return null;
            }

            var previousPath = targetPath + ".previous-" + Guid.NewGuid().ToString("N");
            File.Move(targetPath, previousPath);
            try
            {
                File.Move(temporaryPath, targetPath);
                return previousPath;
            }
            catch
            {
                TryDelete(targetPath);
                File.Move(previousPath, targetPath);
                throw;
            }
        }

        private static void ReplaceDatabaseFile(string stagedPath, string liveDatabasePath)
        {
            if (!File.Exists(stagedPath))
                throw new FileNotFoundException("恢复暂存数据库不存在。", stagedPath);

            if (!File.Exists(liveDatabasePath))
            {
                File.Move(stagedPath, liveDatabasePath);
                return;
            }

            var rawPreviousPath = liveDatabasePath + ".replace-previous-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Replace(stagedPath, liveDatabasePath, rawPreviousPath, true);
                TryDelete(rawPreviousPath);
                return;
            }
            catch (PlatformNotSupportedException)
            {
                TryDelete(rawPreviousPath);
            }
            catch (IOException)
            {
                TryDelete(rawPreviousPath);
                if (!File.Exists(stagedPath) || !File.Exists(liveDatabasePath))
                    throw;
            }

            // Conservative fallback for file systems where File.Replace is unavailable.
            var movedPreviousPath = liveDatabasePath + ".move-previous-" + Guid.NewGuid().ToString("N");
            File.Move(liveDatabasePath, movedPreviousPath);
            try
            {
                File.Move(stagedPath, liveDatabasePath);
                TryDelete(movedPreviousPath);
            }
            catch
            {
                TryDelete(liveDatabasePath);
                if (File.Exists(movedPreviousPath))
                    File.Move(movedPreviousPath, liveDatabasePath);
                throw;
            }
        }

        private static void DeleteSidecars(string databasePath)
        {
            var wal = databasePath + "-wal";
            var shm = databasePath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);
        }

        private static void ValidateBackup(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("数据库文件不存在。", path);

            try
            {
                using (var connection = OpenStandalone(path, true))
                {
                    using (var integrity = connection.CreateCommand())
                    {
                        integrity.CommandText = "PRAGMA integrity_check;";
                        using (var reader = integrity.ExecuteReader())
                        {
                            var sawResult = false;
                            while (reader.Read())
                            {
                                sawResult = true;
                                var result = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
                                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidOperationException("数据库完整性检查失败：" + result);
                            }
                            if (!sawResult)
                                throw new InvalidOperationException("数据库完整性检查没有返回结果。");
                        }
                    }

                    using (var requiredTables = connection.CreateCommand())
                    {
                        requiredTables.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type='table'
  AND name IN ('schema_info','books','inventory_transactions','settings');";
                        if (Convert.ToInt32(requiredTables.ExecuteScalar(), CultureInfo.InvariantCulture) != 4)
                            throw new InvalidOperationException("所选文件不是有效的本系统数据库备份。");
                    }

                    using (var foreignKeys = connection.CreateCommand())
                    {
                        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
                        using (var reader = foreignKeys.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                throw new InvalidOperationException(
                                    "数据库外键检查失败，备份中存在不完整的关联数据。");
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("无法读取或校验数据库备份：" + ex.Message, ex);
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}