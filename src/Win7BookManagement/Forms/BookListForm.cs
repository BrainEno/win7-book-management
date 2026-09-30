using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookListForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("输入店内编码、ISBN、书名、作者或出版社");
        private readonly CheckBox _includeInactive = new CheckBox();
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
        private readonly Label _summary = new Label();
        private readonly Label _resultChip = new Label();
        private readonly Label _lowStockChip = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _publisherColumn;
        private readonly AntdUI.Column _categoryColumn;
        private readonly AntdUI.Column _publicationColumn;
        private readonly AntdUI.Column _bindingColumn;
        private readonly AntdUI.Column _shelfColumn;
        private readonly AntdUI.Column _activeColumn;

        private Book _selectedBook;

        public BookListForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "120", MinWidth = "96" };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "150", MinWidth = "116" };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "120", MinWidth = "76" };
            _publisherColumn = new AntdUI.Column("Publisher", "出版社") { Width = "128", MinWidth = "88" };
            _categoryColumn = new AntdUI.Column("Category", "分类") { Width = "96", MinWidth = "72" };
            _publicationColumn = new AntdUI.Column("PublicationYear", "出版年") { Width = "96", MinWidth = "82" };
            _bindingColumn = new AntdUI.Column("Binding", "装帧") { Width = "80", MinWidth = "68" };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "96", MinWidth = "82" };
            _activeColumn = new AntdUI.Column("IsActive", "启用") { Width = "72", MinWidth = "68" };

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

            root.Controls.Add(CreateSearchSection(), 0, 0);
            root.Controls.Add(CreateContentSection(), 0, 1);

            _summary.AutoSize = true;
            _summary.Dock = DockStyle.Fill;
            _summary.MinimumSize = new Size(0, 38);
            _summary.Padding = new Padding(12, 8, 8, 8);
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.BackColor = UiTheme.Surface;
            _summary.Font = UiTheme.Font(8.2F);
            root.Controls.Add(_summary, 0, 2);

            Controls.Add(root);

            Resize += delegate { ApplyResponsiveLayout(); };
            Shown += delegate
            {
                Reload();
                ApplyResponsiveLayout();
                _search.Focus();
            };

            UiTheme.Apply(this);
        }

        private Control CreateSearchSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 16, 18, 15),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "图书资料",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var addButton = UiTheme.CreateAntdButton("＋ 新增图书", true);
            addButton.Width = 124;
            var editButton = UiTheme.CreateAntdButton("编辑资料", false);
            editButton.Width = 104;
            addButton.Click += delegate { EditBook(null); };
            editButton.Click += delegate { EditSelected(); };

            _includeInactive.Text = "包含停用";
            _includeInactive.AutoSize = true;
            _includeInactive.Margin = new Padding(12, 11, 0, 0);
            _includeInactive.CheckedChanged += delegate { Reload(); };

            actions.Controls.Add(addButton);
            actions.Controls.Add(editButton);
            actions.Controls.Add(_includeInactive);
            section.Controls.Add(actions, 1, 0);

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                MinimumSize = new Size(0, 58),
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 0)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            searchRow.Controls.Add(new Label
            {
                Text = "综合搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.8F, FontStyle.Bold),
                Margin = new Padding(0, 12, 14, 0)
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
            searchRow.Controls.Add(_search, 1, 0);

            var searchButton = UiTheme.CreateAntdButton("查询", true);
            searchButton.Width = 98;
            searchButton.Margin = new Padding(0, 3, 0, 3);
            searchButton.Click += delegate { Reload(); };
            searchRow.Controls.Add(searchButton, 2, 0);

            section.Controls.Add(searchRow, 0, 1);
            section.SetColumnSpan(searchRow, 2);

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 9, 0, 0)
            };

            _resultChip.AutoSize = true;
            _resultChip.Padding = new Padding(10, 5, 10, 5);
            _resultChip.Margin = new Padding(0, 0, 8, 0);
            _resultChip.BackColor = UiTheme.AccentSoft;
            _resultChip.ForeColor = UiTheme.Accent;
            _resultChip.Font = UiTheme.Font(8.2F, FontStyle.Bold);

            _lowStockChip.AutoSize = true;
            _lowStockChip.Padding = new Padding(10, 5, 10, 5);
            _lowStockChip.Margin = new Padding(0, 0, 8, 0);
            _lowStockChip.BackColor = Color.FromArgb(252, 241, 226);
            _lowStockChip.ForeColor = UiTheme.Warning;
            _lowStockChip.Font = UiTheme.Font(8.2F, FontStyle.Bold);

            stats.Controls.Add(_resultChip);
            stats.Controls.Add(_lowStockChip);
            stats.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "可拖动表头调整列宽；窄窗口会自动收起低频字段。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(4, 5, 0, 0)
            });

            section.Controls.Add(stats, 0, 2);
            section.SetColumnSpan(stats, 2);
            return section;
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            ConfigureGrid();

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0)
            };
            gridHost.Controls.Add(_grid);
            _split.Panel1.Controls.Add(gridHost);
            _split.Panel2.Controls.Add(CreateDetailPanel());
            return _split;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.Margin = Padding.Empty;

            _grid.Bordered = false;

            _grid.Gap = 12;
            _grid.RowHeight = 46;
            _grid.RowHeightHeader = 46;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有找到符合条件的图书资料";

            var titleColumn = new AntdUI.Column("Title", "书名")
            {
                Width = "260",
                MinWidth = "220",
                MaxWidth = "520",
                Ellipsis = true
            };
            var priceColumn = new AntdUI.Column("SalePriceYuan", "销售价格")
            {
                Width = "104",
                MinWidth = "92",
                DisplayFormat = "0.00"
            };
            var stockColumn = new AntdUI.Column("StockQuantity", "库存") { Width = "78", MinWidth = "68" };

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _selfCodeColumn,
                _isbnColumn,
                titleColumn,
                _authorColumn,
                _publisherColumn,
                _categoryColumn,
                _publicationColumn,
                _bindingColumn,
                _shelfColumn,
                priceColumn,
                stockColumn,
                _activeColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "book-master");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                ShowSelectedDetails();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                EditSelected();
            };
        }

        private Control CreateDetailPanel()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14)
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 48),
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "图书详情",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var edit = UiTheme.CreateAntdButton("编辑资料", false);
            edit.Width = 100;
            edit.Margin = new Padding(0, 3, 0, 3);
            edit.Click += delegate { EditSelected(); };
            header.Controls.Add(edit, 1, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0, 8, 0, 0)
            };

            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddDetailRow(details, "书名", "title");
            AddDetailRow(details, "店内编码", "selfCode");
            AddDetailRow(details, "ISBN", "isbn");
            AddDetailRow(details, "作者", "author");
            AddDetailRow(details, "出版社", "publisher");
            AddDetailRow(details, "分类", "category");
            AddDetailRow(details, "出版年", "publication");
            AddDetailRow(details, "版次", "edition");
            AddDetailRow(details, "装帧", "binding");
            AddDetailRow(details, "货架位", "shelf");
            AddDetailRow(details, "销售价格", "price");
            AddDetailRow(details, "默认进价", "purchasePrice");
            AddDetailRow(details, "库存", "stock");
            AddDetailRow(details, "状态", "status");
            AddDetailRow(details, "备注", "note", 62);

            scroll.Controls.Add(details);
            host.Controls.Add(scroll);
            host.Controls.Add(header);
            return host;
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key)
        {
            AddDetailRow(table, labelText, key, 44);
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key, int height)
        {
            var row = table.RowCount;
            table.RowCount += 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, height),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F, FontStyle.Bold)
            };
            var value = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, Math.Max(40, height - 6)),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(9, 0, 9, 0),
                Margin = new Padding(0, 3, 0, 3),
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true,
                Font = UiTheme.Font(8.5F)
            };

            table.Controls.Add(label, 0, row);
            table.Controls.Add(value, 1, row);
            _detailValues[key] = value;
        }

        private void Reload()
        {
            var books = _services.Books.Search(_search.Text, _includeInactive.Checked);
            _selectedBook = null;
            _grid.DataSource = books;

            var threshold = _services.Settings.GetLowStockThreshold();
            var lowStockCount = 0;
            foreach (var book in books)
            {
                if (book.IsActive && book.StockQuantity <= threshold)
                    lowStockCount += 1;
            }

            _resultChip.Text = "结果  " + books.Count;
            _lowStockChip.Text = "低库存  " + lowStockCount;
            _summary.Text = "共 " + books.Count + " 条图书资料 · 低库存阈值 " + threshold + " 册 · 双击行可编辑 · 表头边界可拖动";
            ShowSelectedDetails();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0) return;

            var showDetails = ClientSize.Width >= 1680;
            _split.Panel2Collapsed = !showDetails;

            if (showDetails)
            {
                var desiredRightWidth = Math.Min(370, Math.Max(310, _split.Width / 3));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                const int minimumLeftWidth = 420;
                const int minimumRightWidth = 300;
                var maximumDistance = _split.Width - minimumRightWidth - _split.SplitterWidth;
                if (distance >= minimumLeftWidth && maximumDistance >= minimumLeftWidth)
                    _split.SplitterDistance = Math.Min(distance, maximumDistance);
            }

            var gridWidth = showDetails ? _split.Panel1.ClientSize.Width : ClientSize.Width;
            // The compact view keeps the fields most useful for finding and selling a book.
            // Administrative metadata is progressively restored on larger windows.
            _selfCodeColumn.Visible = gridWidth >= 1180;
            _publisherColumn.Visible = gridWidth >= 1450;
            _categoryColumn.Visible = gridWidth >= 1050;
            _publicationColumn.Visible = gridWidth >= 1250;
            _bindingColumn.Visible = gridWidth >= 1500;
            _shelfColumn.Visible = gridWidth >= 900;
            _activeColumn.Visible = gridWidth >= 1100;
            _authorColumn.Visible = gridWidth >= 760;
            _isbnColumn.Visible = gridWidth >= 650;
            _grid.LoadLayout();
        }

        private void ShowSelectedDetails()
        {
            var book = _selectedBook;
            if (book == null)
            {
                foreach (var value in _detailValues.Values) value.Text = "—";
                return;
            }

            _detailValues["title"].Text = EmptyAsDash(book.Title);
            _detailValues["selfCode"].Text = EmptyAsDash(book.SelfCode);
            _detailValues["isbn"].Text = EmptyAsDash(book.Isbn);
            _detailValues["author"].Text = EmptyAsDash(book.Author);
            _detailValues["publisher"].Text = EmptyAsDash(book.Publisher);
            _detailValues["category"].Text = EmptyAsDash(book.Category);
            _detailValues["publication"].Text = EmptyAsDash(book.PublicationYear);
            _detailValues["edition"].Text = EmptyAsDash(book.Edition);
            _detailValues["binding"].Text = EmptyAsDash(book.Binding);
            _detailValues["shelf"].Text = EmptyAsDash(book.ShelfCode);
            _detailValues["price"].Text = "¥" + book.SalePriceYuan.ToString("0.00");
            _detailValues["purchasePrice"].Text = "¥" + book.DefaultPurchasePriceYuan.ToString("0.00");
            _detailValues["stock"].Text = book.StockQuantity + " 册";
            _detailValues["status"].Text = book.IsActive ? "启用" : "停用";
            _detailValues["note"].Text = EmptyAsDash(book.Note);
        }

        private static string EmptyAsDash(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
        }

        private void EditSelected()
        {
            if (_selectedBook == null)
            {
                MessageBox.Show(this, "请先选择一条图书资料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            EditBook(_selectedBook);
        }

        private void EditBook(Book book)
        {
            using (var dialog = new BookEditForm(_services, book))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }
    }
}
