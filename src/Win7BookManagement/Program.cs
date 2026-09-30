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
            // Use the exact same WinForms text-rendering mode in CI/self-test and
            // in the real application. Otherwise layout checks can pass with one
            // font metric mode and fail after startup switches modes.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
                return SelfTest.Run();

            try
            {
                AppPaths.EnsureFolders();
                var services = new ApplicationServices(AppPaths.DatabasePath);
                Application.Run(new MainForm(services));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("程序启动失败：\r\n" + ex.Message, "图书管理系统", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
