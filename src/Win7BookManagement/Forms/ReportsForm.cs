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
        private readonly Label _returnAmountChip = new Label();
        private readonly Label _netAmountChip = new Label();
        private readonly Label _orderChip = new Label();
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
            _reportOptions.Add(new ReportOption("销售月报（模板）", "sales_monthly"));
            _reportOptions.Add(new ReportOption("经营日报", "operating_daily"));
            _reportOptions.Add(new ReportOption("月度经营摘要", "operating_monthly"));
            _reportOptions.Add(new ReportOption("销售退货明细", "sales_return"));
            _reportOptions.Add(new ReportOption("采购明细", "purchase"));
            _reportOptions.Add(new ReportOption("采购月报（工作簿）", "purchase_monthly"));
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
            ConfigureChip(_returnAmountChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            ConfigureChip(_netAmountChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_orderChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            _returnAmountChip.Visible = false;
            _netAmountChip.Visible = false;
            _orderChip.Visible = false;
            chips.Controls.Add(_rowChip);
            chips.Controls.Add(_quantityChip);
            chips.Controls.Add(_amountChip);
            chips.Controls.Add(_returnAmountChip);
            chips.Controls.Add(_netAmountChip);
            chips.Controls.Add(_orderChip);
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
            var key = option == null ? "" : option.Key;
            var snapshot = key == "snapshot";
            var daily = key == "operating_daily";
            var monthly = key == "sales_monthly" ||
                          key == "operating_monthly" ||
                          key == "purchase_monthly";

            _from.Enabled = !snapshot && !daily && !monthly;

            if (snapshot)
                _rangeHint.Text = "库存快照按右侧日期结束时点计算";
            else if (daily)
                _rangeHint.Text = "经营日报按右侧日期统计销售、退货、优惠、客单价、参考毛利与支付净额";
            else if (key == "sales_monthly")
                _rangeHint.Text = "销售月报按右侧日期所在月份生成；导出含月度总表 + 每日明细 Sheet";
            else if (key == "operating_monthly")
                _rangeHint.Text = "月度经营摘要按右侧日期所在月份统计；表格按日展开并附本月合计";
            else if (key == "purchase_monthly")
                _rangeHint.Text = "采购月报按右侧日期所在月份生成；含月汇总、供应商汇总与每日入库/退货明细";
            else
                _rangeHint.Text = "日期范围包含开始日和结束日";
        }

        private void Query()
        {
            try
            {
                var option = SelectedOption;
                if (option == null) return;

                if (!IsSingleDateOrMonthReport(option.Key) &&
                    ToDate < FromDate)
                {
                    MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _to.Focus();
                    return;
                }

                switch (option.Key)
                {
                    case "sales": _current = _services.Reports.SalesDetail(FromDate, ToDate); break;
                    case "sales_monthly":
                        var monthStart = new DateTime(ToDate.Year, ToDate.Month, 1);
                        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                        _current = _services.Reports.SalesDetail(monthStart, monthEnd);
                        break;
                    case "operating_daily":
                        _current = _services.Reports.OperatingDailySummary(ToDate);
                        break;
                    case "operating_monthly":
                        _current = _services.Reports.OperatingMonthlySummary(ToDate);
                        break;
                    case "sales_return": _current = _services.Reports.SalesReturnDetail(FromDate, ToDate); break;
                    case "purchase": _current = _services.Reports.PurchaseDetail(FromDate, ToDate); break;
                    case "purchase_monthly":
                        _current = _services.Reports.PurchaseMonthlySummary(ToDate);
                        break;
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

        private static bool IsSingleDateOrMonthReport(string key)
        {
            return key == "snapshot" ||
                   key == "sales_monthly" ||
                   key == "operating_daily" ||
                   key == "operating_monthly" ||
                   key == "purchase_monthly";
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
            if (name == "订单数" || name == "采购单数" ||
                name == "采购退货单数" || name == "供应商数") return "92";
            if (name.Contains("册数") || name.Contains("数量")) return "96";
            if (name.Contains("折扣%") || name.Contains("毛利率%")) return "118";
            if (name.Contains("净额")) return "112";
            if (name.Contains("价")) return "102";
            if (name.Contains("金额") || name == "净销售" ||
                name == "原金额" || name == "优惠额" ||
                name.Contains("成本") || name.Contains("毛利")) return "112";
            return "120";
        }

        private static bool IsMoneyColumn(string name)
        {
            return name.Contains("金额") ||
                   name.Contains("价") ||
                   name == "净销售" ||
                   name == "原金额" ||
                   name == "优惠额" ||
                   name.Contains("成本") ||
                   name.Contains("毛利") ||
                   name.Contains("净额") ||
                   name.Contains("折扣%");
        }

        private void UpdateSummary(ReportOption option)
        {
            var rowCount = _current == null ? 0 : _current.Rows.Count;
            _rowChip.Text = "明细行  " + rowCount;

            _returnAmountChip.Visible = false;
            _netAmountChip.Visible = false;
            _orderChip.Visible = false;

            if (option.Key == "operating_daily" ||
                option.Key == "operating_monthly")
            {
                var row = GetSummaryRow(option.Key == "operating_monthly");
                var period = option.Key == "operating_daily"
                    ? ToDate.ToString("yyyy-MM-dd")
                    : ToDate.ToString("yyyy-MM");

                var salesAmount = GetDecimal(row, "销售额");
                var returnAmount = GetDecimal(row, "退货额");
                var netSales = GetDecimal(row, "净销售");
                var orderCount = GetLong(row, "订单数");
                var netQuantity = GetLong(row, "净销售册数");
                var averageOrder = GetDecimal(row, "客单价");
                var originalAmount = GetDecimal(row, "原金额");
                var discountAmount = GetDecimal(row, "优惠额");
                var averageDiscount = GetDecimal(row, "平均成交折扣%");
                var referenceCost = GetDecimal(row, "参考成本");
                var referenceProfit = GetDecimal(row, "参考毛利");
                var referenceMargin = GetDecimal(row, "参考毛利率%");

                _rowChip.Text = option.Key == "operating_daily"
                    ? "经营日  " + period
                    : "统计天数  " + Math.Max(0, rowCount - 1);
                _quantityChip.Visible = true;
                _quantityChip.Text = "净销售册数  " + netQuantity;
                _amountChip.Visible = true;
                _amountChip.Text = "销售额  ¥" + salesAmount.ToString("0.00");
                _returnAmountChip.Visible = true;
                _returnAmountChip.Text = "退货额  ¥" + returnAmount.ToString("0.00");
                _netAmountChip.Visible = true;
                _netAmountChip.Text = "净销售  ¥" + netSales.ToString("0.00");
                _orderChip.Visible = true;
                _orderChip.Text = "订单  " + orderCount +
                    " · 客单价 ¥" + averageOrder.ToString("0.00");

                _summary.Text =
                    option.Text + " · " + period +
                    " · 原金额 ¥" + originalAmount.ToString("0.00") +
                    " · 优惠 ¥" + discountAmount.ToString("0.00") +
                    " · 平均成交折扣 " + averageDiscount.ToString("0.00") + "%" +
                    " · 参考成本 ¥" + referenceCost.ToString("0.00") +
                    " · 参考毛利 ¥" + referenceProfit.ToString("0.00") +
                    "（" + referenceMargin.ToString("0.00") + "%）" +
                    " · 支付净额：微信 ¥" + GetDecimal(row, "微信净额").ToString("0.00") +
                    " / 支付宝 ¥" + GetDecimal(row, "支付宝净额").ToString("0.00") +
                    " / 现金 ¥" + GetDecimal(row, "现金净额").ToString("0.00") +
                    " / 其他 ¥" + GetDecimal(row, "其他净额").ToString("0.00");
                return;
            }

            if (option.Key == "purchase_monthly")
            {
                var row = GetSummaryRow(true);
                var purchaseAmount = GetDecimal(row, "采购金额");
                var returnAmount = GetDecimal(row, "退货金额");
                var netAmount = GetDecimal(row, "净采购金额");
                var purchaseCount = GetLong(row, "采购单数");
                var supplierCount = GetLong(row, "供应商数");
                var netQuantity = GetLong(row, "净入库册数");

                _rowChip.Text = "统计天数  " + Math.Max(0, rowCount - 1);
                _quantityChip.Visible = true;
                _quantityChip.Text = "净入库册数  " + netQuantity;
                _amountChip.Visible = true;
                _amountChip.Text = "采购金额  ¥" + purchaseAmount.ToString("0.00");
                _returnAmountChip.Visible = true;
                _returnAmountChip.Text = "退货金额  ¥" + returnAmount.ToString("0.00");
                _netAmountChip.Visible = true;
                _netAmountChip.Text = "净采购  ¥" + netAmount.ToString("0.00");
                _orderChip.Visible = true;
                _orderChip.Text = "采购单  " + purchaseCount +
                    " · 供应商 " + supplierCount;

                _summary.Text = option.Text + " · " + ToDate.ToString("yyyy-MM") +
                    " · 净采购=已复核采购金额-按退货发生日期统计的采购退货金额" +
                    " · 导出生成月汇总、供应商汇总及逐日采购/退货明细";
                return;
            }

            if (option.Key == "sales_monthly")
            {
                var summaryTable = _services.Reports.SalesMonthlySummary(ToDate);
                var summaryRow = summaryTable.Rows.Count == 0 ? null : summaryTable.Rows[0];

                var grossCent = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["gross_sales_cent"]);
                var returnCent = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["return_amount_cent"]);
                var netCent = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["net_sales_cent"]);
                var salesQty = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["sales_quantity"]);
                var returnQty = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["return_quantity"]);
                var orderCount = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["order_count"]);
                var averageOrderCent = summaryRow == null ? 0L : Convert.ToInt64(summaryRow["average_order_cent"]);

                _quantityChip.Visible = true;
                _quantityChip.Text =
                    "净销售册数  " + (salesQty - returnQty) +
                    "（售 " + salesQty + " / 退 " + returnQty + "）";
                _amountChip.Visible = true;
                _amountChip.Text = "销售额  ¥" + (grossCent / 100m).ToString("0.00");
                _returnAmountChip.Visible = true;
                _returnAmountChip.Text = "退货额  ¥" + (returnCent / 100m).ToString("0.00");
                _netAmountChip.Visible = true;
                _netAmountChip.Text = "净销售  ¥" + (netCent / 100m).ToString("0.00");
                _orderChip.Visible = true;
                _orderChip.Text =
                    "订单  " + orderCount +
                    " · 客单价 ¥" + (averageOrderCent / 100m).ToString("0.00");

                _summary.Text = option.Text + " · " + ToDate.ToString("yyyy-MM") +
                    " · 净销售按退货发生日期扣减 · 导出生成月度总表与逐日明细";
                return;
            }

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
                : option.Text + " · " + FromDate.ToString("yyyy-MM-dd") +
                  " 至 " + ToDate.ToString("yyyy-MM-dd") + " · " + rowCount + " 行";
        }

        private DataRow GetSummaryRow(bool useLastRow)
        {
            if (_current == null || _current.Rows.Count == 0)
                return null;
            return useLastRow
                ? _current.Rows[_current.Rows.Count - 1]
                : _current.Rows[0];
        }

        private static long GetLong(DataRow row, string column)
        {
            return row == null || !row.Table.Columns.Contains(column) ||
                   row[column] == DBNull.Value
                ? 0L
                : Convert.ToInt64(row[column]);
        }

        private static decimal GetDecimal(DataRow row, string column)
        {
            return row == null || !row.Table.Columns.Contains(column) ||
                   row[column] == DBNull.Value
                ? 0m
                : Convert.ToDecimal(row[column]);
        }

        private static string QuantityColumnName(string key)
        {
            switch (key)
            {
                case "sales":
                case "sales_monthly":
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
                case "sales_monthly":
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
                var option = SelectedOption;
                if (option == null) return;

                if (option.Key == "sales_monthly")
                {
                    ExportSalesMonthly();
                    return;
                }
                if (option.Key == "purchase_monthly")
                {
                    ExportPurchaseMonthly();
                    return;
                }

                if (_current == null) Query();
                if (_current == null || _current.Rows.Count == 0)
                {
                    MessageBox.Show(this, "当前查询结果为空，没有可导出的数据。", "没有数据", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var title = option.Text;
                var fileName = option.Key == "operating_monthly"
                    ? title + "_" + ToDate.ToString("yyyyMM") + ".xlsx"
                    : title + "_" + ToDate.ToString("yyyyMMdd") + ".xlsx";

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

        private void ExportSalesMonthly()
        {
            var month = new DateTime(ToDate.Year, ToDate.Month, 1);
            var fileName = "销售月报_" + month.ToString("yyyyMM") + ".xlsx";

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.InitialDirectory = AppPaths.ExportDirectory;
                dialog.FileName = fileName;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var detail = _services.Reports.SalesMonthlyExportDetail(month);
                var returnDetail = _services.Reports.SalesMonthlyReturnExportDetail(month);
                _services.SalesMonthlyExcel.Export(
                    detail,
                    returnDetail,
                    dialog.FileName,
                    month,
                    _services.Settings.GetReportStoreName(),
                    _services.Settings.GetReportNightShiftStartHour(),
                    _services.Settings.GetReportCategoryMapping());

                MessageBox.Show(
                    this,
                    "销售月报已生成。\r\n\r\n包含：\r\n" +
                    "• 销售月报表：销售额、销售退货、净销售以及各收款方式的收款 / 退款 / 净额\r\n" +
                    "• 每日明细：按时间和单号排序，同一单号的单据级字段自动合并\r\n" +
                    "• 参考成本口径与当月 / 累计退货数量均写入明细，便于复核\r\n\r\n" +
                    dialog.FileName,
                    "Excel 已生成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ExportPurchaseMonthly()
        {
            var month = new DateTime(ToDate.Year, ToDate.Month, 1);
            var fileName = "采购月报_" + month.ToString("yyyyMM") + ".xlsx";

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.DefaultExt = "xlsx";
                dialog.AddExtension = true;
                dialog.InitialDirectory = AppPaths.ExportDirectory;
                dialog.FileName = fileName;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var detail = _services.Reports.PurchaseMonthlyExportDetail(month);
                var returnDetail = _services.Reports.PurchaseMonthlyReturnExportDetail(month);
                _services.PurchaseMonthlyExcel.Export(
                    detail,
                    returnDetail,
                    dialog.FileName,
                    month,
                    _services.Settings.GetReportStoreName());

                MessageBox.Show(
                    this,
                    "采购月报已生成。\r\n\r\n包含：\r\n" +
                    "• 采购月报表：按日汇总采购入库、采购退货与净采购\r\n" +
                    "• 供应商汇总：按供应商核对采购金额、退货金额与净采购金额\r\n" +
                    "• 每日明细：采购入库与采购退货分区，同一单号字段自动合并\r\n" +
                    "• 已配置 A4、横向打印、页边距、重复表头、打印区域与页码\r\n\r\n" +
                    dialog.FileName,
                    "Excel 已生成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
