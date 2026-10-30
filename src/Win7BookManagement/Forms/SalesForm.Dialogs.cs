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
    public sealed partial class SalesForm
    {
internal sealed class SalesSettlementDialog : Form
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

internal sealed class SalesDraftPickerDialog : Form
        {
            private readonly ApplicationServices _services;
            private readonly AntdUI.Table _grid = new AntdUI.Table();
            private readonly BindingList<DraftRow> _rows = new BindingList<DraftRow>();
            private DraftRow _selected;

            public long? SelectedDraftId { get; private set; }

            public SalesDraftPickerDialog(
                ApplicationServices services,
                IList<SalesDraftSummary> drafts)
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

                public DraftRow(SalesDraftSummary source)
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

internal sealed class DraftRow
            {
                public long Id { get; private set; }
                public string UpdatedAtText { get; private set; }
                public string DraftNo { get; private set; }
                public int ItemCount { get; private set; }
                public int QuantityTotal { get; private set; }
                public decimal TotalYuan { get; private set; }
                public string Note { get; private set; }

                public DraftRow(SalesDraftSummary source)
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

internal sealed class SalesCartRow
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
