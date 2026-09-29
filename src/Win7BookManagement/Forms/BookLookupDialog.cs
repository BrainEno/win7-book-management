using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookLookupDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _search;
        private readonly DataGridView _grid;

        public Book SelectedBook { get; private set; }

        public BookLookupDialog(ApplicationServices services)
        {
            _services = services;
            Text = "选择图书";
            StartPosition = FormStartPosition.CenterParent;
            Width = 760;
            Height = 520;
            Font = new Font("Microsoft YaHei", 9F);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _search = new TextBox { Width = 300, Margin = new Padding(0, 8, 8, 8) };
            var searchButton = new Button { Text = "查询", Width = 72, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            searchButton.Click += delegate { Reload(); };
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(searchButton);

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
                BackgroundColor = Color.White
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 135 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 70 });

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 8)
            };
            var select = new Button { Text = "选择", Width = 86, Height = 30 };
            var cancel = new Button { Text = "取消", Width = 86, Height = 30, DialogResult = DialogResult.Cancel };
            select.Click += delegate { Choose(); };
            buttons.Controls.Add(select);
            buttons.Controls.Add(cancel);

            _grid.CellDoubleClick += delegate { Choose(); };

            Controls.Add(_grid);
            Controls.Add(buttons);
            Controls.Add(toolbar);
            AcceptButton = select;
            CancelButton = cancel;
            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            _grid.DataSource = _services.Books.Search(_search.Text, false);
        }

        private void Choose()
        {
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                MessageBox.Show(this, "请先选择图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedBook = book;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
