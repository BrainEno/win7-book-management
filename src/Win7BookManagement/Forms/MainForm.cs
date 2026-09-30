using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class MainForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Panel _sidebar;
        private readonly AntdUI.Menu _navigation;
        private readonly Panel _contentHost;
        private readonly Label _pageTitle;
        private readonly Label _pageSubtitle;
        private readonly Label _status;
        private readonly Dictionary<string, AntdUI.MenuItem> _menuItems =
            new Dictionary<string, AntdUI.MenuItem>();

        private string _currentKey;
        private Form _currentPage;
        private bool _guideOpen;
        private bool _syncingNavigation;

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
                Width = 240,
                BackColor = UiTheme.NavigationSurface,
                Padding = Padding.Empty
            };

            var brand = CreateBrand();
            _navigation = CreateNavigation();
            BuildNavigation();
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
                MinimumSize = new Size(0, 64),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(20, 7, 20, 6),
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
                Font = UiTheme.Font(14F, FontStyle.Bold),
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
                Margin = new Padding(12, 6, 0, 0),
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
                Height = 82,
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(20, 13, 16, 8)
            };

            var title = new Label
            {
                Text = "BOOK DESK",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var sub = new Label
            {
                Text = "独立书店 · 离线进销存",
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 0)
            };

            brand.Controls.Add(sub);
            brand.Controls.Add(title);
            return brand;
        }

        private AntdUI.Menu CreateNavigation()
        {
            var menu = new AntdUI.Menu
            {
                Dock = DockStyle.Fill,
                Mode = AntdUI.TMenuMode.Inline,
                AutoCollapse = false,
                Collapsed = false,
                Unique = false,
                BackColor = UiTheme.NavigationSurface,
                Margin = Padding.Empty,
                Padding = new Padding(8, 4, 8, 4)
            };

            menu.SelectChanged += HandleNavigationSelection;
            return menu;
        }

        private void BuildNavigation()
        {
            var workbench = AddGroup("工作台");
            AddNavigation(workbench, "dashboard", "经营概览");

            var core = AddGroup("核心业务");
            AddNavigation(core, "sales", "销售开单");
            AddNavigation(core, "books", "图书资料");
            AddNavigation(core, "purchase", "采购入库");
            AddNavigation(core, "inventory", "库存管理");
            AddNavigation(core, "documents", "单据中心");

            var management = AddGroup("经营管理");
            AddNavigation(management, "suppliers", "供应商");
            AddNavigation(management, "reports", "报表与导出");

            var system = AddGroup("系统");
            AddNavigation(system, "backup", "备份与恢复");
            AddNavigation(system, "help", "使用帮助");
            AddNavigation(system, "settings", "系统设置");
        }

        private AntdUI.MenuItem AddGroup(string text)
        {
            var group = new AntdUI.MenuItem(text)
            {
                Expand = true
            };
            _navigation.Items.Add(group);
            return group;
        }

        private void AddNavigation(AntdUI.MenuItem parent, string key, string text)
        {
            var item = new AntdUI.MenuItem(text)
            {
                ID = key,
                Name = key,
                Tag = key
            };
            parent.Sub.Add(item);
            _menuItems[key] = item;
        }

        private void HandleNavigationSelection(object sender, AntdUI.MenuSelectEventArgs e)
        {
            if (_syncingNavigation || e == null || e.Value == null)
                return;

            var key = Convert.ToString(e.Value.Tag);
            if (string.IsNullOrWhiteSpace(key))
            {
                UpdateNavigationState();
                return;
            }

            Navigate(key);

            // A page can veto navigation when there is an unfinished sale or
            // purchase. In that case restore the menu selection immediately.
            if (!string.Equals(key, _currentKey, StringComparison.OrdinalIgnoreCase))
                UpdateNavigationState();
        }

        private static Panel CreateSidebarFooter()
        {
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 66,
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(20, 10, 12, 8)
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
                Font = UiTheme.Font(7.5F),
                Padding = new Padding(0, 4, 0, 0)
            };

            footer.Controls.Add(version);
            footer.Controls.Add(offline);
            return footer;
        }

        public void Navigate(string key)
        {
            if (string.Equals(key, _currentKey, StringComparison.OrdinalIgnoreCase))
            {
                UpdateNavigationState();
                return;
            }

            var guard = _currentPage as INavigationGuard;
            if (guard != null && !guard.CanNavigateAway(this))
            {
                UpdateNavigationState();
                return;
            }

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
                    UpdateNavigationState();
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
            AntdUI.MenuItem target;
            if (!_menuItems.TryGetValue(key, out target) ||
                !_navigation.Visible ||
                !_navigation.IsHandleCreated ||
                _navigation.ClientSize.Width <= 0 ||
                _navigation.ClientSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            // AntdUI.Menu intentionally owns item layout. HitTest lets the
            // onboarding overlay discover the real rendered item rectangle
            // without duplicating Menu's sizing rules.
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < _navigation.ClientSize.Height; y += 2)
            {
                for (var x = 4; x < _navigation.ClientSize.Width; x += 8)
                {
                    var hit = _navigation.HitTest(x, y);
                    if (!ReferenceEquals(hit, target))
                        continue;

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return Rectangle.Empty;

            var local = Rectangle.FromLTRB(
                Math.Max(0, minX - 4),
                Math.Max(0, minY - 2),
                Math.Min(_navigation.ClientSize.Width, maxX + 12),
                Math.Min(_navigation.ClientSize.Height, maxY + 4));

            return _navigation.RectangleToScreen(local);
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            var guard = _currentPage as INavigationGuard;
            if (guard != null && !guard.CanNavigateAway(this))
                e.Cancel = true;
        }

        private void UpdateNavigationState()
        {
            if (string.IsNullOrWhiteSpace(_currentKey))
            {
                _navigation.USelect();
                return;
            }

            AntdUI.MenuItem item;
            if (!_menuItems.TryGetValue(_currentKey, out item))
                return;

            try
            {
                _syncingNavigation = true;
                _navigation.Select(item, false);
            }
            finally
            {
                _syncingNavigation = false;
            }
        }

        private void ApplyResponsiveLayout()
        {
            var compact = ClientSize.Width < UiTheme.WideBreakpoint;
            _sidebar.Width = compact ? 214 : 240;

            _contentHost.Padding = compact
                ? new Padding(8, 8, 8, 8)
                : new Padding(12, 10, 12, 10);
        }
    }
}
