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

        private PrototypeSectionPanel _searchSection;
        private TableLayoutPanel _searchRow;
        private FlowLayoutPanel _stats;
        private AntdUI.Button _searchButton;
        private AntdUI.Button _addButton;
        private AntdUI.Button _editButton;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;
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

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "108", MinWidth = "82", SortOrder = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "110", MinWidth = "92", SortOrder = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "110", MinWidth = "90", SortOrder = true };
            _publisherColumn = new AntdUI.Column("Publisher", "出版社") { Width = "105", MinWidth = "86", SortOrder = true };
            _categoryColumn = new AntdUI.Column("Category", "分类") { Width = "92", MinWidth = "78", SortOrder = true };
            _publicationColumn = new AntdUI.Column("PublicationYear", "出版年") { Width = "86", MinWidth = "72", SortOrder = true };
            _bindingColumn = new AntdUI.Column("Binding", "装帧") { Width = "78", MinWidth = "66", SortOrder = true };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "92", MinWidth = "78", SortOrder = true };
            _activeColumn = new AntdUI.Column("ActiveStatus", "启用") { Width = "94", MinWidth = "82" };

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
            ApplyUiSpecProfile(BookDeskUiSpec.Standard);
        }

        private Control CreateSearchSection()
        {
            _searchSection = new PrototypeSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.BookToolbarStandardHeight),
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(BookDeskUiSpec.Standard.ToolbarPadding),
                Margin = Padding.Empty
            };
            _searchSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _searchSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _searchSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.Standard.ControlHeight),
                ColumnCount = 6,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _searchRow.Controls.Add(new Label
            {
                Text = "搜索",
                AutoSize = false,
                Width = BookDeskUiSpec.BookSearchLabelWidth,
                MinimumSize = new Size(BookDeskUiSpec.BookSearchLabelWidth, BookDeskUiSpec.Standard.ControlHeight),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.BodyFontPoints, FontStyle.Bold),
                Margin = Padding.Empty
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
            _searchRow.Controls.Add(_search, 1, 0);

            _searchButton = UiTheme.CreateAntdButton("查询", false);
            _searchButton.Width = 72;
            _searchButton.MinimumSize = new Size(72, BookDeskUiSpec.Standard.ControlHeight);
            _searchButton.Anchor = AnchorStyles.Left;
            _searchButton.Tag = "toolbar-action";
            _searchButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _searchButton.Click += delegate { Reload(); };
            _searchRow.Controls.Add(_searchButton, 2, 0);

            _addButton = UiTheme.CreateAntdButton("新增图书", true);
            _addButton.IconSvg = "PlusOutlined";
            _addButton.Width = 124;
            _addButton.MinimumSize = new Size(124, BookDeskUiSpec.Standard.ControlHeight);
            _addButton.Anchor = AnchorStyles.Left;
            _addButton.Tag = "toolbar-action";
            _addButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _addButton.Click += delegate { EditBook(null); };
            _searchRow.Controls.Add(_addButton, 3, 0);

            _editButton = UiTheme.CreateAntdButton("编辑资料", false);
            _editButton.IconSvg = "EditOutlined";
            _editButton.Width = 116;
            _editButton.MinimumSize = new Size(116, BookDeskUiSpec.Standard.ControlHeight);
            _editButton.Anchor = AnchorStyles.Left;
            _editButton.Tag = "toolbar-action";
            _editButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _editButton.Click += delegate { EditSelected(); };
            _searchRow.Controls.Add(_editButton, 4, 0);

            _includeInactive.Text = "包含停用";
            _includeInactive.AutoSize = true;
            _includeInactive.Anchor = AnchorStyles.Left;
            _includeInactive.Margin = new Padding(4, 0, 0, 0);
            _includeInactive.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { Reload(); };
            _searchRow.Controls.Add(_includeInactive, 5, 0);

            _stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 14, 0, 0),
                Padding = Padding.Empty
            };

            _resultChip.AutoSize = false;
            _resultChip.Size = new Size(96, BookDeskUiSpec.Standard.MetricHeight);
            _resultChip.MinimumSize = new Size(96, BookDeskUiSpec.Standard.MetricHeight);
            _resultChip.Padding = new Padding(8, 0, 8, 0);
            _resultChip.Margin = new Padding(0, 0, 12, 0);
            _resultChip.BackColor = UiTheme.AccentSoft;
            _resultChip.ForeColor = UiTheme.Accent;
            _resultChip.Font = UiTheme.Font(9.5F, FontStyle.Bold);
            _resultChip.TextAlign = ContentAlignment.MiddleCenter;

            _lowStockChip.AutoSize = false;
            _lowStockChip.Size = new Size(112, BookDeskUiSpec.Standard.MetricHeight);
            _lowStockChip.MinimumSize = new Size(112, BookDeskUiSpec.Standard.MetricHeight);
            _lowStockChip.Padding = new Padding(8, 0, 8, 0);
            _lowStockChip.Margin = Padding.Empty;
            _lowStockChip.BackColor = Color.FromArgb(255, 247, 230);
            _lowStockChip.ForeColor = UiTheme.Warning;
            _lowStockChip.Font = UiTheme.Font(9.5F, FontStyle.Bold);
            _lowStockChip.TextAlign = ContentAlignment.MiddleCenter;

            _stats.Controls.Add(_resultChip);
            _stats.Controls.Add(_lowStockChip);

            _searchSection.Controls.Add(_searchRow, 0, 0);
            _searchSection.Controls.Add(_stats, 0, 1);
            return _searchSection;
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0);

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

            _grid.Gap = 12;
            _grid.RowHeight = UiTheme.TableRowHeight;
            _grid.RowHeightHeader = UiTheme.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有找到符合条件的图书资料";

            var titleColumn = new AntdUI.Column("Title", "书名")
            {
                Width = "fill",
                MinWidth = "140",
                MaxWidth = "300",
                Ellipsis = true,
                SortOrder = true
            };
            var priceColumn = new AntdUI.Column("SalePriceYuan", "销售价格")
            {
                Width = "102",
                MinWidth = "90",
                DisplayFormat = "0.00",
                SortOrder = true
            };
            var stockColumn = new AntdUI.Column("StockQuantity", "库存") { Width = "72", MinWidth = "62", SortOrder = true };

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
            _grid.ConfigureColumnPersistence(_services.Settings, "book-master-ui-spec-v4");

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

        public void ApplyUiSpecProfile(UiSpecProfile profile)
        {
            _profile = profile ?? BookDeskUiSpec.Standard;
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0)
                return;

            var profile = _profile ?? BookDeskUiSpec.Standard;
            var controlHeight = profile.ControlHeight;

            if (_searchSection != null)
            {
                _searchSection.Padding = new Padding(profile.ToolbarPadding);
                _searchSection.MinimumSize = new Size(
                    0,
                    profile.IsCompact
                        ? BookDeskUiSpec.BookToolbarCompactHeight
                        : BookDeskUiSpec.BookToolbarStandardHeight);
            }

            if (_searchRow != null)
                _searchRow.MinimumSize = new Size(0, controlHeight);

            _search.Height = controlHeight;
            _search.MinimumSize = new Size(BookDeskUiSpec.BookSearchMinimumWidth, controlHeight);
            _search.Font = UiTheme.Font(profile.BodyFontPoints);
            _search.Margin = new Padding(0, 0, profile.ControlGap, 0);

            SetToolbarButton(
                _searchButton,
                profile.IsCompact ? 64 : 72,
                profile);
            SetToolbarButton(
                _addButton,
                profile.IsCompact ? 108 : 124,
                profile);
            SetToolbarButton(
                _editButton,
                profile.IsCompact ? 104 : 116,
                profile);

            _includeInactive.Font = UiTheme.Font(profile.BodyFontPoints);
            _includeInactive.Margin = new Padding(4, 0, 0, 0);

            if (_stats != null)
                _stats.Margin = new Padding(0, profile.IsCompact ? 10 : 14, 0, 0);

            ResizeMetric(_resultChip, 96, profile.MetricHeight, profile);
            ResizeMetric(_lowStockChip, 112, profile.MetricHeight, profile);

            _split.Margin = new Padding(0, profile.SectionGap, 0, 0);
            _grid.RowHeightHeader = profile.TableHeaderHeight;
            _grid.RowHeight = profile.TableRowHeight;
            _grid.Font = UiTheme.Font(profile.TableFontPoints);

            // The approved four-page spec is table-first through 2560x1440.
            // Retain the legacy detail pane for exceptional ultra-wide setups,
            // but keep it outside the required acceptance matrix.
            var showDetails = ClientSize.Width >= 3000;
            _split.Panel2Collapsed = !showDetails;

            if (showDetails)
            {
                var desiredRightWidth = Math.Min(360, Math.Max(300, _split.Width / 4));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                const int minimumLeftWidth = 720;
                const int minimumRightWidth = 286;
                var maximumDistance = _split.Width - minimumRightWidth - _split.SplitterWidth;
                if (distance >= minimumLeftWidth && maximumDistance >= minimumLeftWidth)
                    _split.SplitterDistance = Math.Min(distance, maximumDistance);
            }

            var gridWidth = showDetails ? _split.Panel1.ClientSize.Width : ClientSize.Width;

            // The ui-spec requires all columns to remain visible at 1366x768.
            // Below the supported 1280px shell minimum, progressively remove
            // secondary metadata before allowing a table scrollbar.
            var fullColumns = gridWidth >= 1040;
            _selfCodeColumn.Visible = gridWidth >= 900;
            _isbnColumn.Visible = gridWidth >= 700;
            _authorColumn.Visible = gridWidth >= 760;
            _publisherColumn.Visible = fullColumns;
            _categoryColumn.Visible = gridWidth >= 900;
            _publicationColumn.Visible = fullColumns;
            _bindingColumn.Visible = fullColumns;
            _shelfColumn.Visible = gridWidth >= 900;
            _activeColumn.Visible = gridWidth >= 900;

            _grid.LoadLayout();
        }

        private static void SetToolbarButton(
            AntdUI.Button button,
            int width,
            UiSpecProfile profile)
        {
            if (button == null)
                return;

            button.Width = width;
            button.Height = profile.ControlHeight;
            button.MinimumSize = new Size(width, profile.ControlHeight);
            button.Font = UiTheme.Font(profile.BodyFontPoints);
            button.Margin = new Padding(0, 0, profile.ControlGap, 0);
        }

        private static void ResizeMetric(
            Label label,
            int width,
            int height,
            UiSpecProfile profile)
        {
            if (label == null)
                return;

            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
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
                        8).SetBorderWidth(0F)
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
