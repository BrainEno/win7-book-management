using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed partial class PurchaseForm : Form, INavigationGuard, IUiSpecPage
    {
        private readonly ApplicationServices _services;

        private readonly AntdUI.DatePicker _purchaseDate = new AntdUI.DatePicker();
        private readonly AntdUI.Input _orderNo = UiTheme.CreateAntdInput("留空时保存草稿会自动生成");
        private readonly AntdUI.Select _supplier = new AntdUI.Select();
        private readonly AntdUI.InputNumber _orderDiscount = new AntdUI.InputNumber();
        private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("可选：填写到货批次、物流或其他备注");
        private readonly StringBuilder _scanBuffer = new StringBuilder();
        private readonly PersistentAntdTable _grid = new PersistentAntdTable();
        private readonly BindingList<PurchaseCartRow> _rows = new BindingList<PurchaseCartRow>();
        private readonly List<Supplier> _supplierOptions = new List<Supplier>();
        private readonly Label _lineCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _originalTotal = new Label();
        private readonly Label _discountTotal = new Label();
        private readonly Label _orderDiscountLabel = new Label();
        private readonly Label _total = new Label();
        private readonly Label _emptyState = new Label();
        private readonly Label _statusLabel = new Label();
        private readonly List<PurchaseCartRow> _emptyDisplayRows =
            new List<PurchaseCartRow> { new PurchaseCartRow() };

        private TableLayoutPanel _receivingSection;
        private UiSpecSectionPanel _documentSection;
        private FlowLayoutPanel _actionRow;
        private TableLayoutPanel _headerFields;
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
        private AntdUI.Button _lookupButton;
        private AntdUI.Button _clearButton;
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
        private readonly AntdUI.Column _discountColumn;
        private readonly AntdUI.Column _discountedUnitCostColumn;
        private readonly AntdUI.Column _lineTotalColumn;

        private PurchaseCartRow _selectedRow;
        private long? _currentDocumentId;
        private string _currentStatus = PurchaseService.DraftStatus;
        private bool _loadingDocument;
        private bool _dirty;
        private DateTime _baselinePurchaseDate = DateTime.Today;
        private string _baselineOrderNo = "";
        private long? _baselineSupplierId;
        private string _baselineNote = "";
        private int _baselineOrderDiscountBasisPoints = 10000;
        private PurchaseNavigationState _navigationState;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;
        private DateTime _lastScanCharAt = DateTime.MinValue;
        private bool _scannerCaptureEnabled = true;

        public PurchaseForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            KeyPreview = true;

            _purchaseDate.Format = "yyyy-MM-dd";
            _purchaseDate.Value = DateTime.Today;
            _purchaseDate.Leave += delegate { UpdateStatusLabel(); };

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
            _unitCostColumn = new AntdUI.Column("BaseUnitCostYuan", "原进价（元）")
            {
                Width = "108",
                MinWidth = "92",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _discountColumn = new AntdUI.Column("DiscountPercent", "单品折扣%")
            {
                Width = "96",
                MinWidth = "86",
                ReadOnly = false,
                DisplayFormat = "0.00",
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            };
            _discountedUnitCostColumn = new AntdUI.Column("DiscountedUnitCostYuan", "折后进价")
            {
                Width = "96",
                MinWidth = "86",
                ReadOnly = true,
                DisplayFormat = "0.00"
            };
            _lineTotalColumn = new AntdUI.Column("LineTotalYuan", "小计（元）")
            {
                Width = "110",
                MinWidth = "94",
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
            KeyDown += HandlePurchaseShortcut;
            KeyPress += HandleScannerKeyPress;

            Resize += delegate { ApplyResponsiveColumns(); };
            _grid.SizeChanged += delegate { ApplyResponsiveColumns(); };

            Shown += delegate
            {
                ReloadSuppliers();
                ResetOrder(false);
                UpdateTotals();
                ApplyResponsiveColumns();
                FocusGridForScanner();
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

        private void ConfigureOrderDiscount()
        {
            _orderDiscount.Minimum = 0m;
            _orderDiscount.Maximum = 100m;
            _orderDiscount.DecimalPlaces = 2;
            _orderDiscount.Value = 100m;
            _orderDiscount.Width = 112;
            _orderDiscount.MinimumSize = new Size(96, UiTheme.InputHeight);
            _orderDiscount.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e)
            {
                foreach (var row in _rows)
                    row.OrderDiscountPercent = _orderDiscount.Value;
                if (!_loadingDocument) MarkDirty();
                _grid.Refresh();
                UpdateTotals();
            };
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
            else if (string.Equals(e.Column.Key, "BaseUnitCostYuan", StringComparison.Ordinal))
            {
                decimal price;
                if (!decimal.TryParse(e.Value, out price) || price < 0)
                {
                    MessageBox.Show(this, "本次进价必须是有效的非负金额。", "金额格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.BaseUnitCostYuan = price;
            }
            else if (string.Equals(e.Column.Key, "DiscountPercent", StringComparison.Ordinal))
            {
                decimal discount;
                if (!decimal.TryParse(e.Value, out discount) || discount < 0m || discount > 100m)
                {
                    MessageBox.Show(this, "单品折扣必须在 0 到 100 之间，可保留两位小数。", "折扣格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                row.DiscountPercent = discount;
            }

            MarkDirty();
            _grid.Refresh();
            UpdateTotals();
            _scannerCaptureEnabled = true;
            _scanBuffer.Clear();
            return true;
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















        private void HandleScannerKeyPress(object sender, KeyPressEventArgs e)
        {
            if (IsReviewed ||
                !_scannerCaptureEnabled ||
                _grid == null ||
                !_grid.ContainsFocus)
                return;

            if (e.KeyChar == '\r' || e.KeyChar == '\t')
            {
                if (_scanBuffer.Length == 0)
                    return;

                var identifier = _scanBuffer.ToString().Trim();
                _scanBuffer.Clear();
                e.Handled = true;

                if (identifier.Length > 0)
                    ProcessScannedIdentifier(identifier);
                return;
            }

            if (e.KeyChar == '\b')
            {
                if (_scanBuffer.Length > 0)
                    _scanBuffer.Length -= 1;
                e.Handled = true;
                return;
            }

            if (char.IsControl(e.KeyChar))
                return;

            var now = DateTime.UtcNow;
            if (_lastScanCharAt != DateTime.MinValue &&
                (now - _lastScanCharAt).TotalMilliseconds > 900)
            {
                _scanBuffer.Clear();
            }

            _lastScanCharAt = now;
            _scanBuffer.Append(e.KeyChar);
            e.Handled = true;
        }

        private void ProcessScannedIdentifier(string identifier)
        {
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }

            Book book;
            try { book = _services.Books.FindByExactIdentifier(identifier); }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "扫码内容需要核对", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                FocusGridForScanner();
                return;
            }
            if (book != null)
            {
                AddBook(book);
                FocusGridForScanner();
                return;
            }

            OpenAdvancedBookLookup(identifier);
        }

        private void OpenAdvancedBookLookup(string initialIdentifier)
        {
            if (IsReviewed)
            {
                ShowReviewedReadOnlyHint();
                return;
            }

            _scannerCaptureEnabled = false;
            _scanBuffer.Clear();

            using (var dialog = new AdvancedBookLookupDialog(
                _services,
                initialIdentifier,
                delegate(Book book) { AddBook(book); }))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK &&
                    dialog.SelectedBook != null)
                {
                    AddBook(dialog.SelectedBook);
                }
            }

            FocusGridForScanner();
        }

        private void FocusGridForScanner()
        {
            _scanBuffer.Clear();
            _scannerCaptureEnabled = !IsReviewed;
            if (_grid != null && _grid.CanFocus)
                _grid.Focus();
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
                BaseUnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent),
                DiscountPercent = 100m,
                OrderDiscountPercent = _orderDiscount.Value
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
                _orderDiscount.Value = 100m;
                _rows.Clear();
                _selectedRow = null;
                if (_supplierOptions.Count > 0) _supplier.SelectedIndex = 0;
                _scanBuffer.Clear();
                CaptureBaseline();
                _dirty = false;
            }
            finally
            {
                _loadingDocument = false;
            }

            UpdateTotals();
            ApplyReviewState();
            UpdateNavigationState();
            if (focusSearch) FocusGridForScanner();
        }













































        private static int PercentToBasisPoints(decimal percent)
        {
            var clamped = Math.Max(0m, Math.Min(100m, percent));
            return decimal.ToInt32(decimal.Round(
                clamped * 100m, 0, MidpointRounding.AwayFromZero));
        }

        private static long ApplyBasisPoints(long amountCent, int basisPoints)
        {
            if (amountCent <= 0 || basisPoints <= 0) return 0;
            if (basisPoints >= 10000) return amountCent;
            return decimal.ToInt64(decimal.Round(
                amountCent * (basisPoints / 10000m),
                0, MidpointRounding.AwayFromZero));
        }
    }
}
