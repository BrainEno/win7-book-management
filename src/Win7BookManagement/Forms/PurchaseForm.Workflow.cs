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
    public sealed partial class PurchaseForm
    {
private void OpenHistory()
        {
            if (!EnsureCanChangeDocument()) return;

            using (var dialog = new PurchaseHistoryDialog(_services, _currentDocumentId))
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK && dialog.SelectedDocumentId.HasValue)
                {
                    if (dialog.CopyAsNewRequested)
                    {
                        try
                        {
                            var copy = _services.Purchases.CopyToNewDraft(
                                dialog.SelectedDocumentId.Value,
                                DateTime.Today);
                            LoadDocument(copy.Id);
                            MessageBox.Show(
                                this,
                                "已复制为新的采购草稿。\r\n新单号：" + copy.OrderNo + "\r\n采购日期已设为今天，供应商、图书、数量、进价和折扣已复制。",
                                "已复制为新单",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            AppLog.Error("ui", "界面操作失败", ex);
                            MessageBox.Show(
                                this,
                                "复制采购单失败：\r\n" + UserErrorText.For(ex),
                                "无法复制",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        LoadDocument(dialog.SelectedDocumentId.Value);
                    }
                }
                else if (dialog.DeletedCurrentDocument)
                {
                    ResetOrder(true);
                }
                else
                {
                    UpdateNavigationState();
                }
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

            var state = _navigationState != null &&
                        _navigationState.CurrentId == _currentDocumentId.Value
                ? _navigationState
                : _services.Purchases.GetNavigationState(_currentDocumentId.Value);

            var target = next ? state.NextId : state.PreviousId;
            if (!target.HasValue)
            {
                UpdateNavigationState();
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
                _orderDiscount.Value = document.OrderDiscountBasisPoints / 100m;
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
                        BaseUnitCostYuan = Money.ToYuan(line.BaseUnitCostCent),
                        DiscountPercent = line.LineDiscountBasisPoints / 100m,
                        OrderDiscountPercent = document.OrderDiscountBasisPoints / 100m
                    });
                }
                _selectedRow = _rows.Count > 0 ? _rows[0] : null;
                if (_selectedRow != null) _grid.SetSelected(_selectedRow, false);
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
                    _note.Text,
                    PercentToBasisPoints(_orderDiscount.Value));

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
                AppLog.Error("ui", "界面操作失败", ex);
                MessageBox.Show(this, "保存草稿失败：\r\n" + UserErrorText.For(ex), "请检查采购单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                AppLog.Error("ui", "界面操作失败", ex);
                MessageBox.Show(this, "复核失败：\r\n" + UserErrorText.For(ex), "采购未入库", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                AppLog.Error("ui", "界面操作失败", ex);
                MessageBox.Show(this, "反复核失败：\r\n" + UserErrorText.For(ex), "无法取消复核", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    var data = BuildExportTable(document); var path = dialog.FileName;
                    UiTask.Run(this, "导出采购单", (progress, token) =>
                    {
                        _services.Excel.Export(data, path, "采购单", progress, token); return path;
                    }, result => MessageBox.Show(this, "采购单已导出：\r\n" + result, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information));
                }
                catch (Exception ex)
                {
                    AppLog.Error("ui", "界面操作失败", ex);
                    MessageBox.Show(this, "导出失败：\r\n" + UserErrorText.For(ex), "无法导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            table.Columns.Add("原进价（元）", typeof(decimal));
            table.Columns.Add("单品折扣%", typeof(decimal));
            table.Columns.Add("折后进价（元）", typeof(decimal));
            table.Columns.Add("整单折扣%", typeof(decimal));
            table.Columns.Add("实际进价（元）", typeof(decimal));
            table.Columns.Add("小计（元）", typeof(decimal));
            table.Columns.Add("备注", typeof(string));

            if (document.Lines.Count == 0)
            {
                table.Rows.Add(
                    document.OrderNo,
                    document.PurchasedAt.ToString("yyyy-MM-dd"),
                    document.StatusText,
                    document.SupplierName,
                    "", "", "", "", "", 0,
                    0m, 100m, 0m, document.OrderDiscountBasisPoints / 100m,
                    0m, 0m, document.Note);
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
                    Money.ToYuan(line.BaseUnitCostCent),
                    line.LineDiscountBasisPoints / 100m,
                    Money.ToYuan(line.LineDiscountedUnitCostCent),
                    document.OrderDiscountBasisPoints / 100m,
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
                if (row.BaseUnitCostYuan < 0)
                    throw new InvalidOperationException("《" + row.Title + "》的进价不能为负数。");
                if (row.DiscountPercent < 0m || row.DiscountPercent > 100m)
                    throw new InvalidOperationException("《" + row.Title + "》的单品折扣必须在 0 到 100 之间。");

                var baseCent = Money.FromYuan(row.BaseUnitCostYuan);
                result.Add(new TransactionLineInput
                {
                    BookId = row.BookId,
                    Quantity = row.Quantity,
                    UnitPriceCent = baseCent,
                    BaseUnitPriceCent = baseCent,
                    DiscountBasisPoints = PercentToBasisPoints(row.DiscountPercent)
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
            if (IsReviewed) return false;
            if (_dirty || HeaderDiffersFromBaseline()) return true;
            if (_currentDocumentId.HasValue) return false;
            return _rows.Count > 0;
        }

private bool HeaderDiffersFromBaseline()
        {
            return SelectedPurchaseDate != _baselinePurchaseDate ||
                   SelectedSupplierId != _baselineSupplierId ||
                   PercentToBasisPoints(_orderDiscount.Value) != _baselineOrderDiscountBasisPoints ||
                   !string.Equals(
                       (_orderNo.Text ?? "").Trim(),
                       _baselineOrderNo,
                       StringComparison.Ordinal) ||
                   !string.Equals(
                       (_note.Text ?? "").Trim(),
                       _baselineNote,
                       StringComparison.Ordinal);
        }

private void CaptureBaseline()
        {
            _baselinePurchaseDate = SelectedPurchaseDate;
            _baselineOrderNo = (_orderNo.Text ?? "").Trim();
            _baselineSupplierId = SelectedSupplierId;
            _baselineNote = (_note.Text ?? "").Trim();
            _baselineOrderDiscountBasisPoints = PercentToBasisPoints(_orderDiscount.Value);
        }

private void UpdateNavigationState()
        {
            _navigationState = null;

            if (!_currentDocumentId.HasValue)
            {
                if (_previousButton != null) _previousButton.Enabled = false;
                if (_nextButton != null) _nextButton.Enabled = false;
                return;
            }

            _navigationState = _services.Purchases.GetNavigationState(_currentDocumentId.Value);
            if (_previousButton != null)
                _previousButton.Enabled = _navigationState.HasPrevious;
            if (_nextButton != null)
                _nextButton.Enabled = _navigationState.HasNext;
        }

private void HandlePurchaseShortcut(object sender, KeyEventArgs e)
        {
            if (_scannerCaptureEnabled &&
                _grid != null &&
                _grid.ContainsFocus &&
                _scanBuffer.Length > 0 &&
                (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab))
            {
                var identifier = _scanBuffer.ToString().Trim();
                _scanBuffer.Clear();
                e.SuppressKeyPress = true;
                e.Handled = true;

                if (identifier.Length > 0)
                    ProcessScannedIdentifier(identifier);
                return;
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveDraftInternal(true);
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.N)
            {
                StartNewOrder();
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.Alt && e.KeyCode == Keys.Left)
            {
                if (_previousButton == null || _previousButton.Enabled)
                    NavigateAdjacent(false);
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.Alt && e.KeyCode == Keys.Right)
            {
                if (_nextButton == null || _nextButton.Enabled)
                    NavigateAdjacent(true);
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F3)
            {
                OpenAdvancedBookLookup("");
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F4)
            {
                OpenHistory();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
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
            _orderDiscount.Enabled = editable;
            _note.Enabled = editable;
            _lookupButton.Enabled = editable;
            _clearButton.Enabled = editable;
            _removeButton.Enabled = editable;
            _saveDraftButton.Enabled = editable;
            _reviewButton.Enabled = editable;
            _unreviewButton.Enabled = IsReviewed;

            _quantityColumn.ReadOnly = !editable;
            _unitCostColumn.ReadOnly = !editable;
            _discountColumn.ReadOnly = !editable;
            _grid.Refresh();
            _scannerCaptureEnabled = editable;
            if (!editable) _scanBuffer.Clear();

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
                _statusLabel.Text = (_dirty || HeaderDiffersFromBaseline())
                    ? "草稿 · 未保存"
                    : "草稿";
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
            long originalCent = 0;
            long totalCent = 0;
            var quantity = 0;
            var index = 1;
            foreach (var row in _rows)
            {
                row.Index = index++;
                row.OrderDiscountPercent = _orderDiscount.Value;
                var baseCent = Money.FromYuan(row.BaseUnitCostYuan);
                var lineCent = ApplyBasisPoints(baseCent, PercentToBasisPoints(row.DiscountPercent));
                var finalCent = ApplyBasisPoints(lineCent, PercentToBasisPoints(_orderDiscount.Value));
                originalCent = checked(originalCent + checked((long)row.Quantity * baseCent));
                totalCent = checked(totalCent + checked((long)row.Quantity * finalCent));
                quantity += row.Quantity;
            }

            _lineCount.Text = "图书项  " + _rows.Count;
            _quantityTotal.Text = "入库册数  " + quantity;
            _originalTotal.Text = "原金额  ¥" + Money.ToYuan(originalCent).ToString("0.00");
            _discountTotal.Text = "优惠  ¥" + Money.ToYuan(originalCent - totalCent).ToString("0.00");
            _total.Text = "采购金额  ¥" + Money.ToYuan(totalCent).ToString("0.00");

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
    }
}
