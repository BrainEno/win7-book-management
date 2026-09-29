using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class InventoryForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _search = new TextBox();
        private readonly CheckBox _lowOnly = new CheckBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _summary = new Label();
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _categoryColumn;
        private readonly DataGridViewColumn _shelfColumn;

        public InventoryForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = UiTheme.CreateResponsiveToolbar();
            _search.Width = 300;
            _search.Margin = new Padding(0, 5, 8, 5);

            var searchButton = new Button { Text = "查询", Width = 76, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            var adjustButton = new Button { Text = "库存调整", Width = 96, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            _lowOnly.Text = "仅看低库存";
            _lowOnly.AutoSize = true;
            _lowOnly.Margin = new Padding(8, 9, 6, 0);

            searchButton.Click += delegate { Reload(); };
            adjustButton.Click += delegate { AdjustSelected(); };
            _lowOnly.CheckedChanged += delegate { Reload(); };
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
            toolbar.Controls.Add(adjustButton);
            toolbar.Controls.Add(_lowOnly);

            var hint = new Label
            {
                Text = "库存数量只能来自采购、销售、退货或“库存调整”。可按店内编码、ISBN、书名、作者、分类和货架位查找。",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 8, 8, 0),
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            };

            _summary.Dock = DockStyle.Bottom;
            _summary.Height = 32;
            _summary.Padding = new Padding(10, 8, 8, 0);
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;

            var isbnColumn = new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 138 };
            var titleColumn = new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 190 };
            _authorColumn = new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 130 };
            _categoryColumn = new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 100 };
            _shelfColumn = new DataGridViewTextBoxColumn { HeaderText = "货架位", DataPropertyName = "ShelfCode", Width = 94 };
            var stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "库存",
                DataPropertyName = "StockQuantity",
                Width = 82,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            _grid.Columns.Add(isbnColumn);
            _grid.Columns.Add(titleColumn);
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_categoryColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(stockColumn);
            _grid.CellFormatting += HighlightLowStock;
            _grid.CellDoubleClick += delegate { AdjustSelected(); };

            Controls.Add(_grid);
            Controls.Add(_summary);
            Controls.Add(hint);
            Controls.Add(toolbar);

            Resize += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                Reload();
                ApplyResponsiveColumns();
            };
        }

        private void Reload()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var books = _services.Books.Search(_search.Text, false);
            if (_lowOnly.Checked)
                books = books.Where(book => book.StockQuantity <= threshold).ToList();

            _grid.DataSource = books;
            _summary.Text = "显示 " + books.Count + " 个启用品种 · 低库存阈值 ≤ " + threshold + " 册 · 双击图书可调整库存";
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _authorColumn.Visible = width >= 760;
            _categoryColumn.Visible = width >= 900;
            _shelfColumn.Visible = width >= 680;
        }

        private void HighlightLowStock(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var book = _grid.Rows[e.RowIndex].DataBoundItem as Book;
            if (book != null && book.StockQuantity <= _services.Settings.GetLowStockThreshold())
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = UiTheme.Warning;
                _grid.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = UiTheme.Warning;
            }
        }

        private void AdjustSelected()
        {
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                MessageBox.Show(this, "请先选择图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new AdjustmentDialog(book))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var no = _services.Inventory.Adjust(book.Id, dialog.Delta, dialog.Note);
                    MessageBox.Show(this, "库存调整完成。流水号：" + no, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Reload();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "调整失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private sealed class AdjustmentDialog : Form
        {
            private readonly NumericUpDown _delta = new NumericUpDown();
            private readonly TextBox _note = new TextBox();

            public int Delta { get { return Decimal.ToInt32(_delta.Value); } }
            public string Note { get { return _note.Text; } }

            public AdjustmentDialog(Book book)
            {
                UiTheme.ConfigureForm(this);
                Text = "库存调整";
                StartPosition = FormStartPosition.CenterParent;
                Width = 520;
                Height = 360;
                MinimumSize = new Size(460, 330);
                BackColor = UiTheme.Background;
                ShowInTaskbar = false;

                _delta.Minimum = -1000000;
                _delta.Maximum = 1000000;
                _delta.Value = 0;
                _note.Multiline = true;

                var header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 78,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 14, 22, 8)
                };
                header.Controls.Add(new Label
                {
                    Text = book.Title,
                    Dock = DockStyle.Top,
                    Height = 28,
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary
                });
                header.Controls.Add(new Label
                {
                    Text = "当前库存 " + book.StockQuantity + " 册" +
                           (string.IsNullOrWhiteSpace(book.ShelfCode) ? "" : " · 货架位 " + book.ShelfCode),
                    Dock = DockStyle.Bottom,
                    Height = 24,
                    ForeColor = UiTheme.TextSecondary
                });

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 3,
                    Padding = new Padding(22, 18, 22, 10),
                    BackColor = UiTheme.Surface
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

                table.Controls.Add(new Label { Text = "调整数量 *", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
                _delta.Dock = DockStyle.Fill;
                _delta.Margin = new Padding(8, 7, 0, 7);
                table.Controls.Add(_delta, 1, 0);

                table.Controls.Add(new Label { Text = "调整原因 *", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, Padding = new Padding(0, 9, 0, 0) }, 0, 1);
                _note.Dock = DockStyle.Fill;
                _note.Margin = new Padding(8, 7, 0, 7);
                table.Controls.Add(_note, 1, 1);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                var ok = new Button { Text = "确认调整", Width = 96, Height = 34, Tag = "primary" };
                var cancel = new Button { Text = "取消", Width = 84, Height = 34, DialogResult = DialogResult.Cancel };
                ok.Click += delegate
                {
                    if (Delta == 0)
                    {
                        MessageBox.Show(this, "调整数量不能为 0。正数表示增加，负数表示减少。", "请填写数量", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(Note))
                    {
                        MessageBox.Show(this, "请填写调整原因，例如“盘点差异”或“破损报废”。", "请填写原因", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _note.Focus();
                        return;
                    }
                    DialogResult = DialogResult.OK;
                    Close();
                };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                table.Controls.Add(buttons, 1, 2);

                Controls.Add(table);
                Controls.Add(header);
                AcceptButton = ok;
                CancelButton = cancel;
                UiTheme.Apply(this);
            }
        }
    }
}
