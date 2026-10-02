using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class SalesForm : Form, INavigationGuard
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _isbn = UiTheme.CreateAntdInput("扫码或输入店内编码 / ISBN / 书名 / 作者");
        private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("可选：填写销售备注");
        private readonly AntdUI.InputNumber _orderDiscount = new AntdUI.InputNumber();
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
        private readonly BindingList<SalesCartRow> _rows = new BindingList<SalesCartRow>();
        private readonly Label _itemCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _originalTotal = new Label();
        private readonly Label _discountTotal = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();
        private readonly ToolTip _shortcutTips = new ToolTip();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _stockColumn;
        private readonly AntdUI.Column _titleColumn;
        private readonly AntdUI.Column _quantityColumn;
        private readonly AntdUI.Column _priceColumn;
        private readonly AntdUI.Column _discountColumn;
        private readonly AntdUI.Column _discountedUnitColumn;
        private readonly AntdUI.Column _lineTotalColumn;

        private SalesCartRow _selectedRow;
        private long? _currentDraftId;

        public SalesForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            KeyPreview = true;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112", MinWidth = "90", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "142", MinWidth = "112", ReadOnly = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "112", MinWidth = "76", ReadOnly = true };
            _stockColumn = new AntdUI.Column("Stock", "库存") { Width = "70", MinWidth = "62", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "180", MaxWidth = "360", Ellipsis = true, ReadOnly = true };
            _quantityColumn = new AntdUI.Column("Quantity", "数量")
            {
                Width = "82",
                MinWidth = "72",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _priceColumn = new AntdUI.Column("UnitPriceYuan", "销售价")
            {
                Width = "98",
                MinWidth = "88",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _discountColumn = new AntdUI.Column("DiscountPercent", "单品折扣%")
            {
                Width = "98",
                MinWidth = "88",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _discountedUnitColumn = new AntdUI.Column("DiscountedUnitPriceYuan", "折后单价")
            {
                Width = "96",
                MinWidth = "86",
                ReadOnly = true,
                DisplayFormat = "0.00"
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计")
            {
                Width = "104",
                MinWidth = "92",
                ReadOnly = true,
                DisplayFormat = "0.00"
            };

            ConfigureOrderDiscount();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateInputSection(), 0, 0);
            root.Controls.Add(CreateCartSection(), 0, 1);
            root.Controls.Add(CreateSettlementSection(), 0, 2);
            root.Controls.Add(CreateTotalsSection(), 0, 3);
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

        private void ConfigureOrderDiscount()
        {
            _orderDiscount.Minimum = 0m;
            _orderDiscount.Maximum = 100m;
            _orderDiscount.DecimalPlaces = 2;
            _orderDiscount.Value = 100m;
            _orderDiscount.Width = 126;
            _orderDiscount.MinimumSize = new Size(126, UiTheme.InputHeight);
            _orderDiscount.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e)
            {
                foreach (var row in _rows)
                    row.OrderDiscountPercent = _orderDiscount.Value;
                _grid.Refresh();
                UpdateTotals();
            };
        }

        private Control CreateInputSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 18),
                ColumnCount = 8,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 9, 14, 9),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "扫码 / 搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _isbn.Dock = DockStyle.None;
            _isbn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _isbn.Tag = "toolbar-input";
            _isbn.Margin = new Padding(0, 0, 8, 0);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            section.Controls.Add(_isbn, 1, 0);

            var add = UiTheme.CreateAntdButton("加入", true);
            add.Width = 78;
            add.Anchor = AnchorStyles.Left;
            add.Tag = "toolbar-action";
            add.Margin = new Padding(0, 0, 6, 0);
            add.Click += delegate { AddByIsbn(); };
            section.Controls.Add(add, 2, 0);

            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 92;
            pick.Anchor = AnchorStyles.Left;
            pick.Tag = "toolbar-action";
            pick.Margin = new Padding(0, 0, 6, 0);
            pick.Click += delegate { PickBook(); };
            section.Controls.Add(pick, 3, 0);

            var newOrder = UiTheme.CreateAntdButton("新单", false);
            newOrder.Width = 68;
            newOrder.Anchor = AnchorStyles.Left;
            newOrder.Tag = "toolbar-action";
            newOrder.Margin = new Padding(0, 0, 6, 0);
            newOrder.Click += delegate { StartNewOrder(); };
            _shortcutTips.SetToolTip(newOrder, "Ctrl+N 新建空白销售单");
            section.Controls.Add(newOrder, 4, 0);

            var hold = UiTheme.CreateAntdButton("挂单", false);
            hold.Width = 70;
            hold.Anchor = AnchorStyles.Left;
            hold.Tag = "toolbar-action";
            hold.Margin = new Padding(0, 0, 6, 0);
            hold.Click += delegate { HoldCurrentOrder(); };
            _shortcutTips.SetToolTip(hold, "F5 挂单并持久保存");
            section.Controls.Add(hold, 5, 0);

            var recall = UiTheme.CreateAntdButton("取单", false);
            recall.Width = 70;
            recall.Anchor = AnchorStyles.Left;
            recall.Tag = "toolbar-action";
            recall.Margin = new Padding(0, 0, 6, 0);
            recall.Click += delegate { RecallDraft(); };
            _shortcutTips.SetToolTip(recall, "F6 从已保存挂单中取回");
            section.Controls.Add(recall, 6, 0);

            var clear = UiTheme.CreateAntdButton("清空", false);
            clear.Width = 68;
            clear.Anchor = AnchorStyles.Left;
            clear.Tag = "toolbar-action";
            clear.Click += delegate { ClearCartWithConfirmation(); };
            section.Controls.Add(clear, 7, 0);

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
                Margin = new Padding(0, 6, 0, 0)
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 5, 12, 5),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
                Text = "数量、销售价和单品折扣可直接编辑；折扣按百分比输入",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(8, 6, 12, 0)
            }, 1, 0);

            var remove = UiTheme.CreateAntdButton("移除选中", false);
            remove.Width = 92;
            remove.Click += delegate { RemoveSelected(); };
            header.Controls.Add(remove, 2, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前销售单为空\r\n请扫码、搜索或选择图书";
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
            _grid.RowHeight = 44;
            _grid.RowHeightHeader = 42;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.EditMode = AntdUI.TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前销售单还没有商品";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _titleColumn,
                _quantityColumn,
                _priceColumn,
                _discountColumn,
                _discountedUnitColumn,
                _lineTotalColumn,
                _stockColumn,
                _authorColumn,
                _isbnColumn,
                _selfCodeColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "sales-lines-v3-discount");

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
                row.UnitPriceYuan = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
            }
            else if (string.Equals(e.Column.Key, "DiscountPercent", StringComparison.Ordinal))
            {
                decimal percent;
                if (!decimal.TryParse(e.Value, out percent) || percent < 0m || percent > 100m)
                {
                    MessageBox.Show(this, "单品折扣必须在 0 到 100 之间，例如 90 表示九折。", "折扣格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.DiscountPercent = decimal.Round(percent, 2, MidpointRounding.AwayFromZero);
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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 14),
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 6, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 0, 18, 0);
            section.Controls.Add(_note, 1, 0);

            section.Controls.Add(new Label
            {
                Text = "整单折扣 %",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            }, 2, 0);

            _orderDiscount.Dock = DockStyle.None;
            _orderDiscount.Anchor = AnchorStyles.Left;
            _orderDiscount.Margin = Padding.Empty;
            section.Controls.Add(_orderDiscount, 3, 0);
            return section;
        }

        private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 62),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(14, 9, 14, 9),
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
            ConfigureSummaryLabel(_originalTotal, false);
            ConfigureSummaryLabel(_discountTotal, false);
            ConfigureSummaryLabel(_total, true);
            metrics.Controls.Add(_itemCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_originalTotal);
            metrics.Controls.Add(_discountTotal);
            metrics.Controls.Add(_total);

            var submit = UiTheme.CreateAntdButton("确认结账", true);
            submit.Width = 112;
            submit.Margin = new Padding(10, 0, 0, 0);
            submit.Click += delegate { Submit(); };
            _shortcutTips.SetToolTip(submit, "Ctrl+Enter 打开收款结算");

            section.Controls.Add(metrics, 0, 0);
            section.Controls.Add(submit, 1, 0);
            return section;
        }

        private static void ConfigureSummaryLabel(Label label, bool primary)
        {
            label.AutoSize = true;
            label.ForeColor = primary ? UiTheme.Accent : UiTheme.TextSecondary;
            label.Font = UiTheme.Font(primary ? 12F : 8.2F, FontStyle.Bold);
            label.BackColor = primary ? UiTheme.AccentSoft : UiTheme.Surface;
            label.Padding = primary ? new Padding(10, 6, 10, 6) : new Padding(8, 6, 8, 6);
            label.Margin = new Padding(0, 0, 8, 0);
        }

        private void ApplyResponsiveColumns()
        {
            var width = _grid.ClientSize.Width > 0 ? _grid.ClientSize.Width : ClientSize.Width;
            _selfCodeColumn.Visible = width >= 1400;
            _authorColumn.Visible = width >= 1150;
            _isbnColumn.Visible = width >= 950;
            _stockColumn.Visible = width >= 760;
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
                UnitPriceYuan = Money.ToYuan(book.SalePriceCent),
                DiscountPercent = 100m,
                OrderDiscountPercent = _orderDiscount.Value
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
            _currentDraftId = null;
            _note.Text = "";
            _isbn.Text = "";
            _orderDiscount.Value = 100m;
            _isbn.Focus();
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            long originalCent = 0;
            long finalCent = 0;
            var quantity = 0;

            foreach (var row in _rows)
            {
                row.OrderDiscountPercent = _orderDiscount.Value;

                var baseUnitCent = Money.FromYuan(row.UnitPriceYuan);
                var lineBasisPoints = PercentToBasisPoints(row.DiscountPercent);
                var orderBasisPoints = PercentToBasisPoints(row.OrderDiscountPercent);
                var lineUnitCent = ApplyBasisPoints(baseUnitCent, lineBasisPoints);
                var finalUnitCent = ApplyBasisPoints(lineUnitCent, orderBasisPoints);

                originalCent = checked(originalCent + checked((long)row.Quantity * baseUnitCent));
                finalCent = checked(finalCent + checked((long)row.Quantity * finalUnitCent));
                quantity += row.Quantity;
            }

            _itemCount.Text = "商品项  " + _rows.Count;
            _quantityTotal.Text = "合计数量  " + quantity;
            _originalTotal.Text = "原金额  ¥" + Money.ToYuan(originalCent).ToString("0.00");
            _discountTotal.Text = "优惠  ¥" + Money.ToYuan(originalCent - finalCent).ToString("0.00");
            _total.Text = "应收金额  ¥" + Money.ToYuan(finalCent).ToString("0.00");

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
                var lines = BuildTransactionLines();
                var orderBasisPoints = PercentToBasisPoints(_orderDiscount.Value);
                var totalCent = CalculateFinalTotalCent(lines, orderBasisPoints);

                using (var settlement = new SalesSettlementDialog(_services, totalCent))
                {
                    if (settlement.ShowDialog(this) != DialogResult.OK)
                        return;

                    var orderNo = _services.Sales.Checkout(
                        lines,
                        _note.Text,
                        orderBasisPoints,
                        settlement.PaymentMethod,
                        settlement.AmountReceivedCent);

                    var draftCleanupWarning = "";
                    if (_currentDraftId.HasValue)
                    {
                        try
                        {
                            _services.Sales.DeleteDraft(_currentDraftId.Value);
                        }
                        catch (Exception cleanupEx)
                        {
                            draftCleanupWarning =
                                "\r\n\r\n提示：销售已经成功，但原挂单未能自动删除：" +
                                cleanupEx.Message;
                        }
                    }

                    var message =
                        "销售完成。\r\n单号：" + orderNo +
                        "\r\n收款方式：" + settlement.PaymentMethod;
                    if (string.Equals(
                        settlement.PaymentMethod,
                        SalesService.CashPaymentMethod,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        message +=
                            "\r\n实收：¥" + Money.ToYuan(settlement.AmountReceivedCent).ToString("0.00") +
                            "\r\n找零：¥" + Money.ToYuan(settlement.ChangeCent).ToString("0.00");
                    }

                    message += draftCleanupWarning;
                    MessageBox.Show(this, message, "结账成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ResetOrder();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "结账失败：\r\n" + ex.Message, "请检查销售单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<TransactionLineInput> BuildTransactionLines()
        {
            var lines = new List<TransactionLineInput>();
            foreach (var row in _rows)
            {
                if (row.Quantity <= 0)
                    throw new InvalidOperationException("《" + row.Title + "》的数量必须大于 0。");
                if (row.Quantity > row.Stock)
                    throw new InvalidOperationException("《" + row.Title + "》库存只有 " + row.Stock + " 册，请调整销售数量。");
                if (row.UnitPriceYuan < 0)
                    throw new InvalidOperationException("《" + row.Title + "》的售价不能为负数。");
                if (row.DiscountPercent < 0m || row.DiscountPercent > 100m)
                    throw new InvalidOperationException("《" + row.Title + "》的单品折扣必须在 0 到 100 之间。");

                var baseCent = Money.FromYuan(row.UnitPriceYuan);
                lines.Add(new TransactionLineInput
                {
                    BookId = row.BookId,
                    Quantity = row.Quantity,
                    UnitPriceCent = baseCent,
                    BaseUnitPriceCent = baseCent,
                    DiscountBasisPoints = PercentToBasisPoints(row.DiscountPercent)
                });
            }
            return lines;
        }

        private static long CalculateFinalTotalCent(
            IList<TransactionLineInput> lines,
            int orderBasisPoints)
        {
            long total = 0;
            foreach (var line in lines)
            {
                var baseCent = line.BaseUnitPriceCent > 0 || line.UnitPriceCent == 0
                    ? line.BaseUnitPriceCent
                    : line.UnitPriceCent;
                var lineCent = ApplyBasisPoints(baseCent, line.DiscountBasisPoints);
                var finalCent = ApplyBasisPoints(lineCent, orderBasisPoints);
                total = checked(total + checked((long)line.Quantity * finalCent));
            }
            return total;
        }

        private void HoldCurrentOrder()
        {
            if (_rows.Count == 0)
            {
                MessageBox.Show(this, "当前销售单没有商品，不需要挂单。", "挂单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            try
            {
                var draft = _services.Sales.SaveDraft(
                    _currentDraftId,
                    BuildTransactionLines(),
                    _note.Text,
                    PercentToBasisPoints(_orderDiscount.Value));

                MessageBox.Show(
                    this,
                    "挂单已保存。\r\n挂单号：" + draft.DraftNo + "\r\n可按 F6 随时取回。",
                    "挂单成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ResetOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "挂单失败：\r\n" + ex.Message, "无法挂单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RecallDraft()
        {
            if (_rows.Count > 0)
            {
                MessageBox.Show(
                    this,
                    "当前销售单还有商品。请先按 F5 挂单，或清空当前单后再取单。",
                    "先处理当前销售单",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var drafts = _services.Sales.GetDraftSummaries();
            if (drafts.Count == 0)
            {
                MessageBox.Show(this, "当前没有已保存的挂单。", "取单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            using (var picker = new SalesDraftPickerDialog(_services, drafts))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || !picker.SelectedDraftId.HasValue)
                    return;

                var draft = _services.Sales.GetDraft(picker.SelectedDraftId.Value);
                if (draft == null)
                {
                    MessageBox.Show(this, "这张挂单已经不存在，请重新打开取单列表。", "取单失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LoadDraft(draft);
            }
        }

        private void LoadDraft(SalesService.SalesDraft draft)
        {
            _rows.Clear();
            _selectedRow = null;
            _currentDraftId = draft.Id;
            _note.Text = draft.Note ?? "";
            _orderDiscount.Value = draft.OrderDiscountBasisPoints / 100m;

            var hasProblem = false;
            foreach (var line in draft.Lines)
            {
                var row = new SalesCartRow
                {
                    BookId = line.BookId,
                    SelfCode = line.SelfCode,
                    Isbn = line.Isbn,
                    Title = line.Title,
                    Author = line.Author,
                    Stock = line.CurrentStock,
                    Quantity = line.Quantity,
                    UnitPriceYuan = Money.ToYuan(line.BaseUnitPriceCent),
                    DiscountPercent = line.DiscountBasisPoints / 100m,
                    OrderDiscountPercent = _orderDiscount.Value
                };
                _rows.Add(row);
                if (!line.IsActive || line.CurrentStock < line.Quantity)
                    hasProblem = true;
            }

            _grid.DataSource = _rows;
            UpdateTotals();
            _isbn.Focus();

            if (hasProblem)
            {
                MessageBox.Show(
                    this,
                    "挂单已取回，但其中有图书已停用或当前库存不足。结账前请检查对应明细。",
                    "请检查挂单",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                _isbn.Focus();
                _isbn.SelectAll();
                return true;
            }
            if (keyData == Keys.F3)
            {
                PickBook();
                return true;
            }
            if (keyData == Keys.F5)
            {
                HoldCurrentOrder();
                return true;
            }
            if (keyData == Keys.F6)
            {
                RecallDraft();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Enter))
            {
                Submit();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N))
            {
                StartNewOrder();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static int PercentToBasisPoints(decimal percent)
        {
            var clamped = Math.Max(0m, Math.Min(100m, percent));
            return decimal.ToInt32(
                decimal.Round(clamped * 100m, 0, MidpointRounding.AwayFromZero));
        }

        private static long ApplyBasisPoints(long amountCent, int basisPoints)
        {
            if (amountCent <= 0 || basisPoints <= 0)
                return 0;
            if (basisPoints >= 10000)
                return amountCent;

            return decimal.ToInt64(
                decimal.Round(
                    amountCent * (basisPoints / 10000m),
                    0,
                    MidpointRounding.AwayFromZero));
        }

        private sealed class SalesSettlementDialog : Form
        {
            private readonly long _totalCent;
            private readonly AntdUI.Select _payment = new AntdUI.Select();
            private readonly AntdUI.InputNumber _received = new AntdUI.InputNumber();
            private readonly Label _receivedLabel = new Label();
            private readonly Label _change = new Label();
            private readonly List<string> _paymentValues = new List<string>();

            public string PaymentMethod { get; private set; }
            public long AmountReceivedCent { get; private set; }
            public long ChangeCent { get; private set; }

            public SalesSettlementDialog(ApplicationServices services, long totalCent)
            {
                _totalCent = totalCent;
                PaymentMethod = SalesService.DefaultPaymentMethod;

                UiTheme.ConfigureForm(this);
                Text = "收款结算";
                StartPosition = FormStartPosition.CenterParent;
                Width = 520;
                Height = 390;
                MinimumSize = new Size(480, 350);
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = UiTheme.Background;

                var values = services.Dictionaries.GetActiveValues(DictionaryKeys.PaymentMethod);
                foreach (var value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        _paymentValues.Add(value.Trim());
                }
                if (_paymentValues.Count == 0)
                {
                    _paymentValues.Add("微信");
                    _paymentValues.Add("支付宝");
                    _paymentValues.Add("现金");
                }
                foreach (var value in _paymentValues)
                    _payment.Items.Add(value);

                var defaultIndex = 0;
                for (var i = 0; i < _paymentValues.Count; i++)
                {
                    if (string.Equals(
                        _paymentValues[i],
                        SalesService.DefaultPaymentMethod,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        defaultIndex = i;
                        break;
                    }
                }
                _payment.SelectedIndex = defaultIndex;
                _payment.DropDownArrow = true;
                _payment.SelectedIndexChanged += delegate { UpdateCashState(); };

                _received.DecimalPlaces = 2;
                _received.Minimum = 0m;
                _received.Maximum = 1000000m;
                _received.ThousandsSeparator = true;
                _received.Value = Money.ToYuan(totalCent);
                _received.ValueChanged += delegate { UpdateChange(); };

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = UiTheme.Background,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 16, 22, 14),
                    Margin = Padding.Empty
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.Controls.Add(new Label
                {
                    Text = "确认收款",
                    AutoSize = true,
                    Font = UiTheme.Font(13F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 5)
                }, 0, 0);
                header.Controls.Add(new Label
                {
                    Text = "应收金额  ¥" + Money.ToYuan(totalCent).ToString("0.00"),
                    AutoSize = true,
                    Font = UiTheme.Font(11F, FontStyle.Bold),
                    ForeColor = UiTheme.Accent,
                    Margin = Padding.Empty
                }, 0, 1);
                root.Controls.Add(header, 0, 0);

                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 3,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 18, 22, 18),
                    Margin = new Padding(0, 8, 0, 0)
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddSettlementField(body, 0, "收款方式", _payment);

                _receivedLabel.Text = "现金实收";
                _receivedLabel.Dock = DockStyle.Fill;
                _receivedLabel.TextAlign = ContentAlignment.MiddleRight;
                _receivedLabel.ForeColor = UiTheme.TextSecondary;
                _receivedLabel.Font = UiTheme.Font(8.5F, FontStyle.Bold);
                _receivedLabel.Margin = new Padding(0, 0, 12, 0);
                body.Controls.Add(_receivedLabel, 0, 1);
                _received.Dock = DockStyle.Fill;
                _received.Margin = new Padding(0, 6, 0, 6);
                _received.MinimumSize = new Size(0, UiTheme.InputHeight);
                body.Controls.Add(_received, 1, 1);

                body.Controls.Add(new Label
                {
                    Text = "找零",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 12, 0)
                }, 0, 2);
                _change.Dock = DockStyle.Fill;
                _change.MinimumSize = new Size(0, UiTheme.InputHeight);
                _change.TextAlign = ContentAlignment.MiddleLeft;
                _change.Padding = new Padding(10, 0, 0, 0);
                _change.Font = UiTheme.Font(10F, FontStyle.Bold);
                _change.BackColor = UiTheme.SurfaceMuted;
                body.Controls.Add(_change, 1, 2);
                root.Controls.Add(body, 0, 1);

                var footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(18, 11, 18, 11),
                    Margin = new Padding(0, 8, 0, 0)
                };
                var confirm = UiTheme.CreateAntdButton("确认收款", true);
                confirm.Width = 112;
                confirm.Click += delegate { Confirm(); };
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 90;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                footer.Controls.Add(confirm);
                footer.Controls.Add(cancel);
                root.Controls.Add(footer, 0, 2);

                Controls.Add(root);
                AcceptButton = confirm;
                CancelButton = cancel;
                UiTheme.Apply(this);
                Shown += delegate
                {
                    UiTheme.FitDialogToWorkingArea(this, 24);
                    UpdateCashState();
                };
            }

            private static void AddSettlementField(
                TableLayoutPanel table,
                int row,
                string label,
                Control control)
            {
                table.Controls.Add(new Label
                {
                    Text = label,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 12, 0)
                }, 0, row);
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 6, 0, 6);
                control.MinimumSize = new Size(0, UiTheme.InputHeight);
                table.Controls.Add(control, 1, row);
            }

            private string SelectedPayment
            {
                get
                {
                    var index = _payment.SelectedIndex;
                    return index >= 0 && index < _paymentValues.Count
                        ? _paymentValues[index]
                        : "";
                }
            }

            private void UpdateCashState()
            {
                var cash = string.Equals(
                    SelectedPayment,
                    SalesService.CashPaymentMethod,
                    StringComparison.OrdinalIgnoreCase);
                _received.Enabled = cash;
                _receivedLabel.ForeColor = cash ? UiTheme.TextPrimary : UiTheme.TextSecondary;
                if (!cash)
                    _received.Value = Money.ToYuan(_totalCent);
                UpdateChange();
            }

            private void UpdateChange()
            {
                var cash = string.Equals(
                    SelectedPayment,
                    SalesService.CashPaymentMethod,
                    StringComparison.OrdinalIgnoreCase);
                if (!cash)
                {
                    _change.Text = "—";
                    _change.ForeColor = UiTheme.TextSecondary;
                    return;
                }

                var receivedCent = Money.FromYuan(_received.Value);
                if (receivedCent >= _totalCent)
                {
                    _change.Text = "¥" + Money.ToYuan(receivedCent - _totalCent).ToString("0.00");
                    _change.ForeColor = UiTheme.Success;
                }
                else
                {
                    _change.Text = "还差 ¥" + Money.ToYuan(_totalCent - receivedCent).ToString("0.00");
                    _change.ForeColor = UiTheme.Warning;
                }
            }

            private void Confirm()
            {
                var payment = SelectedPayment;
                if (string.IsNullOrWhiteSpace(payment))
                {
                    MessageBox.Show(this, "请选择收款方式。", "还差一项", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                PaymentMethod = payment;
                if (string.Equals(
                    payment,
                    SalesService.CashPaymentMethod,
                    StringComparison.OrdinalIgnoreCase))
                {
                    AmountReceivedCent = Money.FromYuan(_received.Value);
                    if (AmountReceivedCent < _totalCent)
                    {
                        MessageBox.Show(
                            this,
                            "现金实收不能小于应收金额。",
                            "现金不足",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        _received.Focus();
                        return;
                    }
                    ChangeCent = AmountReceivedCent - _totalCent;
                }
                else
                {
                    AmountReceivedCent = _totalCent;
                    ChangeCent = 0;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private sealed class SalesDraftPickerDialog : Form
        {
            private readonly ApplicationServices _services;
            private readonly AntdUI.Table _grid = new AntdUI.Table();
            private readonly BindingList<DraftRow> _rows = new BindingList<DraftRow>();
            private DraftRow _selected;

            public long? SelectedDraftId { get; private set; }

            public SalesDraftPickerDialog(
                ApplicationServices services,
                IList<SalesService.SalesDraftSummary> drafts)
            {
                _services = services;

                UiTheme.ConfigureForm(this);
                Text = "取回挂单";
                StartPosition = FormStartPosition.CenterParent;
                Width = 820;
                Height = 520;
                MinimumSize = new Size(680, 420);
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = UiTheme.Background;

                foreach (var draft in drafts)
                    _rows.Add(new DraftRow(draft));

                _grid.Dock = DockStyle.Fill;
                _grid.RowHeight = UiTheme.TableRowHeight;
                _grid.RowHeightHeader = UiTheme.TableHeaderHeight;
                _grid.EnableHeaderResizing = true;
                _grid.ColumnDragSort = false;
                _grid.ShowTip = true;
                _grid.EmptyText = "当前没有已保存的挂单";
                _grid.Columns = new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("UpdatedAtText", "更新时间") { Width = "150", MinWidth = "132" },
                    new AntdUI.Column("DraftNo", "挂单号") { Width = "180", MinWidth = "155" },
                    new AntdUI.Column("ItemCount", "商品项") { Width = "78", MinWidth = "70" },
                    new AntdUI.Column("QuantityTotal", "数量") { Width = "70", MinWidth = "64" },
                    new AntdUI.Column("TotalYuan", "金额") { Width = "92", MinWidth = "84", DisplayFormat = "0.00" },
                    new AntdUI.Column("Note", "备注") { Width = "fill", MinWidth = "130", Ellipsis = true }
                };
                _grid.DataSource = _rows;
                _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
                {
                    _selected = e.Record as DraftRow;
                };
                _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
                {
                    _selected = e.Record as DraftRow;
                    TakeSelected();
                };

                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = UiTheme.Background,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                root.Controls.Add(new Label
                {
                    Text = "选择一张已保存挂单继续结账",
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    BackColor = UiTheme.Surface,
                    ForeColor = UiTheme.TextPrimary,
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    Padding = new Padding(18, 14, 18, 12)
                }, 0, 0);

                var host = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(0, 8, 0, 0)
                };
                host.Controls.Add(_grid);
                root.Controls.Add(host, 0, 1);

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(16, 10, 16, 10),
                    Margin = new Padding(0, 8, 0, 0)
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                var delete = UiTheme.CreateAntdButton("删除挂单", false);
                delete.Width = 96;
                delete.Click += delegate { DeleteSelected(); };
                footer.Controls.Add(delete, 0, 0);

                var actions = new FlowLayoutPanel
                {
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = Padding.Empty
                };
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 88;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                var take = UiTheme.CreateAntdButton("取回继续", true);
                take.Width = 108;
                take.Click += delegate { TakeSelected(); };
                actions.Controls.Add(cancel);
                actions.Controls.Add(take);
                footer.Controls.Add(actions, 1, 0);
                root.Controls.Add(footer, 0, 2);

                Controls.Add(root);
                AcceptButton = take;
                CancelButton = cancel;
                UiTheme.Apply(this);
                Shown += delegate
                {
                    UiTheme.FitDialogToWorkingArea(this, 24);
                    if (_rows.Count > 0)
                    {
                        _selected = _rows[0];
                        _grid.SetSelected(_rows[0], false);
                    }
                };
            }

            private void TakeSelected()
            {
                if (_selected == null)
                {
                    MessageBox.Show(this, "请先选择一张挂单。", "取单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SelectedDraftId = _selected.Id;
                DialogResult = DialogResult.OK;
                Close();
            }

            private void DeleteSelected()
            {
                if (_selected == null)
                {
                    MessageBox.Show(this, "请先选择要删除的挂单。", "删除挂单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show(
                    this,
                    "确定删除挂单“" + _selected.DraftNo + "”吗？这不会影响库存或历史销售单。",
                    "确认删除挂单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                _services.Sales.DeleteDraft(_selected.Id);
                _rows.Remove(_selected);
                _selected = _rows.Count > 0 ? _rows[0] : null;
                if (_selected != null)
                    _grid.SetSelected(_selected, false);
            }

            private sealed class DraftRow
            {
                public long Id { get; private set; }
                public string UpdatedAtText { get; private set; }
                public string DraftNo { get; private set; }
                public int ItemCount { get; private set; }
                public int QuantityTotal { get; private set; }
                public decimal TotalYuan { get; private set; }
                public string Note { get; private set; }

                public DraftRow(SalesService.SalesDraftSummary source)
                {
                    Id = source.Id;
                    UpdatedAtText = source.UpdatedAt == DateTime.MinValue
                        ? "—"
                        : source.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
                    DraftNo = source.DraftNo;
                    ItemCount = source.ItemCount;
                    QuantityTotal = source.QuantityTotal;
                    TotalYuan = Money.ToYuan(source.TotalCent);
                    Note = string.IsNullOrWhiteSpace(source.Note) ? "—" : source.Note;
                }
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
            public decimal DiscountPercent { get; set; }
            public decimal OrderDiscountPercent { get; set; }

            public decimal DiscountedUnitPriceYuan
            {
                get
                {
                    var baseCent = Money.FromYuan(UnitPriceYuan);
                    return Money.ToYuan(
                        ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent)));
                }
            }

            public decimal LineTotalYuan
            {
                get
                {
                    var baseCent = Money.FromYuan(UnitPriceYuan);
                    var lineCent = ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent));
                    var finalCent = ApplyBasisPoints(lineCent, PercentToBasisPoints(OrderDiscountPercent));
                    return Money.ToYuan(checked((long)Quantity * finalCent));
                }
            }
        }
    }
}
