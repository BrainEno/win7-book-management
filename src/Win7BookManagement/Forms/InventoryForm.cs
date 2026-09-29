using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class InventoryForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _search = new TextBox();
        private readonly DataGridView _grid = new DataGridView();

        public InventoryForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _search.Width = 280;
            _search.Margin = new Padding(0, 8, 8, 8);
            var searchButton = new Button { Text = "查询", Width = 72, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            var adjustButton = new Button { Text = "库存调整", Width = 90, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            searchButton.Click += delegate { Reload(); };
            adjustButton.Click += delegate { AdjustSelected(); };
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

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "作者", DataPropertyName = "Author", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "库存", DataPropertyName = "StockQuantity", Width = 90 });

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            _grid.DataSource = _services.Books.Search(_search.Text, true);
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
                Width = 460;
                Height = 250;
                Font = new Font("Microsoft YaHei", 9F);

                _delta.Minimum = -1000000;
                _delta.Maximum = 1000000;
                _delta.Value = 0;

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(18)
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

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var ok = new Button { Text = "确认", Width = 80, Height = 30 };
                var cancel = new Button { Text = "取消", Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
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
            }
        }
    }
}
