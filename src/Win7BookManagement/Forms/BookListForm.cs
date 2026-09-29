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
        private readonly TextBox _search = new TextBox();
        private readonly CheckBox _includeInactive = new CheckBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _summary = new Label();
        private readonly Label _resultChip = new Label();
        private readonly Label _lowStockChip = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _publisherColumn;
        private readonly DataGridViewColumn _categoryColumn;
        private readonly DataGridViewColumn _publicationColumn;
        private readonly DataGridViewColumn _bindingColumn;
        private readonly DataGridViewColumn _shelfColumn;
        private readonly DataGridViewColumn _activeColumn;

        public BookListForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new DataGridViewTextBoxColumn { HeaderText = "店内编码", DataPropertyName = "SelfCode", Width = 98 };
            _isbnColumn = new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 132 };
            _authorColumn = new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 116 };
            _publisherColumn = new DataGridViewTextBoxColumn { HeaderText = "出版社", DataPropertyName = "Publisher", Width = 122 };
            _categoryColumn = new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 92 };
            _publicationColumn = new DataGridViewTextBoxColumn { HeaderText = "出版年", DataPropertyName = "PublicationYear", Width = 84 };
            _bindingColumn = new DataGridViewTextBoxColumn { HeaderText = "装帧", DataPropertyName = "Binding", Width = 76 };
            _shelfColumn = new DataGridViewTextBoxColumn { HeaderText = "货架位", DataPropertyName = "ShelfCode", Width = 86 };
            _activeColumn = new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = "IsActive", Width = 62 };

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
            _summary.MinimumSize = new Size(0, 36);
            _summary.Padding = new Padding(10, 8, 8, 8);
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.BackColor = UiTheme.Surface;
            _summary.Font = UiTheme.Font(8F);
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
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "图书查询",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            };
            section.Controls.Add(title, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            var addButton = new Button { Text = "＋ 新增图书", Width = 108, Height = UiTheme.ButtonHeight, Tag = "primary" };
            var editButton = new Button { Text = "编辑资料", Width = 94, Height = UiTheme.ButtonHeight };
            addButton.Click += delegate { EditBook(null); };
            editButton.Click += delegate { EditSelected(); };

            _includeInactive.Text = "包含停用";
            _includeInactive.AutoSize = true;
            _includeInactive.Margin = new Padding(10, 10, 0, 0);
            _includeInactive.CheckedChanged += delegate { Reload(); };

            actions.Controls.Add(addButton);
            actions.Controls.Add(editButton);
            actions.Controls.Add(_includeInactive);
            section.Controls.Add(actions, 1, 0);

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 0)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

            var searchLabel = new Label
            {
                Text = "综合搜索",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            };
            searchRow.Controls.Add(searchLabel, 0, 0);

            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 4, 10, 4);
            _search.Font = UiTheme.Font(9.5F);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            searchRow.Controls.Add(_search, 1, 0);

            var searchButton = new Button
            {
                Text = "查询",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                Tag = "primary"
            };
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
                Margin = new Padding(0, 8, 0, 0)
            };

            _resultChip.AutoSize = true;
            _resultChip.Padding = new Padding(9, 5, 9, 5);
            _resultChip.Margin = new Padding(0, 0, 8, 0);
            _resultChip.BackColor = UiTheme.AccentSoft;
            _resultChip.ForeColor = UiTheme.Accent;
            _resultChip.Font = UiTheme.Font(8F, FontStyle.Bold);

            _lowStockChip.AutoSize = true;
            _lowStockChip.Padding = new Padding(9, 5, 9, 5);
            _lowStockChip.Margin = new Padding(0, 0, 8, 0);
            _lowStockChip.BackColor = Color.FromArgb(252, 241, 226);
            _lowStockChip.ForeColor = UiTheme.Warning;
            _lowStockChip.Font = UiTheme.Font(8F, FontStyle.Bold);

            var hint = new Label
            {
                AutoSize = true,
                Text = "支持店内编码、ISBN、书名、作者、出版社、分类、出版年、版次、装帧、货架位和备注。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(4, 5, 0, 0)
            };

            stats.Controls.Add(_resultChip);
            stats.Controls.Add(_lowStockChip);
            stats.Controls.Add(hint);
            section.Controls.Add(stats, 0, 2);
            section.SetColumnSpan(stats, 2);

            return section;
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.Panel1MinSize = 420;
            _split.Panel2MinSize = 300;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            ConfigureGrid();

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = Padding.Empty
            };

            var gridTitle = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Text = "图书表格",
                Padding = new Padding(12, 10, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(9F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                BackColor = UiTheme.Surface
            };

            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(gridTitle);
            _split.Panel1.Controls.Add(gridHost);
            _split.Panel2.Controls.Add(CreateDetailPanel());

            return _split;
        }

        private Control CreateDetailPanel()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
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
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));

            var title = new Label
            {
                Text = "基本信息",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            var edit = new Button
            {
                Text = "编辑资料",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            edit.Click += delegate { EditSelected(); };
            header.Controls.Add(title, 0, 0);
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
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
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
            AddDetailRow(details, "定价", "listPrice");
            AddDetailRow(details, "默认进价", "purchasePrice");
            AddDetailRow(details, "零售价", "salePrice");
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
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, height),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 8, 0),
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

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.BorderStyle = BorderStyle.None;

            var titleColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 190,
                FillWeight = 220
            };
            var priceColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "售价",
                DataPropertyName = "SalePriceYuan",
                Width = 82,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            var stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "库存",
                DataPropertyName = "StockQuantity",
                Width = 68,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            _grid.Columns.Add(_selfCodeColumn);
            _grid.Columns.Add(_isbnColumn);
            _grid.Columns.Add(titleColumn);
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_publisherColumn);
            _grid.Columns.Add(_categoryColumn);
            _grid.Columns.Add(_publicationColumn);
            _grid.Columns.Add(_bindingColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(priceColumn);
            _grid.Columns.Add(stockColumn);
            _grid.Columns.Add(_activeColumn);

            _grid.CellDoubleClick += delegate { EditSelected(); };
            _grid.CellFormatting += HighlightLowStock;
            _grid.SelectionChanged += delegate { ShowSelectedDetails(); };
        }

        private void Reload()
        {
            var books = _services.Books.Search(_search.Text, _includeInactive.Checked);
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
            _summary.Text = "共 " + books.Count + " 条图书资料 · 低库存阈值 " + threshold + " 册 · 双击表格行可编辑";

            ShowSelectedDetails();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0)
                return;

            var showDetails = ClientSize.Width >= UiTheme.WideBreakpoint;
            _split.Panel2Collapsed = !showDetails;

            if (showDetails)
            {
                var desiredRightWidth = Math.Min(370, Math.Max(310, _split.Width / 3));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                if (distance >= _split.Panel1MinSize)
                    _split.SplitterDistance = distance;
            }

            var gridWidth = showDetails ? _split.Panel1.ClientSize.Width : ClientSize.Width;
            _selfCodeColumn.Visible = gridWidth >= 920;
            _publisherColumn.Visible = gridWidth >= 1040;
            _categoryColumn.Visible = gridWidth >= 820;
            _publicationColumn.Visible = gridWidth >= 960;
            _bindingColumn.Visible = gridWidth >= 1110;
            _shelfColumn.Visible = gridWidth >= 760;
            _activeColumn.Visible = gridWidth >= 700;
            _authorColumn.Visible = gridWidth >= 660;
            _isbnColumn.Visible = gridWidth >= 580;
        }

        private void HighlightLowStock(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var book = _grid.Rows[e.RowIndex].DataBoundItem as Book;
            if (book == null) return;

            var low = book.IsActive && book.StockQuantity <= _services.Settings.GetLowStockThreshold();
            _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = low ? UiTheme.Warning : UiTheme.TextPrimary;
            _grid.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = low ? UiTheme.Warning : UiTheme.TextPrimary;
        }

        private void ShowSelectedDetails()
        {
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                foreach (var value in _detailValues.Values)
                    value.Text = "—";
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
            _detailValues["listPrice"].Text = "¥" + book.ListPriceYuan.ToString("0.00");
            _detailValues["purchasePrice"].Text = "¥" + book.DefaultPurchasePriceYuan.ToString("0.00");
            _detailValues["salePrice"].Text = "¥" + book.SalePriceYuan.ToString("0.00");
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
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                MessageBox.Show(this, "请先选择一条图书资料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditBook(book);
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
