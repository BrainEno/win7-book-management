using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class SalesForm : Form, INavigationGuard
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _isbn = new TextBox();
        private readonly TextBox _note = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly BindingList<SalesCartRow> _rows = new BindingList<SalesCartRow>();
        private readonly Label _itemCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _stockColumn;

        public SalesForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "店内编码",
                DataPropertyName = "SelfCode",
                Width = 102,
                ReadOnly = true
            };
            _isbnColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "ISBN",
                DataPropertyName = "Isbn",
                Width = 132,
                ReadOnly = true
            };
            _authorColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "作者",
                DataPropertyName = "Author",
                Width = 110,
                ReadOnly = true
            };
            _stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "库存",
                DataPropertyName = "Stock",
                Width = 66,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateActionToolbar(), 0, 0);
            root.Controls.Add(CreateInputSection(), 0, 1);
            root.Controls.Add(CreateCartSection(), 0, 2);
            root.Controls.Add(CreateSettlementSection(), 0, 3);
            root.Controls.Add(CreateTotalsSection(), 0, 4);

            Controls.Add(root);

            _rows.ListChanged += delegate { UpdateTotals(); };
            Resize += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                _isbn.Focus();
                UpdateTotals();
                ApplyResponsiveColumns();
            };

            UiTheme.Apply(this);
        }

        public bool CanNavigateAway(IWin32Window owner)
        {
            if (_rows.Count == 0) return true;

            return MessageBox.Show(
                owner,
                "当前销售单还有 " + _rows.Count + " 项未结账。离开页面会丢弃这些内容，确定离开吗？",
                "未完成的销售单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private Control CreateActionToolbar()
        {
            var toolbar = UiTheme.CreateResponsiveToolbar();
            toolbar.BackColor = UiTheme.Surface;
            toolbar.BorderStyle = BorderStyle.None;

            var newOrder = new Button { Text = "＋ 新单", Width = 88, Height = UiTheme.ButtonHeight };
            var pick = new Button { Text = "选择图书", Width = 100, Height = UiTheme.ButtonHeight };
            var remove = new Button { Text = "移除选中", Width = 100, Height = UiTheme.ButtonHeight };
            var clear = new Button { Text = "清空当前单", Width = 108, Height = UiTheme.ButtonHeight };

            newOrder.Click += delegate { StartNewOrder(); };
            pick.Click += delegate { PickBook(); };
            remove.Click += delegate { RemoveSelected(); };
            clear.Click += delegate { ClearCartWithConfirmation(); };

            toolbar.Controls.Add(newOrder);
            toolbar.Controls.Add(pick);
            toolbar.Controls.Add(remove);
            toolbar.Controls.Add(clear);

            var hint = new Label
            {
                AutoSize = true,
                Text = "扫码枪直接扫 ISBN 后回车即可加入；数量和零售价可在表格中修改。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 11, 0, 0)
            };
            toolbar.Controls.Add(hint);

            return toolbar;
        }

        private Control CreateInputSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 14, 16, 14),
                Margin = new Padding(0, 10, 0, 10),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "商品输入区",
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 10)
            };
            section.Controls.Add(title, 0, 0);
            section.SetColumnSpan(title, 4);

            var isbnLabel = new Label
            {
                Text = "ISBN / 条码",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 3, 8, 3)
            };
            section.Controls.Add(isbnLabel, 0, 1);

            _isbn.Dock = DockStyle.Fill;
            _isbn.Margin = new Padding(0, 3, 10, 3);
            _isbn.Font = UiTheme.Font(10F);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            section.Controls.Add(_isbn, 1, 1);

            var add = new Button
            {
                Text = "搜索加入",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 8, 3),
                Tag = "primary"
            };
            add.Click += delegate { AddByIsbn(); };
            section.Controls.Add(add, 2, 1);

            var pick = new Button
            {
                Text = "选择图书",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            pick.Click += delegate { PickBook(); };
            section.Controls.Add(pick, 3, 1);

            var inputHint = new Label
            {
                Text = "扫码枪可直接扫描后回车；也可以输入 ISBN 后搜索，或打开图书选择器。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 5, 0, 0)
            };
            section.Controls.Add(inputHint, 1, 2);
            section.SetColumnSpan(inputHint, 3);

            return section;
        }

        private Control CreateCartSection()
        {
            ConfigureGrid();

            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 44),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 8, 14, 8),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "销售明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "数量和零售价可直接在表格中修改",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(8F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(12, 2, 0, 0)
            }, 1, 0);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "购物车为空\r\n请扫描 ISBN、搜索加入，或从图书资料中选择";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.Font = UiTheme.Font(9F);
            _emptyState.BackColor = UiTheme.Surface;

            content.Controls.Add(_grid);
            content.Controls.Add(_emptyState);
            _emptyState.BringToFront();

            host.Controls.Add(header, 0, 0);
            host.Controls.Add(content, 0, 1);
            return host;
        }

        private Control CreateSettlementSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 11, 14, 11),
                Margin = new Padding(0, 10, 0, 0),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "结算信息",
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 7)
            };
            section.Controls.Add(title, 0, 0);
            section.SetColumnSpan(title, 3);

            var noteLabel = new Label
            {
                Text = "备注",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };
            section.Controls.Add(noteLabel, 0, 1);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 3, 10, 3);
            section.Controls.Add(_note, 1, 1);

            var remove = new Button
            {
                Text = "移除选中",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            remove.Click += delegate { RemoveSelected(); };
            section.Controls.Add(remove, 2, 1);

            return section;
        }

        private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 74),
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 11, 16, 11),
                Margin = new Padding(0, 8, 0, 0),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));

            _itemCount.Dock = DockStyle.Fill;
            _itemCount.TextAlign = ContentAlignment.MiddleLeft;
            _itemCount.ForeColor = UiTheme.TextSecondary;
            _itemCount.Font = UiTheme.Font(8.5F, FontStyle.Bold);

            _quantityTotal.Dock = DockStyle.Fill;
            _quantityTotal.TextAlign = ContentAlignment.MiddleLeft;
            _quantityTotal.ForeColor = UiTheme.TextSecondary;
            _quantityTotal.Font = UiTheme.Font(8.5F, FontStyle.Bold);

            _total.Dock = DockStyle.Fill;
            _total.TextAlign = ContentAlignment.MiddleLeft;
            _total.ForeColor = UiTheme.Accent;
            _total.Font = UiTheme.Font(14F, FontStyle.Bold);

            var submit = new Button
            {
                Text = "确认结账",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                Tag = "primary"
            };
            submit.Click += delegate { Submit(); };

            section.Controls.Add(_itemCount, 0, 0);
            section.Controls.Add(_quantityTotal, 1, 0);
            section.Controls.Add(_total, 2, 0);
            section.Controls.Add(submit, 3, 0);

            return section;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.DataSource = _rows;

            _grid.Columns.Add(_selfCodeColumn);
            _grid.Columns.Add(_isbnColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 190,
                FillWeight = 220,
                ReadOnly = true
            });
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_stockColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "数量",
                DataPropertyName = "Quantity",
                Width = 72,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "零售价",
                DataPropertyName = "UnitPriceYuan",
                Width = 92,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "小计",
                DataPropertyName = "LineTotalYuan",
                Width = 100,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            _grid.CellEndEdit += delegate
            {
                _grid.Refresh();
                UpdateTotals();
            };
            _grid.CellValueChanged += delegate
            {
                _grid.Refresh();
                UpdateTotals();
            };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
                MessageBox.Show(this, "数量请输入整数，价格请输入有效金额。", "输入格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _selfCodeColumn.Visible = width >= 980;
            _authorColumn.Visible = width >= 900;
            _isbnColumn.Visible = width >= 760;
            _stockColumn.Visible = width >= 700;
        }

        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描或输入 ISBN。", "还没有商品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            var book = _services.Books.FindByExactIsbn(text);
            if (book == null)
            {
                MessageBox.Show(this, "没有找到该 ISBN 的启用图书。请检查 ISBN，或先在“图书资料”中建立资料。", "未找到图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.SelectAll();
                _isbn.Focus();
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
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedBook != null)
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
                    UpdateTotals();
                    return;
                }
            }

            _rows.Add(new SalesCartRow
            {
                BookId = book.Id,
                SelfCode = book.SelfCode,
                Isbn = book.Isbn,
                Title = book.Title,
                Author = book.Author,
                Stock = book.StockQuantity,
                Quantity = 1,
                UnitPriceYuan = Money.ToYuan(book.SalePriceCent)
            });
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show(this, "请先在购物车中选择一行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = _grid.CurrentRow.DataBoundItem as SalesCartRow;
            if (row != null)
                _rows.Remove(row);
        }

        private void StartNewOrder()
        {
            if (_rows.Count > 0)
            {
                var result = MessageBox.Show(
                    this,
                    "当前单据还有商品。新建空白销售单会清空这些内容，是否继续？",
                    "新建销售单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }

            ResetOrder();
        }

        private void ClearCartWithConfirmation()
        {
            if (_rows.Count == 0)
                return;

            var result = MessageBox.Show(
                this,
                "确定清空当前销售单中的全部商品吗？",
                "清空当前单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
                ResetOrder();
        }

        private void ResetOrder()
        {
            _rows.Clear();
            _note.Clear();
            _isbn.Clear();
            _isbn.Focus();
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            decimal total = 0m;
            var quantity = 0;
            foreach (var row in _rows)
            {
                total += row.Quantity * row.UnitPriceYuan;
                quantity += row.Quantity;
            }

            _itemCount.Text = "商品项  " + _rows.Count;
            _quantityTotal.Text = "合计数量  " + quantity;
            _total.Text = "应收金额  ¥" + total.ToString("0.00");

            _emptyState.Visible = _rows.Count == 0;
            if (_rows.Count == 0)
                _emptyState.BringToFront();
            else
                _grid.BringToFront();
        }

        private void Submit()
        {
            if (_rows.Count == 0)
            {
                MessageBox.Show(this, "当前销售单还没有商品。请先扫码或搜索添加图书。", "无法结账", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            try
            {
                _grid.EndEdit();
                var lines = new List<TransactionLineInput>();
                foreach (var row in _rows)
                {
                    if (row.Quantity <= 0)
                        throw new InvalidOperationException("《" + row.Title + "》的数量必须大于 0。");
                    if (row.Quantity > row.Stock)
                        throw new InvalidOperationException("《" + row.Title + "》库存只有 " + row.Stock + " 册，请调整销售数量。");
                    if (row.UnitPriceYuan < 0)
                        throw new InvalidOperationException("《" + row.Title + "》的售价不能为负数。");

                    lines.Add(new TransactionLineInput
                    {
                        BookId = row.BookId,
                        Quantity = row.Quantity,
                        UnitPriceCent = Money.FromYuan(row.UnitPriceYuan)
                    });
                }

                var orderNo = _services.Sales.Checkout(lines, _note.Text);
                MessageBox.Show(
                    this,
                    "销售完成。\r\n单号：" + orderNo,
                    "结账成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ResetOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "结账失败：\r\n" + ex.Message, "请检查销售单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class SalesCartRow
        {
            public long BookId { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public int Stock { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPriceYuan { get; set; }
            public decimal LineTotalYuan { get { return Quantity * UnitPriceYuan; } }
        }
    }
}
