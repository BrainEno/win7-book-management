using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookLookupDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly bool _isbnOrTitleOnly;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("输入关键词搜索");
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _shelfColumn;
        private readonly AntdUI.Column _priceColumn;

        private Book _selected;
        public Book SelectedBook { get; private set; }

        public BookLookupDialog(ApplicationServices services, string initialKeyword = "", bool isbnOrTitleOnly = false)
        {
            _services = services;
            _isbnOrTitleOnly = isbnOrTitleOnly;
            _search.Text = initialKeyword ?? "";
            UiTheme.ConfigureForm(this);

            Text = "选择图书";
            StartPosition = FormStartPosition.CenterParent;
            Width = 940;
            Height = 640;
            MinimumSize = new Size(700, 480);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;
            KeyPreview = true;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112" };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "138" };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "120" };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "90" };
            _priceColumn = new AntdUI.Column("SalePriceYuan", "销售价格") { Width = "98", DisplayFormat = "0.00" };

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
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateSearchSection(), 0, 1);
            root.Controls.Add(CreateGridSection(), 0, 2);
            root.Controls.Add(CreateFooter(), 0, 3);
            Controls.Add(root);

            Resize += delegate { ApplyResponsiveColumns(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            UiTheme.Apply(this);
            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                Reload();
                ApplyResponsiveColumns();
                _search.Focus();
            };
        }

        private Control CreateHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 16, 22, 13),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "查找并选择图书",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = _isbnOrTitleOnly
                    ? "支持店内编码、ISBN、书名和作者模糊搜索；无 ISBN 的自出版物也能直接选择。"
                    : "支持店内编码、ISBN、书名、作者、出版社、分类、出版信息、货架位和备注。双击结果即可选择。",
                AutoSize = true,
                MaximumSize = new Size(860, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F)
            }, 0, 1);

            return header;
        }

        private Control CreateSearchSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 11, 18, 11),
                Margin = new Padding(0, 10, 0, 10)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = _isbnOrTitleOnly ? "编码 / ISBN / 书名 / 作者" : "综合搜索",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);

            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 3, 10, 3);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            section.Controls.Add(_search, 1, 0);

            var searchButton = UiTheme.CreateAntdButton("查询", true);
            searchButton.Width = 98;
            searchButton.Margin = new Padding(0, 3, 0, 3);
            searchButton.Click += delegate { Reload(); };
            section.Controls.Add(searchButton, 2, 0);
            return section;
        }

        private Control CreateGridSection()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.BackColor = UiTheme.Surface;
            _grid.ForeColor = UiTheme.TextPrimary;
            _grid.ColumnBack = UiTheme.NavigationSurface;
            _grid.ColumnFore = UiTheme.TextSecondary;
            _grid.ColumnFont = UiTheme.Font(8.8F, FontStyle.Bold);
            _grid.BorderColor = UiTheme.Border;
            _grid.Radius = 8;
            _grid.RowHeight = 46;
            _grid.RowHeightHeader = 46;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有找到匹配的图书";
            _grid.RowHoverBg = Color.FromArgb(248, 246, 241);
            _grid.RowSelectedBg = UiTheme.AccentSoft;
            _grid.RowSelectedFore = UiTheme.TextPrimary;

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _selfCodeColumn,
                _isbnColumn,
                new AntdUI.Column("Title", "书名") { Width = "auto", MinWidth = "230", Ellipsis = true },
                _authorColumn,
                _shelfColumn,
                new AntdUI.Column("StockQuantity", "库存") { Width = "72" },
                _priceColumn
            };

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as Book;
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as Book;
                Choose();
            };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            host.Controls.Add(_grid);
            return host;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 72),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 12, 18, 12),
                Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.5F);
            footer.Controls.Add(_summary, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            var cancel = UiTheme.CreateAntdButton("取消", false);
            cancel.Width = 90;
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var select = UiTheme.CreateAntdButton("选择图书", true);
            select.Width = 108;
            select.Click += delegate { Choose(); };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(select);
            footer.Controls.Add(buttons, 1, 0);
            return footer;
        }

        private void Reload()
        {
            var result = _isbnOrTitleOnly
                ? _services.Books.SearchActiveByIsbnOrTitle(_search.Text)
                : _services.Books.Search(_search.Text, false);

            _selected = result.Count > 0 ? result[0] : null;
            _grid.DataSource = result;
            if (_selected != null) _grid.SetSelected(_selected, false);
            _summary.Text = "找到 " + result.Count + " 条启用图书资料 · 单击选择，双击确认";
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _selfCodeColumn.Visible = width >= 840;
            _shelfColumn.Visible = width >= 760;
            _authorColumn.Visible = width >= 700;
            _priceColumn.Visible = width >= 650;
            _isbnColumn.Visible = width >= 580;
            _grid.LoadLayout();
        }

        private void Choose()
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "请先选择图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedBook = _selected;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
