using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class ReportsForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Select _type = new AntdUI.Select();
        private readonly List<ReportOption> _reportOptions = new List<ReportOption>();
        private readonly AntdUI.DatePicker _from = new AntdUI.DatePicker();
        private readonly AntdUI.DatePicker _to = new AntdUI.DatePicker();
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Dictionary<string, AntdUI.Column> _columns = new Dictionary<string, AntdUI.Column>();
        private readonly Label _rowChip = new Label();
        private readonly Label _quantityChip = new Label();
        private readonly Label _amountChip = new Label();
        private readonly Label _rangeHint = new Label();
        private readonly Label _emptyState = new Label();
        private readonly Label _summary = new Label();
        private readonly AntdUI.Button _exportButton;

        private DataTable _current;

        public ReportsForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            _exportButton = UiTheme.CreateAntdButton("导出 Excel", true);
            _exportButton.Width = 112;

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
            _summary.MinimumSize = new Size(0, 38);
            _summary.Padding = new Padding(12, 8, 8, 8);
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.2F);
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
            _reportOptions.Add(new ReportOption("销售明细", "sales"));
            _reportOptions.Add(new ReportOption("销售退货明细", "sales_return"));
            _reportOptions.Add(new ReportOption("采购明细", "purchase"));
            _reportOptions.Add(new ReportOption("采购退货明细", "purchase_return"));
            _reportOptions.Add(new ReportOption("库存变动明细", "movement"));
            _reportOptions.Add(new ReportOption("指定日期库存快照", "snapshot"));

            foreach (var option in _reportOptions) _type.Items.Add(option.Text);
            _type.SelectedIndex = 0;
            _type.DropDownArrow = true;

            _type.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e)
            {
                UpdateDateControls();
                Query();
            };

            _from.Format = "yyyy-MM-dd";
            _to.Format = "yyyy-MM-dd";
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;
            _exportButton.Click += delegate { Export(); };
        }

        private DateTime FromDate { get { return (_from.Value ?? DateTime.Today).Date; } }
        private DateTime ToDate { get { return (_to.Value ?? DateTime.Today).Date; } }

        private ReportOption SelectedOption
        {
            get
            {
                var index = _type.SelectedIndex;
                return index >= 0 && index < _reportOptions.Count ? _reportOptions[index] : null;
            }
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
                Padding = new Padding(18, 15, 18, 15),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "报表查询与导出",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            _exportButton.Margin = Padding.Empty;
            section.Controls.Add(_exportButton, 1, 0);

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 62,
                MinimumSize = new Size(0, 62),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 0),
                Padding = Padding.Empty
            };
            filters.SizeChanged += delegate { ResizeFilterFlow(filters); };
            filters.Controls.Add(CreateFilterField("报表类型", _type, 190));
            filters.Controls.Add(CreateFilterField("开始日期", _from, 136));
            filters.Controls.Add(CreateFilterField("结束 / 快照", _to, 136));

            _rangeHint.AutoSize = true;
            _rangeHint.MaximumSize = new Size(240, 0);
            _rangeHint.ForeColor = UiTheme.TextSecondary;
            _rangeHint.Font = UiTheme.Font(8F);
            _rangeHint.Margin = new Padding(8, 26, 10, 0);
            filters.Controls.Add(_rangeHint);

            var query = UiTheme.CreateAntdButton("查询", true);
            query.Width = 96;
            query.Margin = new Padding(0, 19, 0, 0);
            query.Click += delegate { Query(); };
            filters.Controls.Add(query);

            section.Controls.Add(filters, 0, 1);
            section.SetColumnSpan(filters, 2);

            var hint = new Label
            {
                Text = "查询只读取本地 SQLite；导出为 .xlsx，不依赖 Microsoft Excel。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 6, 0, 0)
            };
            section.Controls.Add(hint, 0, 2);
            section.SetColumnSpan(hint, 2);

            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
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
            if (filters == null || filters.ClientSize.Width <= 0) return;
            var preferred = filters.GetPreferredSize(new Size(filters.ClientSize.Width, 0));
            var nextHeight = Math.Max(62, preferred.Height);
            if (filters.Height != nextHeight) filters.Height = nextHeight;
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
                Margin = new Padding(0, 0, 12, 0)
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
            label.Padding = new Padding(10, 5, 10, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8.2F, FontStyle.Bold);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;

            _grid.RowHeight = 46;
            _grid.RowHeightHeader = 46;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前条件下没有可显示的数据";

        }

        private Control CreateGridSection()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
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
            _emptyState.BringToFront();
            return host;
        }

        private void UpdateDateControls()
        {
            var option = SelectedOption;
            var snapshot = option != null && option.Key == "snapshot";
            _from.Enabled = !snapshot;
            _rangeHint.Text = snapshot ? "库存快照按右侧日期结束时点计算" : "日期范围包含开始日和结束日";
        }

        private void Query()
        {
            try
            {
                var option = SelectedOption;
                if (option == null) return;

                if (option.Key != "snapshot" && ToDate < FromDate)
                {
                    MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _to.Focus();
                    return;
                }

                switch (option.Key)
                {
                    case "sales": _current = _services.Reports.SalesDetail(FromDate, ToDate); break;
                    case "sales_return": _current = _services.Reports.SalesReturnDetail(FromDate, ToDate); break;
                    case "purchase": _current = _services.Reports.PurchaseDetail(FromDate, ToDate); break;
                    case "purchase_return": _current = _services.Reports.PurchaseReturnDetail(FromDate, ToDate); break;
                    case "movement": _current = _services.Reports.InventoryMovements(FromDate, ToDate); break;
                    case "snapshot": _current = _services.Reports.InventorySnapshot(ToDate); break;
                    default: throw new InvalidOperationException("未知报表类型。");
                }

                BuildColumns();
                _grid.DataSource = _current;
                UpdateSummary(option);
                ApplyResponsiveColumns();

                _emptyState.Visible = _current == null || _current.Rows.Count == 0;
                if (_emptyState.Visible) _emptyState.BringToFront();
                else _grid.BringToFront();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询失败：\r\n" + ex.Message, "报表查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BuildColumns()
        {
            _columns.Clear();
            var collection = new AntdUI.ColumnCollection();
            if (_current != null)
            {
                foreach (DataColumn dataColumn in _current.Columns)
                {
                    var name = dataColumn.ColumnName;
                    var column = new AntdUI.Column(name, name)
                    {
                        Width = PreferredWidth(name),
                        Ellipsis = name == "书名" || name == "备注"
                    };
                    if (name == "书名") { column.Width = "auto"; column.MinWidth = "210"; }
                    if (name == "备注") { column.Width = "auto"; column.MinWidth = "170"; }
                    if (IsMoneyColumn(name)) column.DisplayFormat = "0.00";
                    _columns[name] = column;
                    collection.Add(column);
                }
            }
            _grid.Columns = collection;
        }

        private static string PreferredWidth(string name)
        {
            if (name == "日期") return "150";
            if (name.Contains("单号")) return "170";
            if (name == "类型") return "118";
            if (name == "供应商") return "136";
            if (name == "ISBN") return "140";
            if (name == "店内编码") return "112";
            if (name == "作者") return "120";
            if (name == "出版社") return "128";
            if (name == "分类") return "96";
            if (name == "货架位") return "92";
            if (name.Contains("数量")) return "88";
            if (name.Contains("价")) return "96";
            if (name.Contains("金额")) return "108";
            return "120";
        }

        private static bool IsMoneyColumn(string name)
        {
            return name.Contains("金额") || name.Contains("价");
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
                case "movement": _quantityChip.Text = "净库存变动  " + quantity; break;
                case "snapshot": _quantityChip.Text = "库存合计  " + quantity + " 册"; break;
                case "sales_return":
                case "purchase_return": _quantityChip.Text = "退货册数  " + quantity; break;
                default: _quantityChip.Text = "合计册数  " + quantity; break;
            }

            var amountColumn = AmountColumnName(option.Key);
            _amountChip.Visible = amountColumn != null;
            if (amountColumn != null)
            {
                var amount = SumDecimalColumn(amountColumn);
                var label = option.Key == "sales_return" ? "退款金额" :
                    option.Key == "purchase_return" ? "退货金额" :
                    option.Key == "purchase" ? "采购金额" : "销售金额";
                _amountChip.Text = label + "  ¥" + amount.ToString("0.00");
            }

            _summary.Text = option.Key == "snapshot"
                ? option.Text + " · 截至 " + ToDate.ToString("yyyy-MM-dd") + " · " + rowCount + " 行"
                : option.Text + " · " + FromDate.ToString("yyyy-MM-dd") + " 至 " + ToDate.ToString("yyyy-MM-dd") + " · " + rowCount + " 行";
        }

        private static string QuantityColumnName(string key)
        {
            switch (key)
            {
                case "sales":
                case "purchase": return "数量";
                case "sales_return":
                case "purchase_return": return "退货数量";
                case "movement": return "数量变化";
                case "snapshot": return "库存数量";
                default: return null;
            }
        }

        private static string AmountColumnName(string key)
        {
            switch (key)
            {
                case "sales":
                case "purchase": return "金额";
                case "sales_return": return "退款金额";
                case "purchase_return": return "退货金额";
                default: return null;
            }
        }

        private long SumIntegerColumn(string columnName)
        {
            if (_current == null || columnName == null || !_current.Columns.Contains(columnName)) return 0;
            long total = 0;
            foreach (DataRow row in _current.Rows)
                if (row[columnName] != DBNull.Value) total += Convert.ToInt64(row[columnName]);
            return total;
        }

        private decimal SumDecimalColumn(string columnName)
        {
            if (_current == null || columnName == null || !_current.Columns.Contains(columnName)) return 0m;
            decimal total = 0m;
            foreach (DataRow row in _current.Rows)
                if (row[columnName] != DBNull.Value) total += Convert.ToDecimal(row[columnName]);
            return total;
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
            _grid.LoadLayout();
        }

        private void SetVisible(string name, bool visible)
        {
            AntdUI.Column column;
            if (_columns.TryGetValue(name, out column)) column.Visible = visible;
        }

        private void Export()
        {
            try
            {
                if (_current == null) Query();
                if (_current == null || _current.Rows.Count == 0)
                {
                    MessageBox.Show(this, "当前查询结果为空，没有可导出的数据。", "没有数据", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var option = SelectedOption;
                var title = option == null ? "报表" : option.Text;
                var fileName = title + "_" + ToDate.ToString("yyyyMMdd") + ".xlsx";

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                    dialog.DefaultExt = "xlsx";
                    dialog.AddExtension = true;
                    dialog.InitialDirectory = AppPaths.ExportDirectory;
                    dialog.FileName = fileName;

                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    _services.Excel.Export(_current, dialog.FileName, title);
                    MessageBox.Show(this, "导出完成。\r\n" + dialog.FileName, "Excel 已生成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：\r\n" + ex.Message, "无法导出 Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class ReportOption
        {
            public ReportOption(string text, string key) { Text = text; Key = key; }
            public string Text { get; private set; }
            public string Key { get; private set; }
        }
    }
}
