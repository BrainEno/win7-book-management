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
        private readonly FlowLayoutPanel _navigation;
        private readonly Panel _contentHost;
        private readonly Label _pageTitle;
        private readonly Label _pageSubtitle;
        private readonly Label _status;
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        private string _currentKey;
        private Form _currentPage;

        public MainForm(ApplicationServices services)
        {
            _services = services;

            Text = "简易图书管理系统";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1240;
            Height = 780;
            MinimumSize = new Size(960, 640);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(9F);

            _sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 206,
                BackColor = UiTheme.Sidebar,
                Padding = new Padding(14, 18, 14, 14)
            };

            var brand = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = UiTheme.Sidebar
            };
            var brandTitle = new Label
            {
                Text = "BOOK DESK",
                Dock = DockStyle.Top,
                Height = 32,
                ForeColor = Color.White,
                Font = UiTheme.Font(15F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var brandSub = new Label
            {
                Text = "离线书店进销存",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.FromArgb(160, 174, 192),
                Font = UiTheme.Font(8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };
            brand.Controls.Add(brandSub);
            brand.Controls.Add(brandTitle);

            _navigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = UiTheme.Sidebar,
                Padding = new Padding(0, 8, 0, 0),
                AutoScroll = true
            };

            AddNavigation("dashboard", "经营概览");
            AddNavigation("books", "图书资料");
            AddNavigation("purchase", "采购入库");
            AddNavigation("sales", "销售开单");
            AddNavigation("documents", "单据中心");
            AddNavigation("inventory", "库存管理");
            AddNavigation("suppliers", "供应商");
            AddNavigation("reports", "报表与导出");
            AddNavigation("backup", "备份与恢复");
            AddNavigation("settings", "系统设置");

            var sidebarFoot = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = UiTheme.Sidebar,
                Padding = new Padding(2, 10, 2, 0)
            };
            var offline = new Label
            {
                Text = "●  本机离线模式",
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.FromArgb(134, 239, 172),
                Font = UiTheme.Font(8.5F)
            };
            var version = new Label
            {
                Text = ".NET Framework 4.8 · SQLite",
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.FromArgb(126, 142, 164),
                Font = UiTheme.Font(7.8F)
            };
            sidebarFoot.Controls.Add(version);
            sidebarFoot.Controls.Add(offline);

            _sidebar.Controls.Add(_navigation);
            _sidebar.Controls.Add(sidebarFoot);
            _sidebar.Controls.Add(brand);

            var main = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background
            };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 86,
                BackColor = UiTheme.Surface,
                Padding = new Padding(24, 14, 24, 8)
            };

            _pageTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Font = UiTheme.Font(16F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Text = "经营概览"
            };
            _pageSubtitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Font = UiTheme.Font(8.8F),
                ForeColor = UiTheme.TextSecondary,
                Text = "今天的销售与库存情况"
            };
            header.Controls.Add(_pageSubtitle);
            header.Controls.Add(_pageTitle);

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = UiTheme.TextSecondary,
                Padding = new Padding(22, 5, 8, 0),
                Font = UiTheme.Font(7.8F),
                Text = "数据库：" + _services.Database.DatabasePath
            };

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Background,
                Padding = new Padding(22, 18, 22, 18)
            };

            main.Controls.Add(_contentHost);
            main.Controls.Add(_status);
            main.Controls.Add(header);

            Controls.Add(main);
            Controls.Add(_sidebar);

            Resize += delegate { ApplyResponsiveLayout(); };
            FormClosing += HandleFormClosing;
            Shown += delegate { Navigate("dashboard"); };
            ApplyResponsiveLayout();
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
                    subtitle = "销售、库存和提醒集中查看";
                    child = new DashboardForm(_services, Navigate);
                    break;
                case "books":
                    title = "图书资料";
                    subtitle = "维护 ISBN、书名、作者、出版社和售价";
                    child = new BookListForm(_services);
                    break;
                case "purchase":
                    title = "采购入库";
                    subtitle = "登记进货并自动增加库存";
                    child = new PurchaseForm(_services);
                    break;
                case "sales":
                    title = "销售开单";
                    subtitle = "支持扫码枪输入 ISBN，结账后自动扣减库存";
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
                    subtitle = "按日期查询并导出 Excel";
                    child = new ReportsForm(_services);
                    break;
                case "backup":
                    title = "备份与恢复";
                    subtitle = "保护本机 SQLite 数据";
                    child = new BackupForm(_services);
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

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            var guard = _currentPage as INavigationGuard;
            if (guard != null && !guard.CanNavigateAway(this))
                e.Cancel = true;
        }

        private void AddNavigation(string key, string text)
        {
            var button = new Button
            {
                Text = text,
                Name = "nav_" + key,
                Tag = "nav",
                Width = 174,
                Height = 42,
                Margin = new Padding(0, 2, 0, 2),
                Padding = new Padding(14, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Sidebar,
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = UiTheme.Font(9.2F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.SidebarHover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.SidebarHover;
            button.Click += delegate { Navigate(key); };

            _navButtons[key] = button;
            _navigation.Controls.Add(button);
        }

        private void UpdateNavigationState()
        {
            foreach (var pair in _navButtons)
            {
                var active = string.Equals(pair.Key, _currentKey, StringComparison.OrdinalIgnoreCase);
                pair.Value.BackColor = active ? UiTheme.Accent : UiTheme.Sidebar;
                pair.Value.ForeColor = active ? Color.White : Color.FromArgb(203, 213, 225);
                pair.Value.Font = UiTheme.Font(9.2F, active ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        private void ApplyResponsiveLayout()
        {
            var compact = ClientSize.Width < 1080;
            _sidebar.Width = compact ? 178 : 206;

            foreach (var pair in _navButtons)
                pair.Value.Width = compact ? 146 : 174;
        }
    }
}
