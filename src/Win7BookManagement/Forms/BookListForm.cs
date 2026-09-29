using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookListForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _search;
        private readonly CheckBox _includeInactive;
        private readonly DataGridView _grid;
        private readonly Label _summary;

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _publisherColumn;
        private readonly DataGridViewColumn _categoryColumn;
        private readonly DataGridViewColumn _shelfColumn;
        private readonly DataGridViewColumn _activeColumn;

        public BookListForm(ApplicationServices services)
        {
            _services = services;
            BackColor = UiTheme.Background;

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 9, 12, 9)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

            _search = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 10, 3)
            };
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };

            var searchButton = new Button { Text = "查询", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 8, 1) };
            searchButton.Click += delegate { Reload(); };

            var addButton = new Button { Text = "新增图书", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 8, 1), Tag = "primary" };
            addButton.Click += delegate { EditBook(null); };

            var editButton = new Button { Text = "编辑", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 8, 1) };
            editButton.Click += delegate { EditSelected(); };

            _includeInactive = new CheckBox
            {
                Text = "包含停用",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 0, 0)
            };
            _includeInactive.CheckedChanged += delegate { Reload(); };

            toolbar.Controls.Add(_search, 0, 0);
            toolbar.Controls.Add(searchButton, 1, 0);
            toolbar.Controls.Add(addButton, 2, 0);
            toolbar.Controls.Add(editButton, 3, 0);
            toolbar.Controls.Add(_includeInactive, 4, 0);

            var hint = new Label
            {
                Text = "可按店内编码、ISBN、书名、作者、出版社、分类、出版年、装帧或货架位搜索；双击一行可直接编辑。",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 8, 8, 0),
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            };

            _summary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Padding = new Padding(10, 7, 8, 0),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.Surface
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BackgroundColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None
            };

            _selfCodeColumn = new DataGridViewTextBoxColumn { HeaderText = "店内编码", DataPropertyName = "SelfCode", Width = 100 };
            var isbnColumn = new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 132 };
            var titleColumn = new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180, FillWeight = 220 };
            var authorColumn = new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 120 };
            _publisherColumn = new DataGridViewTextBoxColumn { HeaderText = "出版社", DataPropertyName = "Publisher", Width = 120 };
            _categoryColumn = new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 92 };
            _shelfColumn = new DataGridViewTextBoxColumn { HeaderText = "货架位", DataPropertyName = "ShelfCode", Width = 86 };
            var priceColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "售价",
                DataPropertyName = "SalePriceYuan",
                Width = 82,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00", Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "库存",
                DataPropertyName = "StockQuantity",
                Width = 72,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            _activeColumn = new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = "IsActive", Width = 62 };

            _grid.Columns.Add(_selfCodeColumn);
            _grid.Columns.Add(isbnColumn);
            _grid.Columns.Add(titleColumn);
            _grid.Columns.Add(authorColumn);
            _grid.Columns.Add(_publisherColumn);
            _grid.Columns.Add(_categoryColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(priceColumn);
            _grid.Columns.Add(stockColumn);
            _grid.Columns.Add(_activeColumn);

            _grid.CellDoubleClick += delegate { EditSelected(); };
            _grid.CellFormatting += HighlightLowStock;

            Controls.Add(_grid);
            Controls.Add(_summary);
            Controls.Add(hint);
            Controls.Add(toolbar);

            Resize += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                Reload();
                ApplyResponsiveColumns();
                _search.Focus();
            };
        }

        private void Reload()
        {
            var books = _services.Books.Search(_search.Text, _includeInactive.Checked);
            _grid.DataSource = books;
            _summary.Text = "共 " + books.Count + " 条图书资料 · 低库存阈值 " + _services.Settings.GetLowStockThreshold() + " 册";
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;

            _selfCodeColumn.Visible = width >= 980;
            _publisherColumn.Visible = width >= 1080;
            _categoryColumn.Visible = width >= 900;
            _shelfColumn.Visible = width >= 820;
            _activeColumn.Visible = width >= 760;
        }

        private void HighlightLowStock(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var book = _grid.Rows[e.RowIndex].DataBoundItem as Book;
            if (book == null || !book.IsActive) return;

            if (book.StockQuantity <= _services.Settings.GetLowStockThreshold())
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UiTheme.Warning;
                _grid.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = UiTheme.Warning;
            }
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
