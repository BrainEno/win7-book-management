using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class ReportsForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly ComboBox _type = new ComboBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _rowChip = new Label();
        private readonly Label _quantityChip = new Label();
        private readonly Label _amountChip = new Label();
        private readonly Label _rangeHint = new Label();
        private readonly Label _emptyState = new Label();
        private readonly Label _summary = new Label();
        private readonly Button _exportButton = new Button();

        private DataTable _current;

        public ReportsForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            ConfigureFilters();
            ConfigureGrid();

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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateQuerySection(), 0, 0);
            root.Controls.Add(CreateGridSection(), 0, 1);

            _summary.AutoSize = true;
            _summary.Dock = DockStyle.Fill;
            _summary.MinimumSize = new Size(0, 36);
            _summary.Padding = new Padding(10, 8, 8, 8);
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8F);
            root.Controls.Add(_summary, 0, 2);

            Controls.Add(root);

            Resize += delegate { ApplyResponsiveColumns(); };

            UiTheme.Apply(this);
            Shown += delegate
            {
                UpdateDateControls();
                Query();
                ApplyResponsiveColumns();
            };
        }

        private void ConfigureFilters()
        {
            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Items.Add(new ReportOption("销售明细", "sales"));
            _type.Items.Add(new ReportOption("销售退货明细", "sales_return"));
            _type.Items.Add(new ReportOption("采购明细", "purchase"));
            _type.Items.Add(new ReportOption("采购退货明细", "purchase_return"));
            _type.Items.Add(new ReportOption("库存变动明细", "movement"));
            _type.Items.Add(new ReportOption("指定日期库存快照", "snapshot"));
            _type.SelectedIndex = 0;
            _type.SelectedIndexChanged += delegate
            {
                UpdateDateControls();
                Query();
            };

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;

            _exportButton.Text = "导出 Excel";
            _exportButton.Width = 108;
            _exportButton.Height = UiTheme.ButtonHeight;
            _exportButton.Tag = "primary";
            _exportButton.Click += delegate { Export(); };
        }

        private Control CreateQuerySection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (var row = 0; row < 4; row++)
                section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "报表查询与导出",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            _exportButton.Margin = Padding.Empty;
            section.Controls.Add(_exportButton, 1, 0);

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 58,
                MinimumSize = new Size(0, 58),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 0),
                Padding = Padding.Empty
            };
            filters.SizeChanged += delegate { ResizeFilterFlow(filters); };

            filters.Controls.Add(CreateFilterField("报表类型", _type, 180));
            filters.Controls.Add(CreateFilterField("开始日期", _from, 132));
            filters.Controls.Add(CreateFilterField("结束 / 快照", _to, 132));

            _rangeHint.AutoSize = true;
            _rangeHint.MaximumSize = new Size(240, 0);
            _rangeHint.ForeColor = UiTheme.TextSecondary;
            _rangeHint.Font = UiTheme.Font(8F);
            _rangeHint.Margin = new Padding(8, 25, 10, 0);
            filters.Controls.Add(_rangeHint);

            var query = new Button
            {
                Text = "查询",
                Width = 92,
                Height = UiTheme.ButtonHeight,
                Margin = new Padding(0, 18, 0, 0)
            };
            query.Click += delegate { Query(); };
            filters.Controls.Add(query);

            section.Controls.Add(filters, 0, 1);
            section.SetColumnSpan(filters, 2);

            section.Controls.Add(new Label
            {
                Text = "查询结果只读取本地 SQLite 数据；导出为 .xlsx，不需要安装 Microsoft Excel。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 6, 0, 0)
            }, 0, 2);
            section.SetColumnSpan(section.GetControlFromPosition(0, 2), 2);

            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            ConfigureChip(_rowChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_quantityChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            ConfigureChip(_amountChip, Color.FromArgb(252, 241, 226), UiTheme.Warning);

            chips.Controls.Add(_rowChip);
            chips.Controls.Add(_quantityChip);
            chips.Controls.Add(_amountChip);

            section.Controls.Add(chips, 0, 3);
            section.SetColumnSpan(chips, 2);

            return section;
        }

        private static void ResizeFilterFlow(FlowLayoutPanel filters)
        {
            if (filters == null || filters.ClientSize.Width <= 0)
                return;

            var preferred = filters.GetPreferredSize(
                new Size(filters.ClientSize.Width, 0));
            var nextHeight = Math.Max(58, preferred.Height);
            if (filters.Height != nextHeight)
                filters.Height = nextHeight;
        }


        private static Control CreateFilterField(string labelText, Control input, int width)
        {
            var field = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                MinimumSize = new Size(width, 0),
                Margin = new Padding(0, 0, 12, 0),
                Padding = Padding.Empty
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            field.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            input.Dock = DockStyle.Top;
            input.Width = width;
            input.Margin = Padding.Empty;
            field.Controls.Add(input, 0, 1);
            return field;
        }

        private static void ConfigureChip(Label label, Color backColor, Color foreColor)
        {
            label.AutoSize = true;
            label.Padding = new Padding(9, 5, 9, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8F, FontStyle.Bold);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoGenerateColumns = true;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        private Control CreateGridSection()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 10, 0, 0)
            };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前条件下没有可显示的数据";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.Font = UiTheme.Font(9F);
            _emptyState.BackColor = UiTheme.Surface;

            host.Controls.Add(_grid);
            host.Controls.Add(_emptyState);
            host.Controls.Add(new Label
            {
                Text = "报表明细",
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(12, 10, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(9.2F, FontStyle.Bold)
            });

            return host;
        }

        private void UpdateDateControls()
        {
            var option = _type.SelectedItem as ReportOption;
            var snapshot = option != null && option.Key == "snapshot";

            _from.Enabled = !snapshot;
            _rangeHint.Text = snapshot
                ? "库存快照按右侧日期结束时点计算"
                : "日期范围包含开始日和结束日";
        }

        private void Query()
        {
            try
            {
                var option = _type.SelectedItem as ReportOption;
                if (option == null)
                    return;

                if (option.Key != "snapshot" && _to.Value.Date < _from.Value.Date)
                {
                    MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _to.Focus();
                    return;
                }

                switch (option.Key)
                {
                    case "sales":
                        _current = _services.Reports.SalesDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "sales_return":
                        _current = _services.Reports.SalesReturnDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "purchase":
                        _current = _services.Reports.PurchaseDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "purchase_return":
                        _current = _services.Reports.PurchaseReturnDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "movement":
                        _current = _services.Reports.InventoryMovements(_from.Value.Date, _to.Value.Date);
                        break;
                    case "snapshot":
                        _current = _services.Reports.InventorySnapshot(_to.Value.Date);
                        break;
                    default:
                        throw new InvalidOperationException("未知报表类型。");
                }

                _grid.DataSource = _current;
                StyleColumns();
                UpdateSummary(option);
                ApplyResponsiveColumns();

                _emptyState.Visible = _current == null || _current.Rows.Count == 0;
                if (_emptyState.Visible)
                    _emptyState.BringToFront();
                else
                    _grid.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询失败：\r\n" + ex.Message, "报表查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateSummary(ReportOption option)
        {
            var rowCount = _current == null ? 0 : _current.Rows.Count;
            _rowChip.Text = "明细行  " + rowCount;

            var quantityColumn = QuantityColumnName(option.Key);
            var quantity = SumIntegerColumn(quantityColumn);
            _quantityChip.Visible = quantityColumn != null;

            switch (option.Key)
            {
                case "movement":
                    _quantityChip.Text = "净库存变动  " + quantity;
                    break;
                case "snapshot":
                    _quantityChip.Text = "库存合计  " + quantity + " 册";
                    break;
                case "sales_return":
                case "purchase_return":
                    _quantityChip.Text = "退货册数  " + quantity;
                    break;
                default:
                    _quantityChip.Text = "合计册数  " + quantity;
                    break;
            }

            var amountColumn = AmountColumnName(option.Key);
            _amountChip.Visible = amountColumn != null;
            if (amountColumn != null)
            {
                var amount = SumDecimalColumn(amountColumn);
                var amountLabel = option.Key == "sales_return"
                    ? "退款金额"
                    : option.Key == "purchase_return"
                        ? "退货金额"
                        : option.Key == "purchase"
                            ? "采购金额"
                            : "销售金额";
                _amountChip.Text = amountLabel + "  ¥" + amount.ToString("0.00");
            }

            _summary.Text = BuildSummaryText(option, rowCount);
        }

        private string BuildSummaryText(ReportOption option, int rowCount)
        {
            if (option.Key == "snapshot")
                return option.Text + " · 截至 " + _to.Value.ToString("yyyy-MM-dd") + " · " + rowCount + " 行";

            return option.Text + " · " +
                   _from.Value.ToString("yyyy-MM-dd") + " 至 " + _to.Value.ToString("yyyy-MM-dd") +
                   " · " + rowCount + " 行";
        }

        private string QuantityColumnName(string key)
        {
            switch (key)
            {
                case "sales":
                case "purchase":
                    return "数量";
                case "sales_return":
                case "purchase_return":
                    return "退货数量";
                case "movement":
                    return "数量变化";
                case "snapshot":
                    return "库存数量";
                default:
                    return null;
            }
        }

        private static string AmountColumnName(string key)
        {
            switch (key)
            {
                case "sales":
                case "purchase":
                    return "金额";
                case "sales_return":
                    return "退款金额";
                case "purchase_return":
                    return "退货金额";
                default:
                    return null;
            }
        }

        private long SumIntegerColumn(string columnName)
        {
            if (_current == null || columnName == null || !_current.Columns.Contains(columnName))
                return 0;

            long total = 0;
            foreach (DataRow row in _current.Rows)
            {
                if (row[columnName] != DBNull.Value)
                    total += Convert.ToInt64(row[columnName]);
            }
            return total;
        }

        private decimal SumDecimalColumn(string columnName)
        {
            if (_current == null || columnName == null || !_current.Columns.Contains(columnName))
                return 0m;

            decimal total = 0m;
            foreach (DataRow row in _current.Rows)
            {
                if (row[columnName] != DBNull.Value)
                    total += Convert.ToDecimal(row[columnName]);
            }
            return total;
        }

        private void StyleColumns()
        {
            SetColumnWidth("日期", 148);
            SetColumnWidth("销售单号", 168);
            SetColumnWidth("销售退货单号", 168);
            SetColumnWidth("采购单号", 168);
            SetColumnWidth("采购退货单号", 168);
            SetColumnWidth("原销售单号", 168);
            SetColumnWidth("原采购单号", 168);
            SetColumnWidth("单号", 168);
            SetColumnWidth("类型", 118);
            SetColumnWidth("供应商", 132);
            SetColumnWidth("ISBN", 132);
            SetColumnWidth("店内编码", 100);
            SetColumnWidth("作者", 118);
            SetColumnWidth("出版社", 124);
            SetColumnWidth("分类", 92);
            SetColumnWidth("货架位", 86);

            foreach (var name in new[]
            {
                "数量", "退货数量", "数量变化", "库存数量",
                "单价", "进价", "原售价", "原进价",
                "金额", "退款金额", "退货金额"
            })
            {
                if (_grid.Columns.Contains(name))
                {
                    _grid.Columns[name].Width =
                        name.Contains("金额") ? 98 :
                        name.Contains("价") ? 88 : 82;
                    _grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }

            if (_grid.Columns.Contains("书名"))
            {
                _grid.Columns["书名"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.Columns["书名"].MinimumWidth = 190;
                _grid.Columns["书名"].FillWeight = 220;
            }

            if (_grid.Columns.Contains("备注"))
            {
                _grid.Columns["备注"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.Columns["备注"].MinimumWidth = 150;
                _grid.Columns["备注"].FillWeight = 160;
            }
        }

        private void SetColumnWidth(string name, int width)
        {
            if (_grid.Columns.Contains(name))
                _grid.Columns[name].Width = width;
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;

            SetVisible("ISBN", width >= 760);
            SetVisible("供应商", width >= 830);
            SetVisible("原销售单号", width >= 930);
            SetVisible("原采购单号", width >= 930);
            SetVisible("备注", width >= 900);

            SetVisible("作者", width >= 840);
            SetVisible("出版社", width >= 980);
            SetVisible("分类", width >= 900);
            SetVisible("货架位", width >= 760);
            SetVisible("店内编码", width >= 700);
        }

        private void SetVisible(string name, bool visible)
        {
            if (_grid.Columns.Contains(name))
                _grid.Columns[name].Visible = visible;
        }

        private void Export()
        {
            try
            {
                if (_current == null)
                    Query();

                if (_current == null || _current.Rows.Count == 0)
                {
                    MessageBox.Show(this, "当前查询结果为空，没有可导出的数据。", "没有数据", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var option = _type.SelectedItem as ReportOption;
                var title = option == null ? "报表" : option.Text;
                var fileName = title + "_" + _to.Value.ToString("yyyyMMdd") + ".xlsx";

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                    dialog.DefaultExt = "xlsx";
                    dialog.AddExtension = true;
                    dialog.InitialDirectory = AppPaths.ExportDirectory;
                    dialog.FileName = fileName;

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    _services.Excel.Export(_current, dialog.FileName, title);
                    MessageBox.Show(
                        this,
                        "导出完成。\r\n" + dialog.FileName,
                        "Excel 已生成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：\r\n" + ex.Message, "无法导出 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class ReportOption
        {
            public ReportOption(string text, string key)
            {
                Text = text;
                Key = key;
            }

            public string Text { get; private set; }
            public string Key { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
