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
        public void ChineseFontFallback_PrefersInstalledSimSunToSegoe()
        {
            Assert.AreEqual("SimSun", UiTheme.SelectFontFamily(new[] { "Segoe UI", "simsun" }, "fallback"));
            Assert.AreEqual("Microsoft YaHei", UiTheme.SelectFontFamily(new[] { "SimSun", "Microsoft YaHei" }, "fallback"));
            Assert.AreEqual("Segoe UI", UiTheme.SelectFontFamily(new[] { "Segoe UI" }, "fallback"));
            Assert.AreEqual("fallback", UiTheme.SelectFontFamily(new string[0], "fallback"));
        }

        [TestMethod]
        public void ExportDialog_UsesSelectedFormatAndRequiresMatchingSuffix()
        {
            var formats = new[] { ".xlsx", ".xls", ".csv" };
            using (var dialog = ExportFileDialog.Create("销售报表_202611", formats))
            {
                Assert.AreEqual("销售报表_202611", dialog.FileName);
                Assert.AreEqual("", dialog.DefaultExt);
                Assert.IsTrue(dialog.AddExtension);
                Assert.IsTrue(dialog.OverwritePrompt);
                Assert.AreEqual(1, dialog.FilterIndex);
                Assert.AreEqual("Excel 工作簿 (*.xlsx)|*.xlsx|旧版 Excel (*.xls)|*.xls|CSV 文本 (*.csv)|*.csv", dialog.Filter);
            }
            Assert.IsTrue(ExportFileDialog.MatchesSelection(@"C:\报表.目录\销售.XLSX", 1, formats));
            Assert.IsTrue(ExportFileDialog.MatchesSelection("报表.XLS", 2, formats));
            Assert.IsTrue(ExportFileDialog.MatchesSelection("报表.csv", 3, formats));
            Assert.IsFalse(ExportFileDialog.MatchesSelection("报表.xlsx", 2, formats));
            Assert.IsFalse(ExportFileDialog.MatchesSelection("报表", 1, formats));
            Assert.IsFalse(ExportFileDialog.MatchesSelection("报表.pdf", 1, formats));
            Assert.IsFalse(ExportFileDialog.MatchesSelection("报表.xlsx", 0, formats));
            Assert.IsFalse(ExportFileDialog.MatchesSelection("报表.xlsx", 4, formats));
            using (var dialog = ExportFileDialog.Create("导入模板", ".xlsx", ".csv"))
            {
                Assert.IsTrue(ExportFileDialog.MatchesSelection("模板.csv", 2, ".xlsx", ".csv"));
                Assert.IsFalse(ExportFileDialog.MatchesSelection("模板.xls", 2, ".xlsx", ".csv"));
                StringAssert.Contains(dialog.Filter, "*.csv");
            }
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
