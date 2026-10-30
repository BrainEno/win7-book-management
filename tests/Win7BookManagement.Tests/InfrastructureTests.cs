using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Tests
{
    [TestClass]
    public class InfrastructureTests
    {
        [TestMethod]
        public void WindowPlacement_ClampsSmallAndNegativeOriginDisplays()
        {
            var area = new Rectangle(-1024, 0, 1024, 728);
            var bounds = WindowPlacement.FitBounds(area, new Size(1600, 900), new Size(1024, 680));
            Assert.IsTrue(area.Contains(bounds));
            Assert.AreEqual(new Size(1024, 728), bounds.Size);
            Assert.AreEqual(new Size(800, 600), WindowPlacement.ClampMinimum(new Size(800, 600), new Size(1024, 680)));
        }

        [TestMethod]
        public void SqliteErrors_AreTranslatedWithoutLosingBusinessInstructions()
        {
            var busy = new SQLiteException(SQLiteErrorCode.Busy, "English driver detail");
            StringAssert.Contains(UserErrorText.For(busy), "数据库正被占用");
            Assert.AreEqual("库存不足，请先核对。", UserErrorText.For(new InvalidOperationException("库存不足，请先核对。")));
        }

        [TestMethod]
        public void LowSpecOverride_IsExplicitAndRestorable()
        {
            try
            {
                LowSpecMode.Configure(true); Assert.IsTrue(LowSpecMode.IsLowSpec);
                LowSpecMode.Configure(false); Assert.IsFalse(LowSpecMode.IsLowSpec);
            }
            finally { LowSpecMode.Configure(null); }
        }

        [TestMethod]
        public void Diagnostics_IsAZipWithoutTheBusinessDatabase()
        {
            var root = Path.Combine(Path.GetTempPath(), "BookDesk-Diagnostics-Test-" + Guid.NewGuid().ToString("N"));
            try
            {
                AppLog.Info("test", "diagnostic fixture");
                var path = CrashReporter.ExportDiagnostics(root);
                Assert.IsTrue(path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                using (var zip = ZipFile.OpenRead(path))
                {
                    Assert.IsTrue(zip.Entries.Any(e => e.Name == "diagnostics.txt"));
                    Assert.IsFalse(zip.Entries.Any(e => e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase)));
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
