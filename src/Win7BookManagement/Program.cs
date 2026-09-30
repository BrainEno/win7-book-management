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
            // CI layout checks must use the same WinForms rendering mode as the
            // real application. Otherwise text metrics can differ between the
            // self-test and the executable that users actually see.
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
