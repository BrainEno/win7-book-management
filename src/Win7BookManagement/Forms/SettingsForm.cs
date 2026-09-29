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

        public SettingsForm(ApplicationServices services)
        {
            _services = services;
            BackColor = UiTheme.Background;

            var card = UiTheme.CreateCard();
            card.Dock = DockStyle.Top;
            card.Height = 230;
            card.Margin = new Padding(0);
            card.Padding = new Padding(22);

            var title = new Label
            {
                Text = "库存提醒",
                Dock = DockStyle.Top,
                Height = 32,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            var hint = new Label
            {
                Text = "当启用图书库存小于或等于这个数量时，在首页显示为低库存。建议小型书店先设为 3–5 册。",
                Dock = DockStyle.Top,
                Height = 48,
                ForeColor = UiTheme.TextSecondary
            };

            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface
            };
            row.Controls.Add(new Label
            {
                Text = "低库存阈值",
                AutoSize = true,
                Margin = new Padding(0, 15, 12, 0)
            });
            _lowStock.Minimum = 0;
            _lowStock.Maximum = 9999;
            _lowStock.Width = 100;
            _lowStock.Margin = new Padding(0, 10, 12, 0);
            row.Controls.Add(_lowStock);

            var save = new Button
            {
                Text = "保存",
                Width = 88,
                Height = 32,
                Margin = new Padding(0, 8, 10, 0),
                Tag = "primary"
            };
            save.Click += delegate { Save(); };
            row.Controls.Add(save);

            var openFolder = new Button
            {
                Text = "打开数据目录",
                Width = 112,
                Height = 32,
                Margin = new Padding(0, 8, 0, 0)
            };
            openFolder.Click += delegate { OpenDataFolder(); };
            row.Controls.Add(openFolder);

            var dataHint = new Label
            {
                Text = "数据库位置：" + _services.Database.DatabasePath,
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = UiTheme.TextSecondary
            };

            card.Controls.Add(dataHint);
            card.Controls.Add(row);
            card.Controls.Add(hint);
            card.Controls.Add(title);

            Controls.Add(card);
            UiTheme.Apply(this);

            Shown += delegate
            {
                _lowStock.Value = _services.Settings.GetLowStockThreshold();
            };
        }

        private void Save()
        {
            try
            {
                _services.Settings.SetLowStockThreshold(Decimal.ToInt32(_lowStock.Value));
                MessageBox.Show(this, "设置已保存。首页会使用新的低库存阈值。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(this, "无法打开目录：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
