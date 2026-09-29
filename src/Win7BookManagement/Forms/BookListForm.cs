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

        public BookListForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _search = new TextBox { Width = 280, Margin = new Padding(0, 8, 8, 8) };
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };

            var searchButton = new Button { Text = "查询", Width = 72, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            searchButton.Click += delegate { Reload(); };

            var addButton = new Button { Text = "新增图书", Width = 88, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            addButton.Click += delegate { EditBook(null); };

            var editButton = new Button { Text = "编辑", Width = 72, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            editButton.Click += delegate { EditSelected(); };

            _includeInactive = new CheckBox { Text = "包含停用", AutoSize = true, Margin = new Padding(6, 10, 0, 0) };
            _includeInactive.CheckedChanged += delegate { Reload(); };

            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(searchButton);
            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(_includeInactive);

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
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "出版社", DataPropertyName = "Publisher", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 70 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = "IsActive", Width = 60 });
            _grid.CellDoubleClick += delegate { EditSelected(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);

            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            _grid.DataSource = _services.Books.Search(_search.Text, _includeInactive.Checked);
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
