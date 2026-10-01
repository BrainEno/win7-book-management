using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class PurchaseForm : Form, INavigationGuard, IUiSpecPage
    {
        private readonly ApplicationServices _services;

        private readonly AntdUI.DatePicker _purchaseDate = new AntdUI.DatePicker();
        private readonly AntdUI.Input _orderNo = UiTheme.CreateAntdInput("留空时保存草稿会自动生成");
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
        private readonly Label _statusLabel = new Label();
        private readonly List<PurchaseCartRow> _emptyDisplayRows =
            new List<PurchaseCartRow> { new PurchaseCartRow() };

        private TableLayoutPanel _receivingSection;
        private UiSpecSectionPanel _documentSection;
        private UiSpecSectionPanel _scanSection;
        private FlowLayoutPanel _actionRow;
        private FlowLayoutPanel _headerFields;
        private TableLayoutPanel _scanRow;
        private TableLayoutPanel _cartHost;
        private TableLayoutPanel _cartHeader;
        private TableLayoutPanel _noteSection;
        private TableLayoutPanel _totalsSection;
        private FlowLayoutPanel _metrics;
        private FlowLayoutPanel _bottomActions;

        private AntdUI.Button _newOrderButton;
        private AntdUI.Button _historyButton;
        private AntdUI.Button _previousButton;
        private AntdUI.Button _nextButton;
        private AntdUI.Button _exportButton;
        private AntdUI.Button _clearButton;
        private AntdUI.Button _addButton;
        private AntdUI.Button _pickButton;
        private AntdUI.Button _removeButton;
        private AntdUI.Button _saveDraftButton;
        private AntdUI.Button _reviewButton;
        private AntdUI.Button _unreviewButton;

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
        private long? _currentDocumentId;
        private string _currentStatus = PurchaseService.DraftStatus;
        private bool _loadingDocument;
        private bool _dirty;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;

        public PurchaseForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _purchaseDate.Format = "yyyy-MM-dd";
            _purchaseDate.Value = DateTime.Today;

            _indexColumn = new AntdUI.Column("Index", "序号") { Width = "58", MinWidth = "52", ReadOnly = true };
            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112", MinWidth = "86", ReadOnly = true };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "122", MinWidth = "94", ReadOnly = true };
            _titleColumn = new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "150", MaxWidth = "420", Ellipsis = true, ReadOnly = true };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "118", MinWidth = "84", Ellipsis = true, ReadOnly = true };
            _publisherColumn = new AntdUI.Column("Publisher", "出版社") { Width = "118", MinWidth = "84", Ellipsis = true, ReadOnly = true };
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
                Width = "108",
                MinWidth = "92",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计（元）")
            {
                Width = "110",
                MinWidth = "94",
                ReadOnly = true,
                DisplayFormat = "0.00"
            };

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
                UpdateTotals();
                if (!_loadingDocument) MarkDirty();
            };
            _orderNo.TextChanged += delegate { if (!_loadingDocument) MarkDirty(); };
            _note.TextChanged += delegate { if (!_loadingDocument) MarkDirty(); };
            _supplier.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e)
            {
                if (!_loadingDocument) MarkDirty();
            };

            Resize += delegate { ApplyResponsiveColumns(); };
            _grid.SizeChanged += delegate { ApplyResponsiveColumns(); };

            Shown += delegate
            {
                ReloadSuppliers();
                ResetOrder(false);
                UpdateTotals();
                ApplyResponsiveColumns();
                _isbn.Focus();
            };

            UiTheme.Apply(this);
            ApplyUiSpecProfile(BookDeskUiSpec.Standard);
        }

        public bool CanNavigateAway(IWin32Window owner)
        {
            if (!HasUnsavedWork()) return true;

            var result = MessageBox.Show(
                owner,
                "当前采购单有尚未保存的修改。\r\n\r\n选择“是”保存为草稿后离开；选择“否”放弃这些修改；选择“取消”继续编辑。",
                "采购单尚未保存",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.No) return true;
            return SaveDraftInternal(false);
        }

        private Control CreateReceivingSection()
        {
            _receivingSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            _receivingSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseTopToolbarStandardHeight),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _newOrderButton = CreateToolbarButton("新入库单", 112, true, delegate { StartNewOrder(); });
            _newOrderButton.IconSvg = "FileAddOutlined";
            _historyButton = CreateToolbarButton("历史单据", 106, false, delegate { OpenHistory(); });
            _previousButton = CreateToolbarButton("上一张", 92, false, delegate { NavigateAdjacent(false); });
            _nextButton = CreateToolbarButton("下一张", 92, false, delegate { NavigateAdjacent(true); });
            _exportButton = CreateToolbarButton("导出 Excel", 112, false, delegate { ExportCurrent(); });
            _clearButton = CreateToolbarButton("清空明细", 112, false, delegate { ClearCartWithConfirmation(); });

            _actionRow.Controls.Add(_newOrderButton);
            _actionRow.Controls.Add(_historyButton);
            _actionRow.Controls.Add(_previousButton);
            _actionRow.Controls.Add(_nextButton);
            _actionRow.Controls.Add(_exportButton);
            _actionRow.Controls.Add(_clearButton);

            _documentSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseDocumentStandardHeight),
                ColumnCount = 1,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 28, 14, 28),
                Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0)
            };
            _documentSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _documentSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _headerFields = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _purchaseDate.Tag = "toolbar-input";
            _orderNo.Tag = "toolbar-input";
            _supplier.DropDownArrow = true;
            _supplier.Tag = "toolbar-input";
            _headerFields.Controls.Add(CreateField("采购日期", _purchaseDate, 72, 206));
            _headerFields.Controls.Add(CreateField("采购单号", _orderNo, 72, 258));
            _headerFields.Controls.Add(CreateField("供应商", _supplier, 62, 320));

            _statusLabel.AutoSize = false;
            _statusLabel.Size = new Size(118, BookDeskUiSpec.Standard.ControlHeight);
            _statusLabel.MinimumSize = new Size(118, BookDeskUiSpec.Standard.ControlHeight);
            _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            _statusLabel.Font = UiTheme.Font(8.5F, FontStyle.Bold);
            _statusLabel.Margin = Padding.Empty;
            _headerFields.Controls.Add(_statusLabel);
            _documentSection.Controls.Add(_headerFields, 0, 0);

            _scanSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseScanStandardHeight),
                ColumnCount = 1,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(10),
                Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0)
            };
            _scanSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _scanSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _scanRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.Standard.ControlHeight),
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty,
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
            _isbn.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                AddBySearch();
                e.SuppressKeyPress = true;
            };
            _scanRow.Controls.Add(_isbn, 1, 0);

            _addButton = UiTheme.CreateAntdButton("加入", true);
            _addButton.Width = 104;
            _addButton.Tag = "toolbar-action";
            _addButton.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            _addButton.Click += delegate { AddBySearch(); };
            _scanRow.Controls.Add(_addButton, 2, 0);

            _pickButton = UiTheme.CreateAntdButton("选择图书", false);
            _pickButton.Width = 118;
            _pickButton.Tag = "toolbar-action";
            _pickButton.Click += delegate { PickBook(); };
            _scanRow.Controls.Add(_pickButton, 3, 0);

            _scanSection.Controls.Add(_scanRow, 0, 0);
            _receivingSection.Controls.Add(_actionRow, 0, 0);
            _receivingSection.Controls.Add(_documentSection, 0, 1);
            _receivingSection.Controls.Add(_scanSection, 0, 2);
            return _receivingSection;
        }

        private AntdUI.Button CreateToolbarButton(string text, int width, bool primary, Action action)
        {
            var button = UiTheme.CreateAntdButton(text, primary);
            button.Width = width;
            button.Tag = "toolbar-action";
            button.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            button.Click += delegate { action(); };
            return button;
        }

        private static Control CreateField(string labelText, Control control, int labelWidth, int controlWidth)
        {
            var host = new TableLayoutPanel
            {
                Width = labelWidth + controlWidth,
                Height = BookDeskUiSpec.Standard.ControlHeight,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 10, 0),
                Padding = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            host.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = false,
                Width = labelWidth,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.4F, FontStyle.Bold),
                Margin = Padding.Empty
            }, 0, 0);

            control.Dock = DockStyle.Fill;
            control.Margin = Padding.Empty;
            host.Controls.Add(control, 1, 0);
            return host;
        }

        private Control CreateCartSection()
        {
            ConfigureGrid();

            _cartHost = new UiSpecSectionPanel
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
                Text = "草稿状态下可直接编辑数量和进价",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(8, 6, 12, 0)
            }, 1, 0);

            _removeButton = UiTheme.CreateAntdButton("移除选中", false);
            _removeButton.Width = 106;
            _removeButton.Click += delegate { RemoveSelected(); };
            _cartHeader.Controls.Add(_removeButton, 2, 0);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _emptyState.Dock = DockStyle.None;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前入库单为空\r\n请扫码、搜索或选择图书";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.BackColor = UiTheme.Surface;
            _emptyState.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);
            _emptyState.Margin = Padding.Empty;
            _emptyState.Padding = Padding.Empty;

            content.Controls.Add(_grid);
            _grid.Controls.Add(_emptyState);
            _emptyState.BringToFront();
            _grid.Resize += delegate { LayoutEmptyCartSurface(); };

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
                _lineTotalColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "purchase-lines-ui-spec-v5");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedRow = e.Record as PurchaseCartRow;
            };
            _grid.CellEndEdit += HandleCellEndEdit;
        }

        private bool HandleCellEndEdit(object sender, AntdUI.TableEndEditEventArgs e)
        {
            if (IsReviewed) return false;

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

            MarkDirty();
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
                WrapContents = false,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty
            };
            ConfigureSummaryLabel(_lineCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_total, true);
            _metrics.Controls.Add(_lineCount);
            _metrics.Controls.Add(_quantityTotal);
            _metrics.Controls.Add(_total);

            _bottomActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _saveDraftButton = UiTheme.CreateAntdButton("保存草稿", false);
            _saveDraftButton.Width = 104;
            _saveDraftButton.Click += delegate { SaveDraftInternal(true); };

            _unreviewButton = UiTheme.CreateAntdButton("反复核", false);
            _unreviewButton.Width = 92;
            _unreviewButton.Click += delegate { UnreviewCurrent(); };

            _reviewButton = UiTheme.CreateAntdButton("复核入库", true);
            _reviewButton.Width = 112;
            _reviewButton.Click += delegate { ReviewCurrent(); };

            _bottomActions.Controls.Add(_saveDraftButton);
            _bottomActions.Controls.Add(_unreviewButton);
            _bottomActions.Controls.Add(_reviewButton);

            _totalsSection.Controls.Add(_metrics, 0, 0);
            _totalsSection.Controls.Add(_bottomActions, 1, 0);
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

        private bool IsReviewed
        {
            get { return string.Equals(_currentStatus, PurchaseService.ReviewedStatus, StringComparison.OrdinalIgnoreCase); }
        }

        private DateTime SelectedPurchaseDate
        {
            get { return (_purchaseDate.Value ?? DateTime.Today).Date; }
        }

        private long? SelectedSupplierId
        {
            get
            {
                var index = _supplier.SelectedIndex;
                if (index < 0 || index >= _supplierOptions.Count) return null;
                var selected = _supplierOptions[index];
                return selected.Id > 0 ? (long?)selected.Id : null;
            }
        }

        private void ReloadSuppliers()
        {
            _loadingDocument = true;
            try
            {
                _supplierOptions.Clear();
                _supplier.Items.Clear();
                _supplierOptions.Add(new Supplier { Id = 0, Name = "不区分（默认）", IsActive = true });

                var suppliers = _services.Suppliers.GetAll(false);
                foreach (var supplier in suppliers) _supplierOptions.Add(supplier);
                foreach (var supplier in _supplierOptions) _supplier.Items.Add(supplier.Name);

                if (_supplierOptions.Count > 0) _supplier.SelectedIndex = 0;
            }
            finally
            {
                _loadingDocument = false;
            }
        }

        private void SelectSupplier(long? supplierId, string snapshotName)
        {
            if (!supplierId.HasValue)
            {
                _supplier.SelectedIndex = 0;
                return;
            }

            for (var i = 0; i < _supplierOptions.Count; i++)
            {
                if (_supplierOptions[i].Id != supplierId.Value) continue;
                _supplier.SelectedIndex = i;
                return;
            }

            var historical = new Supplier
            {
                Id = supplierId.Value,
                Name = string.IsNullOrWhiteSpace(snapshotName) ? "历史供应商" : snapshotName,
                IsActive = false
            };
            _supplierOptions.Add(historical);
            _supplier.Items.Add(historical.Name + "（已停用）");
            _supplier.SelectedIndex = _supplierOptions.Count - 1;
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
                _receivingSection.Padding = Padding.Empty;

            if (_actionRow != null)
            {
                _actionRow.Margin = Padding.Empty;
                _actionRow.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseTopToolbarCompactHeight
                        : BookDeskUiSpec.PurchaseTopToolbarStandardHeight);
                _actionRow.WrapContents = compact;
            }

            SetToolbarButton(_newOrderButton, 112, profile);
            SetToolbarButton(_historyButton, 106, profile);
            SetToolbarButton(_previousButton, 92, profile);
            SetToolbarButton(_nextButton, 92, profile);
            SetToolbarButton(_exportButton, 112, profile);
            SetToolbarButton(_clearButton, 112, profile);
            SetToolbarButton(_addButton, 104, profile);
            SetToolbarButton(_pickButton, 118, profile);
            SetToolbarButton(_removeButton, 106, profile);
            SetToolbarButton(_saveDraftButton, compact ? 96 : 104, profile);
            SetToolbarButton(_unreviewButton, compact ? 80 : 88, profile);
            SetToolbarButton(_reviewButton, compact ? 104 : 112, profile);

            _purchaseDate.Height = controlHeight;
            _purchaseDate.MinimumSize = new Size(0, controlHeight);
            _orderNo.Height = controlHeight;
            _orderNo.MinimumSize = new Size(0, controlHeight);
            _supplier.Height = controlHeight;
            _supplier.MinimumSize = new Size(0, controlHeight);
            _isbn.Height = controlHeight;
            _isbn.MinimumSize = new Size(BookDeskUiSpec.PurchaseSearchCompactMinimumWidth, controlHeight);
            _note.Height = controlHeight;
            _note.MinimumSize = new Size(0, controlHeight);

            _purchaseDate.Font = UiTheme.Font(profile.BodyFontPoints);
            _orderNo.Font = UiTheme.Font(profile.BodyFontPoints);
            _supplier.Font = UiTheme.Font(profile.BodyFontPoints);
            _isbn.Font = UiTheme.Font(profile.BodyFontPoints);
            _note.Font = UiTheme.Font(profile.BodyFontPoints);

            ResizeFieldHost(_purchaseDate, 72, compact ? 176 : 206, controlHeight);
            ResizeFieldHost(_orderNo, 72, compact ? 210 : 258, controlHeight);
            ResizeFieldHost(_supplier, 62, compact ? 230 : 320, controlHeight);

            if (_headerFields != null)
                _headerFields.WrapContents = false;

            if (_statusLabel != null)
            {
                _statusLabel.Size = new Size(118, controlHeight);
                _statusLabel.MinimumSize = new Size(118, controlHeight);
                _statusLabel.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
            }

            if (_documentSection != null)
            {
                _documentSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseDocumentCompactHeight
                        : BookDeskUiSpec.PurchaseDocumentStandardHeight);
                var horizontalPadding = compact ? 10 : 14;
                var targetHeight = compact
                    ? BookDeskUiSpec.PurchaseDocumentCompactHeight
                    : BookDeskUiSpec.PurchaseDocumentStandardHeight;
                var verticalPadding = Math.Max(0, (targetHeight - controlHeight) / 2);
                _documentSection.Padding = new Padding(
                    horizontalPadding,
                    verticalPadding,
                    horizontalPadding,
                    verticalPadding);
                _documentSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_scanSection != null)
            {
                _scanSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseScanCompactHeight
                        : BookDeskUiSpec.PurchaseScanStandardHeight);
                var scanHorizontalPadding = compact ? 8 : 10;
                var scanTargetHeight = compact
                    ? BookDeskUiSpec.PurchaseScanCompactHeight
                    : BookDeskUiSpec.PurchaseScanStandardHeight;
                var scanVerticalPadding = Math.Max(0, (scanTargetHeight - controlHeight) / 2);
                _scanSection.Padding = new Padding(
                    scanHorizontalPadding,
                    scanVerticalPadding,
                    scanHorizontalPadding,
                    scanVerticalPadding);
                _scanSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_scanRow != null)
            {
                _scanRow.MinimumSize = new Size(0, controlHeight);
                _scanRow.Margin = Padding.Empty;
                foreach (Control child in _scanRow.Controls)
                {
                    var label = child as Label;
                    if (label != null && string.Equals(label.Text, "扫码 / 搜索", StringComparison.Ordinal))
                    {
                        label.Width = BookDeskUiSpec.PurchaseScanLabelWidth;
                        label.MinimumSize = new Size(BookDeskUiSpec.PurchaseScanLabelWidth, controlHeight);
                        label.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
                    }
                }
            }

            if (_cartHost != null)
                _cartHost.Margin = new Padding(0, profile.SectionGap, 0, 0);

            if (_cartHeader != null)
            {
                _cartHeader.MinimumSize = new Size(
                    0,
                    compact ? 44 : BookDeskUiSpec.PurchaseCartHeaderHeight);
                _cartHeader.Padding = compact
                    ? new Padding(10, 4, 10, 4)
                    : new Padding(12, 5, 12, 5);
            }

            _grid.RowHeightHeader = profile.TableHeaderHeight;
            _grid.RowHeight = profile.TableRowHeight;
            _grid.Font = UiTheme.Font(profile.TableFontPoints);
            ApplyColumnWidths(profile);
            _emptyState.Font = UiTheme.Font(profile.SecondaryFontPoints);
            LayoutEmptyCartSurface();

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

            if (_totalsSection != null)
            {
                _totalsSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseSummaryCompactHeight
                        : BookDeskUiSpec.PurchaseSummaryStandardHeight);
                _totalsSection.Padding = compact
                    ? new Padding(8, 7, 8, 7)
                    : new Padding(12, 9, 12, 9);
                _totalsSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_metrics != null)
                _metrics.WrapContents = false;
            if (_bottomActions != null)
                _bottomActions.WrapContents = false;

            ResizeSummaryLabel(_lineCount, compact ? 84 : 110, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_quantityTotal, compact ? 94 : 126, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_total, compact ? 146 : 190, compact ? 34 : 40, profile, true);

            _indexColumn.Visible = true;
            _selfCodeColumn.Visible = true;
            _isbnColumn.Visible = true;
            _titleColumn.Visible = true;
            _authorColumn.Visible = true;
            _publisherColumn.Visible = true;
            _quantityColumn.Visible = true;
            _unitCostColumn.Visible = true;
            _lineTotalColumn.Visible = true;
            _shelfColumn.Visible = false;
            _stockColumn.Visible = false;

            _grid.LoadLayout();
            _grid.Refresh();
            if (_rows.Count == 0)
            {
                LayoutEmptyCartSurface();
                _emptyState.BringToFront();
            }
        }

        private static void ResizeFieldHost(
            Control control,
            int labelWidth,
            int controlWidth,
            int height)
        {
            if (control == null || control.Parent == null)
                return;

            var host = control.Parent as TableLayoutPanel;
            if (host == null)
                return;

            host.Width = labelWidth + controlWidth;
            host.Height = height;
            host.MinimumSize = new Size(labelWidth + controlWidth, height);
            if (host.ColumnStyles.Count >= 2)
            {
                host.ColumnStyles[0].SizeType = SizeType.Absolute;
                host.ColumnStyles[0].Width = labelWidth;
            }
        }

        private static void SetToolbarButton(AntdUI.Button button, int width, UiSpecProfile profile)
        {
            if (button == null) return;
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
            if (label == null) return;
            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Padding = new Padding(8, 0, 8, 0);
            label.Font = UiTheme.Font(
                primary ? BookDeskUiSpec.PixelFontToPoints(15) : profile.BodyFontPoints,
                FontStyle.Bold);
        }

        private void ApplyColumnWidths(UiSpecProfile profile)
        {
            var compact = profile != null && profile.IsCompact;
            _indexColumn.Width = compact ? "52" : "58";
            _selfCodeColumn.Width = compact ? "86" : "112";
            _isbnColumn.Width = compact ? "94" : "122";
            _titleColumn.Width = "fill";
            _titleColumn.MinWidth = "150";
            _titleColumn.MaxWidth = "420";
            _authorColumn.Width = compact ? "84" : "118";
            _publisherColumn.Width = compact ? "84" : "118";
            _quantityColumn.Width = compact ? "70" : "82";
            _unitCostColumn.Width = compact ? "92" : "108";
            _lineTotalColumn.Width = compact ? "94" : "110";
            _shelfColumn.Width = "88";
            _stockColumn.Width = "92";
        }

        private void LayoutEmptyCartSurface()
        {
            if (_grid == null) return;
            var profile = _profile ?? BookDeskUiSpec.Standard;
            var headerHeight = Math.Min(profile.TableHeaderHeight, Math.Max(0, _grid.ClientSize.Height));
            _emptyState.SetBounds(
                0,
                headerHeight,
                Math.Max(0, _grid.ClientSize.Width),
                Math.Max(0, _grid.ClientSize.Height - headerHeight));
        }

        private void AddBySearch()
        {
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }

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
                MessageBox.Show(
                    this,
                    "没有找到匹配的启用图书。可以点击“选择图书”，再在弹窗底部选择“新增资料”。",
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
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }

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
                if (row.BookId != book.Id) continue;
                row.Quantity += 1;
                _selectedRow = row;
                MarkDirty();
                _grid.Refresh();
                UpdateTotals();
                return;
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
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }
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
            if (!EnsureCanChangeDocument()) return;
            ResetOrder(true);
        }

        private void ClearCartWithConfirmation()
        {
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }
            if (_rows.Count == 0) return;
            if (MessageBox.Show(
                    this,
                    "确定清空当前采购单中的全部明细吗？采购日期、单号、供应商和备注会保留。",
                    "清空明细",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _rows.Clear();
            _selectedRow = null;
            MarkDirty();
        }

        private void ResetOrder(bool focusSearch)
        {
            _loadingDocument = true;
            try
            {
                _currentDocumentId = null;
                _currentStatus = PurchaseService.DraftStatus;
                _orderNo.Text = "";
                _purchaseDate.Value = DateTime.Today;
                _note.Text = "";
                _rows.Clear();
                _selectedRow = null;
                if (_supplierOptions.Count > 0) _supplier.SelectedIndex = 0;
                _isbn.Text = "";
                _dirty = false;
            }
            finally
            {
                _loadingDocument = false;
            }

            UpdateTotals();
            ApplyReviewState();
            if (focusSearch) _isbn.Focus();
        }

        private void OpenHistory()
        {
            if (!EnsureCanChangeDocument()) return;

            using (var dialog = new PurchaseHistoryDialog(_services, _currentDocumentId))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedDocumentId.HasValue)
                    LoadDocument(dialog.SelectedDocumentId.Value);
            }
        }

        private void NavigateAdjacent(bool next)
        {
            if (!_currentDocumentId.HasValue)
            {
                OpenHistory();
                return;
            }
            if (!EnsureCanChangeDocument()) return;

            var target = _services.Purchases.GetAdjacentDocumentId(_currentDocumentId.Value, next);
            if (!target.HasValue)
            {
                MessageBox.Show(
                    this,
                    next ? "已经是最后一张采购单。" : "已经是第一张采购单。",
                    "没有更多单据",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            LoadDocument(target.Value);
        }

        private void LoadDocument(long id)
        {
            var document = _services.Purchases.GetDocument(id);
            if (document == null)
            {
                MessageBox.Show(this, "采购单不存在或已被移除。", "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _loadingDocument = true;
            try
            {
                _currentDocumentId = document.Id;
                _currentStatus = document.Status;
                _orderNo.Text = document.OrderNo;
                _purchaseDate.Value = document.PurchasedAt.Date;
                _note.Text = document.Note;
                SelectSupplier(document.SupplierId, document.SupplierName);

                _rows.Clear();
                foreach (var line in document.Lines)
                {
                    _rows.Add(new PurchaseCartRow
                    {
                        BookId = line.BookId,
                        SelfCode = line.SelfCode,
                        Isbn = line.Isbn,
                        Title = line.Title,
                        Author = line.Author,
                        Publisher = line.Publisher,
                        ShelfCode = line.ShelfCode,
                        CurrentStock = line.CurrentStock,
                        Quantity = line.Quantity,
                        UnitCostYuan = Money.ToYuan(line.UnitCostCent)
                    });
                }
                _selectedRow = _rows.Count > 0 ? _rows[0] : null;
                if (_selectedRow != null) _grid.SetSelected(_selectedRow, false);
                _dirty = false;
            }
            finally
            {
                _loadingDocument = false;
            }

            UpdateTotals();
            ApplyReviewState();
        }

        private bool SaveDraftInternal(bool showMessage)
        {
            if (IsReviewed)
            {
                MessageBox.Show(this, "已复核采购单不能直接保存修改。请先点击“反复核”。", "单据已复核", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                var document = _services.Purchases.SaveDraft(
                    _currentDocumentId,
                    _orderNo.Text,
                    SelectedPurchaseDate,
                    SelectedSupplierId,
                    BuildLineInputs(),
                    _note.Text);

                LoadDocument(document.Id);
                if (showMessage)
                {
                    MessageBox.Show(
                        this,
                        "草稿已保存。\r\n采购单号：" + document.OrderNo + "\r\n库存尚未变化，复核后才会正式入库。",
                        "草稿已保存",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存草稿失败：\r\n" + ex.Message, "请检查采购单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void ReviewCurrent()
        {
            if (IsReviewed)
            {
                MessageBox.Show(this, "当前采购单已经复核。", "无需重复复核", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_rows.Count == 0)
            {
                MessageBox.Show(this, "采购单至少需要一项图书才能复核入库。", "无法复核", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!SaveDraftInternal(false)) return;
            if (!_currentDocumentId.HasValue) return;

            var quantity = 0;
            foreach (var row in _rows) quantity += row.Quantity;
            if (MessageBox.Show(
                    this,
                    "复核后，本单 " + quantity + " 册图书会正式增加库存并写入库存流水。\r\n\r\n确定复核入库吗？",
                    "复核采购单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var orderNo = _services.Purchases.Review(_currentDocumentId.Value);
                LoadDocument(_currentDocumentId.Value);
                MessageBox.Show(
                    this,
                    "复核完成。\r\n采购单号：" + orderNo + "\r\n库存已正式增加。",
                    "采购已入库",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复核失败：\r\n" + ex.Message, "采购未入库", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UnreviewCurrent()
        {
            if (!_currentDocumentId.HasValue || !IsReviewed)
            {
                MessageBox.Show(this, "当前采购单尚未复核。", "无需反复核", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    this,
                    "反复核会撤销本单对库存造成的增加，并写入一条反向库存流水。\r\n如果这些库存已经被后续销售或退货占用，系统会拒绝反复核。\r\n\r\n确定继续吗？",
                    "反复核采购单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                _services.Purchases.Unreview(_currentDocumentId.Value);
                LoadDocument(_currentDocumentId.Value);
                MessageBox.Show(
                    this,
                    "反复核完成。当前单据已恢复为草稿，可以继续修改后再次复核。",
                    "已取消复核",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "反复核失败：\r\n" + ex.Message, "无法取消复核", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportCurrent()
        {
            if (!_currentDocumentId.HasValue || HasUnsavedWork())
            {
                if (IsReviewed)
                {
                    MessageBox.Show(this, "请先重新打开当前已复核单据后再导出。", "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var save = MessageBox.Show(
                    this,
                    "导出前需要先保存当前采购草稿，是否继续？",
                    "保存并导出",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (save != DialogResult.Yes || !SaveDraftInternal(false)) return;
            }

            var document = _currentDocumentId.HasValue
                ? _services.Purchases.GetDocument(_currentDocumentId.Value)
                : null;
            if (document == null) return;

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "导出采购单";
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.FileName = "采购单-" + SanitizeFileName(document.OrderNo) + ".xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _services.Excel.Export(BuildExportTable(document), dialog.FileName, "采购单");
                    MessageBox.Show(this, "采购单已导出：\r\n" + dialog.FileName, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：\r\n" + ex.Message, "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static DataTable BuildExportTable(PurchaseDocument document)
        {
            var table = new DataTable();
            table.Columns.Add("采购单号", typeof(string));
            table.Columns.Add("采购日期", typeof(string));
            table.Columns.Add("状态", typeof(string));
            table.Columns.Add("供应商", typeof(string));
            table.Columns.Add("店内编码", typeof(string));
            table.Columns.Add("ISBN", typeof(string));
            table.Columns.Add("书名", typeof(string));
            table.Columns.Add("作者", typeof(string));
            table.Columns.Add("出版社", typeof(string));
            table.Columns.Add("数量", typeof(int));
            table.Columns.Add("进价（元）", typeof(decimal));
            table.Columns.Add("小计（元）", typeof(decimal));
            table.Columns.Add("备注", typeof(string));

            if (document.Lines.Count == 0)
            {
                table.Rows.Add(
                    document.OrderNo,
                    document.PurchasedAt.ToString("yyyy-MM-dd"),
                    document.StatusText,
                    document.SupplierName,
                    "", "", "", "", "", 0, 0m, 0m, document.Note);
                return table;
            }

            foreach (var line in document.Lines)
            {
                table.Rows.Add(
                    document.OrderNo,
                    document.PurchasedAt.ToString("yyyy-MM-dd"),
                    document.StatusText,
                    document.SupplierName,
                    line.SelfCode,
                    line.Isbn,
                    line.Title,
                    line.Author,
                    line.Publisher,
                    line.Quantity,
                    Money.ToYuan(line.UnitCostCent),
                    Money.ToYuan(line.LineTotalCent),
                    document.Note);
            }
            return table;
        }

        private static string SanitizeFileName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value) ? "采购单" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result;
        }

        private IList<TransactionLineInput> BuildLineInputs()
        {
            var result = new List<TransactionLineInput>();
            foreach (var row in _rows)
            {
                if (row.Quantity <= 0)
                    throw new InvalidOperationException("《" + row.Title + "》的入库数量必须大于 0。");
                if (row.UnitCostYuan < 0)
                    throw new InvalidOperationException("《" + row.Title + "》的进价不能为负数。");

                result.Add(new TransactionLineInput
                {
                    BookId = row.BookId,
                    Quantity = row.Quantity,
                    UnitPriceCent = Money.FromYuan(row.UnitCostYuan)
                });
            }
            return result;
        }

        private bool EnsureCanChangeDocument()
        {
            if (!HasUnsavedWork()) return true;

            var result = MessageBox.Show(
                this,
                "当前采购单有尚未保存的修改。\r\n\r\n选择“是”先保存草稿；选择“否”放弃修改并继续；选择“取消”留在当前单据。",
                "切换采购单",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.No) return true;
            return SaveDraftInternal(false);
        }

        private bool HasUnsavedWork()
        {
            if (_dirty) return true;
            if (_currentDocumentId.HasValue) return false;
            return _rows.Count > 0 ||
                   !string.IsNullOrWhiteSpace(_orderNo.Text) ||
                   !string.IsNullOrWhiteSpace(_note.Text);
        }

        private void MarkDirty()
        {
            if (_loadingDocument || IsReviewed) return;
            _dirty = true;
            UpdateStatusLabel();
        }

        private void ApplyReviewState()
        {
            var editable = !IsReviewed;

            _purchaseDate.Enabled = editable;
            _orderNo.Enabled = editable;
            _supplier.Enabled = editable;
            _isbn.Enabled = editable;
            _note.Enabled = editable;
            _addButton.Enabled = editable;
            _pickButton.Enabled = editable;
            _clearButton.Enabled = editable;
            _removeButton.Enabled = editable;
            _saveDraftButton.Enabled = editable;
            _reviewButton.Enabled = editable;
            _unreviewButton.Enabled = IsReviewed;

            _quantityColumn.ReadOnly = !editable;
            _unitCostColumn.ReadOnly = !editable;
            _grid.Refresh();

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null) return;

            if (IsReviewed)
            {
                _statusLabel.Text = "已复核";
                _statusLabel.ForeColor = Color.FromArgb(39, 126, 71);
                _statusLabel.BackColor = Color.FromArgb(237, 248, 240);
            }
            else
            {
                _statusLabel.Text = _dirty ? "草稿 · 未保存" : "草稿";
                _statusLabel.ForeColor = UiTheme.TextSecondary;
                _statusLabel.BackColor = UiTheme.SurfaceMuted;
            }
        }

        private void ShowReviewedReadOnlyHint()
        {
            MessageBox.Show(
                this,
                "已复核采购单为只读状态。需要修改时，请先点击底部“反复核”。",
                "单据已复核",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

            var empty = _rows.Count == 0;
            _emptyState.Visible = empty;
            if (empty)
            {
                _grid.DataSource = _emptyDisplayRows;
                LayoutEmptyCartSurface();
                _emptyState.BringToFront();
            }
            else
            {
                _grid.DataSource = _rows;
            }

            UpdateStatusLabel();
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
