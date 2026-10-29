using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// P1-1：最小可用、完全离线的日志设施。
    /// 按日滚动写入 %ProgramData%\Win7BookManagement\logs\app-yyyyMMdd.log，
    /// 线程安全、永不因为写日志失败而影响业务，并自动清理 30 天前的日志。
    /// </summary>
    public static class AppLog
    {
        private static readonly object Gate = new object();
        private static bool _pruned;

        public const int RetentionDays = 30;

        public static string NewErrorId()
        {
            return "E" + DateTime.Now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) +
                   "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        }

        public static string CurrentLogFile
        {
            get
            {
                return Path.Combine(
                    AppPaths.LogsDirectory,
                    "app-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            }
        }

        public static void Info(string category, string message)
        {
            Write("INFO", category, message, null, null);
        }

        public static void Warn(string category, string message)
        {
            Write("WARN", category, message, null, null);
        }

        public static void Error(string category, string message, Exception exception)
        {
            Write("ERROR", category, message, exception, NewErrorId());
        }

        /// <summary>写入错误并返回错误编号，供界面展示“请把编号告诉维护人员”。</summary>
        public static string ErrorWithId(string category, string message, Exception exception)
        {
            var errorId = NewErrorId();
            Write("ERROR", category, message, exception, errorId);
            return errorId;
        }

        public static void Write(string level, string category, string message, Exception exception, string errorId)
        {
            try
            {
                PruneOnce();

                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                builder.Append(" [").Append(level).Append(']');
                builder.Append(" [").Append(string.IsNullOrEmpty(category) ? "app" : category).Append(']');
                if (!string.IsNullOrEmpty(errorId)) builder.Append(" [").Append(errorId).Append(']');
                builder.Append(' ').Append(message ?? string.Empty);

                if (exception != null)
                {
                    builder.AppendLine();
                    builder.Append("  ").Append(exception.GetType().FullName).Append(": ").Append(exception.Message);
                    builder.AppendLine();
                    builder.Append(exception.StackTrace ?? string.Empty);

                    var inner = exception.InnerException;
                    var depth = 0;
                    while (inner != null && depth < 4)
                    {
                        builder.AppendLine();
                        builder.Append("  inner: ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
                        builder.AppendLine();
                        builder.Append(inner.StackTrace ?? string.Empty);
                        inner = inner.InnerException;
                        depth++;
                    }
                }

                lock (Gate)
                {
                    Directory.CreateDirectory(AppPaths.LogsDirectory);
                    File.AppendAllText(CurrentLogFile, builder.ToString() + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志永远不能让业务失败。
            }
        }

        /// <summary>导出最近 n 天日志到指定目录，返回目标目录；供“导出诊断包”使用。</summary>
        public static string ExportRecentLogs(int days, string targetDirectory)
        {
            if (days < 1) days = 7;
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArgumentException("请选择导出目录。", "targetDirectory");

            Directory.CreateDirectory(targetDirectory);
            if (!Directory.Exists(AppPaths.LogsDirectory)) return targetDirectory;

            var threshold = DateTime.Now.AddDays(-days);
            foreach (var file in Directory.GetFiles(AppPaths.LogsDirectory, "app-*.log"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < threshold) continue;
                File.Copy(file, Path.Combine(targetDirectory, info.Name), true);
            }

            return targetDirectory;
        }

        private static void PruneOnce()
        {
            if (_pruned) return;
            _pruned = true;

            try
            {
                if (!Directory.Exists(AppPaths.LogsDirectory)) return;
                var threshold = DateTime.Now.AddDays(-RetentionDays);
                foreach (var file in Directory.GetFiles(AppPaths.LogsDirectory, "app-*.log"))
                {
                    if (File.GetLastWriteTime(file) < threshold)
                        File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }
}
