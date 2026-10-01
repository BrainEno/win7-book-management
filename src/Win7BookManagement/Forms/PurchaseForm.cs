using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class PurchaseForm : Form, INavigationGuard, IUiSpecPage
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

        private TableLayoutPanel _emptySurface;
        private TableLayoutPanel _emptyHeaderRow;
        private UiSpecSectionPanel _receivingSection;
        private TableLayoutPanel _supplierRow;
        private TableLayoutPanel _scanRow;
        private TableLayoutPanel _cartHost;
        private TableLayoutPanel _cartHeader;
        private TableLayoutPanel _noteSection;
        private TableLayoutPanel _totalsSection;
        private FlowLayoutPanel _metrics;
        private AntdUI.Button _newOrderButton;
        private AntdUI.Button _clearButton;
        private AntdUI.Button _addButton;
        private AntdUI.Button _pickButton;
        private AntdUI.Button _removeButton;
        private AntdUI.Button _submitButton;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;

        private readonly AntdUI.Column _indexColumn;
        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _titleColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _publisherColumn;
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

            _indexColumn = new AntdUI.Column("Index", "序号") { Width = "60", MinWidth = "54", ReadOnly = true };
            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "110", MinWidth = "90", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "118", MinWidth = "96", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "130", MaxWidth = "300", Ellipsis = true, ReadOnly = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "110", MinWidth = "86", Ellipsis = true, ReadOnly = true };
            _publisherColumn = new AntdUI.Column("Publisher", "出版社") { Width = "110", MinWidth = "86", Ellipsis = true, ReadOnly = true };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "96", MinWidth = "82", ReadOnly = true };
            _stockColumn = new AntdUI.Column("CurrentStock", "当前库存") { Width = "104", MinWidth = "92", ReadOnly = true };
            _quantityColumn = new AntdUI.Column("Quantity", "数量")
            {
                Width = "82",
                MinWidth = "70",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _unitCostColumn = new AntdUI.Column("UnitCostYuan", "进价（元）")
            {
                Width = "112",
                MinWidth = "94",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计（元）") { Width = "112", MinWidth = "94", ReadOnly = true, DisplayFormat = "0.00" };

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
            ApplyUiSpecProfile(BookDeskUiSpec.Standard);
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
            _receivingSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseToolbarStandardHeight),
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(BookDeskUiSpec.Standard.ToolbarPadding),
                Margin = Padding.Empty
            };
            _receivingSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _supplierRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.Standard.ControlHeight),
                ColumnCount = 5,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BookDeskUiSpec.PurchaseSupplierStandardWidth));
            _supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _supplierRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _supplierRow.Controls.Add(new Label
            {
                Text = "供应商",
                AutoSize = false,
                Width = BookDeskUiSpec.PurchaseSupplierLabelWidth,
                MinimumSize = new Size(BookDeskUiSpec.PurchaseSupplierLabelWidth, BookDeskUiSpec.Standard.ControlHeight),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.BodyFontPoints, FontStyle.Bold),
                Margin = Padding.Empty
            }, 0, 0);

            _supplier.Dock = DockStyle.None;
            _supplier.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _supplier.Tag = "toolbar-input";
            _supplier.Margin = new Padding(0, 0, 10, 0);
            _supplier.DropDownArrow = true;
            _supplierRow.Controls.Add(_supplier, 1, 0);

            _newOrderButton = UiTheme.CreateAntdButton("新入库单", false);
            _newOrderButton.Width = 112;
            _newOrderButton.Anchor = AnchorStyles.Left;
            _newOrderButton.Tag = "toolbar-action";
            _newOrderButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _newOrderButton.Click += delegate { StartNewOrder(); };
            _supplierRow.Controls.Add(_newOrderButton, 3, 0);

            _clearButton = UiTheme.CreateAntdButton("清空", false);
            _clearButton.Width = 78;
            _clearButton.Anchor = AnchorStyles.Left;
            _clearButton.Tag = "toolbar-action";
            _clearButton.Click += delegate { ClearCartWithConfirmation(); };
            _supplierRow.Controls.Add(_clearButton, 4, 0);

            _scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.Standard.ControlHeight),
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 12, 0, 0),
                Padding = Padding.Empty
            };
            _scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _scanRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _scanRow.Controls.Add(new Label
            {
                Text = "扫码 / 搜索",
                AutoSize = false,
                Width = BookDeskUiSpec.PurchaseScanLabelWidth,
                MinimumSize = new Size(BookDeskUiSpec.PurchaseScanLabelWidth, BookDeskUiSpec.Standard.ControlHeight),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.BodyFontPoints, FontStyle.Bold),
                Margin = Padding.Empty
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
            _scanRow.Controls.Add(_isbn, 1, 0);

            _addButton = UiTheme.CreateAntdButton("加入", true);
            _addButton.Width = 108;
            _addButton.Anchor = AnchorStyles.Left;
            _addButton.Tag = "toolbar-action";
            _addButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _addButton.Click += delegate { AddByIsbn(); };
            _scanRow.Controls.Add(_addButton, 2, 0);

            _pickButton = UiTheme.CreateAntdButton("选择图书", false);
            _pickButton.Width = 118;
            _pickButton.Anchor = AnchorStyles.Left;
            _pickButton.Tag = "toolbar-action";
            _pickButton.Click += delegate { PickBook(); };
            _scanRow.Controls.Add(_pickButton, 3, 0);

            _receivingSection.Controls.Add(_supplierRow, 0, 0);
            _receivingSection.Controls.Add(_scanRow, 0, 1);
            return _receivingSection;
        }

        private Control CreateCartSection()
        {
            ConfigureGrid();

            _cartHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0)
            };
            _cartHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cartHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _cartHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _cartHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseCartHeaderHeight),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 5, 12, 5),
                Margin = Padding.Empty
            };
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _cartHeader.Controls.Add(new Label
            {
                Text = "入库明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            _cartHeader.Controls.Add(new Label
            {
                Text = "数量和进价可直接编辑",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(8, 6, 12, 0)
            }, 1, 0);

            _removeButton = UiTheme.CreateAntdButton("移除选中", false);
            _removeButton.Width = 110;
            _removeButton.Click += delegate { RemoveSelected(); };
            _cartHeader.Controls.Add(_removeButton, 2, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

            _emptySurface = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _emptySurface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _emptySurface.RowStyles.Add(new RowStyle(SizeType.Absolute, BookDeskUiSpec.Standard.TableHeaderHeight));
            _emptySurface.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _emptyHeaderRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 9,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            ConfigureEmptyHeaderColumns(BookDeskUiSpec.Standard);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("序号"), 0, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("店内编码"), 1, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("ISBN"), 2, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("书名"), 3, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("作者"), 4, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("出版社"), 5, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("数量"), 6, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("进价（元）"), 7, 0);
            _emptyHeaderRow.Controls.Add(CreateEmptyHeaderCell("小计（元）"), 8, 0);

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前入库单为空\r\n请扫码、搜索或选择图书";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);
            _emptyState.BackColor = UiTheme.Surface;

            _emptySurface.Controls.Add(_emptyHeaderRow, 0, 0);
            _emptySurface.Controls.Add(_emptyState, 0, 1);

            content.Controls.Add(_grid);
            content.Controls.Add(_emptySurface);
            _emptySurface.BringToFront();

            _cartHost.Controls.Add(_cartHeader, 0, 0);
            _cartHost.Controls.Add(content, 0, 1);
            return _cartHost;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;

            _grid.RowHeight = BookDeskUiSpec.Standard.TableRowHeight;
            _grid.RowHeightHeader = BookDeskUiSpec.Standard.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.EditMode = AntdUI.TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前入库单为空\r\n请扫码、搜索或选择图书";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _indexColumn,
                _selfCodeColumn,
                _isbnColumn,
                _titleColumn,
                _authorColumn,
                _publisherColumn,
                _quantityColumn,
                _unitCostColumn,
                _lineTotalColumn,
                _shelfColumn,
                _stockColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "purchase-lines-ui-spec-v3");

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
            _noteSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseNoteStandardHeight),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 6, 0, 0)
            };
            _noteSection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _noteSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _noteSection.Controls.Add(new Label
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
            _noteSection.Controls.Add(_note, 1, 0);
            return _noteSection;
        }

        private Control CreateTotalsSection()
        {
            _totalsSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseSummaryStandardHeight),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(14, 9, 14, 9),
                Margin = new Padding(0, 8, 0, 0)
            };
            _totalsSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _totalsSection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _metrics = new FlowLayoutPanel
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
            _metrics.Controls.Add(_lineCount);
            _metrics.Controls.Add(_quantityTotal);
            _metrics.Controls.Add(_total);

            _submitButton = UiTheme.CreateAntdButton("确认入库", true);
            _submitButton.Width = BookDeskUiSpec.PurchaseConfirmWidth;
            _submitButton.Height = BookDeskUiSpec.PurchaseConfirmHeight;
            _submitButton.MinimumSize = new Size(BookDeskUiSpec.PurchaseConfirmWidth, BookDeskUiSpec.PurchaseConfirmHeight);
            _submitButton.Margin = new Padding(10, 0, 0, 0);
            _submitButton.Click += delegate { Submit(); };

            _totalsSection.Controls.Add(_metrics, 0, 0);
            _totalsSection.Controls.Add(_submitButton, 1, 0);
            return _totalsSection;
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

        public void ApplyUiSpecProfile(UiSpecProfile profile)
        {
            _profile = profile ?? BookDeskUiSpec.Standard;
            ApplyResponsiveColumns();
        }

        private void ApplyResponsiveColumns()
        {
            var profile = _profile ?? BookDeskUiSpec.Standard;
            var compact = profile.IsCompact;
            var controlHeight = profile.ControlHeight;

            if (_receivingSection != null)
            {
                _receivingSection.Padding = new Padding(profile.ToolbarPadding);
                _receivingSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseToolbarCompactHeight
                        : BookDeskUiSpec.PurchaseToolbarStandardHeight);
            }

            if (_supplierRow != null)
            {
                _supplierRow.MinimumSize = new Size(0, controlHeight);
                if (_supplierRow.ColumnStyles.Count > 1)
                    _supplierRow.ColumnStyles[1].Width = compact
                        ? BookDeskUiSpec.PurchaseSupplierCompactWidth
                        : BookDeskUiSpec.PurchaseSupplierStandardWidth;
            }

            _supplier.Height = controlHeight;
            _supplier.MinimumSize = new Size(
                compact
                    ? BookDeskUiSpec.PurchaseSupplierCompactWidth
                    : BookDeskUiSpec.PurchaseSupplierStandardWidth,
                controlHeight);
            _supplier.Font = UiTheme.Font(profile.BodyFontPoints);
            _supplier.Margin = new Padding(0, 0, profile.ControlGap, 0);

            SetToolbarButton(_newOrderButton, compact ? 100 : 112, profile);
            SetToolbarButton(_clearButton, compact ? 68 : 78, profile);

            if (_scanRow != null)
            {
                _scanRow.MinimumSize = new Size(0, controlHeight);
                _scanRow.Margin = new Padding(0, compact ? 8 : 12, 0, 0);
            }

            _isbn.Height = controlHeight;
            _isbn.MinimumSize = new Size(
                compact
                    ? BookDeskUiSpec.PurchaseSearchCompactMinimumWidth
                    : BookDeskUiSpec.PurchaseSearchMinimumWidth,
                controlHeight);
            _isbn.Font = UiTheme.Font(profile.BodyFontPoints);
            _isbn.Margin = new Padding(0, 0, profile.ControlGap, 0);

            SetToolbarButton(_addButton, compact ? 96 : 108, profile);
            SetToolbarButton(_pickButton, compact ? 106 : 118, profile);
            SetToolbarButton(_removeButton, compact ? 102 : 110, profile);

            if (_cartHost != null)
                _cartHost.Margin = new Padding(0, profile.SectionGap, 0, 0);
            if (_cartHeader != null)
                _cartHeader.MinimumSize = new Size(0, BookDeskUiSpec.PurchaseCartHeaderHeight);

            _grid.RowHeightHeader = profile.TableHeaderHeight;
            _grid.RowHeight = profile.TableRowHeight;
            _grid.Font = UiTheme.Font(profile.TableFontPoints);
            ApplyColumnWidths(profile);
            ConfigureEmptyHeaderColumns(profile);
            if (_emptySurface != null && _emptySurface.RowStyles.Count > 0)
                _emptySurface.RowStyles[0].Height = profile.TableHeaderHeight;
            _emptyState.Font = UiTheme.Font(profile.SecondaryFontPoints);

            if (_noteSection != null)
            {
                _noteSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseNoteCompactHeight
                        : BookDeskUiSpec.PurchaseNoteStandardHeight);
                _noteSection.Padding = compact
                    ? new Padding(10, 4, 10, 4)
                    : new Padding(14, 6, 14, 6);
                _noteSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            _note.Height = controlHeight;
            _note.MinimumSize = new Size(0, controlHeight);
            _note.Font = UiTheme.Font(profile.BodyFontPoints);

            if (_totalsSection != null)
            {
                _totalsSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseSummaryCompactHeight
                        : BookDeskUiSpec.PurchaseSummaryStandardHeight);
                _totalsSection.Padding = compact
                    ? new Padding(10, 7, 10, 7)
                    : new Padding(14, 9, 14, 9);
                _totalsSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            ResizeSummaryLabel(_lineCount, 110, compact ? 38 : 44, profile, false);
            ResizeSummaryLabel(_quantityTotal, 126, compact ? 38 : 44, profile, false);
            ResizeSummaryLabel(_total, 214, compact ? 38 : 44, profile, true);

            if (_submitButton != null)
            {
                _submitButton.Width = BookDeskUiSpec.PurchaseConfirmWidth;
                _submitButton.Height = BookDeskUiSpec.PurchaseConfirmHeight;
                _submitButton.MinimumSize = new Size(
                    BookDeskUiSpec.PurchaseConfirmWidth,
                    BookDeskUiSpec.PurchaseConfirmHeight);
                _submitButton.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
            }

            // Prototype columns stay visible throughout the required 1366+ matrix.
            // Existing shelf/current-stock reference fields return only in the
            // expanded profile so no business information is removed.
            _indexColumn.Visible = true;
            _selfCodeColumn.Visible = true;
            _isbnColumn.Visible = true;
            _titleColumn.Visible = true;
            _authorColumn.Visible = true;
            _publisherColumn.Visible = true;
            _quantityColumn.Visible = true;
            _unitCostColumn.Visible = true;
            _lineTotalColumn.Visible = true;
            _shelfColumn.Visible = string.Equals(profile.Name, "expanded", StringComparison.Ordinal);
            _stockColumn.Visible = string.Equals(profile.Name, "expanded", StringComparison.Ordinal);

            _grid.LoadLayout();
            _grid.Refresh();
        }

        private static Label CreateEmptyHeaderCell(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 6, 0),
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.TableFontPoints, FontStyle.Bold),
                Margin = Padding.Empty
            };
        }

        private void ConfigureEmptyHeaderColumns(UiSpecProfile profile)
        {
            if (_emptyHeaderRow == null)
                return;

            var compact = profile != null && profile.IsCompact;
            _emptyHeaderRow.ColumnStyles.Clear();
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 54 : 60));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 90 : 110));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 96 : 118));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 86 : 110));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 86 : 110));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 70 : 82));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 94 : 112));
            _emptyHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, compact ? 94 : 112));

            foreach (Control control in _emptyHeaderRow.Controls)
                control.Font = UiTheme.Font(profile.TableFontPoints, FontStyle.Bold);
        }

        private void ApplyColumnWidths(UiSpecProfile profile)
        {
            var compact = profile != null && profile.IsCompact;

            _indexColumn.Width = compact ? "54" : "60";
            _selfCodeColumn.Width = compact ? "90" : "110";
            _isbnColumn.Width = compact ? "96" : "118";
            _titleColumn.Width = "fill";
            _titleColumn.MinWidth = "130";
            _titleColumn.MaxWidth = compact ? "260" : "300";
            _authorColumn.Width = compact ? "86" : "110";
            _publisherColumn.Width = compact ? "86" : "110";
            _quantityColumn.Width = compact ? "70" : "82";
            _unitCostColumn.Width = compact ? "94" : "112";
            _lineTotalColumn.Width = compact ? "94" : "112";
            _shelfColumn.Width = compact ? "82" : "96";
            _stockColumn.Width = compact ? "92" : "104";
        }

        private static void SetToolbarButton(
            AntdUI.Button button,
            int width,
            UiSpecProfile profile)
        {
            if (button == null)
                return;

            button.Width = width;
            button.Height = profile.ControlHeight;
            button.MinimumSize = new Size(width, profile.ControlHeight);
            button.Font = UiTheme.Font(profile.BodyFontPoints);
            button.Margin = new Padding(0, 0, profile.ControlGap, 0);
        }

        private static void ResizeSummaryLabel(
            Label label,
            int width,
            int height,
            UiSpecProfile profile,
            bool primary)
        {
            if (label == null)
                return;

            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Padding = new Padding(8, 0, 8, 0);
            label.Font = UiTheme.Font(
                primary
                    ? BookDeskUiSpec.PixelFontToPoints(15)
                    : profile.BodyFontPoints,
                FontStyle.Bold);
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
                Author = book.Author,
                Publisher = book.Publisher,
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
            var index = 1;
            foreach (var row in _rows)
            {
                row.Index = index++;
                total += row.Quantity * row.UnitCostYuan;
                quantity += row.Quantity;
            }

            _lineCount.Text = "图书项  " + _rows.Count;
            _quantityTotal.Text = "入库册数  " + quantity;
            _total.Text = "采购金额  ¥" + total.ToString("0.00");

            if (_emptySurface != null)
            {
                _emptySurface.Visible = _rows.Count == 0;
                if (_rows.Count == 0) _emptySurface.BringToFront();
                else _grid.BringToFront();
            }
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
            public int Index { get; set; }
            public long BookId { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public string Publisher { get; set; }
            public string ShelfCode { get; set; }
            public int CurrentStock { get; set; }
            public int Quantity { get; set; }
            public decimal UnitCostYuan { get; set; }
            public decimal LineTotalYuan { get { return Quantity * UnitCostYuan; } }
        }
    }
}
