using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed partial class SettingsForm
    {
        private readonly AntdUI.Input _backupPath = UiTheme.CreateAntdInput("主备份目录");
        private readonly AntdUI.Input _secondaryPath = UiTheme.CreateAntdInput("可留空，自动使用已插入的 U 盘");
        private readonly AntdUI.InputNumber _retention = new AntdUI.InputNumber();
        private readonly AntdUI.Checkbox _secondaryEnabled = new AntdUI.Checkbox { Text = "同时保存第二份备份", AutoSize = true };
        private readonly AntdUI.Select _performance = new AntdUI.Select();
        private readonly AntdUI.Select _receiptWidth = new AntdUI.Select();
        private readonly Label _backupStatus = new Label { AutoSize = true, MaximumSize = new Size(880, 0) };

        private Control CreateMaintenanceSection()
        {
            var section = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(18, 16, 18, 16), Margin = new Padding(0, 0, 0, 12), BackColor = UiTheme.Surface };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.Controls.Add(new Label { Text = "数据安全与低配电脑", AutoSize = true, Font = UiTheme.Font(12F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
            section.Controls.Add(new Label { Text = "主备份仍在本机时，硬盘损坏会同时丢失主库和备份。请插入 U 盘，或选择另一块硬盘 / 网络共享作为第二备份位置。不要手工复制正在使用的 .db 文件。", AutoSize = true, MaximumSize = new Size(900, 0), ForeColor = UiTheme.TextSecondary, Margin = new Padding(0, 0, 0, 10) });
            _backupPath.Width = 360; _secondaryPath.Width = 360;
            _retention.Minimum = AutoBackupService.MinRetention; _retention.Maximum = AutoBackupService.MaxRetention; _retention.Width = 90;
            section.Controls.Add(PathRow("主备份目录", _backupPath, delegate { BrowseFolder(_backupPath); }));
            section.Controls.Add(PathRow("第二备份位置", _secondaryPath, delegate { BrowseFolder(_secondaryPath); }));
            var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            options.Controls.Add(_secondaryEnabled);
            options.Controls.Add(new Label { Text = "每个位置保留", AutoSize = true, Margin = new Padding(12, 8, 6, 0) });
            options.Controls.Add(_retention);
            options.Controls.Add(new Label { Text = "份", AutoSize = true, Margin = new Padding(4, 8, 12, 0) });
            var save = UiTheme.CreateAntdButton("保存备份设置", true); save.Width = 140;
            save.Click += delegate { SaveMaintenance(); };
            options.Controls.Add(save);
            section.Controls.Add(options);
            section.Controls.Add(_backupStatus);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            var verify = UiTheme.CreateAntdButton("验证最近一次备份", false); verify.Width = 170;
            verify.Click += delegate
            {
                UiTask.Run(this, "验证最近一次备份",
                    (progress, token) => _services.AutoBackup.VerifyLatestBackup(),
                    result => MessageBox.Show(this, result.Detail + "\r\n" + result.Path,
                        result.Passed ? "备份可以使用" : "备份不可使用", MessageBoxButtons.OK,
                        result.Passed ? MessageBoxIcon.Information : MessageBoxIcon.Warning));
            };
            actions.Controls.Add(verify);
            var diagnostics = UiTheme.CreateAntdButton("导出诊断包", false); diagnostics.Width = 130;
            diagnostics.Click += delegate { ExportDiagnostics(); };
            actions.Controls.Add(diagnostics);
            section.Controls.Add(actions);
            _performance.Width = 180;
            _performance.Items.Add("自动检测（推荐）"); _performance.Items.Add("低配模式：始终开启"); _performance.Items.Add("低配模式：关闭");
            var performanceRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            performanceRow.Controls.Add(new Label { Text = "渲染性能", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
            performanceRow.Controls.Add(_performance);
            performanceRow.Controls.Add(new Label { Text = "低配模式关闭动画和高质量文字渲染，不影响账务数据。", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            section.Controls.Add(performanceRow);
            var receiptRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            receiptRow.Controls.Add(new Label { Text = "小票纸宽", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
            _receiptWidth.Width = 120; _receiptWidth.Items.Add("80 mm"); _receiptWidth.Items.Add("58 mm");
            receiptRow.Controls.Add(_receiptWidth);
            receiptRow.Controls.Add(new Label { Text = "销售结账后可打印 / 重打小票。", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            section.Controls.Add(receiptRow);
            return section;
        }

        private static Control PathRow(string title, Control input, EventHandler browse)
        {
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            row.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
            input.Margin = new Padding(0, 0, 6, 4); row.Controls.Add(input);
            var button = UiTheme.CreateAntdButton("选择目录", false); button.Width = 100; button.Click += browse;
            row.Controls.Add(button);
            return row;
        }

        private void LoadMaintenance()
        {
            _backupPath.Text = _services.AutoBackup.BackupDirectory;
            _secondaryPath.Text = _services.AutoBackup.SecondaryBackupPath;
            _retention.Value = _services.AutoBackup.Retention;
            _secondaryEnabled.Checked = _services.AutoBackup.SecondaryBackupEnabled;
            var mode = _services.Settings.GetString("low_spec_mode", "auto");
            _performance.SelectedIndex = mode == "on" ? 1 : mode == "off" ? 2 : 0;
            _receiptWidth.SelectedIndex = _services.Settings.GetInt("receipt_width_mm", 80) == 58 ? 1 : 0;
            _backupStatus.Text = _services.AutoBackup.GetStatus().Message;
        }

        private void SaveMaintenance()
        {
            try
            {
                var primary = (_backupPath.Text ?? "").Trim();
                var secondary = (_secondaryPath.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(primary)) throw new InvalidOperationException("请选择主备份目录。");
                if (string.Equals(Path.GetFullPath(primary).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetDirectoryName(Path.GetFullPath(_services.Database.DatabasePath)).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("主备份目录不能与正在使用的数据库目录相同。");
                if (secondary.Length > 0) Path.GetFullPath(secondary);
                _services.Settings.SetString(AutoBackupService.BackupDirectoryKey, primary);
                _services.Settings.SetString(AutoBackupService.SecondaryBackupPathKey, secondary);
                _services.Settings.SetInt(AutoBackupService.BackupRetentionKey, decimal.ToInt32(_retention.Value));
                _services.Settings.SetBool(AutoBackupService.SecondaryBackupEnabledKey, _secondaryEnabled.Checked);
                var mode = _performance.SelectedIndex == 1 ? "on" : _performance.SelectedIndex == 2 ? "off" : "auto";
                _services.Settings.SetString("low_spec_mode", mode);
                _services.Settings.SetInt("receipt_width_mm", _receiptWidth.SelectedIndex == 1 ? 58 : 80);
                LowSpecMode.Configure(mode == "auto" ? (bool?)null : mode == "on");
                LowSpecMode.ApplyToAntdUi();
                _backupStatus.Text = _services.AutoBackup.GetStatus().Message;
                MessageBox.Show(this, "备份与性能设置已保存。下一次自动备份将使用新位置。", "设置已保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { CrashReporter.Handle(this, ex, "保存备份与性能设置"); }
        }

        private static void BrowseFolder(Control input)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择可写的位置；第二份备份应放在另一块硬盘或 U 盘。";
                if (Directory.Exists(input.Text)) dialog.SelectedPath = input.Text;
                if (dialog.ShowDialog() == DialogResult.OK) input.Text = dialog.SelectedPath;
            }
        }

        private void ExportDiagnostics()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "诊断包 (*.zip)|*.zip"; dialog.DefaultExt = "zip"; dialog.FileName = "BOOKDESK-诊断包.zip";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var path = dialog.FileName;
                UiTask.Run(this, "导出诊断包", (progress, token) =>
                {
                    var folder = Path.GetDirectoryName(path);
                    var actual = CrashReporter.ExportDiagnostics(folder);
                    if (!string.Equals(Path.GetFullPath(actual), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(actual, path);
                    }
                    return path;
                }, result => MessageBox.Show(this, "诊断包已保存（不包含数据库）：\r\n" + result, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
        }
    }
}
