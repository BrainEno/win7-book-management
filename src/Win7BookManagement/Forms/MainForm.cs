using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class MainForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Panel _sidebar;
        private readonly FlowLayoutPanel _navigation;
        private readonly Panel _contentHost;
        private readonly Label _pageTitle;
        private readonly Label _pageSubtitle;
        private readonly Label _status;

        private readonly Dictionary<string, AntdUI.Button> _navButtons = new Dictionary<string, AntdUI.Button>();
        private readonly Dictionary<string, TableLayoutPanel> _navRows = new Dictionary<string, TableLayoutPanel>();
        private readonly Dictionary<string, Panel> _navIndicators = new Dictionary<string, Panel>();
        private readonly List<Label> _navGroupLabels = new List<Label>();

        private string _currentKey;
        private Form _currentPage;
        private bool _guideOpen;

        public MainForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);

            Text = "简易图书管理系统";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1400;
            Height = 860;
            MinimumSize = new Size(960, 640);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9F);

            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 226,
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(14, 16, 14, 12)
            };

            var brand = CreateBrand();
            _navigation = CreateNavigation();

            AddNavigationGroup("工作台");
            AddNavigation("dashboard", "经营概览");

            AddNavigationGroup("核心业务");
            AddNavigation("sales", "销售开单");
            AddNavigation("books", "图书资料");
            AddNavigation("purchase", "采购入库");
            AddNavigation("inventory", "库存管理");
            AddNavigation("documents", "单据中心");

            AddNavigationGroup("经营管理");
            AddNavigation("suppliers", "供应商");
            AddNavigation("reports", "报表与导出");

            AddNavigationGroup("系统");
            AddNavigation("backup", "备份与恢复");
            AddNavigation("help", "使用帮助");
            AddNavigation("settings", "系统设置");

            var sidebarFoot = CreateSidebarFooter();

            _sidebar.Controls.Add(_navigation);
            _sidebar.Controls.Add(sidebarFoot);
            _sidebar.Controls.Add(brand);

            var main = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 80),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 11, 22, 9),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var titles = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            titles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _pageTitle = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(16F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Text = "经营概览",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _pageSubtitle = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8.7F),
                ForeColor = UiTheme.TextSecondary,
                Text = "今天的销售与库存情况",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            titles.Controls.Add(_pageTitle, 0, 0);
            titles.Controls.Add(_pageSubtitle, 0, 1);

            var offlineBadge = new Label
            {
                Text = "●  本机离线",
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 6),
                Margin = new Padding(12, 10, 0, 0),
                BackColor = UiTheme.AccentSoft,
                ForeColor = UiTheme.Success,
                Font = UiTheme.Font(8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            header.Controls.Add(titles, 0, 0);
            header.Controls.Add(offlineBadge, 1, 0);

            _status = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Bottom,
                MinimumSize = new Size(0, 32),
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextSecondary,
                Padding = new Padding(18, 7, 8, 0),
                Font = UiTheme.Font(7.8F),
                AutoEllipsis = true,
                Text = "完全离线 · 数据库：" + _services.Database.DatabasePath
            };

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background,
                Padding = new Padding(20, 16, 20, 16)
            };

            main.Controls.Add(_contentHost);
            main.Controls.Add(_status);
            main.Controls.Add(header);

            Controls.Add(main);
            Controls.Add(_sidebar);

            UiTheme.Apply(this);

            Resize += delegate { ApplyResponsiveLayout(); };
            FormClosing += HandleFormClosing;
            Shown += delegate
            {
                Navigate("dashboard");
                if (!_services.Settings.IsOnboardingCompleted())
                    BeginInvoke(new Action(StartOnboardingGuide));
            };

            ApplyResponsiveLayout();
        }

        private static Panel CreateBrand()
        {
            var brand = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 76),
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(4, 4, 4, 0)
            };

            var title = new Label
            {
                Text = "BOOK DESK",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(15F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var sub = new Label
            {
                Text = "独立书店 · 离线进销存",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            brand.Controls.Add(sub);
            brand.Controls.Add(title);
            return brand;
        }

        private FlowLayoutPanel CreateNavigation()
        {
            return new VerticalNavigationPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(0, 2, 0, 2),
                AutoScroll = true,
                Margin = Padding.Empty
            };
        }

        private static Panel CreateSidebarFooter()
        {
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 58),
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(4, 9, 4, 0)
            };

            var offline = new Label
            {
                Text = "●  本机离线模式",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.Success,
                Font = UiTheme.Font(8.2F, FontStyle.Bold)
            };
            var version = new Label
            {
                Text = ".NET Framework 4.8 · SQLite",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(7.5F)
            };

            footer.Controls.Add(version);
            footer.Controls.Add(offline);
            return footer;
        }

        public void Navigate(string key)
        {
            if (string.Equals(key, _currentKey, StringComparison.OrdinalIgnoreCase))
                return;

            var guard = _currentPage as INavigationGuard;
            if (guard != null && !guard.CanNavigateAway(this))
                return;

            Form child;
            string title;
            string subtitle;

            switch (key)
            {
                case "dashboard":
                    title = "经营概览";
                    subtitle = "今天做什么、从哪里开始，都可以从这里看";
                    child = new DashboardForm(_services, Navigate, StartOnboardingGuide);
                    break;
                case "books":
                    title = "图书资料";
                    subtitle = "维护书目信息、货架位和经营价格";
                    child = new BookListForm(_services);
                    break;
                case "purchase":
                    title = "采购入库";
                    subtitle = "登记进货并自动增加库存";
                    child = new PurchaseForm(_services);
                    break;
                case "sales":
                    title = "销售开单";
                    subtitle = "扫描 ISBN，确认后自动扣减库存";
                    child = new SalesForm(_services);
                    break;
                case "documents":
                    title = "单据中心";
                    subtitle = "查看销售、采购和退货历史，并从原单据发起退货";
                    child = new DocumentCenterForm(_services);
                    break;
                case "inventory":
                    title = "库存管理";
                    subtitle = "查询当前库存并进行有记录的库存调整";
                    child = new InventoryForm(_services);
                    break;
                case "suppliers":
                    title = "供应商";
                    subtitle = "维护常用供货方资料";
                    child = new SupplierForm(_services);
                    break;
                case "reports":
                    title = "报表与导出";
                    subtitle = "按日期查询经营数据并导出 Excel";
                    child = new ReportsForm(_services);
                    break;
                case "backup":
                    title = "备份与恢复";
                    subtitle = "保护本机 SQLite 数据，建议每天关店前备份";
                    child = new BackupForm(_services);
                    break;
                case "help":
                    title = "使用帮助";
                    subtitle = "按最简单的路线完成日常书店操作";
                    child = new HelpForm(_services, StartOnboardingGuide, Navigate);
                    break;
                case "settings":
                    title = "系统设置";
                    subtitle = "调整低库存提醒等本机设置";
                    child = new SettingsForm(_services);
                    break;
                default:
                    return;
            }

            if (_currentPage != null)
            {
                _contentHost.Controls.Remove(_currentPage);
                _currentPage.Dispose();
            }

            _currentKey = key;
            _currentPage = child;
            _pageTitle.Text = title;
            _pageSubtitle.Text = subtitle;
            UpdateNavigationState();

            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            UiTheme.Apply(child);
            _contentHost.Controls.Add(child);
            child.Show();
        }

        public void StartOnboardingGuide()
        {
            if (_guideOpen)
                return;

            _guideOpen = true;
            try
            {
                using (var guide = new OnboardingGuideForm(this, _services))
                    guide.ShowDialog(this);
            }
            finally
            {
                _guideOpen = false;
            }
        }

        public Rectangle GetNavigationScreenBounds(string key)
        {
            AntdUI.Button button;
            if (!_navButtons.TryGetValue(key, out button) || !button.Visible)
                return Rectangle.Empty;

            return button.RectangleToScreen(button.ClientRectangle);
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            var guard = _currentPage as INavigationGuard;
            if (guard != null && !guard.CanNavigateAway(this))
                e.Cancel = true;
        }

        private void AddNavigationGroup(string text)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                Width = 180,
                MinimumSize = new Size(0, 32),
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(0, 7, 0, 0),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.NavigationSurface,
                Font = UiTheme.Font(8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _navGroupLabels.Add(label);
            _navigation.Controls.Add(label);
        }

        private void AddNavigation(string key, string text)
        {
            var row = new TableLayoutPanel
            {
                Name = "navrow_" + key,
                Width = 180,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(180, 48),
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 1, 0, 1),
                Padding = Padding.Empty,
                BackColor = UiTheme.NavigationSurface
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 4));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var indicator = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.NavigationSurface,
                Margin = Padding.Empty
            };

            var button = UiTheme.CreateAntdButton(text, false);
            button.Name = "nav_" + key;
            button.Tag = "nav";
            button.Dock = DockStyle.Fill;
            button.Margin = Padding.Empty;
            button.Padding = new Padding(14, 0, 10, 0);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.BackColor = UiTheme.NavigationSurface;
            button.ForeColor = UiTheme.NavigationText;
            button.Font = UiTheme.Font(9.6F, FontStyle.Regular);
            button.BorderWidth = 0F;
            button.Radius = 6;
            button.Click += delegate { Navigate(key); };

            row.Controls.Add(indicator, 0, 0);
            row.Controls.Add(button, 1, 0);

            _navButtons[key] = button;
            _navRows[key] = row;
            _navIndicators[key] = indicator;
            _navigation.Controls.Add(row);
        }

        private void UpdateNavigationState()
        {
            foreach (var pair in _navButtons)
            {
                var active = string.Equals(pair.Key, _currentKey, StringComparison.OrdinalIgnoreCase);

                pair.Value.BackColor = active ? UiTheme.NavigationSelected : UiTheme.NavigationSurface;
                pair.Value.ForeColor = active ? UiTheme.Accent : UiTheme.NavigationText;
                pair.Value.Font = UiTheme.Font(9.6F, active ? FontStyle.Bold : FontStyle.Regular);
                pair.Value.Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
                pair.Value.BorderWidth = 0F;

                Panel indicator;
                if (_navIndicators.TryGetValue(pair.Key, out indicator))
                    indicator.BackColor = active ? UiTheme.Accent : UiTheme.NavigationSurface;

                TableLayoutPanel row;
                if (_navRows.TryGetValue(pair.Key, out row))
                    row.BackColor = active ? UiTheme.NavigationSelected : UiTheme.NavigationSurface;
            }
        }

        private void ApplyResponsiveLayout()
        {
            var compact = ClientSize.Width < UiTheme.WideBreakpoint;
            _sidebar.Width = compact ? 202 : 228;
            _sidebar.Padding = compact
                ? new Padding(10, 14, 10, 10)
                : new Padding(14, 16, 14, 12);

            _contentHost.Padding = compact
                ? new Padding(10, 10, 10, 10)
                : new Padding(20, 16, 20, 16);

            var rowWidth = Math.Max(
                128,
                _sidebar.Width -
                _sidebar.Padding.Horizontal -
                SystemInformation.VerticalScrollBarWidth -
                6);

            foreach (var pair in _navRows)
                pair.Value.Width = rowWidth;

            foreach (var label in _navGroupLabels)
                label.Width = rowWidth;

            _navigation.AutoScrollMinSize = new Size(0, 0);
            _navigation.HorizontalScroll.Enabled = false;
            _navigation.HorizontalScroll.Visible = false;
        }
        private sealed class VerticalNavigationPanel : FlowLayoutPanel
        {
            private const int SbHorz = 0;

            [DllImport("user32.dll")]
            private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                HideHorizontalScrollBar();
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                HideHorizontalScrollBar();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                HideHorizontalScrollBar();
            }

            private void HideHorizontalScrollBar()
            {
                if (IsHandleCreated)
                    ShowScrollBar(Handle, SbHorz, false);
            }
        }

    }
}
