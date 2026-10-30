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
    public sealed partial class SalesForm : Form, INavigationGuard
    {
        private readonly ApplicationServices _services;
        private Win7BookManagement.Models.SalesReceipt _lastReceipt;
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









        private void AddByIsbn()
        {
            var text = (_isbn.Text ?? "").Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "请先扫描 ISBN，或输入店内编码 / ISBN / 书名关键词。", "还没有商品", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _isbn.Focus();
                return;
            }

            Book exactBook;
            try { exactBook = _services.Books.FindByExactIsbn(text); }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "ISBN 需要核对", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _isbn.SelectAll(); _isbn.Focus(); return;
            }
            if (exactBook != null)
            {
                AddBook(exactBook);
                _isbn.Text = "";
                _isbn.Focus();
                return;
            }

            var matches = _services.Books.SearchActiveByIsbnOrTitle(Isbn.LooksLikeIsbn(text) ? Isbn.Normalize(text) : text);
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
                                UserErrorText.For(cleanupEx);
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
                    try
                    {
                        _lastReceipt = _services.Receipts.Load(orderNo);
                        if (MessageBox.Show(this, "是否打印本单小票？", "打印小票", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            _services.Receipts.Print(this, _lastReceipt);
                    }
                    catch (Exception printEx) { CrashReporter.Handle(this, printEx, "打印销售小票"); }
                    ResetOrder();
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("ui", "界面操作失败", ex);
                MessageBox.Show(this, "结账失败：\r\n" + UserErrorText.For(ex), "请检查销售单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                AppLog.Error("ui", "界面操作失败", ex);
                MessageBox.Show(this, "挂单失败：\r\n" + UserErrorText.For(ex), "无法挂单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void LoadDraft(SalesDraft draft)
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




    }
}
