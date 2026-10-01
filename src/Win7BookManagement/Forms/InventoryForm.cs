using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class InventoryForm : Form, IUiSpecPage
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("按编码、ISBN、书名、作者、分类或货架位搜索");
        private readonly AntdUI.Checkbox _lowOnly = new AntdUI.Checkbox();
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();
        private readonly Label _resultChip = new Label();
        private readonly Label _lowChip = new Label();
        private readonly Label _stockChip = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private UiSpecSectionPanel _searchSection;
        private TableLayoutPanel _searchRow;
        private FlowLayoutPanel _stats;
        private Label _helperText;
        private AntdUI.Button _queryButton;
        private AntdUI.Button _topAdjustButton;
        private AntdUI.Button _detailAdjustButton;
        private UiSpecSectionPanel _leftSurface;
        private UiSpecSectionPanel _detailSurface;
        private TableLayoutPanel _detailHeader;
        private TableLayoutPanel _detailsTable;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _titleColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _categoryColumn;
        private readonly AntdUI.Column _shelfColumn;
        private readonly AntdUI.Column _stockColumn;
        private Book _selectedBook;

        public InventoryForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "108", MinWidth = "88" };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "116", MinWidth = "96" };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "140", MaxWidth = "320", Ellipsis = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "112", MinWidth = "92" };
            _categoryColumn = new AntdUI.Column("Category", "分类") { Width = "96", MinWidth = "82" };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "100", MinWidth = "84" };
            _stockColumn = new AntdUI.Column("StockQuantity", "当前库存") { Width = "100", MinWidth = "90" };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
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
            _searchSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.InventoryToolbarStandardHeight),
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
                ColumnCount = 5,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _searchRow.Controls.Add(new Label
            {
                Text = "综合搜索",
                AutoSize = false,
                Width = BookDeskUiSpec.InventorySearchLabelWidth,
                MinimumSize = new Size(BookDeskUiSpec.InventorySearchLabelWidth, BookDeskUiSpec.Standard.ControlHeight),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.BodyFontPoints, FontStyle.Bold),
                Margin = Padding.Empty
            }, 0, 0);

            _search.Dock = DockStyle.None;
            _search.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _search.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            _searchRow.Controls.Add(_search, 1, 0);

            _queryButton = UiTheme.CreateAntdButton("查询", false);
            _queryButton.Width = BookDeskUiSpec.InventoryQueryWidth;
            _queryButton.Click += delegate { Reload(); };
            _searchRow.Controls.Add(_queryButton, 2, 0);

            _topAdjustButton = UiTheme.CreateAntdButton("库存调整", true);
            _topAdjustButton.Width = BookDeskUiSpec.InventoryTopAdjustWidth;
            _topAdjustButton.Click += delegate { AdjustSelected(); };
            _searchRow.Controls.Add(_topAdjustButton, 3, 0);

            _lowOnly.Text = "仅看低库存";
            _lowOnly.AutoSize = false;
            _lowOnly.Width = BookDeskUiSpec.InventoryLowOnlyWidth;
            _lowOnly.Anchor = AnchorStyles.Left;
            _lowOnly.Margin = Padding.Empty;
            _lowOnly.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { Reload(); };
            _searchRow.Controls.Add(_lowOnly, 4, 0);

            _stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0),
                Padding = Padding.Empty
            };
            ConfigureChip(_resultChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_lowChip, Color.FromArgb(252, 241, 226), UiTheme.Warning);
            ConfigureChip(_stockChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            _stats.Controls.Add(_resultChip);
            _stats.Controls.Add(_lowChip);
            _stats.Controls.Add(_stockChip);

            _helperText = new Label
            {
                AutoSize = true,
                Text = "库存变化必须来自业务单据或带原因的库存调整。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints),
                Margin = new Padding(6, 7, 0, 0)
            };
            _stats.Controls.Add(_helperText);

            _searchSection.Controls.Add(_searchRow, 0, 0);
            _searchSection.Controls.Add(_stats, 0, 1);
            return _searchSection;
        }

        private static void ConfigureChip(Label label, Color backColor, Color foreColor)
        {
            label.AutoSize = false;
            label.Height = BookDeskUiSpec.Standard.MetricHeight;
            label.Padding = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8.2F, FontStyle.Bold);
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.SplitterWidth = BookDeskUiSpec.Standard.SectionGap;
            _split.IsSplitterFixed = true;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0);

            ConfigureGrid();

            _leftSurface = new UiSpecSectionPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _leftSurface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _leftSurface.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _leftSurface.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _summary.AutoSize = false;
            _summary.Dock = DockStyle.Fill;
            _summary.MinimumSize = new Size(0, 34);
            _summary.Padding = new Padding(12, 0, 8, 0);
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);

            _leftSurface.Controls.Add(_grid, 0, 0);
            _leftSurface.Controls.Add(_summary, 0, 1);

            _detailSurface = CreateDetailPanel();
            _split.Panel1.Controls.Add(_leftSurface);
            _split.Panel2.Controls.Add(_detailSurface);
            return _split;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;

            _grid.RowHeight = BookDeskUiSpec.Standard.TableRowHeight;
            _grid.RowHeightHeader = BookDeskUiSpec.Standard.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有符合条件的库存记录";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _selfCodeColumn,
                _isbnColumn,
                _titleColumn,
                _authorColumn,
                _categoryColumn,
                _shelfColumn,
                _stockColumn
            };
            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                ShowSelectedDetails();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                AdjustSelected();
            };
            _grid.SetRowStyle += delegate(object sender, AntdUI.TableSetRowStyleEventArgs e)
            {
                var book = e.Record as Book;
                if (book != null && book.StockQuantity <= _services.Settings.GetLowStockThreshold())
                    return new AntdUI.Table.CellStyleInfo { ForeColor = UiTheme.Warning };
                return null;
            };
        }

        private UiSpecSectionPanel CreateDetailPanel()
        {
            var host = new UiSpecSectionPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14),
                Margin = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _detailHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 48,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _detailHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _detailHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _detailHeader.Controls.Add(new Label
            {
                Text = "库存详情",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(BookDeskUiSpec.PixelFontToPoints(16), FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            _detailAdjustButton = UiTheme.CreateAntdButton("库存调整", true);
            _detailAdjustButton.Width = BookDeskUiSpec.InventoryDetailAdjustWidth;
            _detailAdjustButton.Click += delegate { AdjustSelected(); };
            _detailHeader.Controls.Add(_detailAdjustButton, 1, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0, 8, 0, 0)
            };

            _detailsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _detailsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BookDeskUiSpec.InventoryDetailLabelWidth));
            _detailsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddDetailRow(_detailsTable, "书名", "title");
            AddDetailRow(_detailsTable, "店内编码", "selfCode");
            AddDetailRow(_detailsTable, "ISBN", "isbn");
            AddDetailRow(_detailsTable, "作者", "author");
            AddDetailRow(_detailsTable, "分类", "category");
            AddDetailRow(_detailsTable, "货架位", "shelf");
            AddDetailRow(_detailsTable, "当前库存", "stock");

            scroll.Controls.Add(_detailsTable);
            host.Controls.Add(_detailHeader, 0, 0);
            host.Controls.Add(scroll, 0, 1);
            return host;
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = false,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(BookDeskUiSpec.InventoryDetailLabelWidth, BookDeskUiSpec.InventoryDetailRowHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F, FontStyle.Bold)
            }, 0, row);
            var value = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, BookDeskUiSpec.InventoryDetailRowHeight - 8),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 4, 0, 4),
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true,
                Font = UiTheme.Font(8.5F)
            };
            table.Controls.Add(value, 1, row);
            _detailValues[key] = value;
        }

        private void Reload()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var books = _services.Books.Search(_search.Text, false);
            if (_lowOnly.Checked) books = books.Where(book => book.StockQuantity <= threshold).ToList();

            _selectedBook = null;
            _grid.DataSource = books;

            var lowCount = 0;
            long stockTotal = 0;
            foreach (var book in books)
            {
                stockTotal += book.StockQuantity;
                if (book.StockQuantity <= threshold) lowCount++;
            }

            _resultChip.Text = "结果  " + books.Count;
            _lowChip.Text = "低库存  " + lowCount;
            _stockChip.Text = "合计库存  " + stockTotal + " 册";
            _summary.Text = "显示 " + books.Count + " 个启用品种 · 低库存阈值 ≤ " + threshold + " 册 · 表头可拖动调整宽度";
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
            var compact = profile.IsCompact;
            var controlHeight = profile.ControlHeight;

            if (_searchSection != null)
            {
                _searchSection.Padding = new Padding(profile.ToolbarPadding);
                _searchSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.InventoryToolbarCompactHeight
                        : BookDeskUiSpec.InventoryToolbarStandardHeight);
            }

            if (_searchRow != null)
                _searchRow.MinimumSize = new Size(0, controlHeight);

            _search.Height = controlHeight;
            _search.MinimumSize = new Size(BookDeskUiSpec.InventorySearchMinimumWidth, controlHeight);
            _search.Font = UiTheme.Font(profile.BodyFontPoints);
            _search.Margin = new Padding(0, 0, profile.ControlGap, 0);

            SetToolbarButton(_queryButton, BookDeskUiSpec.InventoryQueryWidth, profile);
            SetToolbarButton(_topAdjustButton, BookDeskUiSpec.InventoryTopAdjustWidth, profile);
            SetToolbarButton(_detailAdjustButton, BookDeskUiSpec.InventoryDetailAdjustWidth, profile);

            _lowOnly.Width = BookDeskUiSpec.InventoryLowOnlyWidth;
            _lowOnly.Height = controlHeight;
            _lowOnly.Font = UiTheme.Font(profile.BodyFontPoints);

            if (_stats != null)
                _stats.Margin = new Padding(0, compact ? 8 : 12, 0, 0);

            ResizeMetric(_resultChip, 96, profile.MetricHeight, profile);
            ResizeMetric(_lowChip, 108, profile.MetricHeight, profile);
            ResizeMetric(_stockChip, 138, profile.MetricHeight, profile);

            if (_helperText != null)
            {
                _helperText.Font = UiTheme.Font(profile.SecondaryFontPoints);
                _helperText.Visible = !compact || ClientSize.Width >= 1320;
            }

            _split.Margin = new Padding(0, profile.SectionGap, 0, 0);
            _split.SplitterWidth = profile.SectionGap;

            var workspaceWidth = Math.Max(0, ClientSize.Width);
            var stackDetails = workspaceWidth < BookDeskUiSpec.InventorySplitStackThreshold;

            if (stackDetails)
            {
                if (_split.Orientation != Orientation.Horizontal)
                    _split.Orientation = Orientation.Horizontal;

                _split.FixedPanel = FixedPanel.Panel2;
                var desiredBottomHeight = Math.Min(330, Math.Max(250, _split.Height / 3));
                var maxDistance = _split.Height - desiredBottomHeight - _split.SplitterWidth;
                if (maxDistance > 220)
                    _split.SplitterDistance = maxDistance;
            }
            else
            {
                if (_split.Orientation != Orientation.Vertical)
                    _split.Orientation = Orientation.Vertical;

                _split.FixedPanel = FixedPanel.Panel2;
                var desiredRightWidth = compact
                    ? BookDeskUiSpec.InventoryDetailCompactWidth
                    : BookDeskUiSpec.InventoryDetailStandardWidth;
                desiredRightWidth = Math.Min(
                    BookDeskUiSpec.InventoryDetailMaxWidth,
                    Math.Max(BookDeskUiSpec.InventoryDetailCompactWidth, desiredRightWidth));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                var maximumDistance = _split.Width - BookDeskUiSpec.InventoryDetailCompactWidth - _split.SplitterWidth;
                if (distance >= 600 && maximumDistance >= 600)
                    _split.SplitterDistance = Math.Min(distance, maximumDistance);
            }

            _grid.RowHeightHeader = profile.TableHeaderHeight;
            _grid.RowHeight = profile.TableRowHeight;
            _grid.Font = UiTheme.Font(profile.TableFontPoints);
            ApplyColumnWidths(profile);

            _summary.MinimumSize = new Size(0, compact ? 30 : 34);
            _summary.Font = UiTheme.Font(profile.SecondaryFontPoints);

            if (_detailSurface != null)
                _detailSurface.Padding = compact ? new Padding(10) : new Padding(14);

            foreach (var value in _detailValues.Values)
            {
                value.Font = UiTheme.Font(profile.SecondaryFontPoints);
                value.MinimumSize = new Size(0, compact ? 50 : BookDeskUiSpec.InventoryDetailRowHeight - 8);
            }

            _selfCodeColumn.Visible = true;
            _isbnColumn.Visible = true;
            _titleColumn.Visible = true;
            _authorColumn.Visible = true;
            _categoryColumn.Visible = true;
            _shelfColumn.Visible = true;
            _stockColumn.Visible = true;
            _grid.LoadLayout();
        }

        private void ApplyColumnWidths(UiSpecProfile profile)
        {
            var compact = profile != null && profile.IsCompact;
            _selfCodeColumn.Width = compact ? "88" : "108";
            _isbnColumn.Width = compact ? "96" : "116";
            _titleColumn.Width = "fill";
            _titleColumn.MinWidth = compact ? "130" : "140";
            _titleColumn.MaxWidth = compact ? "260" : "320";
            _authorColumn.Width = compact ? "92" : "112";
            _categoryColumn.Width = compact ? "82" : "96";
            _shelfColumn.Width = compact ? "84" : "100";
            _stockColumn.Width = compact ? "90" : "100";
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

            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleCenter;
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

            var threshold = _services.Settings.GetLowStockThreshold();
            var low = book.StockQuantity <= threshold;
            _detailValues["title"].Text = EmptyAsDash(book.Title);
            _detailValues["selfCode"].Text = EmptyAsDash(book.SelfCode);
            _detailValues["isbn"].Text = EmptyAsDash(book.Isbn);
            _detailValues["author"].Text = EmptyAsDash(book.Author);
            _detailValues["category"].Text = EmptyAsDash(book.Category);
            _detailValues["shelf"].Text = EmptyAsDash(book.ShelfCode);
            _detailValues["stock"].Text = book.StockQuantity + " 册";
        }

        private static string EmptyAsDash(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
        }

        private void AdjustSelected()
        {
            if (_selectedBook == null)
            {
                MessageBox.Show(this, "请先选择要调整库存的图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new AdjustmentDialog(_selectedBook))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var no = _services.Inventory.Adjust(_selectedBook.Id, dialog.Delta, dialog.Note);
                    MessageBox.Show(this, "库存调整完成。\r\n流水号：" + no, "调整成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Reload();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "调整失败：\r\n" + ex.Message, "请检查调整内容", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private sealed class AdjustmentDialog : Form
        {
            private readonly Book _book;
            private readonly AntdUI.Select _direction = new AntdUI.Select();
            private readonly AntdUI.InputNumber _quantity = new AntdUI.InputNumber();
            private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("例如：盘点差异、破损报废、录入修正");
            private readonly Label _preview = new Label();

            public int Delta
            {
                get
                {
                    var amount = Decimal.ToInt32(_quantity.Value);
                    return _direction.SelectedIndex == 1 ? -amount : amount;
                }
            }

            public string Note { get { return _note.Text; } }

            public AdjustmentDialog(Book book)
            {
                _book = book;
                UiTheme.ConfigureForm(this);
                Text = "库存调整";
                StartPosition = FormStartPosition.CenterParent;
                Width = 600;
                Height = 450;
                MinimumSize = new Size(520, 400);
                BackColor = UiTheme.Background;
                ShowInTaskbar = false;
                MinimizeBox = false;
                KeyPreview = true;

                _direction.Items.Add("增加库存");
                _direction.Items.Add("减少库存");
                _direction.SelectedIndex = 0;
                _direction.DropDownArrow = true;

                _quantity.Minimum = 1;
                _quantity.Maximum = 1000000;
                _quantity.Value = 1;

                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 14, 22, 12)
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.Controls.Add(new Label
                {
                    Text = book.Title,
                    AutoSize = true,
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 5)
                }, 0, 0);
                header.Controls.Add(new Label
                {
                    Text = "当前库存 " + book.StockQuantity + " 册" +
                           (string.IsNullOrWhiteSpace(book.ShelfCode) ? "" : " · 货架位 " + book.ShelfCode),
                    AutoSize = true,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F)
                }, 0, 1);

                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(22, 18, 22, 14),
                    BackColor = UiTheme.Surface
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddDialogLabel(body, 0, "调整方向 *");
                _direction.Dock = DockStyle.Fill;
                _direction.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_direction, 1, 0);

                AddDialogLabel(body, 1, "调整数量 *");
                _quantity.Dock = DockStyle.Fill;
                _quantity.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_quantity, 1, 1);

                AddDialogLabel(body, 2, "调整后");
                _preview.Dock = DockStyle.Fill;
                _preview.TextAlign = ContentAlignment.MiddleLeft;
                _preview.Padding = new Padding(8, 0, 0, 0);
                _preview.Font = UiTheme.Font(10F, FontStyle.Bold);
                body.Controls.Add(_preview, 1, 2);

                AddDialogLabel(body, 3, "调整原因 *");
                _note.Dock = DockStyle.Fill;
                _note.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_note, 1, 3);

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    MinimumSize = new Size(0, 70),
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 12, 22, 12)
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                footer.Controls.Add(new Label
                {
                    Text = "每次调整都会写入库存流水；不能直接改写历史流水。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8F)
                }, 0, 0);

                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 90;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                var ok = UiTheme.CreateAntdButton("确认调整", true);
                ok.Width = 110;
                ok.Click += Confirm;
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                footer.Controls.Add(buttons, 1, 0);

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                _direction.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e) { UpdatePreview(); };
                _quantity.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e) { UpdatePreview(); };
                KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                };

                UiTheme.Apply(this);
                UpdatePreview();
                Shown += delegate { UiTheme.FitDialogToWorkingArea(this, 24); };
            }

            private static void AddDialogLabel(TableLayoutPanel body, int row, string text)
            {
                body.Controls.Add(new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold)
                }, 0, row);
            }

            private void UpdatePreview()
            {
                var after = (long)_book.StockQuantity + Delta;
                _preview.Text = after + " 册";
                _preview.ForeColor = after < 0 ? UiTheme.Danger : UiTheme.Accent;
            }

            private void Confirm(object sender, EventArgs e)
            {
                var after = (long)_book.StockQuantity + Delta;
                if (after < 0)
                {
                    MessageBox.Show(this, "减少后的库存不能小于 0。当前库存只有 " + _book.StockQuantity + " 册，请修改调整数量。", "数量过大", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _quantity.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(Note))
                {
                    MessageBox.Show(this, "请填写调整原因，例如“盘点差异”“破损报废”或“录入修正”。", "请填写原因", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _note.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
