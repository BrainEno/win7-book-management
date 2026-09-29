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

        public string CreateBackup(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new ArgumentException("备份路径不能为空。", "targetPath");

            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            using (var source = _factory.Open())
            using (var checkpoint = source.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(FULL);";
                checkpoint.ExecuteNonQuery();

                using (var destination = new SQLiteConnection("Data Source=" + targetPath + ";Version=3;"))
                {
                    destination.Open();
                    source.BackupDatabase(destination, "main", "main", -1, null, 0);
                }
            }

            return targetPath;
        }

        public string CreateTimestampedBackup(string directory)
        {
            Directory.CreateDirectory(directory);
            var file = "bookstore_" +
                       DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) +
                       ".db";
            return CreateBackup(Path.Combine(directory, file));
        }

        public void Restore(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                throw new FileNotFoundException("备份文件不存在。", backupPath);

            ValidateBackup(backupPath);

            SQLiteConnection.ClearAllPools();
            var safetyCopy = _factory.DatabasePath + ".before_restore";
            if (File.Exists(_factory.DatabasePath))
                File.Copy(_factory.DatabasePath, safetyCopy, true);

            File.Copy(backupPath, _factory.DatabasePath, true);

            var wal = _factory.DatabasePath + "-wal";
            var shm = _factory.DatabasePath + "-shm";
            if (File.Exists(wal)) File.Delete(wal);
            if (File.Exists(shm)) File.Delete(shm);

            new DatabaseInitializer(_factory).EnsureCreated();
        }

        private static void ValidateBackup(string path)
        {
            using (var connection = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('books','inventory_transactions');";
                    if (Convert.ToInt32(command.ExecuteScalar()) != 2)
                        throw new InvalidOperationException("所选文件不是有效的本系统数据库备份。");
                }
            }
        }
    }
}
