using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Services;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// P0-3 的界面部分：首页 / 状态栏可复用的备份提醒条。
    /// 用法：var banner = BackupStatusBanner.Create(services.AutoBackup); host.Controls.Add(banner);
    /// </summary>
    public static class BackupStatusBanner
    {
        public static Control Create(AutoBackupService autoBackup)
        {
            if (autoBackup == null) throw new ArgumentNullException("autoBackup");

            var status = autoBackup.GetStatus();
            var background = UiTheme.Surface;
            var foreground = UiTheme.TextSecondary;

            if (status.Level == BackupHealthLevel.Warning)
            {
                background = Color.FromArgb(255, 251, 235);
                foreground = Color.FromArgb(146, 64, 14);
            }
            else if (status.Level == BackupHealthLevel.Critical)
            {
                background = Color.FromArgb(254, 242, 242);
                foreground = Color.FromArgb(153, 27, 27);
            }

            var label = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                Font = UiTheme.Font(9F),
                BackColor = background,
                ForeColor = foreground,
                Text = "备份状态：" + status.Message
            };

            if (status.LastBackupUtc.HasValue)
            {
                label.Text += "（备份目录：" + status.Directory + "）";
            }

            return label;
        }
    }
}
