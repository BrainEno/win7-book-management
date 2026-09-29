using System;
using System.Data;
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
        private readonly FlowLayoutPanel _metrics = new FlowLayoutPanel();
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
                Text = "销售、库存和低库存提醒都集中在这里。",
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
            _metrics.WrapContents = false;
            _metrics.AutoScroll = true;
            _metrics.BackColor = UiTheme.Background;
            _metrics.Padding = new Padding(4, 0, 0, 12);

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

            lower.Controls.Add(CreateTableCard("最近销售", "最近 10 张销售单", _recentSales, "查看销售报表", delegate
            {
                if (_navigate != null) _navigate("reports");
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
            _metrics.Controls.Add(CreateMetric("今日销售额", "¥" + Money.Format(summary.TodaySalesCent),
                summary.TodaySalesOrders + " 单 · " + summary.TodaySalesQuantity + " 册", UiTheme.Accent));
            _metrics.Controls.Add(CreateMetric("本月销售额", "¥" + Money.Format(summary.MonthSalesCent),
                "按自然月累计", UiTheme.Success));
            _metrics.Controls.Add(CreateMetric("当前库存", summary.StockUnits.ToString(),
                summary.ActiveTitles + " 个启用品种", UiTheme.TextPrimary));
            _metrics.Controls.Add(CreateMetric("低库存", summary.LowStockTitles.ToString(),
                "阈值 ≤ " + threshold + " 册", summary.LowStockTitles > 0 ? UiTheme.Warning : UiTheme.Success));
            _metrics.ResumeLayout();

            _recentSales.DataSource = _services.Dashboard.RecentSales(10);
            _lowStock.DataSource = _services.Dashboard.LowStock(threshold, 30);
            _updatedAt.Text = "最后刷新：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static Panel CreateMetric(string caption, string value, string foot, Color valueColor)
        {
            var card = UiTheme.CreateCard();
            card.Width = 220;
            card.Height = 104;
            card.Padding = new Padding(16, 12, 16, 10);

            var captionLabel = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 23,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F)
            };
            var valueLabel = new Label
            {
                Text = value,
                Dock = DockStyle.Top,
                Height = 38,
                ForeColor = valueColor,
                Font = UiTheme.Font(18F, FontStyle.Bold)
            };
            var footLabel = new Label
            {
                Text = foot,
                Dock = DockStyle.Top,
                Height = 21,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
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
            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            var subtitleLabel = new Label
            {
                Text = subtitle,
                Dock = DockStyle.Top,
                Height = 24,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextSecondary
            };
            var actionButton = new Button
            {
                Text = actionText,
                Dock = DockStyle.Right,
                Width = 104,
                Height = 30,
                Tag = "primary"
            };
            actionButton.Click += action;
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
