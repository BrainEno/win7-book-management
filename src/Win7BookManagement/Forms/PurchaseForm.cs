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
        private readonly AntdUI.Select _supplier = new AntdUI.Select();
        private readonly AntdUI.Input _isbn = UiTheme.CreateAntdInput("扫码或输入店内编码 / ISBN / 书名 / 作者");
        private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("可选：填写到货批次、物流或其他备注");
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
        private readonly BindingList<PurchaseCartRow> _rows = new BindingList<PurchaseCartRow>();
        private readonly List<Supplier> _supplierOptions = new List<Supplier>();
        private readonly Label _lineCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _titleColumn;
        private readonly AntdUI.Column _shelfColumn;
        private readonly AntdUI.Column _stockColumn;
        private readonly AntdUI.Column _quantityColumn;
        private readonly AntdUI.Column _unitCostColumn;
        private readonly AntdUI.Column _lineTotalColumn;

        private PurchaseCartRow _selectedRow;

        public PurchaseForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "120", MinWidth = "96", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "150", MinWidth = "116", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "220", MaxWidth = "420", Ellipsis = true, ReadOnly = true };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "96", MinWidth = "82", ReadOnly = true };
            _stockColumn = new AntdUI.Column("CurrentStock", "当前库存") { Width = "104", MinWidth = "92", ReadOnly = true };
            _quantityColumn = new AntdUI.Column("Quantity", "入库数量")
            {
                Width = "112",
                MinWidth = "96",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _unitCostColumn = new AntdUI.Column("UnitCostYuan", "本次进价")
            {
                Width = "116",
                MinWidth = "104",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计") { Width = "112", MinWidth = "92", ReadOnly = true, DisplayFormat = "0.00" };

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

            root.Controls.Add(CreateReceivingSection(), 0, 0);
            root.Controls.Add(CreateCartSection(), 0, 1);
            root.Controls.Add(CreateNoteSection(), 0, 2);
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
                ReloadSuppliers();
                _grid.DataSource = _rows;
                UpdateTotals();
                ApplyResponsiveColumns();
                _isbn.Focus();
            };

            UiTheme.Apply(this);
            ApplyResponsiveColumns();
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

        private Control CreateReceivingSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 8, 14, 9),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 4),
                ColumnCount = 5,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            supplierRow.Controls.Add(new Label
            {
                Text = "供应商",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _supplier.Dock = DockStyle.Fill;
            _supplier.Margin = new Padding(0, 0, 10, 0);
            _supplier.DropDownArrow = true;
            supplierRow.Controls.Add(_supplier, 1, 0);

            var newOrder = UiTheme.CreateAntdButton("新入库单", false);
            newOrder.Width = 88;
            newOrder.Margin = new Padding(0, 0, 6, 0);
            newOrder.Click += delegate { StartNewOrder(); };
            supplierRow.Controls.Add(newOrder, 3, 0);

            var clear = UiTheme.CreateAntdButton("清空", false);
            clear.Width = 76;
            clear.Click += delegate { ClearCartWithConfirmation(); };
            supplierRow.Controls.Add(clear, 4, 0);

            var scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 6),
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0),
                Padding = Padding.Empty
            };
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            scanRow.Controls.Add(new Label
            {
                Text = "扫码 / 搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _isbn.Dock = DockStyle.Fill;
            _isbn.Margin = new Padding(0, 0, 8, 0);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            scanRow.Controls.Add(_isbn, 1, 0);

            var add = UiTheme.CreateAntdButton("加入", true);
            add.Width = 78;
            add.Margin = new Padding(0, 0, 6, 0);
            add.Click += delegate { AddByIsbn(); };
            scanRow.Controls.Add(add, 2, 0);

            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 92;
            pick.Click += delegate { PickBook(); };
            scanRow.Controls.Add(pick, 3, 0);

            section.Controls.Add(supplierRow, 0, 0);
            section.Controls.Add(scanRow, 0, 1);
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
                Text = "入库明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "数量和进价可直接编辑",
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
            _emptyState.Text = "当前入库单为空\r\n请扫码、搜索或选择图书";
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
            _grid.EmptyText = "当前入库单还没有图书";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _titleColumn,
                _quantityColumn,
                _unitCostColumn,
                _lineTotalColumn,
                _stockColumn,
                _isbnColumn,
                _selfCodeColumn,
                _shelfColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "purchase-lines-v2");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedRow = e.Record as PurchaseCartRow;
            };
            _grid.CellEndEdit += HandleCellEndEdit;
        }

        private bool HandleCellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            var row = e.Record as PurchaseCartRow;
            if (row == null || e.Column == null) return false;

            if (string.Equals(e.Column.Key, "Quantity", StringComparison.Ordinal))
            {
                int quantity;
                if (!int.TryParse(e.Value, out quantity) || quantity <= 0)
                {
                    MessageBox.Show(this, "入库数量必须是大于 0 的整数。", "数量格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.Quantity = quantity;
            }
            else if (string.Equals(e.Column.Key, "UnitCostYuan", StringComparison.Ordinal))
            {
                decimal price;
                if (!decimal.TryParse(e.Value, out price) || price < 0)
                {
                    MessageBox.Show(this, "本次进价必须是有效的非负金额。", "金额格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.UnitCostYuan = price;
            }

            _grid.Refresh();
            UpdateTotals();
            return true;
        }

        private Control CreateNoteSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 14),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 6, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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
            _note.Margin = Padding.Empty;
            section.Controls.Add(_note, 1, 0);
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
            ConfigureSummaryLabel(_lineCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_total, true);
            metrics.Controls.Add(_lineCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_total);

            var submit = UiTheme.CreateAntdButton("确认入库", true);
            submit.Width = 112;
            submit.Margin = new Padding(10, 0, 0, 0);
            submit.Click += delegate { Submit(); };

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

        private void ReloadSuppliers()
        {
            _supplierOptions.Clear();
            _supplier.Items.Clear();

            _supplierOptions.Add(new Supplier { Id = 0, Name = "不区分（默认）", IsActive = true });
            var suppliers = _services.Suppliers.GetAll(false);
            foreach (var supplier in suppliers) _supplierOptions.Add(supplier);

            foreach (var supplier in _supplierOptions) _supplier.Items.Add(supplier.Name);
            if (_supplierOptions.Count > 0) _supplier.SelectedIndex = 0;
        }

        private void ApplyResponsiveColumns()
        {
            var width = _grid.ClientSize.Width > 0 ? _grid.ClientSize.Width : ClientSize.Width;
            // Prioritize title, quantity and cost on compact workstations.
            // Reference columns progressively return on larger windows.
            _selfCodeColumn.Visible = width >= 1250;
            _shelfColumn.Visible = width >= 1100;
            _stockColumn.Visible = width >= 720;
            _isbnColumn.Visible = width >= 900;
            _grid.LoadLayout();
        }

        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描 ISBN，或输入店内编码 / ISBN / 书名关键词。", "还没有图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    row.Quantity += 1;
                    _selectedRow = row;
                    _grid.Refresh();
                    UpdateTotals();
                    return;
                }
            }

            var added = new PurchaseCartRow
            {
                BookId = book.Id,
                SelfCode = book.SelfCode,
                Isbn = book.Isbn,
                Title = book.Title,
                ShelfCode = book.ShelfCode,
                CurrentStock = book.StockQuantity,
                Quantity = 1,
                UnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent)
            };
            _rows.Add(added);
            _selectedRow = added;
            _grid.SetSelected(added, false);
        }

        private void RemoveSelected()
        {
            if (_selectedRow == null)
            {
                MessageBox.Show(this, "请先在入库明细中选择一行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _rows.Remove(_selectedRow);
            _selectedRow = null;
        }

        private void StartNewOrder()
        {
            if (_rows.Count > 0)
            {
                var result = MessageBox.Show(this, "当前入库单还有图书。新建空白入库单会清空这些内容，是否继续？", "新建入库单", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            ResetOrder(true);
        }

        private void ClearCartWithConfirmation()
        {
            if (_rows.Count == 0) return;
            if (MessageBox.Show(this, "确定清空当前入库单中的全部图书吗？", "清空当前单", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                ResetOrder(false);
        }

        private void ResetOrder(bool resetSupplier)
        {
            _rows.Clear();
            _selectedRow = null;
            _note.Text = "";
            _isbn.Text = "";
            if (resetSupplier && _supplierOptions.Count > 0) _supplier.SelectedIndex = 0;
            _isbn.Focus();
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            decimal total = 0m;
            var quantity = 0;
            foreach (var row in _rows)
            {
                total += row.Quantity * row.UnitCostYuan;
                quantity += row.Quantity;
            }

            _lineCount.Text = "图书项  " + _rows.Count;
            _quantityTotal.Text = "入库册数  " + quantity;
            _total.Text = "采购金额  ¥" + total.ToString("0.00");

            _emptyState.Visible = _rows.Count == 0;
            if (_rows.Count == 0) _emptyState.BringToFront();
            else _grid.BringToFront();
        }

        private void Submit()
        {
            if (_rows.Count == 0)
            {
                MessageBox.Show(this, "当前入库单还没有图书。请先扫码或搜索添加图书。", "无法入库", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            try
            {
                var lines = new List<TransactionLineInput>();
                foreach (var row in _rows)
                {
                    if (row.Quantity <= 0) throw new InvalidOperationException("《" + row.Title + "》的入库数量必须大于 0。");
                    if (row.UnitCostYuan < 0) throw new InvalidOperationException("《" + row.Title + "》的进价不能为负数。");

                    lines.Add(new TransactionLineInput
                    {
                        BookId = row.BookId,
                        Quantity = row.Quantity,
                        UnitPriceCent = Money.FromYuan(row.UnitCostYuan)
                    });
                }

                var index = _supplier.SelectedIndex;
                var selected = index >= 0 && index < _supplierOptions.Count ? _supplierOptions[index] : null;
                long? supplierId = selected != null && selected.Id > 0 ? (long?)selected.Id : null;

                var orderNo = _services.Purchases.Receive(supplierId, lines, _note.Text);
                MessageBox.Show(this, "入库完成。\r\n单号：" + orderNo + "\r\n库存已同步增加并写入库存流水。", "入库成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetOrder(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "入库失败：\r\n" + ex.Message, "请检查入库单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class PurchaseCartRow
        {
            public long BookId { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string ShelfCode { get; set; }
            public int CurrentStock { get; set; }
            public int Quantity { get; set; }
            public decimal UnitCostYuan { get; set; }
            public decimal LineTotalYuan { get { return Quantity * UnitCostYuan; } }
        }
    }
}
