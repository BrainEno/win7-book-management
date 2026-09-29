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

        public InventoryForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 8, 12, 8)
            };
            _search.Width = 280;
            _search.Margin = new Padding(0, 5, 8, 5);
            var searchButton = new Button { Text = "查询", Width = 72, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            var adjustButton = new Button { Text = "库存调整", Width = 90, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            _lowOnly.Text = "仅看低库存";
            _lowOnly.AutoSize = true;
            _lowOnly.Margin = new Padding(8, 9, 0, 0);

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

            _summary.Dock = DockStyle.Bottom;
            _summary.Height = 30;
            _summary.Padding = new Padding(10, 7, 0, 0);
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "Category", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 90 });
            _grid.CellFormatting += HighlightLowStock;

            Controls.Add(_grid);
            Controls.Add(_summary);
            Controls.Add(toolbar);
            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var books = _services.Books.Search(_search.Text, false);
            if (_lowOnly.Checked)
                books = books.Where(book => book.StockQuantity <= threshold).ToList();

            _grid.DataSource = books;
            _summary.Text = "显示 " + books.Count + " 个启用品种 · 低库存阈值 ≤ " + threshold + " 册";
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
                Text = "库存调整 - " + book.Title;
                StartPosition = FormStartPosition.CenterParent;
                Width = 480;
                Height = 270;
                BackColor = UiTheme.Background;

                _delta.Minimum = -1000000;
                _delta.Maximum = 1000000;
                _delta.Value = 0;

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(22),
                    BackColor = UiTheme.Surface
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                table.Controls.Add(new Label { Text = "当前库存", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
                table.Controls.Add(new Label { Text = book.StockQuantity.ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
                table.Controls.Add(new Label { Text = "调整数量 *", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
                _delta.Dock = DockStyle.Fill;
                _delta.Margin = new Padding(6);
                table.Controls.Add(_delta, 1, 1);
                table.Controls.Add(new Label { Text = "原因", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
                _note.Dock = DockStyle.Fill;
                _note.Margin = new Padding(6);
                table.Controls.Add(_note, 1, 2);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = UiTheme.Surface };
                var ok = new Button { Text = "确认", Width = 84, Height = 32, Tag = "primary" };
                var cancel = new Button { Text = "取消", Width = 84, Height = 32, DialogResult = DialogResult.Cancel };
                ok.Click += delegate
                {
                    if (Delta == 0)
                    {
                        MessageBox.Show(this, "调整数量不能为 0。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    DialogResult = DialogResult.OK;
                    Close();
                };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                table.Controls.Add(buttons, 1, 3);

                Controls.Add(table);
                AcceptButton = ok;
                CancelButton = cancel;
                UiTheme.Apply(this);
            }
        }
    }
}
