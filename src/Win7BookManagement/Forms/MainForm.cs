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
        private readonly TableLayoutPanel _header;
        private readonly TableLayoutPanel _statusBar;
        private readonly AntdUI.Label _pageTitle;
        private readonly Label _status;
        private readonly Label _recordStatus;
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

            Text = "简易图书管理系统  BOOK DESK";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1600;
            Height = 900;
            MinimumSize = new Size(1280, 720);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9F);

            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = BookDeskUiSpec.Standard.SidebarWidth,
                BackColor = UiTheme.NavigationSurface,
                Padding = Padding.Empty
            };

            _navigation = CreateNavigation();
            BuildNavigation();
            var sidebarFoot = CreateSidebarFooter();

            _sidebar.Controls.Add(_navigation);
            _sidebar.Controls.Add(sidebarFoot);

            var main = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background
            };

            _header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = BookDeskUiSpec.Standard.PageHeaderHeight,
                MinimumSize = new Size(0, BookDeskUiSpec.Compact.PageHeaderHeight),
                ColumnCount = 1,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 6, 16, 6),
                Margin = Padding.Empty
            };
            _header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _pageTitle = new AntdUI.Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.PageTitleFontPoints, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Text = "工作台",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                PrefixSvg = "HomeOutlined",
                PrefixColor = UiTheme.Accent,
                IconRatio = 0.9F,
                IconGap = 8,
                Margin = Padding.Empty
            };

            _header.Controls.Add(_pageTitle, 0, 0);

            _status = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextSecondary,
                Padding = new Padding(14, 0, 8, 0),
                Font = UiTheme.Font(8.25F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = "完全离线  |  数据库：" + _services.Database.DatabasePath
            };

            _recordStatus = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextSecondary,
                Padding = new Padding(8, 0, 14, 0),
                Font = UiTheme.Font(8.25F),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Text = ""
            };

            _statusBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = BookDeskUiSpec.ShellStatusHeight,
                MinimumSize = new Size(0, BookDeskUiSpec.ShellStatusHeight),
                MaximumSize = new Size(0, BookDeskUiSpec.ShellStatusHeight),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _statusBar.Controls.Add(_status, 0, 0);
            _statusBar.Controls.Add(_recordStatus, 1, 0);

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background,
                Padding = new Padding(
                    BookDeskUiSpec.Standard.ContentPaddingX,
                    BookDeskUiSpec.Standard.ContentPaddingY,
                    BookDeskUiSpec.Standard.ContentPaddingX,
                    BookDeskUiSpec.Standard.ContentPaddingY)
            };

            main.Controls.Add(_contentHost);
            main.Controls.Add(_statusBar);
            main.Controls.Add(_header);

            Controls.Add(main);
            Controls.Add(_sidebar);

            UiTheme.Apply(this);

            Resize += delegate { ApplyResponsiveLayout(); };
            FormClosing += HandleFormClosing;
            Shown += delegate
            {
                if (string.IsNullOrWhiteSpace(_currentKey))
                    Navigate("dashboard");
                if (!_services.Settings.IsOnboardingCompleted())
                    BeginInvoke(new Action(StartOnboardingGuide));
            };

            ApplyResponsiveLayout();
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
                Padding = new Padding(9, 14, 6, 8),
                Radius = 6,
                Gap = 12,
                itemMargin = 2,
                InlineIndent = 18,
                IconRatio = 1.05F,
                FocusMode = AntdUI.TFocusMode.Line,
                FocusModeAlign = AntdUI.TAlignMini.Left,
                FocusModeColor = UiTheme.Accent,
                FocusModeSize = 3
            };

            menu.SelectChanged += HandleNavigationSelection;
            return menu;
        }

        private void BuildNavigation()
        {
            // Dashboard and operating overview are one screen. Keep a single
            // clickable entry so the sidebar does not spend two rows on the same
            // destination.
            AddRootNavigation("dashboard", "工作台", "HomeOutlined");

            var core = AddGroup("核心业务");
            AddNavigation(core, "sales", "销售开单", "FileAddOutlined");
            AddNavigation(core, "books", "图书资料", "BookOutlined");
            AddNavigation(core, "purchase", "采购入库", "ShoppingCartOutlined");
            AddNavigation(core, "inventory", "库存管理", "CodeSandboxOutlined");
            AddNavigation(core, "documents", "单据中心", "FileTextOutlined");

            var management = AddGroup("经营管理");
            AddNavigation(management, "suppliers", "字典管理", "ProfileOutlined");
            AddNavigation(management, "reports", "报表与导出", "BarChartOutlined");

            var system = AddGroup("系统");
            AddNavigation(system, "backup", "备份与恢复", "DatabaseOutlined");
            AddNavigation(system, "help", "使用帮助", "QuestionCircleOutlined");
            AddNavigation(system, "settings", "系统设置", "SettingOutlined");
        }

        private void AddRootNavigation(string key, string text, string iconSvg)
        {
            var item = new AntdUI.MenuItem(text)
            {
                ID = key,
                Name = key,
                Tag = key,
                IconSvg = iconSvg
            };
            _navigation.Items.Add(item);
            _menuItems[key] = item;
        }

        private AntdUI.MenuItem AddGroup(string text)
        {
            var group = new AntdUI.MenuItem(text)
            {
                Expand = true,
                Font = UiTheme.Font(8.2F, FontStyle.Bold)
            };
            _navigation.Items.Add(group);
            return group;
        }

        private void AddNavigation(AntdUI.MenuItem parent, string key, string text, string iconSvg)
        {
            var item = new AntdUI.MenuItem(text)
            {
                ID = key,
                Name = key,
                Tag = key,
                IconSvg = iconSvg
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
                Height = BookDeskUiSpec.SidebarFooterHeight,
                BackColor = UiTheme.NavigationSurface,
                Padding = new Padding(12, 8, 10, 6)
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

            switch (key)
            {
                case "dashboard":
                    title = "工作台";
                    child = new DashboardForm(_services, Navigate, StartOnboardingGuide);
                    break;
                case "books":
                    title = "图书资料";
                    child = new BookListForm(_services);
                    break;
                case "purchase":
                    title = "采购入库";
                    child = new PurchaseForm(_services);
                    break;
                case "sales":
                    title = "销售开单";
                    child = new SalesForm(_services);
                    break;
                case "documents":
                    title = "单据中心";
                    child = new DocumentCenterForm(_services);
                    break;
                case "inventory":
                    title = "库存管理";
                    child = new InventoryForm(_services);
                    break;
                case "suppliers":
                    title = "字典管理";
                    child = new DictionaryManagementForm(_services);
                    break;
                case "reports":
                    title = "报表与导出";
                    child = new ReportsForm(_services);
                    break;
                case "backup":
                    title = "备份与恢复";
                    child = new BackupForm(_services);
                    break;
                case "help":
                    title = "使用帮助";
                    child = new HelpForm(_services, StartOnboardingGuide, Navigate);
                    break;
                case "settings":
                    title = "系统设置";
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
            _pageTitle.PrefixSvg = ResolvePageIcon(key);
            _recordStatus.Text = "";

            var bookPage = child as BookListForm;
            if (bookPage != null)
            {
                _recordStatus.Text = "共 0 条记录";
                bookPage.ResultCountChanged += delegate
                {
                    _recordStatus.Text = "共 " + bookPage.ResultCount + " 条记录";
                };
            }

            UpdateNavigationState();

            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            UiTheme.Apply(child);
            _contentHost.Controls.Add(child);
            child.Show();

            var profile = BookDeskUiSpec.Resolve(ClientSize.Width, ClientSize.Height);
            var specPage = child as IUiSpecPage;
            if (specPage != null)
                specPage.ApplyUiSpecProfile(profile);
            else
                UiTheme.ApplyResponsiveDensity(child, Math.Max(1, _contentHost.ClientSize.Width));
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

        private static string ResolvePageIcon(string key)
        {
            switch (key)
            {
                case "dashboard": return "HomeOutlined";
                case "books": return "BookFilled";
                case "purchase": return "ShoppingCartOutlined";
                case "inventory": return "CodeSandboxOutlined";
                case "documents": return "FileTextOutlined";
                case "sales": return "FileAddOutlined";
                case "suppliers": return "ProfileOutlined";
                case "reports": return "BarChartOutlined";
                case "backup": return "DatabaseOutlined";
                case "help": return "QuestionCircleOutlined";
                case "settings": return "SettingOutlined";
                default: return "BarChartOutlined";
            }
        }

        private void ApplyResponsiveLayout()
        {
            var profile = BookDeskUiSpec.Resolve(ClientSize.Width, ClientSize.Height);

            _sidebar.Width = profile.SidebarWidth;
            _header.Height = profile.PageHeaderHeight;
            _header.Padding = profile.IsCompact
                ? new Padding(12, 4, 12, 4)
                : new Padding(16, 6, 16, 6);

            _navigation.Font = UiTheme.Font(
                profile.IsCompact
                    ? BookDeskUiSpec.PixelFontToPoints(13)
                    : BookDeskUiSpec.PixelFontToPoints(14));

            _pageTitle.Font = UiTheme.Font(profile.PageTitleFontPoints, FontStyle.Bold);
            _pageTitle.IconRatio = profile.IsCompact ? 0.86F : 0.9F;
            _pageTitle.IconGap = profile.IsCompact ? 7 : 8;

            _contentHost.Padding = new Padding(
                profile.ContentPaddingX,
                profile.ContentPaddingY,
                profile.ContentPaddingX,
                profile.ContentPaddingY);

            if (_currentPage == null)
                return;

            var specPage = _currentPage as IUiSpecPage;
            if (specPage != null)
            {
                specPage.ApplyUiSpecProfile(profile);
                return;
            }

            UiTheme.ApplyResponsiveDensity(
                _currentPage,
                Math.Max(1, _contentHost.ClientSize.Width));
        }
    }
}
