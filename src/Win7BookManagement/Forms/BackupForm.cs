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
                Text = "备份会生成经过 SQLite 完整性校验的独立 .db 文件。恢复前系统会自动保存当前完整数据；恢复失败时会自动回滚。建议每天营业结束后备份，并另存到 U 盘或另一块硬盘。",
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

        private void CreateBackup()
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "SQLite 数据库 (*.db)|*.db";
                    dialog.DefaultExt = "db";
                    dialog.AddExtension = true;
                    dialog.InitialDirectory = AppPaths.BackupDirectory;
                    dialog.FileName = "bookstore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    _services.Backup.CreateBackup(dialog.FileName);
                    MessageBox.Show(
                        this,
                        "备份完成，并已通过数据库完整性检查：\r\n" + dialog.FileName,
                        "备份成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "备份失败：\r\n" + ex.Message, "无法创建备份", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Restore()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*";
                dialog.InitialDirectory = Directory.Exists(AppPaths.BackupDirectory) ? AppPaths.BackupDirectory : "";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (MessageBox.Show(
                    this,
                    "恢复会替换当前数据库。系统将按以下顺序执行：\r\n" +
                    "1. 校验所选备份的完整性；\r\n" +
                    "2. 创建恢复前安全备份；\r\n" +
                    "3. 在暂存数据库中准备恢复数据；\r\n" +
                    "4. 替换后再次校验，失败则自动回滚。\r\n\r\n" +
                    "确定继续吗？",
                    "确认安全恢复",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                try
                {
                    var safetyBackupPath = _services.Backup.Restore(dialog.FileName);
                    var safetyMessage = string.IsNullOrWhiteSpace(safetyBackupPath)
                        ? ""
                        : "\r\n\r\n恢复前安全备份保存在：\r\n" + safetyBackupPath;
                    MessageBox.Show(
                        this,
                        "恢复完成，恢复后的数据库已通过完整性检查。" + safetyMessage +
                        "\r\n\r\n请关闭并重新启动程序后再继续操作。",
                        "恢复成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    var safetyMessage = string.IsNullOrWhiteSpace(_services.Backup.LastSafetyBackupPath)
                        ? ""
                        : "\r\n\r\n恢复前安全备份：\r\n" + _services.Backup.LastSafetyBackupPath;
                    MessageBox.Show(
                        this,
                        "恢复失败：\r\n" + ex.Message + safetyMessage,
                        "无法恢复数据库",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }
}