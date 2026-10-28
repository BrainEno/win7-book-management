using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// P1-1：全局异常入口。安装后所有未处理异常都会：
    /// 1) 写入带错误编号的日志；2) 显示中文提示；3) 提供“打开日志目录”。
    /// 非致命异常允许继续操作，致命异常安全退出，绝不出现 .NET 默认英文对话框。
    /// </summary>
    public static class CrashReporter
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;

            AppLog.Info("crash", "全局异常处理已安装");
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Handle(null, e.Exception, "界面操作");
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null) HandleFatal(exception, "AppDomain");

            if (e.IsTerminating)
                Environment.Exit(1);
        }

        private static void OnTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var exception = e.Exception;
            if (exception != null)
            {
                AppLog.Error("crash", "后台任务未观察异常", exception);
            }
            e.SetObserved();
        }

        /// <summary>非致命：记录 → 中文提示（含错误编号）→ 用户可继续工作。</summary>
        public static void Handle(IWin32Window owner, Exception exception, string operation)
        {
            if (exception == null) return;
            var errorId = AppLog.ErrorWithId("crash", operation ?? "未知操作", exception);
            ShowFriendly(owner, exception, operation, errorId, false);
        }

        public static void HandleFatal(Exception exception, string source)
        {
            var errorId = AppLog.ErrorWithId("crash", "致命错误 / " + source, exception);
            ShowFriendly(null, exception, source, errorId, true);
        }

        private static void ShowFriendly(
            IWin32Window owner,
            Exception exception,
            string operation,
            string errorId,
            bool fatal)
        {
            var message = new StringBuilder();
            message.AppendLine(fatal
                ? "程序遇到无法继续的问题，需要退出。"
                : "刚才的操作没有完成，业务数据没有被修改。");
            message.AppendLine();
            message.AppendLine("操作：" + (operation ?? "未知"));
            message.AppendLine("原因：" + UserErrorText.For(exception));
            message.AppendLine("错误编号：" + errorId);
            message.AppendLine();
            message.AppendLine("日志位置：");
            message.AppendLine(AppPaths.LogsDirectory);
            message.AppendLine();
            message.AppendLine(fatal
                ? "请重新启动程序；如反复出现，请把日志目录中的文件发给维护人员。"
                : "可以按“是”打开日志目录，或关闭提示后继续工作。");

            var buttons = fatal ? MessageBoxButtons.OK : MessageBoxButtons.YesNo;
            var result = owner == null
                ? MessageBox.Show(message.ToString(), "BOOK DESK 提示", buttons, MessageBoxIcon.Error)
                : MessageBox.Show(owner, message.ToString(), "BOOK DESK 提示", buttons, MessageBoxIcon.Error);

            if (!fatal && result == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", AppPaths.LogsDirectory);
                }
                catch
                {
                }
            }
        }

        /// <summary>导出诊断包：日志 + 版本 + 数据库健康摘要，写入用户选择的目录。</summary>
        public static string ExportDiagnostics(string targetDirectory)
        {
            var folder = Path.Combine(
                targetDirectory,
                "BOOKDESK-Diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);

            AppLog.ExportRecentLogs(7, folder);

            var summary = new StringBuilder();
            summary.AppendLine("BOOK DESK 诊断信息");
            summary.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            summary.AppendLine("版本：" + BuildInfo.DisplayVersion);
            summary.AppendLine("系统：" + Environment.OSVersion.VersionString + " / " + Environment.Is64BitOperatingSystem);
            summary.AppendLine("CLR：" + Environment.Version);
            summary.AppendLine("数据库：" + AppPaths.DatabasePath);
            summary.AppendLine("低配模式：" + LowSpecMode.IsLowSpec);
            summary.AppendLine("服务：见 logs 目录");

            File.WriteAllText(Path.Combine(folder, "diagnostics.txt"), summary.ToString(), Encoding.UTF8);
            return folder;
        }
    }
}
