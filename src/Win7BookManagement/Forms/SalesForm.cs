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
        private readonly AntdUI.Input _isbn = UiTheme.CreateAntdInput("扫码或输入店内编码 / ISBN / 书名 / 作者");
        private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("可选：填写销售备注");
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
        private readonly BindingList<SalesCartRow> _rows = new BindingList<SalesCartRow>();
        private readonly Label _itemCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _stockColumn;
        private readonly AntdUI.Column _titleColumn;
        private readonly AntdUI.Column _quantityColumn;
        private readonly AntdUI.Column _priceColumn;
        private readonly AntdUI.Column _lineTotalColumn;

        private SalesCartRow _selectedRow;

        public SalesForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "120", MinWidth = "96", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "150", MinWidth = "116", ReadOnly = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "116", MinWidth = "76", ReadOnly = true };
            _stockColumn = new AntdUI.Column("Stock", "库存") { Width = "78", MinWidth = "68", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "280", MinWidth = "220", MaxWidth = "520", Ellipsis = true, ReadOnly = true };
            _quantityColumn = new AntdUI.Column("Quantity", "数量")
            {
                Width = "96",
                MinWidth = "82",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _priceColumn = new AntdUI.Column("UnitPriceYuan", "销售价格")
            {
                Width = "112",
                MinWidth = "100",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计") { Width = "112", MinWidth = "92", ReadOnly = true, DisplayFormat = "0.00" };

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

            _rows.ListChanged += delegate
            {
                _grid.DataSource = _rows;
                UpdateTotals();
            };
            Resize += delegate { ApplyResponsiveColumns(); };
            _grid.SizeChanged += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                _grid.DataSource = _rows;
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

            var newOrder = UiTheme.CreateAntdButton("＋ 新单", false);
            newOrder.Width = 96;
            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 106;
            var remove = UiTheme.CreateAntdButton("移除选中", false);
            remove.Width = 106;
            var clear = UiTheme.CreateAntdButton("清空当前单", false);
            clear.Width = 116;

            newOrder.Click += delegate { StartNewOrder(); };
            pick.Click += delegate { PickBook(); };
            remove.Click += delegate { RemoveSelected(); };
            clear.Click += delegate { ClearCartWithConfirmation(); };

            toolbar.Controls.Add(newOrder);
            toolbar.Controls.Add(pick);
            toolbar.Controls.Add(remove);
            toolbar.Controls.Add(clear);
            toolbar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "扫描后回车，或按店内编码 / ISBN / 书名 / 作者搜索。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 12, 0, 0)
            });
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
                Padding = new Padding(18, 15, 18, 15),
                Margin = new Padding(0, 10, 0, 10)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "商品输入区",
                AutoSize = true,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 10)
            };
            section.Controls.Add(title, 0, 0);
            section.SetColumnSpan(title, 4);

            section.Controls.Add(new Label
            {
                Text = "添加图书",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 3, 12, 3)
            }, 0, 1);

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

            var add = UiTheme.CreateAntdButton("搜索加入", true);
            add.Width = 114;
            add.Margin = new Padding(0, 3, 8, 3);
            add.Click += delegate { AddByIsbn(); };
            section.Controls.Add(add, 2, 1);

            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 110;
            pick.Margin = new Padding(0, 3, 0, 3);
            pick.Click += delegate { PickBook(); };
            section.Controls.Add(pick, 3, 1);

            section.Controls.Add(new Label
            {
                Text = "扫码枪可直接扫描后回车；没有 ISBN 的自出版物可使用系统店内编码、书名或作者。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 5, 0, 0)
            }, 1, 2);
            section.SetColumnSpan(section.GetControlFromPosition(1, 2), 3);
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
                Margin = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 50),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 8, 14, 8)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "销售明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "点击浅绿色单元格修改数量或销售价格",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(8F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(12, 2, 0, 0)
            }, 1, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "购物车为空\r\n请扫描、搜索加入，或点击“选择图书”";
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

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.BackColor = UiTheme.Surface;
            _grid.ForeColor = UiTheme.TextPrimary;
            _grid.ColumnBack = UiTheme.NavigationSurface;
            _grid.ColumnFore = UiTheme.TextSecondary;
            _grid.ColumnFont = UiTheme.Font(8.8F, FontStyle.Bold);
            _grid.BorderColor = UiTheme.Border;
            _grid.Radius = 8;
            _grid.RowHeight = 48;
            _grid.RowHeightHeader = 48;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.EditMode = AntdUI.TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前销售单还没有商品";
            _grid.RowHoverBg = Color.FromArgb(248, 246, 241);
            _grid.RowSelectedBg = UiTheme.AccentSoft;
            _grid.RowSelectedFore = UiTheme.TextPrimary;

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _titleColumn,
                _quantityColumn,
                _priceColumn,
                _lineTotalColumn,
                _stockColumn,
                _authorColumn,
                _isbnColumn,
                _selfCodeColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "sales-lines");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedRow = e.Record as SalesCartRow;
            };
            _grid.CellEndEdit += HandleCellEndEdit;
        }

        private bool HandleCellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            var row = e.Record as SalesCartRow;
            if (row == null || e.Column == null) return false;

            if (string.Equals(e.Column.Key, "Quantity", StringComparison.Ordinal))
            {
                int quantity;
                if (!int.TryParse(e.Value, out quantity) || quantity <= 0)
                {
                    MessageBox.Show(this, "数量必须是大于 0 的整数。", "数量格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                if (quantity > row.Stock)
                {
                    MessageBox.Show(this, "库存只有 " + row.Stock + " 册，请降低销售数量。", "库存不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                row.Quantity = quantity;
            }
            else if (string.Equals(e.Column.Key, "UnitPriceYuan", StringComparison.Ordinal))
            {
                decimal price;
                if (!decimal.TryParse(e.Value, out price) || price < 0)
                {
                    MessageBox.Show(this, "销售价格必须是有效的非负金额。", "金额格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.UnitPriceYuan = price;
            }

            _grid.Refresh();
            UpdateTotals();
            return true;
        }

        private Control CreateSettlementSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 11, 16, 11),
                Margin = new Padding(0, 10, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 2, 12, 2);
            section.Controls.Add(_note, 1, 0);

            var remove = UiTheme.CreateAntdButton("移除选中", false);
            remove.Width = 108;
            remove.Click += delegate { RemoveSelected(); };
            section.Controls.Add(remove, 2, 0);
            return section;
        }

        private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 78),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 13, 16, 13),
                Margin = new Padding(0, 8, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var metrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty
            };

            ConfigureSummaryLabel(_itemCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_total, true);
            metrics.Controls.Add(_itemCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_total);

            var submit = UiTheme.CreateAntdButton("确认结账", true);
            submit.Width = 132;
            submit.Margin = new Padding(14, 0, 0, 0);
            submit.Click += delegate { Submit(); };

            section.Controls.Add(metrics, 0, 0);
            section.Controls.Add(submit, 1, 0);
            return section;
        }

        private static void ConfigureSummaryLabel(Label label, bool primary)
        {
            label.AutoSize = true;
            label.ForeColor = primary ? UiTheme.Accent : UiTheme.TextSecondary;
            label.Font = UiTheme.Font(primary ? 13.5F : 8.5F, FontStyle.Bold);
            label.BackColor = primary ? UiTheme.AccentSoft : UiTheme.Surface;
            label.Padding = primary ? new Padding(12, 8, 12, 8) : new Padding(10, 8, 10, 8);
            label.Margin = new Padding(0, 0, 10, 0);
        }

        private void ApplyResponsiveColumns()
        {
            var width = _grid.ClientSize.Width > 0 ? _grid.ClientSize.Width : ClientSize.Width;
            // Keep the checkout table readable without horizontal scrolling.
            // Low-priority reference fields progressively return as width grows.
            _selfCodeColumn.Visible = width >= 1180;
            _authorColumn.Visible = width >= 980;
            _isbnColumn.Visible = width >= 760;
            _stockColumn.Visible = width >= 680;
            _grid.LoadLayout();
        }

        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描 ISBN，或输入店内编码 / ISBN / 书名关键词。", "还没有商品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            var exactBook = _services.Books.FindByExactIsbn(text);
            if (exactBook != null)
            {
                AddBook(exactBook);
                _isbn.Text = "";
                _isbn.Focus();
                return;
            }

            var matches = _services.Books.SearchActiveByIsbnOrTitle(text);
            if (matches.Count == 0)
            {
                MessageBox.Show(this, "没有找到匹配的启用图书。可以输入店内编码、ISBN、书名或作者。", "未找到图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.SelectAll();
                _isbn.Focus();
                return;
            }

            if (matches.Count == 1)
            {
                AddBook(matches[0]);
                _isbn.Text = "";
                _isbn.Focus();
                return;
            }

            using (var dialog = new BookLookupDialog(_services, text, true))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedBook != null)
                {
                    AddBook(dialog.SelectedBook);
                    _isbn.Text = "";
                }
            }
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
                    if (row.Quantity < row.Stock) row.Quantity += 1;
                    _selectedRow = row;
                    _grid.Refresh();
                    UpdateTotals();
                    return;
                }
            }

            var added = new SalesCartRow
            {
                BookId = book.Id,
                SelfCode = book.SelfCode,
                Isbn = book.Isbn,
                Title = book.Title,
                Author = book.Author,
                Stock = book.StockQuantity,
                Quantity = 1,
                UnitPriceYuan = Money.ToYuan(book.SalePriceCent)
            };
            _rows.Add(added);
            _selectedRow = added;
            _grid.SetSelected(added, false);
        }

        private void RemoveSelected()
        {
            if (_selectedRow == null)
            {
                MessageBox.Show(this, "请先在购物车中选择一行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _rows.Remove(_selectedRow);
            _selectedRow = null;
        }

        private void StartNewOrder()
        {
            if (_rows.Count > 0)
            {
                var result = MessageBox.Show(this, "当前单据还有商品。新建空白销售单会清空这些内容，是否继续？", "新建销售单", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            ResetOrder();
        }

        private void ClearCartWithConfirmation()
        {
            if (_rows.Count == 0) return;
            if (MessageBox.Show(this, "确定清空当前销售单中的全部商品吗？", "清空当前单", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                ResetOrder();
        }

        private void ResetOrder()
        {
            _rows.Clear();
            _selectedRow = null;
            _note.Text = "";
            _isbn.Text = "";
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
            if (_rows.Count == 0) _emptyState.BringToFront();
            else _grid.BringToFront();
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
                var lines = new List<TransactionLineInput>();
                foreach (var row in _rows)
                {
                    if (row.Quantity <= 0) throw new InvalidOperationException("《" + row.Title + "》的数量必须大于 0。");
                    if (row.Quantity > row.Stock) throw new InvalidOperationException("《" + row.Title + "》库存只有 " + row.Stock + " 册，请调整销售数量。");
                    if (row.UnitPriceYuan < 0) throw new InvalidOperationException("《" + row.Title + "》的售价不能为负数。");

                    lines.Add(new TransactionLineInput
                    {
                        BookId = row.BookId,
                        Quantity = row.Quantity,
                        UnitPriceCent = Money.FromYuan(row.UnitPriceYuan)
                    });
                }

                var orderNo = _services.Sales.Checkout(lines, _note.Text);
                MessageBox.Show(this, "销售完成。\r\n单号：" + orderNo, "结账成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
