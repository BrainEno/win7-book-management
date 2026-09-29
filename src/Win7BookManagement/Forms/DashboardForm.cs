using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class DashboardForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Action<string> _navigate;
        private readonly Action _startGuide;
        private readonly TableLayoutPanel _metrics = new TableLayoutPanel();
        private readonly DataGridView _recentSales = new DataGridView();
        private readonly DataGridView _lowStock = new DataGridView();
        private readonly Label _updatedAt = new Label();
        private readonly Panel _guideCard = new Panel();
        private readonly Label _guideBody = new Label();
        private readonly Button _toggleGuide = new Button();

        public DashboardForm(ApplicationServices services, Action<string> navigate, Action startGuide)
        {
            _services = services;
            _navigate = navigate;
            _startGuide = startGuide;

            BackColor = UiTheme.Background;
            Padding = new Padding(0);

            var hero = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = UiTheme.Background,
                Padding = new Padding(4, 2, 4, 8)
            };

            var headline = new Label
            {
                Text = "今天的书店，一眼看清",
                Dock = DockStyle.Top,
                Height = 32,
                Font = UiTheme.Font(17F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            var sub = new Label
            {
                Text = "净销售、库存和低库存提醒都集中在这里；第一次使用请先看下面的操作指南。",
                Dock = DockStyle.Top,
                Height = 24,
                Font = UiTheme.Font(9F),
                ForeColor = UiTheme.TextSecondary
            };

            _updatedAt.Dock = DockStyle.Top;
            _updatedAt.Height = 18;
            _updatedAt.ForeColor = UiTheme.TextSecondary;
            _updatedAt.Font = UiTheme.Font(8F);

            hero.Controls.Add(_updatedAt);
            hero.Controls.Add(sub);
            hero.Controls.Add(headline);

            _metrics.Dock = DockStyle.Top;
            _metrics.Height = 118;
            _metrics.ColumnCount = 4;
            _metrics.RowCount = 1;
            _metrics.BackColor = UiTheme.Background;
            _metrics.Padding = new Padding(4, 0, 4, 8);
            for (var i = 0; i < 4; i++)
                _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            BuildGuideCard();

            var lower = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4, 0, 4, 4),
                BackColor = UiTheme.Background
            };
            lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            lower.Controls.Add(CreateTableCard("最近销售单", "最近 10 张原销售单", _recentSales, "打开单据中心", delegate
            {
                if (_navigate != null) _navigate("documents");
            }), 0, 0);

            lower.Controls.Add(CreateTableCard("低库存", "达到提醒阈值的启用图书", _lowStock, "管理库存", delegate
            {
                if (_navigate != null) _navigate("inventory");
            }), 1, 0);

            Controls.Add(lower);
            Controls.Add(_guideCard);
            Controls.Add(_metrics);
            Controls.Add(hero);

            ConfigureGrid(_recentSales);
            ConfigureGrid(_lowStock);
            UiTheme.Apply(this);

            Shown += delegate
            {
                ApplyGuideExpanded(_services.Settings.IsHomeGuideExpanded());
                ReloadDashboard();
            };
        }

        private void BuildGuideCard()
        {
            _guideCard.Dock = DockStyle.Top;
            _guideCard.Height = 166;
            _guideCard.BackColor = UiTheme.Surface;
            _guideCard.Margin = new Padding(4, 0, 4, 10);
            _guideCard.Padding = new Padding(18, 12, 18, 10);
            _guideCard.BorderStyle = BorderStyle.FixedSingle;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = UiTheme.Surface
            };

            var title = new Label
            {
                Text = "第一次用？照着这 6 步就不会出错",
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _toggleGuide.Text = "收起";
            _toggleGuide.Dock = DockStyle.Right;
            _toggleGuide.Width = 70;
            _toggleGuide.Height = 30;
            _toggleGuide.Click += delegate
            {
                ApplyGuideExpanded(_guideCard.Height > 70 ? false : true);
            };

            var interactive = new Button
            {
                Text = "开始逐步引导",
                Dock = DockStyle.Right,
                Width = 116,
                Height = 30,
                Tag = "primary"
            };
            interactive.Click += delegate { if (_startGuide != null) _startGuide(); };

            var fullHelp = new Button
            {
                Text = "完整说明",
                Dock = DockStyle.Right,
                Width = 90,
                Height = 30
            };
            fullHelp.Click += delegate { if (_navigate != null) _navigate("help"); };

            header.Controls.Add(title);
            header.Controls.Add(_toggleGuide);
            header.Controls.Add(interactive);
            header.Controls.Add(fullHelp);

            _guideBody.Dock = DockStyle.Fill;
            _guideBody.Padding = new Padding(0, 8, 0, 0);
            _guideBody.ForeColor = UiTheme.TextSecondary;
            _guideBody.Font = UiTheme.Font(8.8F);
            _guideBody.Text =
                "① 图书资料：先建立书目和 ISBN   →   ② 采购入库：进货后库存自动增加   →   ③ 销售开单：扫码结账后库存自动减少\r\n" +
                "④ 单据中心：查历史单据、销售退货、采购退货   →   ⑤ 报表与导出：按日期查销售/库存并导出 Excel\r\n" +
                "⑥ 备份与恢复：每天关店前建议备份一次；正常销售和退货都不要用“库存调整”代替。";

            _guideCard.Controls.Add(_guideBody);
            _guideCard.Controls.Add(header);
        }

        private void ApplyGuideExpanded(bool expanded)
        {
            _guideCard.Height = expanded ? 166 : 58;
            _guideBody.Visible = expanded;
            _toggleGuide.Text = expanded ? "收起" : "展开";
            _services.Settings.SetHomeGuideExpanded(expanded);
        }

        private void ReloadDashboard()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var summary = _services.Dashboard.GetSummary(threshold);

            _metrics.SuspendLayout();
            _metrics.Controls.Clear();
            _metrics.Controls.Add(CreateMetric("今日净销售额", "¥" + Money.Format(summary.TodaySalesCent),
                summary.TodaySalesOrders + " 单 · 净 " + summary.TodaySalesQuantity + " 册", UiTheme.Accent), 0, 0);
            _metrics.Controls.Add(CreateMetric("本月净销售额", "¥" + Money.Format(summary.MonthSalesCent),
                "已扣除本月销售退货", UiTheme.Success), 1, 0);
            _metrics.Controls.Add(CreateMetric("当前库存", summary.StockUnits.ToString(),
                summary.ActiveTitles + " 个启用品种", UiTheme.TextPrimary), 2, 0);
            _metrics.Controls.Add(CreateMetric("低库存", summary.LowStockTitles.ToString(),
                "阈值 ≤ " + threshold + " 册", summary.LowStockTitles > 0 ? UiTheme.Warning : UiTheme.Success), 3, 0);
            _metrics.ResumeLayout();

            _recentSales.DataSource = _services.Dashboard.RecentSales(10);
            _lowStock.DataSource = _services.Dashboard.LowStock(threshold, 30);
            _updatedAt.Text = "最后刷新：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                Height = 21,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F)
            };
            var valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                Height = 34,
                ForeColor = valueColor,
                Font = UiTheme.Font(17F, FontStyle.Bold),
                AutoEllipsis = true
            };
            var footLabel = new Label
            {
                Text = foot,
                Dock = DockStyle.Top,
                Height = 19,
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
            card.Margin = new Padding(0, 0, 12, 0);
            card.Padding = new Padding(16);

            var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = UiTheme.Surface };
            var actionButton = new Button
            {
                Text = actionText,
                Dock = DockStyle.Right,
                Width = 112,
                Height = 30,
                Tag = "primary"
            };
            actionButton.Click += action;

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 27,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true
            };
            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Top,
                Height = 23,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextSecondary,
                AutoEllipsis = true
            };

            header.Controls.Add(actionButton);
            header.Controls.Add(subtitleLabel);
            header.Controls.Add(titleLabel);

            grid.Dock = DockStyle.Fill;
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
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
    }
}
