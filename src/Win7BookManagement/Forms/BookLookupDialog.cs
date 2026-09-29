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
        private readonly Label _summary = new Label();
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _shelfColumn;

        public Book SelectedBook { get; private set; }

        public BookLookupDialog(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);

            Text = "选择图书";
            StartPosition = FormStartPosition.CenterParent;
            Width = 860;
            Height = 590;
            MinimumSize = new Size(680, 460);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 12, 18, 6)
            };
            header.Controls.Add(new Label
            {
                Text = "查找并选择图书",
                Dock = DockStyle.Top,
                Height = 28,
                Font = UiTheme.Font(12.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            });
            header.Controls.Add(new Label
            {
                Text = "支持店内编码、ISBN、书名、作者、出版社、分类和货架位。双击结果也可以直接选择。",
                Dock = DockStyle.Bottom,
                Height = 24,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            });

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                ColumnCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 9, 12, 9)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

            _search = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 10, 3) };
            var searchButton = new Button { Text = "查询", Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 1) };
            searchButton.Click += delegate { Reload(); };
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            toolbar.Controls.Add(_search, 0, 0);
            toolbar.Controls.Add(searchButton, 1, 0);

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
                BackgroundColor = UiTheme.Surface
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 135 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });
            _authorColumn = new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 130 };
            _shelfColumn = new DataGridViewTextBoxColumn { HeaderText = "货架位", DataPropertyName = "ShelfCode", Width = 90 };
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 72 });
            _grid.CellDoubleClick += delegate { Choose(); };

            _summary.Dock = DockStyle.Bottom;
            _summary.Height = 28;
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Padding = new Padding(12, 6, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 12, 10),
                BackColor = UiTheme.Surface
            };
            var select = new Button { Text = "选择图书", Width = 100, Height = 34, Tag = "primary" };
            var cancel = new Button { Text = "取消", Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
            select.Click += delegate { Choose(); };
            buttons.Controls.Add(select);
            buttons.Controls.Add(cancel);

            Controls.Add(_grid);
            Controls.Add(_summary);
            Controls.Add(buttons);
            Controls.Add(toolbar);
            Controls.Add(header);

            AcceptButton = select;
            CancelButton = cancel;
            Resize += delegate { ApplyResponsiveColumns(); };

            UiTheme.Apply(this);
            Shown += delegate
            {
                Reload();
                ApplyResponsiveColumns();
                _search.Focus();
            };
        }

        private void Reload()
        {
            var result = _services.Books.Search(_search.Text, false);
            _grid.DataSource = result;
            _summary.Text = "找到 " + result.Count + " 条启用图书资料";
        }

        private void ApplyResponsiveColumns()
        {
            _authorColumn.Visible = ClientSize.Width >= 720;
            _shelfColumn.Visible = ClientSize.Width >= 780;
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
