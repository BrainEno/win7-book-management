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
        private readonly TextBox _search;
        private readonly CheckBox _includeInactive;
        private readonly DataGridView _grid;
        private readonly Label _summary;

        public BookListForm(ApplicationServices services)
        {
            _services = services;
            BackColor = UiTheme.Background;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 8, 12, 8)
            };

            _search = new TextBox { Width = 320, Margin = new Padding(0, 5, 8, 5) };
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };

            var searchButton = new Button { Text = "查询", Width = 72, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            searchButton.Click += delegate { Reload(); };

            var addButton = new Button { Text = "新增图书", Width = 92, Height = 32, Margin = new Padding(0, 3, 8, 3), Tag = "primary" };
            addButton.Click += delegate { EditBook(null); };

            var editButton = new Button { Text = "编辑", Width = 72, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            editButton.Click += delegate { EditSelected(); };

            _includeInactive = new CheckBox { Text = "包含停用", AutoSize = true, Margin = new Padding(8, 9, 0, 0) };
            _includeInactive.CheckedChanged += delegate { Reload(); };

            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(searchButton);
            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(_includeInactive);

            var hint = new Label
            {
                Text = "可按 ISBN、书名、作者、出版社或分类搜索",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 7, 0, 0),
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            };

            _summary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Padding = new Padding(10, 6, 0, 0),
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "出版社", DataPropertyName = "Publisher", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "售价",
                DataPropertyName = "SalePriceYuan",
                Width = 78,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 68 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = "IsActive", Width = 56 });
            _grid.CellDoubleClick += delegate { EditSelected(); };
            _grid.CellFormatting += HighlightLowStock;

            Controls.Add(_grid);
            Controls.Add(_summary);
            Controls.Add(hint);
            Controls.Add(toolbar);

            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            var books = _services.Books.Search(_search.Text, _includeInactive.Checked);
            _grid.DataSource = books;
            _summary.Text = "共 " + books.Count + " 条图书资料 · 低库存阈值 " + _services.Settings.GetLowStockThreshold() + " 册";
        }

        private void HighlightLowStock(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var book = _grid.Rows[e.RowIndex].DataBoundItem as Book;
            if (book == null || !book.IsActive) return;

            if (book.StockQuantity <= _services.Settings.GetLowStockThreshold())
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UiTheme.Warning;
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
