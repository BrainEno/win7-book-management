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
        private readonly DataGridViewColumn _titleColumn;
        private readonly DataGridViewColumn _shelfColumn;
        private readonly DataGridViewColumn _stockColumn;
        private readonly DataGridViewColumn _quantityColumn;
        private readonly DataGridViewColumn _unitCostColumn;
        private readonly DataGridViewColumn _lineTotalColumn;

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
                Width = 96,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            _titleColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 240,
                FillWeight = 240,
                ReadOnly = true
            };
            _quantityColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "入库数量",
                DataPropertyName = "Quantity",
                Width = 112,
                MinimumWidth = 96,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = UiTheme.AccentSoft,
                    SelectionBackColor = UiTheme.Surface,
                    SelectionForeColor = UiTheme.TextPrimary
                }
            };
            _unitCostColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "本次进价",
                DataPropertyName = "UnitCostYuan",
                Width = 120,
                MinimumWidth = 104,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = UiTheme.AccentSoft,
                    SelectionBackColor = UiTheme.Surface,
                    SelectionForeColor = UiTheme.TextPrimary
                }
            };
            _lineTotalColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "小计",
                DataPropertyName = "LineTotalYuan",
                Width = 120,
                MinimumWidth = 96,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
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
            _grid.SizeChanged += delegate { ApplyResponsiveColumns(); };
            Shown += delegate
            {
                ReloadSuppliers();
                UpdateTotals();
                ApplyResponsiveColumns();
                _isbn.Focus();
            };

            UiTheme.Apply(this);
            UiTheme.StyleEditableGrid(_grid);
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
            toolbar.BorderStyle = BorderStyle.None;

            var newOrder = new Button
            {
                Text = "新入库单",
                Width = 96,
                Height = UiTheme.ButtonHeight
            };
            var clear = new Button
            {
                Text = "清空当前单",
                Width = 108,
                Height = UiTheme.ButtonHeight
            };

            newOrder.Click += delegate { StartNewOrder(); };
            clear.Click += delegate { ClearCartWithConfirmation(); };

            toolbar.Controls.Add(newOrder);
            toolbar.Controls.Add(clear);
            toolbar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "扫码或搜索添加图书；数量和本次进价在下方明细中直接修改。",
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
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 8),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "入库信息",
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            var supplierRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            supplierRow.Controls.Add(new Label
            {
                Text = "供应商（可选）",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 11, 14, 0)
            });

            _supplier.DropDownStyle = ComboBoxStyle.DropDownList;
            _supplier.Font = UiTheme.Font(9.3F);
            _supplier.Margin = Padding.Empty;

            var supplierInput = UiTheme.CreateInputFrame(_supplier);
            supplierInput.Dock = DockStyle.None;
            supplierInput.Width = 320;
            supplierInput.Margin = new Padding(0, 2, 12, 4);
            supplierRow.Controls.Add(supplierInput);

            supplierRow.Controls.Add(new Label
            {
                Text = "默认“不区分”，不选择具体供应商也可以正常入库",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 11, 0, 0)
            });

            section.Controls.Add(supplierRow, 0, 1);

            var scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 0)
            };
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            scanRow.Controls.Add(new Label
            {
                Text = "ISBN / 书名",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 11, 14, 0)
            }, 0, 0);

            _isbn.Margin = Padding.Empty;
            _isbn.Font = UiTheme.Font(9.5F);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };

            var isbnInput = UiTheme.CreateInputFrame(_isbn);
            isbnInput.Margin = new Padding(0, 2, 10, 2);
            scanRow.Controls.Add(isbnInput, 1, 0);

            var add = new Button
            {
                Text = "搜索加入",
                AutoSize = true,
                MinimumSize = new Size(108, UiTheme.ButtonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0, 2, 8, 2),
                Tag = "primary"
            };
            add.Click += delegate { AddByIsbn(); };
            scanRow.Controls.Add(add, 2, 0);

            var pick = new Button
            {
                Text = "选择图书",
                AutoSize = true,
                MinimumSize = new Size(104, UiTheme.ButtonHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0, 2, 0, 2)
            };
            pick.Click += delegate { PickBook(); };
            scanRow.Controls.Add(pick, 3, 0);

            section.Controls.Add(scanRow, 0, 2);

            section.Controls.Add(new Label
            {
                Text = "支持完整或部分 ISBN、书名模糊搜索。数量和本次进价在下方明细中修改。",
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
                Text = "入库明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var gridActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            gridActions.Controls.Add(new Label
            {
                Text = "浅绿色单元格可编辑",
                AutoSize = true,
                Font = UiTheme.Font(8F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 10, 10, 0)
            });

            var remove = new Button
            {
                Text = "移除选中",
                Width = 98,
                Height = UiTheme.ButtonHeight,
                Margin = Padding.Empty
            };
            remove.Click += delegate { RemoveSelected(); };
            gridActions.Controls.Add(remove);

            header.Controls.Add(gridActions, 1, 0);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "入库明细为空\r\n请扫描 ISBN、输入 ISBN / 书名搜索加入，或从图书资料中选择";
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

        private Control CreateNoteSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 62),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0, 10, 0, 0),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 9, 14, 0)
            }, 0, 0);

            _note.Margin = Padding.Empty;
            _note.Font = UiTheme.Font(9F);
            var noteInput = UiTheme.CreateInputFrame(_note);
            noteInput.Margin = new Padding(0, 1, 0, 1);
            section.Controls.Add(noteInput, 1, 0);

            return section;
        }

        private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 76),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 0),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var metrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            ConfigureSummaryLabel(_lineCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_total, true);

            metrics.Controls.Add(_lineCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_total);

            var submit = new Button
            {
                Text = "确认入库",
                AutoSize = true,
                MinimumSize = new Size(124, UiTheme.ButtonHeight),
                Margin = new Padding(14, 0, 0, 0),
                Tag = "primary"
            };
            submit.Click += delegate { Submit(); };

            section.Controls.Add(metrics, 0, 0);
            section.Controls.Add(submit, 1, 0);

            return section;
        }

        private static void ConfigureSummaryLabel(Label label, bool primary)
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = primary ? UiTheme.Accent : UiTheme.TextSecondary;
            label.Font = UiTheme.Font(primary ? 13.5F : 8.5F, FontStyle.Bold);
            label.BackColor = primary ? UiTheme.AccentSoft : UiTheme.Surface;
            label.Padding = primary
                ? new Padding(12, 8, 12, 8)
                : new Padding(10, 8, 10, 8);
            label.Margin = new Padding(0, 0, 10, 0);
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

            _grid.Columns.Add(_isbnColumn);
            _grid.Columns.Add(_titleColumn);
            _grid.Columns.Add(_stockColumn);
            _grid.Columns.Add(_quantityColumn);
            _grid.Columns.Add(_unitCostColumn);
            _grid.Columns.Add(_lineTotalColumn);
            _grid.Columns.Add(_selfCodeColumn);
            _grid.Columns.Add(_shelfColumn);

            _grid.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
            };
            _grid.EditingControlShowing += GridEditingControlShowing;
            _grid.CellEndEdit += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
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
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    cell.ErrorText = cell.OwningColumn == _quantityColumn
                        ? "请输入大于 0 的整数。"
                        : "请输入有效金额。";
                }
            };
        }

        private void GridEditingControlShowing(
            object sender,
            DataGridViewEditingControlShowingEventArgs e)
        {
            var editor = e.Control as TextBox;
            if (editor == null)
                return;

            editor.TextAlign = HorizontalAlignment.Right;
            editor.KeyDown -= GridEditorKeyDown;
            editor.KeyDown += GridEditorKeyDown;

            BeginInvoke((MethodInvoker)delegate
            {
                if (!editor.IsDisposed)
                    editor.SelectAll();
            });
        }

        private void GridEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            _grid.EndEdit();
            MoveToNextEditableCell();
        }

        private void MoveToNextEditableCell()
        {
            var current = _grid.CurrentCell;
            if (current == null)
                return;

            var rowIndex = current.RowIndex;
            if (current.OwningColumn == _quantityColumn)
            {
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells[_unitCostColumn.Index];
                _grid.BeginEdit(true);
                return;
            }

            if (current.OwningColumn == _unitCostColumn)
            {
                if (rowIndex + 1 < _grid.Rows.Count)
                {
                    _grid.CurrentCell = _grid.Rows[rowIndex + 1].Cells[_quantityColumn.Index];
                    _grid.BeginEdit(true);
                }
                else
                {
                    _isbn.Focus();
                    _isbn.SelectAll();
                }
            }
        }

        private int MeasureGridColumnWidth(
            DataGridViewColumn column,
            string sample,
            int minimum)
        {
            var headerFont = _grid.ColumnHeadersDefaultCellStyle.Font ?? _grid.Font;
            var header = TextRenderer.MeasureText(
                column.HeaderText ?? "",
                headerFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);
            var body = TextRenderer.MeasureText(
                sample ?? "",
                _grid.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            return Math.Max(minimum, Math.Max(header.Width, body.Width) + 30);
        }

        private void ReloadSuppliers()
        {
            var options = new List<Supplier>
            {
                new Supplier { Id = 0, Name = "不区分（默认）", IsActive = true }
            };

            var suppliers = _services.Suppliers.GetAll(false);
            foreach (var supplier in suppliers)
                options.Add(supplier);

            _supplier.DataSource = options;
            _supplier.DisplayMember = "Name";
            _supplier.ValueMember = "Id";
            if (_supplier.Items.Count > 0)
                _supplier.SelectedIndex = 0;
        }

        private void ApplyResponsiveColumns()
        {
            _selfCodeColumn.Width = MeasureGridColumnWidth(
                _selfCodeColumn, "BK-123456", 112);
            _isbnColumn.Width = MeasureGridColumnWidth(
                _isbnColumn, "9781234567890", 150);
            _shelfColumn.Width = MeasureGridColumnWidth(
                _shelfColumn, "A-01-12", 96);
            _stockColumn.Width = MeasureGridColumnWidth(
                _stockColumn, "99999", 100);
            _quantityColumn.Width = MeasureGridColumnWidth(
                _quantityColumn, "99999", 112);
            _unitCostColumn.Width = MeasureGridColumnWidth(
                _unitCostColumn, "99999.00", 120);
            _lineTotalColumn.Width = MeasureGridColumnWidth(
                _lineTotalColumn, "999999.00", 120);
            _titleColumn.MinimumWidth = MeasureGridColumnWidth(
                _titleColumn, "较长的中文图书名称", 260);

            _selfCodeColumn.Visible = true;
            _shelfColumn.Visible = true;
            _isbnColumn.Visible = true;
            _stockColumn.Visible = true;

            var width = _grid.ClientSize.Width > 0
                ? _grid.ClientSize.Width
                : ClientSize.Width;
            var required =
                _titleColumn.MinimumWidth +
                _quantityColumn.Width +
                _unitCostColumn.Width +
                _lineTotalColumn.Width +
                _selfCodeColumn.Width +
                _shelfColumn.Width +
                _isbnColumn.Width +
                _stockColumn.Width +
                24;

            if (required > width)
            {
                _selfCodeColumn.Visible = false;
                required -= _selfCodeColumn.Width;
            }
            if (required > width)
            {
                _shelfColumn.Visible = false;
                required -= _shelfColumn.Width;
            }
            if (required > width)
            {
                _stockColumn.Visible = false;
                required -= _stockColumn.Width;
            }
            if (required > width)
                _isbnColumn.Visible = false;
        }

        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描 ISBN，或输入 ISBN / 书名关键词。", "还没有图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            // Keep the scanner path fast: an exact ISBN match is added immediately.
            var exactBook = _services.Books.FindByExactIsbn(text);
            if (exactBook != null)
            {
                AddBook(exactBook);
                _isbn.Clear();
                _isbn.Focus();
                return;
            }

            var matches = _services.Books.SearchActiveByIsbnOrTitle(text);
            if (matches.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "没有找到与“" + text + "”匹配的启用图书。可以输入完整/部分 ISBN 或书名；若仍找不到，请先在“图书资料”中建立资料。",
                    "未找到图书",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _isbn.SelectAll();
                _isbn.Focus();
                return;
            }

            if (matches.Count == 1)
            {
                AddBook(matches[0]);
                _isbn.Clear();
                _isbn.Focus();
                return;
            }

            using (var dialog = new BookLookupDialog(_services, text, true))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedBook != null)
                {
                    AddBook(dialog.SelectedBook);
                    _isbn.Clear();
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

            ResetOrder(true);
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
