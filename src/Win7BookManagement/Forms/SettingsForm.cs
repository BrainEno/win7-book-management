using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Reporting;

namespace Win7BookManagement.Forms
{
    public sealed class SettingsForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.InputNumber _lowStock = new AntdUI.InputNumber();
        private readonly AntdUI.Input _reportStoreName = UiTheme.CreateAntdInput("例如：目田书店");
        private readonly AntdUI.InputNumber _nightShiftStartHour = new AntdUI.InputNumber();
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
                RowCount = 3,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateStockSection(), 0, 0);
            root.Controls.Add(CreateReportSection(), 0, 1);
            root.Controls.Add(CreateDataSection(), 0, 2);
            Controls.Add(root);

            UiTheme.Apply(this);

            Shown += delegate
            {
                _lowStock.Value = _services.Settings.GetLowStockThreshold();
                _reportStoreName.Text = _services.Settings.GetReportStoreName();
                _nightShiftStartHour.Value = _services.Settings.GetReportNightShiftStartHour();
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
                Text = "当启用图书库存小于或等于这个数量时，工作台和库存页会把它标记为低库存。小型书店可以先从 3–5 册开始。",
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
            _lowStock.Height = UiTheme.InputHeight;

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

        private Control CreateReportSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 16),
                Margin = new Padding(0, 0, 0, 12),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "销售月报设置",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            section.Controls.Add(new Label
            {
                Text = "销售月报会按模版生成月度总表和每天的明细 Sheet。班次按操作时间自动归入白班 / 晚班；默认 14:00 起计入晚班，可按门店实际交班时间调整。",
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
                Text = "报表店名",
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 10, 10, 0)
            });

            _reportStoreName.Width = 210;
            _reportStoreName.Height = UiTheme.InputHeight;
            _reportStoreName.Margin = new Padding(0, 3, 18, 3);
            row.Controls.Add(_reportStoreName);

            row.Controls.Add(new Label
            {
                Text = "晚班开始",
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 10, 10, 0)
            });

            _nightShiftStartHour.Minimum = 0;
            _nightShiftStartHour.Maximum = 23;
            _nightShiftStartHour.DecimalPlaces = 0;
            _nightShiftStartHour.Width = 88;
            _nightShiftStartHour.Height = UiTheme.InputHeight;
            _nightShiftStartHour.Margin = new Padding(0, 3, 6, 3);
            row.Controls.Add(_nightShiftStartHour);

            row.Controls.Add(new Label
            {
                Text = ":00",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 10, 16, 0)
            });

            var save = UiTheme.CreateAntdButton("保存报表设置", true);
            save.Width = 124;
            save.Margin = Padding.Empty;
            save.Click += delegate { Save(); };
            row.Controls.Add(save);

            section.Controls.Add(row, 0, 2);

            var mappingRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, 10, 0, 0),
                Padding = Padding.Empty
            };
            mappingRow.Controls.Add(new Label
            {
                Text = "月报分类映射",
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 10, 10, 0)
            });

            var editMapping = UiTheme.CreateAntdButton("编辑分类映射…", false);
            editMapping.Width = 132;
            editMapping.Margin = Padding.Empty;
            editMapping.Click += delegate { EditReportCategoryMapping(); };
            mappingRow.Controls.Add(editMapping);

            mappingRow.Controls.Add(new Label
            {
                Text = "把系统里的图书分类映射到月报项目；未配置的分类仍按名称 / 关键词自动匹配。",
                AutoSize = true,
                MaximumSize = new Size(620, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(12, 10, 0, 0)
            });

            section.Controls.Add(mappingRow, 0, 3);
            return section;
        }

        private void EditReportCategoryMapping()
        {
            using (var dialog = new Form())
            {
                UiTheme.ConfigureForm(dialog);
                dialog.Text = "月报分类映射";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Width = 760;
                dialog.Height = 560;
                dialog.MinimumSize = new Size(620, 460);
                dialog.ShowInTaskbar = false;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = UiTheme.Background;

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = UiTheme.Background,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                root.Controls.Add(new Label
                {
                    Text = "系统分类 → 销售月报项目",
                    AutoSize = true,
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(18, 14, 18, 6),
                    Margin = Padding.Empty
                }, 0, 0);

                var targets = SalesMonthlyExcelExporter.GetBusinessLineNames();
                root.Controls.Add(new Label
                {
                    Text = "每行填写“系统分类=月报项目”。例如：独立出版=独立出版书籍。\r\n" +
                           "可用月报项目：" + string.Join("、", new List<string>(targets).ToArray()),
                    AutoSize = true,
                    MaximumSize = new Size(700, 0),
                    Font = UiTheme.Font(8.2F),
                    ForeColor = UiTheme.TextSecondary,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(18, 4, 18, 12),
                    Margin = Padding.Empty
                }, 0, 1);

                var input = UiTheme.CreateAntdInput("每行一个映射，例如：独立出版=独立出版书籍");
                input.Multiline = true;
                input.Dock = DockStyle.Fill;
                input.Margin = new Padding(14, 10, 14, 10);
                input.Text = _services.Settings.GetReportCategoryMapping();
                root.Controls.Add(input, 0, 2);

                var footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(14, 10, 14, 10),
                    Margin = Padding.Empty
                };
                var save = UiTheme.CreateAntdButton("保存映射", true);
                save.Width = 104;
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 88;
                save.Click += delegate
                {
                    var error = ValidateReportCategoryMapping(input.Text, targets);
                    if (error != null)
                    {
                        MessageBox.Show(dialog, error, "分类映射格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    _services.Settings.SetReportCategoryMapping(input.Text);
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };
                cancel.Click += delegate
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                };
                footer.Controls.Add(save);
                footer.Controls.Add(cancel);
                root.Controls.Add(footer, 0, 3);

                dialog.Controls.Add(root);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                UiTheme.Apply(dialog);
                dialog.Shown += delegate { UiTheme.FitDialogToWorkingArea(dialog, 24); };
                dialog.ShowDialog(this);
            }
        }

        private static string ValidateReportCategoryMapping(string text, IList<string> targets)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var lines = text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = (lines[i] ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0 || separator >= line.Length - 1)
                    return "第 " + (i + 1) + " 行需要使用“系统分类=月报项目”的格式。";

                var source = line.Substring(0, separator).Trim();
                var target = line.Substring(separator + 1).Trim();
                if (source.Length == 0 || target.Length == 0)
                    return "第 " + (i + 1) + " 行的分类和月报项目都不能为空。";

                var known = false;
                foreach (var item in targets)
                {
                    if (string.Equals(item, target, StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                    return "第 " + (i + 1) + " 行的月报项目“" + target + "”不存在，请从提示的项目中选择。";
            }

            return null;
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
                _services.Settings.SetReportStoreName(_reportStoreName.Text);
                _services.Settings.SetReportNightShiftStartHour(Decimal.ToInt32(_nightShiftStartHour.Value));
                MessageBox.Show(this, "设置已保存。库存提醒与销售月报会立即使用新的设置。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
