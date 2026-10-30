using System;
using System.IO;
using System.Data.SQLite;
using Win7BookManagement.Database;
using Win7BookManagement.Services;

namespace Win7BookManagement.Infrastructure
{
    public static class SmokeTest
    {
        public static int Run()
        {
            var root = Path.Combine(Path.GetTempPath(), "BookDesk-Smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                var factory = new DatabaseConnectionFactory(Path.Combine(root, "smoke.db"));
                new DatabaseInitializer(factory).EnsureCreated();
                if (DatabaseInitializer.GetDatabaseVersion(factory) != DatabaseInitializer.SupportedVersion)
                    throw new InvalidOperationException("Schema version smoke test failed.");
                for (var i = 0; i < 3; i++)
                    using (var connection = factory.Open())
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "PRAGMA synchronous;";
                        if (Convert.ToInt32(command.ExecuteScalar()) != 2)
                            throw new InvalidOperationException("Every pooled connection must use FULL synchronous.");
                    }
                var backup = Path.Combine(root, "smoke-backup.db");
                new BackupService(factory).CreateBackup(backup);
                BackupService.ValidateBackup(backup);
                Console.WriteLine("BOOK DESK release smoke test passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-error.txt"), ex.ToString()); }
                catch { }
                return 1;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try { Directory.Delete(root, true); }
                catch (Exception ex) { AppLog.Warn("smoke", "Temporary directory cleanup failed: " + ex.GetType().Name); }
            }
        }
    }
}
