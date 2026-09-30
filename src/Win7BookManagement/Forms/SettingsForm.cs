using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class SettingsForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly NumericUpDown _lowStock = new NumericUpDown();
        private readonly Label _dataPath = new Label();

        public SettingsForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateStockSection(), 0, 0);
            root.Controls.Add(CreateDataSection(), 0, 1);
            Controls.Add(root);

            UiTheme.Apply(this);

            Shown += delegate
            {
                _lowStock.Value = _services.Settings.GetLowStockThreshold();
                _dataPath.Text = _services.Database.DatabasePath;
            };
        }

        private Control CreateStockSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 16),
                Margin = new Padding(0, 0, 0, 12),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "库存提醒",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            section.Controls.Add(new Label
            {
                Text = "当启用图书库存小于或等于这个数量时，经营概览和库存页会把它标记为低库存。小型书店可以先从 3–5 册开始。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 0, 12)
            }, 0, 1);

            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            row.Controls.Add(new Label
            {
                Text = "低库存阈值",
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 10, 12, 0)
            });

            _lowStock.Minimum = 0;
            _lowStock.Maximum = 9999;
            _lowStock.Width = 120;
            _lowStock.Margin = new Padding(0, 3, 6, 3);
            row.Controls.Add(_lowStock);

            row.Controls.Add(new Label
            {
                Text = "册",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 10, 16, 0)
            });

            var save = UiTheme.CreateAntdButton("保存设置", true);
            save.Width = 104;
            save.Margin = Padding.Empty;
            save.Click += delegate { Save(); };
            row.Controls.Add(save);

            section.Controls.Add(row, 0, 2);
            return section;
        }

        private Control CreateDataSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 16),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "本机数据",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            var openFolder = UiTheme.CreateAntdButton("打开数据目录", false);
            openFolder.Width = 124;
            openFolder.Margin = Padding.Empty;
            openFolder.Click += delegate { OpenDataFolder(); };
            section.Controls.Add(openFolder, 1, 0);

            section.Controls.Add(new Label
            {
                Text = "数据库完全保存在本机。日常请通过“备份与恢复”创建备份，不要在程序运行时手工替换数据库文件。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 1);
            section.SetColumnSpan(section.GetControlFromPosition(0, 1), 2);

            var pathRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(10, 8, 10, 8),
                Margin = Padding.Empty
            };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            pathRow.Controls.Add(new Label
            {
                Text = "数据库位置",
                AutoSize = true,
                Font = UiTheme.Font(8.2F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 2, 12, 0)
            }, 0, 0);

            _dataPath.AutoSize = false;
            _dataPath.Dock = DockStyle.Fill;
            _dataPath.AutoEllipsis = true;
            _dataPath.MinimumSize = new Size(0, 28);
            _dataPath.TextAlign = ContentAlignment.MiddleLeft;
            _dataPath.ForeColor = UiTheme.TextPrimary;
            _dataPath.Font = UiTheme.Font(8.2F);
            _dataPath.Margin = new Padding(0, 0, 0, 0);
            pathRow.Controls.Add(_dataPath, 1, 0);

            section.Controls.Add(pathRow, 0, 2);
            section.SetColumnSpan(pathRow, 2);
            return section;
        }

        private void Save()
        {
            try
            {
                _services.Settings.SetLowStockThreshold(Decimal.ToInt32(_lowStock.Value));
                MessageBox.Show(this, "设置已保存。经营概览和库存页会立即使用新的低库存阈值。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：\r\n" + ex.Message, "无法保存设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenDataFolder()
        {
            try
            {
                Process.Start("explorer.exe", AppPaths.RootPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开数据目录：\r\n" + ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
