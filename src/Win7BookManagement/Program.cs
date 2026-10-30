using System;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement
{
    internal static class Program
    {
        public static bool RestartRequested { get; private set; }
        public static void RequestRestart() { RestartRequested = true; Application.Exit(); }

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
                return SmokeTest.Run();

            if (args != null && args.Any(a => string.Equals(a, "--verify", StringComparison.OrdinalIgnoreCase)))
                return InvariantChecks.RunAndPrint(AppPaths.DatabasePath);


            ApplicationServices services = null;
            try
            {
                services = new ApplicationServices(AppPaths.DatabasePath);
                var lowSpec = services.Settings.GetString("low_spec_mode", "auto");
                LowSpecMode.Configure(lowSpec == "auto" ? (bool?)null : lowSpec == "on");
                LowSpecMode.ApplyToAntdUi();
                services.AutoBackup.RunStartupBackupIfNeeded();

                Application.Run(new MainForm(services));
                if (RestartRequested)
                {
                    if (services != null) services.AutoBackup.RunExitBackup();
                    System.Diagnostics.Process.Start(Application.ExecutablePath);
                    return 0;
                }
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
                if (services != null && !RestartRequested)
                    services.AutoBackup.RunExitBackup();
            }
        }
    }
}
