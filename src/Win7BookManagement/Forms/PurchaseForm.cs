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
        private readonly Label _lineCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _shelfColumn;
        private readonly DataGridViewColumn _stockColumn;

        public PurchaseForm(ApplicationServices services)
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
            _shelfColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "货架位",
                DataPropertyName = "ShelfCode",
                Width = 86,
                ReadOnly = true
            };
            _stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "当前库存",
                DataPropertyName = "CurrentStock",
                Width = 82,
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
            root.Controls.Add(CreateReceivingSection(), 0, 1);
            root.Controls.Add(CreateCartSection(), 0, 2);
            root.Controls.Add(CreateNoteSection(), 0, 3);
            root.Controls.Add(CreateTotalsSection(), 0, 4);

            Controls.Add(root);

            _rows.ListChanged += delegate { UpdateTotals(); };
            Resize += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                ReloadSuppliers();
                UpdateTotals();
                ApplyResponsiveColumns();
                _isbn.Focus();
            };

            UiTheme.Apply(this);
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
            toolbar.BorderStyle = BorderStyle.FixedSingle;

            var newOrder = new Button { Text = "新入库单", Width = 92, Height = UiTheme.ButtonHeight };
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

            toolbar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "供应商可选；扫码 ISBN 后回车即可加入。默认进价只负责预填，本次进价可直接修改。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 11, 0, 0)
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
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 10, 0, 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "入库信息",
                AutoSize = true,
                Font = UiTheme.Font(9.4F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            var supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty
            };
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));

            supplierRow.Controls.Add(new Label
            {
                Text = "供应商",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _supplier.DropDownStyle = ComboBoxStyle.DropDownList;
            _supplier.Dock = DockStyle.Fill;
            _supplier.Margin = new Padding(0, 4, 10, 4);
            supplierRow.Controls.Add(_supplier, 1, 0);

            supplierRow.Controls.Add(new Label
            {
                Text = "可不指定供应商",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            }, 2, 0);

            section.Controls.Add(supplierRow, 0, 1);

            var scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty
            };
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

            scanRow.Controls.Add(new Label
            {
                Text = "ISBN",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _isbn.Dock = DockStyle.Fill;
            _isbn.Margin = new Padding(0, 4, 10, 4);
            _isbn.Font = UiTheme.Font(9.5F);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            scanRow.Controls.Add(_isbn, 1, 0);

            var add = new Button
            {
                Text = "搜索加入",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4),
                Tag = "primary"
            };
            add.Click += delegate { AddByIsbn(); };
            scanRow.Controls.Add(add, 2, 0);

            var pick = new Button
            {
                Text = "选择图书",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            pick.Click += delegate { PickBook(); };
            scanRow.Controls.Add(pick, 3, 0);

            section.Controls.Add(scanRow, 0, 2);

            section.Controls.Add(new Label
            {
                Text = "本次入库数量和进价以表格中的值为准；提交后会生成采购单、增加库存并写入库存流水。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(82, 5, 0, 0)
            }, 0, 3);

            return section;
        }

        private Control CreateCartSection()
        {
            ConfigureGrid();

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "入库明细为空，请先扫码或搜索添加图书";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.Font = UiTheme.Font(9F);
            _emptyState.BackColor = UiTheme.Surface;

            host.Controls.Add(_grid);
            host.Controls.Add(_emptyState);
            _emptyState.BringToFront();

            return host;
        }

        private Control CreateNoteSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 9, 14, 9),
                Margin = new Padding(0, 10, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "备注",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 3, 0, 3);
            section.Controls.Add(_note, 1, 0);

            return section;
        }

        private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 74,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 11, 16, 11),
                Margin = new Padding(0, 8, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));

            _lineCount.Dock = DockStyle.Fill;
            _lineCount.TextAlign = ContentAlignment.MiddleLeft;
            _lineCount.ForeColor = UiTheme.TextSecondary;
            _lineCount.Font = UiTheme.Font(8.5F, FontStyle.Bold);

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
                Text = "确认入库",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                Tag = "primary"
            };
            submit.Click += delegate { Submit(); };

            section.Controls.Add(_lineCount, 0, 0);
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
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(_stockColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "入库数量",
                DataPropertyName = "Quantity",
                Width = 88,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "本次进价",
                DataPropertyName = "UnitCostYuan",
                Width = 96,
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
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
                MessageBox.Show(this, "入库数量请输入整数，本次进价请输入有效金额。", "输入格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void ReloadSuppliers()
        {
            var options = new List<Supplier>
            {
                new Supplier { Id = 0, Name = "未指定供应商（可选）", IsActive = true }
            };

            var suppliers = _services.Suppliers.GetAll(false);
            foreach (var supplier in suppliers)
                options.Add(supplier);

            _supplier.DataSource = options;
            _supplier.DisplayMember = "Name";
            _supplier.ValueMember = "Id";
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _selfCodeColumn.Visible = width >= 980;
            _shelfColumn.Visible = width >= 860;
            _isbnColumn.Visible = width >= 740;
            _stockColumn.Visible = width >= 680;
        }

        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描或输入 ISBN。", "还没有图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            _rows.Add(new PurchaseCartRow
            {
                BookId = book.Id,
                SelfCode = book.SelfCode,
                Isbn = book.Isbn,
                Title = book.Title,
                ShelfCode = book.ShelfCode,
                CurrentStock = book.StockQuantity,
                Quantity = 1,
                UnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent)
            });
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show(this, "请先在入库明细中选择一行。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = _grid.CurrentRow.DataBoundItem as PurchaseCartRow;
            if (row != null)
                _rows.Remove(row);
        }

        private void StartNewOrder()
        {
            if (_rows.Count > 0)
            {
                var result = MessageBox.Show(
                    this,
                    "当前入库单还有图书。新建空白入库单会清空这些内容，是否继续？",
                    "新建入库单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }

            ResetOrder(false);
        }

        private void ClearCartWithConfirmation()
        {
            if (_rows.Count == 0)
                return;

            var result = MessageBox.Show(
                this,
                "确定清空当前入库单中的全部图书吗？",
                "清空当前单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
                ResetOrder(false);
        }

        private void ResetOrder(bool resetSupplier)
        {
            _rows.Clear();
            _note.Clear();
            _isbn.Clear();

            if (resetSupplier && _supplier.Items.Count > 0)
                _supplier.SelectedIndex = 0;

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
            if (_rows.Count == 0)
                _emptyState.BringToFront();
            else
                _grid.BringToFront();
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
                _grid.EndEdit();

                var lines = new List<TransactionLineInput>();
                foreach (var row in _rows)
                {
                    if (row.Quantity <= 0)
                        throw new InvalidOperationException("《" + row.Title + "》的入库数量必须大于 0。");
                    if (row.UnitCostYuan < 0)
                        throw new InvalidOperationException("《" + row.Title + "》的进价不能为负数。");

                    lines.Add(new TransactionLineInput
                    {
                        BookId = row.BookId,
                        Quantity = row.Quantity,
                        UnitPriceCent = Money.FromYuan(row.UnitCostYuan)
                    });
                }

                var selected = _supplier.SelectedItem as Supplier;
                long? supplierId = selected != null && selected.Id > 0
                    ? (long?)selected.Id
                    : null;

                var orderNo = _services.Purchases.Receive(supplierId, lines, _note.Text);
                MessageBox.Show(
                    this,
                    "入库完成。\r\n单号：" + orderNo + "\r\n库存已同步增加并写入库存流水。",
                    "入库成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ResetOrder(false);
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
