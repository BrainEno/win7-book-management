using System;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.UiTools
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppPaths.EnsureFolders();
            CrashReporter.Install();
            UiTheme.InitializeAntdUi();
            LowSpecMode.ApplyToAntdUi();
            if (args.Any(a => a == "--ui-snapshots"))
            {
                var index = Array.FindIndex(args, a => a == "--ui-snapshots");
                return UiSnapshotGenerator.Run(index + 1 < args.Length ? args[index + 1] : null);
            }
            if (args.Any(a => a == "--verify")) return InvariantChecks.RunAndPrint(AppPaths.DatabasePath);
            return SelfTest.Run();
        }
    }
}
