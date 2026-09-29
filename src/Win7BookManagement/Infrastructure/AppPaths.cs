using System;
using System.IO;

namespace Win7BookManagement.Infrastructure
{
    public static class AppPaths
    {
        private const string ProductFolder = "Win7BookManagement";
        private static string _rootPath;

        public static string RootPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_rootPath)) return _rootPath;
                var common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductFolder);
                try
                {
                    Directory.CreateDirectory(common);
                    var probe = Path.Combine(common, ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    _rootPath = common;
                }
                catch
                {
                    _rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolder);
                }
                return _rootPath;
            }
        }

        public static string DataDirectory { get { return Path.Combine(RootPath, "data"); } }
        public static string BackupDirectory { get { return Path.Combine(RootPath, "backup"); } }
        public static string ExportDirectory { get { return Path.Combine(RootPath, "exports"); } }
        public static string DatabasePath { get { return Path.Combine(DataDirectory, "bookstore.db"); } }

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(ExportDirectory);
        }
    }
}
