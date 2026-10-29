using System;
using System.Data.SQLite;
using System.IO;

namespace Win7BookManagement.Database
{
    /// <summary>
    /// 数据库中唯一允许创建连接的地方。
    /// P0-3：持久性参数必须写在连接字符串上，而不是在初始化时对某一条连接执行 PRAGMA，
    /// 否则 Pooling=True 时不同池连接的 synchronous 会不一致，断电可能丢失已提交的成交。
    /// </summary>
    public sealed class DatabaseConnectionFactory
    {
        public const string PersistencePragma = "Synchronous=Full";

        public DatabaseConnectionFactory(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("数据库路径不能为空。", "databasePath");

            DatabasePath = databasePath;
            ConnectionString = BuildConnectionString(databasePath);
        }

        public string DatabasePath { get; private set; }

        /// <summary>对外只读暴露，供 --verify / InvariantChecks 断言。</summary>
        public string ConnectionString { get; private set; }

        public static string BuildConnectionString(string databasePath)
        {
            return "Data Source=" + databasePath +
                   ";Version=3" +
                   ";Pooling=True" +
                   ";Synchronous=Full" +
                   ";Journal Mode=Wal" +
                   ";Foreign Keys=True" +
                   ";Cache Size=8192";
        }

        public SQLiteConnection Open()
        {
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var connection = new SQLiteConnection(ConnectionString);
            connection.Open();

            // 只做会话级设置；持久性已在连接字符串中统一指定。
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA busy_timeout = 5000;";
                command.ExecuteNonQuery();
            }

            return connection;
        }

        public SQLiteConnection OpenReadOnly()
        {
            var connection = new SQLiteConnection(
                "Data Source=" + DatabasePath + ";Version=3;Read Only=True;Pooling=False;");
            connection.Open();
            return connection;
        }
    }
}
