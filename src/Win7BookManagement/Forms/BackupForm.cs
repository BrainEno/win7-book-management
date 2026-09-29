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
        private readonly Label _path;

        public BackupForm(ApplicationServices services)
        {
            _services = services;

            var info = new Label
            {
                Dock = DockStyle.Top,
                Height = 78,
                Padding = new Padding(6),
                Text = "数据库备份会生成一个可独立保存的 .db 文件。恢复前系统会先保留当前数据库的安全副本。\r\n" +
                       "建议在每天营业结束后备份一次，并把备份复制到另一块磁盘或 U 盘。"
            };

            _path = new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(6),
                Text = "当前数据库：" + _services.Database.DatabasePath
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(6)
            };
            var backup = new Button { Text = "创建备份", Width = 100, Height = 32 };
            var restore = new Button { Text = "从备份恢复", Width = 110, Height = 32 };
            backup.Click += delegate { CreateBackup(); };
            restore.Click += delegate { Restore(); };
            buttons.Controls.Add(backup);
            buttons.Controls.Add(restore);

            Controls.Add(buttons);
            Controls.Add(_path);
            Controls.Add(info);
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
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    _services.Backup.CreateBackup(dialog.FileName);
                    MessageBox.Show(this, "备份完成：" + dialog.FileName, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "备份失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Restore()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*";
                dialog.InitialDirectory = Directory.Exists(AppPaths.BackupDirectory) ? AppPaths.BackupDirectory : "";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                if (MessageBox.Show(
                    this,
                    "恢复会覆盖当前数据库。系统会先自动保留一份 .before_restore 安全副本。确定继续吗？",
                    "确认恢复",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                try
                {
                    _services.Backup.Restore(dialog.FileName);
                    MessageBox.Show(this, "恢复完成。建议关闭并重新启动程序后继续操作。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "恢复失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
