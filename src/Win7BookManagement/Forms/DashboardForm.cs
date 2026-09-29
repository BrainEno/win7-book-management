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
        private readonly TableLayoutPanel _metrics = new TableLayoutPanel();
        private readonly DataGridView _recentSales = new DataGridView();
        private readonly DataGridView _lowStock = new DataGridView();
        private readonly Label _updatedAt = new Label();

        public DashboardForm(ApplicationServices services, Action<string> navigate)
        {
            _services = services;
            _navigate = navigate;

            BackColor = UiTheme.Background;
            Padding = new Padding(0);

            var hero = new Panel
            {
                Dock = DockStyle.Top,
                Height = 94,
                BackColor = UiTheme.Background,
                Padding = new Padding(4, 2, 4, 14)
            };

            var headline = new Label
            {
                Text = "今天的书店，一眼看清",
                Dock = DockStyle.Top,
                Height = 34,
                Font = UiTheme.Font(17F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            var sub = new Label
            {
                Text = "净销售、库存和低库存提醒都集中在这里；退货会实时扣减净销售。",
                Dock = DockStyle.Top,
                Height = 26,
                Font = UiTheme.Font(9F),
                ForeColor = UiTheme.TextSecondary
            };

            _updatedAt.Dock = DockStyle.Top;
            _updatedAt.Height = 20;
            _updatedAt.ForeColor = UiTheme.TextSecondary;
            _updatedAt.Font = UiTheme.Font(8F);

            hero.Controls.Add(_updatedAt);
            hero.Controls.Add(sub);
            hero.Controls.Add(headline);

            _metrics.Dock = DockStyle.Top;
            _metrics.Height = 128;
            _metrics.ColumnCount = 4;
            _metrics.RowCount = 1;
            _metrics.BackColor = UiTheme.Background;
            _metrics.Padding = new Padding(4, 0, 4, 12);
            for (var i = 0; i < 4; i++)
                _metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

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
            Controls.Add(_metrics);
            Controls.Add(hero);

            ConfigureGrid(_recentSales);
            ConfigureGrid(_lowStock);
            UiTheme.Apply(this);

            Shown += delegate { ReloadDashboard(); };
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
            card.Margin = new Padding(0, 0, 10, 12);
            card.Padding = new Padding(14, 10, 14, 8);

            var captionLabel = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F)
            };
            var valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                Height = 36,
                ForeColor = valueColor,
                Font = UiTheme.Font(17F, FontStyle.Bold),
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
            card.Margin = new Padding(0, 0, 12, 0);
            card.Padding = new Padding(16);

            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = UiTheme.Surface };
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
                Height = 28,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true
            };
            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Top,
                Height = 24,
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
