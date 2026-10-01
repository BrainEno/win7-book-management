using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookListForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("输入店内编码、ISBN、书名、作者或出版社");
        private readonly AntdUI.Checkbox _includeInactive = new AntdUI.Checkbox();
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
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

        public event EventHandler ResultCountChanged;
        public int ResultCount { get; private set; }

        public BookListForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112", MinWidth = "92" };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "130", MinWidth = "108" };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "100", MinWidth = "72" };
            _publisherColumn = new AntdUI.Column("Publisher", "出版社") { Width = "110", MinWidth = "82" };
            _categoryColumn = new AntdUI.Column("Category", "分类") { Width = "88", MinWidth = "68" };
            _publicationColumn = new AntdUI.Column("PublicationYear", "出版年") { Width = "82", MinWidth = "74" };
            _bindingColumn = new AntdUI.Column("Binding", "装帧") { Width = "74", MinWidth = "64" };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "86", MinWidth = "74" };
            _activeColumn = new AntdUI.Column("ActiveStatus", "启用") { Width = "98", MinWidth = "88" };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(CreateSearchSection(), 0, 0);
            root.Controls.Add(CreateContentSection(), 0, 1);

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
            var section = new PrototypeSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 6),
                ColumnCount = 6,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            searchRow.Controls.Add(new Label
            {
                Text = "搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _search.Dock = DockStyle.None;
            _search.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _search.Tag = "toolbar-input";
            _search.PrefixSvg = "SearchOutlined";
            _search.Margin = new Padding(0, 0, 8, 0);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            searchRow.Controls.Add(_search, 1, 0);

            var searchButton = UiTheme.CreateAntdButton("查询", false);
            searchButton.Width = 78;
            searchButton.Anchor = AnchorStyles.Left;
            searchButton.Tag = "toolbar-action";
            searchButton.Margin = new Padding(0, 0, 6, 0);
            searchButton.Click += delegate { Reload(); };
            searchRow.Controls.Add(searchButton, 2, 0);

            var addButton = UiTheme.CreateAntdButton("新增图书", true);
            addButton.IconSvg = "PlusOutlined";
            addButton.Width = 110;
            addButton.Anchor = AnchorStyles.Left;
            addButton.Tag = "toolbar-action";
            addButton.Margin = new Padding(0, 0, 6, 0);
            addButton.Click += delegate { EditBook(null); };
            searchRow.Controls.Add(addButton, 3, 0);

            var editButton = UiTheme.CreateAntdButton("编辑资料", false);
            editButton.IconSvg = "EditOutlined";
            editButton.Width = 104;
            editButton.Anchor = AnchorStyles.Left;
            editButton.Tag = "toolbar-action";
            editButton.Margin = new Padding(0, 0, 8, 0);
            editButton.Click += delegate { EditSelected(); };
            searchRow.Controls.Add(editButton, 4, 0);

            _includeInactive.Text = "包含停用";
            _includeInactive.AutoSize = true;
            _includeInactive.Anchor = AnchorStyles.Left;
            _includeInactive.Margin = new Padding(4, 4, 0, 0);
            _includeInactive.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { Reload(); };
            searchRow.Controls.Add(_includeInactive, 5, 0);

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 8, 0, 0),
                Padding = Padding.Empty
            };

            _resultChip.AutoSize = true;
            _resultChip.Padding = new Padding(8, 3, 8, 3);
            _resultChip.Margin = new Padding(0, 0, 6, 0);
            _resultChip.BackColor = UiTheme.AccentSoft;
            _resultChip.ForeColor = UiTheme.Accent;
            _resultChip.Font = UiTheme.Font(8F, FontStyle.Bold);

            _lowStockChip.AutoSize = true;
            _lowStockChip.Padding = new Padding(8, 3, 8, 3);
            _lowStockChip.Margin = Padding.Empty;
            _lowStockChip.BackColor = Color.FromArgb(255, 247, 230);
            _lowStockChip.ForeColor = UiTheme.Warning;
            _lowStockChip.Font = UiTheme.Font(8F, FontStyle.Bold);

            stats.Controls.Add(_resultChip);
            stats.Controls.Add(_lowStockChip);

            section.Controls.Add(searchRow, 0, 0);
            section.Controls.Add(stats, 0, 1);
            return section;
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, UiTheme.SectionGap, 0, 0);

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

            _grid.Bordered = true;

            _grid.Gap = 9;
            _grid.RowHeight = UiTheme.TableRowHeight;
            _grid.RowHeightHeader = UiTheme.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有找到符合条件的图书资料";

            var titleColumn = new AntdUI.Column("Title", "书名")
            {
                Width = "fill",
                MinWidth = "160",
                MaxWidth = "320",
                Ellipsis = true
            };
            var priceColumn = new AntdUI.Column("SalePriceYuan", "销售价格")
            {
                Width = "96",
                MinWidth = "86",
                DisplayFormat = "0.00"
            };
            var stockColumn = new AntdUI.Column("StockQuantity", "库存") { Width = "72", MinWidth = "64" };

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
            _grid.ConfigureColumnPersistence(_services.Settings, "book-master-prototype-v3");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                var row = e.Record as BookRow;
                _selectedBook = row == null ? e.Record as Book : row.Source;
                ShowSelectedDetails();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                var row = e.Record as BookRow;
                _selectedBook = row == null ? e.Record as Book : row.Source;
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

            var rows = new List<BookRow>();
            foreach (var book in books)
                rows.Add(new BookRow(book));
            _grid.DataSource = rows;

            ResultCount = books.Count;
            var countChanged = ResultCountChanged;
            if (countChanged != null)
                countChanged(this, EventArgs.Empty);

            var threshold = _services.Settings.GetLowStockThreshold();
            var lowStockCount = 0;
            foreach (var book in books)
            {
                if (book.IsActive && book.StockQuantity <= threshold)
                    lowStockCount += 1;
            }

            _resultChip.Text = "结果  " + books.Count;
            _lowStockChip.Text = "低库存  " + lowStockCount;
            ShowSelectedDetails();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0) return;

            // The approved prototype is table-first through the requested
            // 2560-wide validation range.  Preserve the existing detail pane
            // only for genuinely ultra-wide workspaces.
            var showDetails = ClientSize.Width >= 2500;
            _split.Panel2Collapsed = !showDetails;

            if (showDetails)
            {
                var desiredRightWidth = Math.Min(340, Math.Max(300, _split.Width / 4));
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
            _selfCodeColumn.Visible = gridWidth >= 960;
            _publisherColumn.Visible = gridWidth >= 1160;
            _categoryColumn.Visible = gridWidth >= 880;
            _publicationColumn.Visible = gridWidth >= 1080;
            _bindingColumn.Visible = gridWidth >= 1220;
            _shelfColumn.Visible = gridWidth >= 920;
            _activeColumn.Visible = gridWidth >= 900;
            _authorColumn.Visible = gridWidth >= 740;
            _isbnColumn.Visible = gridWidth >= 660;
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

        private sealed class BookRow
        {
            public Book Source { get; private set; }
            public string SelfCode { get { return Source.SelfCode; } }
            public string Isbn { get { return Source.Isbn; } }
            public string Title { get { return Source.Title; } }
            public string Author { get { return Source.Author; } }
            public string Publisher { get { return Source.Publisher; } }
            public string Category { get { return Source.Category; } }
            public string PublicationYear { get { return Source.PublicationYear; } }
            public string Binding { get { return Source.Binding; } }
            public string ShelfCode { get { return Source.ShelfCode; } }
            public decimal SalePriceYuan { get { return Source.SalePriceYuan; } }
            public int StockQuantity { get { return Source.StockQuantity; } }
            public AntdUI.CellTag[] ActiveStatus { get; private set; }

            public BookRow(Book source)
            {
                Source = source;
                ActiveStatus = new[]
                {
                    new AntdUI.CellTag(
                        source.IsActive ? "● 已启用" : "● 已停用",
                        source.IsActive ? AntdUI.TTypeMini.Success : AntdUI.TTypeMini.Default,
                        4).SetBorderWidth(0F)
                };
            }
        }

        private sealed class PrototypeSectionPanel : TableLayoutPanel
        {
            public PrototypeSectionPanel()
            {
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var scale = DeviceDpi > 0 ? DeviceDpi / 96F : 1F;
                var radius = Math.Max(2F, UiTheme.SectionRadius * scale);
                var inset = Math.Max(0.5F, UiTheme.SectionBorderWidth * scale / 2F);
                var rect = new RectangleF(
                    inset,
                    inset,
                    Math.Max(1F, ClientSize.Width - inset * 2F - 1F),
                    Math.Max(1F, ClientSize.Height - inset * 2F - 1F));

                using (var path = CreateRoundedRectangle(rect, radius))
                using (var pen = new Pen(UiTheme.Border, UiTheme.SectionBorderWidth * scale))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, path);
                }
            }

            private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
            {
                var diameter = radius * 2F;
                var path = new GraphicsPath();
                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180F, 90F);
                path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270F, 90F);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0F, 90F);
                path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90F, 90F);
                path.CloseFigure();
                return path;
            }
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
