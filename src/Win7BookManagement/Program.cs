using System;
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
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
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
