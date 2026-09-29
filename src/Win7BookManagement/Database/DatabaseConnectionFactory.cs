using System;
using System.Data.SQLite;
using System.IO;

namespace Win7BookManagement.Database
{
    public sealed class DatabaseConnectionFactory
    {
        public DatabaseConnectionFactory(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("数据库路径不能为空。", "databasePath");

            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public SQLiteConnection Open()
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var connection = new SQLiteConnection("Data Source=" + DatabasePath + ";Version=3;Pooling=True;");
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
                command.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
