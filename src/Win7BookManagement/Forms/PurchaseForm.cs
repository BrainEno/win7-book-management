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
        private readonly AntdUI.Table _grid = new AntdUI.Table();
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

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "146", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "auto", MinWidth = "260", Ellipsis = true, ReadOnly = true };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "96", ReadOnly = true };
            _stockColumn = new AntdUI.Column("CurrentStock", "当前库存") { Width = "100", ReadOnly = true };
            _quantityColumn = new AntdUI.Column("Quantity", "入库数量")
            {
                Width = "116",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _unitCostColumn = new AntdUI.Column("UnitCostYuan", "本次进价")
            {
                Width = "124",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计") { Width = "124", ReadOnly = true, DisplayFormat = "0.00" };

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
            root.Controls.Add(CreateReceivingSection(), 0, 1);
            root.Controls.Add(CreateCartSection(), 0, 2);
            root.Controls.Add(CreateNoteSection(), 0, 3);
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

        private Control CreateActionToolbar()
        {
            var toolbar = UiTheme.CreateResponsiveToolbar();
            toolbar.BackColor = UiTheme.Surface;

            var newOrder = UiTheme.CreateAntdButton("＋ 新入库单", false);
            newOrder.Width = 112;
            var clear = UiTheme.CreateAntdButton("清空当前单", false);
            clear.Width = 116;

            newOrder.Click += delegate { StartNewOrder(); };
            clear.Click += delegate { ClearCartWithConfirmation(); };

            toolbar.Controls.Add(newOrder);
            toolbar.Controls.Add(clear);
            toolbar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "点击浅绿色单元格可直接修改入库数量或本次进价。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 12, 0, 0)
            });
            return toolbar;
        }

        private Control CreateReceivingSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(0, 8, 0, 8)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "入库信息",
                AutoSize = true,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 9)
            }, 0, 0);

            var supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty
            };
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            supplierRow.Controls.Add(new Label
            {
                Text = "供应商（可选）",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);

            _supplier.Dock = DockStyle.Fill;
            _supplier.Margin = new Padding(0, 3, 12, 3);
            _supplier.DropDownArrow = true;
            _supplier.Radius = 7;
            _supplier.BorderWidth = 1.2F;
            _supplier.BorderColor = UiTheme.Border;
            supplierRow.Controls.Add(_supplier, 1, 0);

            supplierRow.Controls.Add(new Label
            {
                Text = "默认“不区分”，不选择具体供应商也可以正常入库",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            }, 2, 0);
            section.Controls.Add(supplierRow, 0, 1);

            var scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 5, 0, 0)
            };
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            scanRow.Controls.Add(new Label
            {
                Text = "添加图书",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);

            _isbn.Dock = DockStyle.Fill;
            _isbn.Margin = new Padding(0, 3, 10, 3);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            scanRow.Controls.Add(_isbn, 1, 0);

            var add = UiTheme.CreateAntdButton("搜索加入", true);
            add.Width = 114;
            add.Margin = new Padding(0, 3, 8, 3);
            add.Click += delegate { AddByIsbn(); };
            scanRow.Controls.Add(add, 2, 0);

            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 110;
            pick.Margin = new Padding(0, 3, 0, 3);
            pick.Click += delegate { PickBook(); };
            scanRow.Controls.Add(pick, 3, 0);

            section.Controls.Add(scanRow, 0, 2);
            section.Controls.Add(new Label
            {
                Text = "支持店内编码、完整/部分 ISBN、书名或作者；自出版物没有 ISBN 也能正常入库。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 6, 0, 0)
            }, 0, 3);
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
                Text = "入库明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var remove = UiTheme.CreateAntdButton("移除选中", false);
            remove.Width = 106;
            remove.Click += delegate { RemoveSelected(); };
            header.Controls.Add(remove, 1, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "入库明细为空\r\n请扫码、搜索加入，或点击“选择图书”";
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
            _grid.EmptyText = "当前入库单还没有图书";
            _grid.RowHoverBg = Color.FromArgb(248, 246, 241);
            _grid.RowSelectedBg = UiTheme.AccentSoft;
            _grid.RowSelectedFore = UiTheme.TextPrimary;

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
                Height = 66,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0, 10, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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
            _note.Margin = new Padding(0, 2, 0, 2);
            section.Controls.Add(_note, 1, 0);
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
            ConfigureSummaryLabel(_lineCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_total, true);
            metrics.Controls.Add(_lineCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_total);

            var submit = UiTheme.CreateAntdButton("确认入库", true);
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
            _selfCodeColumn.Visible = width >= 1180;
            _shelfColumn.Visible = width >= 1050;
            _stockColumn.Visible = width >= 850;
            _isbnColumn.Visible = width >= 720;
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
