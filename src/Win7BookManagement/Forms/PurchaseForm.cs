using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class PurchaseForm : Form, INavigationGuard
    {
        private readonly ApplicationServices _services;
        private readonly ComboBox _supplier = new ComboBox();
        private readonly TextBox _isbn = new TextBox();
        private readonly TextBox _note = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly BindingList<PurchaseCartRow> _rows = new BindingList<PurchaseCartRow>();
        private readonly Label _total = new Label();

        public PurchaseForm(ApplicationServices services)
        {
            _services = services;
            BackColor = UiTheme.Background;

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 112,
                ColumnCount = 6,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 8, 12, 8)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));

            _supplier.DropDownStyle = ComboBoxStyle.DropDownList;
            AddCell(top, 0, 0, "供应商", _supplier);
            AddCell(top, 2, 0, "ISBN", _isbn);

            var addByIsbn = new Button { Text = "加入", Dock = DockStyle.Fill, Margin = new Padding(6) };
            addByIsbn.Click += delegate { AddByIsbn(); };
            top.Controls.Add(addByIsbn, 4, 0);

            var pick = new Button { Text = "选择图书", Dock = DockStyle.Fill, Margin = new Padding(6) };
            pick.Click += delegate { PickBook(); };
            top.Controls.Add(pick, 5, 0);

            AddCell(top, 0, 1, "备注", _note);
            top.SetColumnSpan(_note, 5);

            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };

            ConfigureGrid();

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 12, 0, 10),
                BackColor = UiTheme.Surface
            };

            var submit = new Button { Text = "确认入库", Width = 104, Height = 34, Tag = "primary" };
            var remove = new Button { Text = "移除选中", Width = 94, Height = 34 };
            _total.AutoSize = true;
            _total.Margin = new Padding(15, 8, 20, 0);
            _total.Font = UiTheme.Font(12F, FontStyle.Bold);
            _total.ForeColor = UiTheme.TextPrimary;
            submit.Click += delegate { Submit(); };
            remove.Click += delegate { RemoveSelected(); };
            bottom.Controls.Add(submit);
            bottom.Controls.Add(remove);
            bottom.Controls.Add(_total);

            var hint = new Label
            {
                Text = "选择供应商后可扫码或搜索图书；数量和本次进价可直接在表格中修改。若图书资料维护了默认进价，会自动预填。",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(12, 7, 0, 0),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(8F)
            };

            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(hint);
            Controls.Add(top);

            _rows.ListChanged += delegate { UpdateTotal(); };
            Shown += delegate
            {
                ReloadSuppliers();
                UpdateTotal();
            };
        }

        public bool CanNavigateAway(IWin32Window owner)
        {
            if (_rows.Count == 0) return true;

            return MessageBox.Show(
                owner,
                "当前采购入库单还有 " + _rows.Count + " 项未提交。离开页面会丢弃这些内容，确定离开吗？",
                "未完成的采购单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private static void AddCell(TableLayoutPanel table, int column, int row, string label, Control control)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, column, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(6);
            table.Controls.Add(control, column + 1, row);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.DataSource = _rows;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISBN", DataPropertyName = "Isbn", Width = 140, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "书名", DataPropertyName = "Title", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "数量", DataPropertyName = "Quantity", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "进价（元）",
                DataPropertyName = "UnitCostYuan",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
            });
            _grid.CellValueChanged += delegate { UpdateTotal(); };
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private void ReloadSuppliers()
        {
            _supplier.DataSource = _services.Suppliers.GetAll(false);
            _supplier.DisplayMember = "Name";
            _supplier.ValueMember = "Id";
        }

        private void AddByIsbn()
        {
            var book = _services.Books.FindByExactIsbn(_isbn.Text);
            if (book == null)
            {
                MessageBox.Show(this, "没有找到该 ISBN 的启用图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            AddBook(book);
            _isbn.Clear();
            _isbn.Focus();
        }

        private void PickBook()
        {
            using (var dialog = new BookLookupDialog(_services))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    AddBook(dialog.SelectedBook);
            }
        }

        private void AddBook(Book book)
        {
            foreach (var row in _rows)
            {
                if (row.BookId == book.Id)
                {
                    row.Quantity += 1;
                    _grid.Refresh();
                    UpdateTotal();
                    return;
                }
            }

            _rows.Add(new PurchaseCartRow
            {
                BookId = book.Id,
                Isbn = book.Isbn,
                Title = book.Title,
                Quantity = 1,
                UnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent)
            });
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow == null) return;
            var row = _grid.CurrentRow.DataBoundItem as PurchaseCartRow;
            if (row != null) _rows.Remove(row);
        }

        private void UpdateTotal()
        {
            decimal total = 0m;
            foreach (var row in _rows)
                total += row.Quantity * row.UnitCostYuan;
            _total.Text = "合计 ¥" + total.ToString("0.00");
        }

        private void Submit()
        {
            try
            {
                _grid.EndEdit();
                var lines = new List<TransactionLineInput>();
                foreach (var row in _rows)
                {
                    if (row.Quantity <= 0) throw new InvalidOperationException("数量必须大于 0。");
                    if (row.UnitCostYuan < 0) throw new InvalidOperationException("进价不能为负数。");
                    lines.Add(new TransactionLineInput
                    {
                        BookId = row.BookId,
                        Quantity = row.Quantity,
                        UnitPriceCent = Money.FromYuan(row.UnitCostYuan)
                    });
                }

                var selected = _supplier.SelectedItem as Supplier;
                var orderNo = _services.Purchases.Receive(selected == null ? (long?)null : selected.Id, lines, _note.Text);
                MessageBox.Show(this, "入库成功。单号：" + orderNo, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _rows.Clear();
                _note.Clear();
                UpdateTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "入库失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class PurchaseCartRow
        {
            public long BookId { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public int Quantity { get; set; }
            public decimal UnitCostYuan { get; set; }
        }
    }
}
