using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class BackupForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Label _path = new Label();

        public BackupForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateActionSection(), 0, 1);
            root.Controls.Add(CreatePathSection(), 0, 2);

            Controls.Add(root);
            UiTheme.Apply(this);
            Shown += delegate { UpdatePathWrap(); };
            Resize += delegate { UpdatePathWrap(); };
        }

        private Control CreateHeader()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 16),
                Margin = new Padding(0, 0, 0, 12),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "备份与恢复",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            section.Controls.Add(new Label
            {
                Text = "备份会生成经过 SQLite 完整性校验的独立 .db 文件。恢复前系统会自动保存当前完整数据；恢复失败时会自动回滚。系统会在每日首次启动和正常退出时自动备份。仍建议把第二份备份保存到 U 盘或另一块硬盘；主备份与数据库同盘不能防止硬盘损坏。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Font = UiTheme.Font(8.6F),
                ForeColor = UiTheme.TextSecondary
            }, 0, 1);
            return section;
        }

        private Control CreateActionSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 16),
                Margin = new Padding(0, 0, 0, 12),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var backupCard = CreateActionCard(
                "创建安全备份",
                "使用 SQLite 一致性快照保存当前全部数据，写入完成后再做完整性和外键检查；校验失败不会留下一个看似可用的坏备份。",
                "创建备份",
                true,
                delegate { CreateBackup(); });
            var restoreCard = CreateActionCard(
                "从备份安全恢复",
                "先校验所选备份，再创建恢复前安全备份，随后从暂存数据库切换；恢复或升级失败会自动回滚到操作前数据。",
                "从备份恢复",
                false,
                delegate { Restore(); });

            section.Controls.Add(backupCard, 0, 0);
            section.Controls.Add(restoreCard, 1, 0);
            section.SetRowSpan(backupCard, 2);
            section.SetRowSpan(restoreCard, 2);
            return section;
        }

        private static Control CreateActionCard(
            string title,
            string body,
            string buttonText,
            bool primary,
            EventHandler action)
        {
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 12, 0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            card.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            card.Controls.Add(new Label
            {
                Text = body,
                AutoSize = true,
                MaximumSize = new Size(430, 0),
                Font = UiTheme.Font(8.3F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 1);

            var button = UiTheme.CreateAntdButton(buttonText, primary);
            button.Width = 124;
            button.Anchor = AnchorStyles.Left;
            button.Click += action;
            card.Controls.Add(button, 0, 2);
            return card;
        }

        private Control CreatePathSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 14, 18, 14),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "当前数据库",
                AutoSize = true,
                Font = UiTheme.Font(8.4F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 2, 14, 0)
            }, 0, 0);

            _path.Text = _services.Database.DatabasePath;
            _path.AutoSize = true;
            _path.Font = UiTheme.Font(8.4F);
            _path.ForeColor = UiTheme.TextPrimary;
            _path.Margin = new Padding(0, 2, 0, 0);
            section.Controls.Add(_path, 1, 0);
            return section;
        }

        private void UpdatePathWrap()
        {
            var width = Math.Max(280, ClientSize.Width - 230);
            _path.MaximumSize = new Size(width, 0);
        }

        private static string PreferredBackupDirectory(ApplicationServices services)
        {
            var secondary = services.AutoBackup.SecondaryBackupPath;
            if (!string.IsNullOrWhiteSpace(secondary) && Directory.Exists(secondary)) return secondary;
            foreach (var drive in DriveInfo.GetDrives())
                if (drive.DriveType == DriveType.Removable && drive.IsReady) return drive.RootDirectory.FullName;
            return services.AutoBackup.BackupDirectory;
        }

        private void CreateBackup()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "SQLite 数据库 (*.db)|*.db";
                dialog.DefaultExt = "db"; dialog.AddExtension = true;
                dialog.InitialDirectory = PreferredBackupDirectory(_services);
                dialog.FileName = "bookstore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var path = dialog.FileName;
                UiTask.Run(this, "创建安全备份",
                    (progress, token) => _services.Backup.CreateBackup(path, progress, token),
                    result =>
                    {
                        _services.Settings.SetString(Services.AutoBackupService.LastBackupUtcKey, DateTime.UtcNow.ToString("o"));
                        MessageBox.Show(this, "备份完成，完整性、核心表和外键检查均已通过：\r\n" + result,
                            "备份成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
            }
        }

        private void Restore()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*";
                dialog.InitialDirectory = PreferredBackupDirectory(_services);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (MessageBox.Show(this,
                    "恢复会替换当前数据库。系统会先校验备份并保存恢复前快照；替换或升级失败会自动回滚。\r\n" +
                    "开始后不能取消，请勿关机。完成后程序会自动重启。确定继续吗？",
                    "确认安全恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                var path = dialog.FileName;
                UiTask.Run(this, "安全恢复数据库", (progress, token) =>
                {
                    progress.Report(-1);
                    return _services.Backup.Restore(path);
                }, safety =>
                {
                    MessageBox.Show(this, "恢复成功，程序将重新启动以重新加载全部数据。\r\n恢复前安全备份：\r\n" + safety,
                        "恢复成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Program.RequestRestart();
                }, false);
            }
        }
    }
}
