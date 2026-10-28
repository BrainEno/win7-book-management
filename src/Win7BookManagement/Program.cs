using System;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // CI 布局检查必须与真实应用使用同一 WinForms 渲染模式。
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // P0-3 / P1-1 / P1-6 接线顺序：目录 → 日志 → 全局异常 → 主题 → 业务。
            AppPaths.EnsureFolders();
            AppLog.Info("app", "BOOK DESK 启动");
            CrashReporter.Install();
            UiTheme.InitializeAntdUi();
            LowSpecMode.ApplyToAntdUi();

            if (args != null && args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
                return SelfTest.Run();

            if (args != null && args.Any(a => string.Equals(a, "--verify", StringComparison.OrdinalIgnoreCase)))
                return InvariantChecks.RunAndPrint(AppPaths.DatabasePath);

            if (args != null)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (!string.Equals(args[i], "--ui-snapshots", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var outputDirectory = i + 1 < args.Length ? args[i + 1] : null;
                    return UiSnapshotGenerator.Run(outputDirectory);
                }
            }

            ApplicationServices services = null;
            try
            {
                services = new ApplicationServices(AppPaths.DatabasePath);
                services.AutoBackup.RunStartupBackupIfNeeded();

                Application.Run(new MainForm(services));
                return 0;
            }
            catch (Exception ex)
            {
                var errorId = AppLog.ErrorWithId("startup", "程序启动失败", ex);
                MessageBox.Show(
                    "程序启动失败，不修改任何数据。\r\n\r\n原因：" + UserErrorText.For(ex) +
                    "\r\n错误编号：" + errorId +
                    "\r\n日志目录：" + AppPaths.LogsDirectory,
                    "BOOK DESK 启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                if (services != null)
                    services.AutoBackup.RunExitBackup();
            }
        }
    }
}
