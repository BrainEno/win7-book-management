using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class DashboardForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Action<string> _navigate;
        private readonly Action _startGuide;

        private readonly TableLayoutPanel _metrics = new TableLayoutPanel();
        private readonly TableLayoutPanel _guideCard = new TableLayoutPanel();
        private readonly TableLayoutPanel _lower = new TableLayoutPanel();
        private readonly DataGridView _recentSales = new DataGridView();
        private readonly DataGridView _lowStock = new DataGridView();
        private readonly Label _updatedAt = new Label();
        private readonly Label _guideBody = new Label();
        private readonly Button _toggleGuide = new Button();

        private DashboardSummary _lastSummary;
        private int _lastThreshold;
        private bool _guideExpanded = true;
        private bool _metricLayoutInitialized;
        private bool _metricsCompact;

        public DashboardForm(ApplicationServices services, Action<string> navigate, Action startGuide)
        {
            _services = services;
            _navigate = navigate;
            _startGuide = startGuide;

            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            Padding = Padding.Empty;

            ConfigureGrid(_recentSales);
            ConfigureGrid(_lowStock);

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
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(CreateHero(), 0, 0);
            ConfigureMetricHost();
            root.Controls.Add(_metrics, 0, 1);
            BuildGuideCard();
            root.Controls.Add(_guideCard, 0, 2);
            ConfigureLower();
            root.Controls.Add(_lower, 0, 3);

            Controls.Add(root);

            Resize += delegate
            {
                ApplyResponsiveLayout();
                UpdateGuideBodyWidth();
            };

            UiTheme.Apply(this);

            Shown += delegate
            {
                ApplyGuideExpanded(_services.Settings.IsHomeGuideExpanded(), false);
                ReloadDashboard();
                ApplyResponsiveLayout();
                UpdateGuideBodyWidth();
            };
        }

        private Control CreateHero()
        {
            var hero = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Background,
                Padding = new Padding(2, 0, 2, 8),
                Margin = Padding.Empty
            };
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var text = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty
            };
            text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));

            text.Controls.Add(new Label
            {
                Text = "今天的书店，一眼看清",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(15F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true
            }, 0, 0);

            text.Controls.Add(new Label
            {
                Text = "净销售、库存和低库存提醒集中在这里；需要操作时直接从卡片进入对应模块。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(8.7F),
                ForeColor = UiTheme.TextSecondary,
                AutoEllipsis = true
            }, 0, 1);

            _updatedAt.Dock = DockStyle.Fill;
            _updatedAt.TextAlign = ContentAlignment.MiddleLeft;
            _updatedAt.ForeColor = UiTheme.TextSecondary;
            _updatedAt.Font = UiTheme.Font(7.7F);
            text.Controls.Add(_updatedAt, 0, 2);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(12, 12, 0, 0),
                BackColor = UiTheme.Background
            };

            var sales = new Button
            {
                Text = "销售开单",
                Width = 94,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            sales.Click += delegate
            {
                if (_navigate != null)
                    _navigate("sales");
            };

            var refresh = new Button
            {
                Text = "刷新",
                Width = 78,
                Height = UiTheme.ButtonHeight
            };
            refresh.Click += delegate { ReloadDashboard(); };

            actions.Controls.Add(sales);
            actions.Controls.Add(refresh);

            hero.Controls.Add(text, 0, 0);
            hero.Controls.Add(actions, 1, 0);
            return hero;
        }

        private void ConfigureMetricHost()
        {
            _metrics.Dock = DockStyle.Top;
            _metrics.Height = 116;
            _metrics.ColumnCount = 4;
            _metrics.RowCount = 1;
            _metrics.BackColor = UiTheme.Background;
            _metrics.Padding = new Padding(2, 0, 2, 8);
            _metrics.Margin = Padding.Empty;
        }

        private void BuildGuideCard()
        {
            _guideCard.Dock = DockStyle.Top;
            _guideCard.AutoSize = true;
            _guideCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _guideCard.ColumnCount = 1;
            _guideCard.RowCount = 2;
            _guideCard.BackColor = UiTheme.Surface;
            _guideCard.Margin = new Padding(2, 0, 2, 10);
            _guideCard.Padding = new Padding(14, 10, 14, 10);
            _guideCard.BorderStyle = BorderStyle.FixedSingle;
            _guideCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _guideCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            _guideCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "第一次使用？按这 6 步完成一套完整经营流程",
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                BackColor = UiTheme.Surface
            };

            _toggleGuide.Text = "收起";
            _toggleGuide.Width = 68;
            _toggleGuide.Height = UiTheme.ButtonHeight;
            _toggleGuide.Click += delegate { ApplyGuideExpanded(!_guideExpanded, true); };

            var interactive = new Button
            {
                Text = "开始逐步引导",
                Width = 118,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            interactive.Click += delegate
            {
                if (_startGuide != null)
                    _startGuide();
            };

            var fullHelp = new Button
            {
                Text = "完整说明",
                Width = 92,
                Height = UiTheme.ButtonHeight
            };
            fullHelp.Click += delegate
            {
                if (_navigate != null)
                    _navigate("help");
            };

            actions.Controls.Add(_toggleGuide);
            actions.Controls.Add(interactive);
            actions.Controls.Add(fullHelp);
            header.Controls.Add(actions, 1, 0);

            _guideBody.AutoSize = true;
            _guideBody.Dock = DockStyle.Top;
            _guideBody.Padding = new Padding(2, 7, 2, 2);
            _guideBody.ForeColor = UiTheme.TextSecondary;
            _guideBody.Font = UiTheme.Font(8.4F);
            _guideBody.Text =
                "① 图书资料：建立书目和 ISBN   →   ② 采购入库：进货后库存自动增加   →   ③ 销售开单：扫码结账后库存自动减少\r\n" +
                "④ 单据中心：查历史单据并从原单发起销售 / 采购退货   →   ⑤ 报表与导出：按日期查询并导出 Excel\r\n" +
                "⑥ 备份与恢复：每天关店前建议创建备份；正常销售和退货不要用“库存调整”代替。";

            _guideCard.Controls.Add(header, 0, 0);
            _guideCard.Controls.Add(_guideBody, 0, 1);
        }

        private void ConfigureLower()
        {
            _lower.Dock = DockStyle.Fill;
            _lower.ColumnCount = 2;
            _lower.RowCount = 1;
            _lower.Padding = new Padding(2, 0, 2, 2);
            _lower.Margin = Padding.Empty;
            _lower.BackColor = UiTheme.Background;
            _lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            _lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            _lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lower.Controls.Add(CreateTableCard(
                "最近销售单",
                "最近 10 张原销售单",
                _recentSales,
                "打开单据中心",
                delegate
                {
                    if (_navigate != null)
                        _navigate("documents");
                }), 0, 0);

            _lower.Controls.Add(CreateTableCard(
                "低库存",
                "达到当前提醒阈值的启用图书",
                _lowStock,
                "管理库存",
                delegate
                {
                    if (_navigate != null)
                        _navigate("inventory");
                }), 1, 0);
        }

        private void ApplyGuideExpanded(bool expanded, bool persist)
        {
            _guideExpanded = expanded;
            _guideBody.Visible = expanded;
            _toggleGuide.Text = expanded ? "收起" : "展开";

            if (persist)
                _services.Settings.SetHomeGuideExpanded(expanded);
        }

        private void ReloadDashboard()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var summary = _services.Dashboard.GetSummary(threshold);

            _lastSummary = summary;
            _lastThreshold = threshold;
            RebuildMetricCards(true);

            _recentSales.DataSource = _services.Dashboard.RecentSales(10);
            _lowStock.DataSource = _services.Dashboard.LowStock(threshold, 30);

            StyleRecentSalesGrid();
            StyleLowStockGrid();
            ApplyResponsiveGridColumns();

            _updatedAt.Text = "最后刷新：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void RebuildMetricCards(bool force)
        {
            if (_lastSummary == null)
                return;

            var compact = ClientSize.Width < 1080;
            if (!force && _metricLayoutInitialized && compact == _metricsCompact)
                return;

            _metricLayoutInitialized = true;
            _metricsCompact = compact;

            _metrics.SuspendLayout();
            while (_metrics.Controls.Count > 0)
            {
                var control = _metrics.Controls[0];
                _metrics.Controls.RemoveAt(0);
                control.Dispose();
            }
            _metrics.ColumnStyles.Clear();
            _metrics.RowStyles.Clear();

            _metrics.ColumnCount = compact ? 2 : 4;
            _metrics.RowCount = compact ? 2 : 1;
            _metrics.Height = compact ? 216 : 116;

            for (var column = 0; column < _metrics.ColumnCount; column++)
                _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / _metrics.ColumnCount));
            for (var row = 0; row < _metrics.RowCount; row++)
                _metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / _metrics.RowCount));

            var cards = new[]
            {
                CreateMetric(
                    "今日净销售额",
                    "¥" + Money.Format(_lastSummary.TodaySalesCent),
                    _lastSummary.TodaySalesOrders + " 单 · 净 " + _lastSummary.TodaySalesQuantity + " 册",
                    UiTheme.Accent),
                CreateMetric(
                    "本月净销售额",
                    "¥" + Money.Format(_lastSummary.MonthSalesCent),
                    "已扣除本月销售退货",
                    UiTheme.Success),
                CreateMetric(
                    "当前库存",
                    _lastSummary.StockUnits.ToString(),
                    _lastSummary.ActiveTitles + " 个启用品种",
                    UiTheme.TextPrimary),
                CreateMetric(
                    "低库存",
                    _lastSummary.LowStockTitles.ToString(),
                    "阈值 ≤ " + _lastThreshold + " 册",
                    _lastSummary.LowStockTitles > 0 ? UiTheme.Warning : UiTheme.Success)
            };

            for (var i = 0; i < cards.Length; i++)
            {
                var column = compact ? i % 2 : i;
                var row = compact ? i / 2 : 0;
                _metrics.Controls.Add(cards[i], column, row);
            }

            _metrics.ResumeLayout();
        }

        private static Panel CreateMetric(string caption, string value, string foot, Color valueColor)
        {
            var card = UiTheme.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 10, 8);
            card.Padding = new Padding(14, 9, 14, 7);

            var captionLabel = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F),
                AutoEllipsis = true
            };
            var valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                Height = 34,
                ForeColor = valueColor,
                Font = UiTheme.Font(15.5F, FontStyle.Bold),
                AutoEllipsis = true
            };
            var footLabel = new Label
            {
                Text = foot,
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(7.8F),
                AutoEllipsis = true
            };

            card.Controls.Add(footLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(captionLabel);
            return card;
        }

        private static Panel CreateTableCard(
            string title,
            string subtitle,
            DataGridView grid,
            string actionText,
            EventHandler action)
        {
            var card = UiTheme.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 10, 0);
            card.Padding = new Padding(14);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));

            var text = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            text.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10.2F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, 0);
            text.Controls.Add(new Label
            {
                Text = subtitle,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8F),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            }, 0, 1);

            var actionButton = new Button
            {
                Text = actionText,
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 8, 0, 8)
            };
            actionButton.Click += action;

            header.Controls.Add(text, 0, 0);
            header.Controls.Add(actionButton, 1, 0);

            card.Controls.Add(grid);
            card.Controls.Add(header);
            return card;
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = UiTheme.Surface;
        }

        private void StyleRecentSalesGrid()
        {
            if (_recentSales.Columns.Contains("时间"))
                _recentSales.Columns["时间"].Width = 138;
            if (_recentSales.Columns.Contains("销售单号"))
            {
                _recentSales.Columns["销售单号"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _recentSales.Columns["销售单号"].MinimumWidth = 120;
                _recentSales.Columns["销售单号"].FillWeight = 180;
            }
            SetNumericColumn(_recentSales, "册数", 64);
            SetNumericColumn(_recentSales, "金额", 86);
        }

        private void StyleLowStockGrid()
        {
            if (_lowStock.Columns.Contains("ISBN"))
                _lowStock.Columns["ISBN"].Width = 126;
            if (_lowStock.Columns.Contains("书名"))
            {
                _lowStock.Columns["书名"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _lowStock.Columns["书名"].MinimumWidth = 130;
                _lowStock.Columns["书名"].FillWeight = 190;
            }
            if (_lowStock.Columns.Contains("货架位"))
                _lowStock.Columns["货架位"].Width = 78;
            SetNumericColumn(_lowStock, "当前库存", 78);
        }

        private static void SetNumericColumn(DataGridView grid, string name, int width)
        {
            if (!grid.Columns.Contains(name))
                return;

            grid.Columns[name].Width = width;
            grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void ApplyResponsiveLayout()
        {
            RebuildMetricCards(false);

            var compact = ClientSize.Width < UiTheme.WideBreakpoint;
            _lower.ColumnStyles.Clear();
            _lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, compact ? 55F : 60F));
            _lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, compact ? 45F : 40F));

            ApplyResponsiveGridColumns();
        }

        private void ApplyResponsiveGridColumns()
        {
            var compact = ClientSize.Width < UiTheme.WideBreakpoint;

            if (_recentSales.Columns.Contains("时间"))
                _recentSales.Columns["时间"].Visible = !compact;

            if (_lowStock.Columns.Contains("ISBN"))
                _lowStock.Columns["ISBN"].Visible = !compact;

            if (_lowStock.Columns.Contains("货架位"))
                _lowStock.Columns["货架位"].Visible = ClientSize.Width >= 1040;
        }

        private void UpdateGuideBodyWidth()
        {
            if (_guideCard.ClientSize.Width > 80)
                _guideBody.MaximumSize = new Size(_guideCard.ClientSize.Width - 34, 0);
        }
    }
}
