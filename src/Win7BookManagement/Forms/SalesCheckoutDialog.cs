using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class SalesCheckoutDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly long _amountDueCent;
        private readonly AntdUI.Select _payment = new AntdUI.Select();
        private readonly AntdUI.InputNumber _received = new AntdUI.InputNumber();
        private readonly Label _change = new Label();
        private readonly Label _cashHint = new Label();
        private readonly List<string> _paymentMethods = new List<string>();

        public string PaymentMethod { get; private set; }
        public long AmountReceivedCent { get; private set; }
        public long ChangeCent { get; private set; }

        public SalesCheckoutDialog(ApplicationServices services, long amountDueCent)
        {
            _services = services;
            _amountDueCent = Math.Max(0, amountDueCent);
            PaymentMethod = "";
            AmountReceivedCent = 0;
            ChangeCent = 0;

            UiTheme.ConfigureForm(this);
            Text = "确认收款";
            StartPosition = FormStartPosition.CenterParent;
            Width = 570;
            Height = 430;
            MinimumSize = new Size(520, 390);
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UiTheme.Background;

            ConfigurePaymentMethods();
            ConfigureMoney();

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

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateBody(), 0, 1);

            AntdUI.Button confirm;
            AntdUI.Button cancel;
            root.Controls.Add(CreateFooter(out confirm, out cancel), 0, 2);

            Controls.Add(root);
            AcceptButton = confirm;
            CancelButton = cancel;

            UiTheme.Apply(this);
            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                UpdatePaymentState();
                _payment.Focus();
            };
        }

        private void ConfigurePaymentMethods()
        {
            var values = _services.Dictionaries.GetActiveValues(DictionaryKeys.PaymentMethod);
            foreach (var value in values)
            {
                var normalized = (value ?? "").Trim();
                if (normalized.Length == 0) continue;
                _paymentMethods.Add(normalized);
                _payment.Items.Add(normalized);
            }

            if (_paymentMethods.Count == 0)
            {
                AddFallbackPayment("微信");
                AddFallbackPayment("支付宝");
                AddFallbackPayment("现金");
            }

            var defaultIndex = 0;
            for (var i = 0; i < _paymentMethods.Count; i++)
            {
                if (string.Equals(
                    _paymentMethods[i],
                    SalesService.DefaultPaymentMethod,
                    StringComparison.OrdinalIgnoreCase))
                {
                    defaultIndex = i;
                    break;
                }
            }

            _payment.SelectedIndex = defaultIndex;
            _payment.DropDownArrow = true;
            _payment.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e)
            {
                UpdatePaymentState();
            };
        }

        private void AddFallbackPayment(string value)
        {
            _paymentMethods.Add(value);
            _payment.Items.Add(value);
        }

        private void ConfigureMoney()
        {
            _received.DecimalPlaces = 2;
            _received.Minimum = 0m;
            _received.Maximum = 10000000m;
            _received.ThousandsSeparator = true;
            _received.Value = Money.ToYuan(_amountDueCent);
            _received.MinimumSize = new Size(0, UiTheme.InputHeight);
            _received.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e)
            {
                UpdateChangePreview();
            };
        }

        private Control CreateHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 17, 22, 15),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "确认收款",
                AutoSize = true,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 7)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "应收金额  ¥" + Money.ToYuan(_amountDueCent).ToString("0.00"),
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.Accent,
                BackColor = UiTheme.AccentSoft,
                Padding = new Padding(10, 6, 10, 6),
                Margin = Padding.Empty
            }, 0, 1);

            return header;
        }

        private Control CreateBody()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 18, 22, 18),
                Margin = new Padding(0, 8, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 4; i++)
                section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AddFieldLabel(section, 0, "收款方式");
            _payment.Dock = DockStyle.Top;
            _payment.MinimumSize = new Size(0, UiTheme.InputHeight);
            _payment.Margin = new Padding(0, 4, 0, 8);
            section.Controls.Add(_payment, 1, 0);

            AddFieldLabel(section, 1, "现金实收");
            _received.Dock = DockStyle.Top;
            _received.Margin = new Padding(0, 4, 0, 8);
            section.Controls.Add(_received, 1, 1);

            AddFieldLabel(section, 2, "找零");
            _change.AutoSize = true;
            _change.MinimumSize = new Size(0, UiTheme.InputHeight);
            _change.TextAlign = ContentAlignment.MiddleLeft;
            _change.Font = UiTheme.Font(11F, FontStyle.Bold);
            _change.ForeColor = UiTheme.Success;
            _change.Margin = new Padding(0, 4, 0, 8);
            section.Controls.Add(_change, 1, 2);

            _cashHint.AutoSize = true;
            _cashHint.MaximumSize = new Size(390, 0);
            _cashHint.ForeColor = UiTheme.TextSecondary;
            _cashHint.Font = UiTheme.Font(8.2F);
            _cashHint.Margin = new Padding(0, 3, 0, 0);
            section.Controls.Add(_cashHint, 0, 3);
            section.SetColumnSpan(_cashHint, 2);

            return section;
        }

        private static void AddFieldLabel(TableLayoutPanel table, int row, string text)
        {
            table.Controls.Add(new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = UiTheme.Font(8.7F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 4, 14, 8)
            }, 0, row);
        }

        private Control CreateFooter(out AntdUI.Button confirm, out AntdUI.Button cancel)
        {
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

            confirm = UiTheme.CreateAntdButton("确认收款", true);
            confirm.Width = 112;
            confirm.Click += Confirm;

            cancel = UiTheme.CreateAntdButton("返回修改", false);
            cancel.Width = 104;
            cancel.DialogResult = DialogResult.Cancel;

            footer.Controls.Add(confirm);
            footer.Controls.Add(cancel);
            return footer;
        }

        private string SelectedPaymentMethod
        {
            get
            {
                var index = _payment.SelectedIndex;
                return index >= 0 && index < _paymentMethods.Count
                    ? _paymentMethods[index]
                    : "";
            }
        }

        private void UpdatePaymentState()
        {
            var method = SelectedPaymentMethod;
            var cash = string.Equals(
                method,
                SalesService.CashPaymentMethod,
                StringComparison.OrdinalIgnoreCase);

            _received.Enabled = cash;
            _received.Value = Money.ToYuan(_amountDueCent);
            _cashHint.Text = cash
                ? "现金收款请输入顾客实际交付金额，系统自动计算找零。"
                : "微信、支付宝等非现金方式按应收金额记账，不需要填写实收与找零。";
            UpdateChangePreview();
        }

        private void UpdateChangePreview()
        {
            var cash = string.Equals(
                SelectedPaymentMethod,
                SalesService.CashPaymentMethod,
                StringComparison.OrdinalIgnoreCase);

            if (!cash)
            {
                _change.Text = "¥0.00";
                return;
            }

            var receivedCent = Money.FromYuan(_received.Value);
            var difference = receivedCent - _amountDueCent;
            _change.Text = difference >= 0
                ? "¥" + Money.ToYuan(difference).ToString("0.00")
                : "还差 ¥" + Money.ToYuan(-difference).ToString("0.00");
            _change.ForeColor = difference >= 0 ? UiTheme.Success : UiTheme.Warning;
        }

        private void Confirm(object sender, EventArgs e)
        {
            var method = SelectedPaymentMethod;
            if (method.Length == 0)
            {
                MessageBox.Show(
                    this,
                    "请选择收款方式。",
                    "还差一项",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _payment.Focus();
                return;
            }

            var cash = string.Equals(
                method,
                SalesService.CashPaymentMethod,
                StringComparison.OrdinalIgnoreCase);

            var receivedCent = cash
                ? Money.FromYuan(_received.Value)
                : _amountDueCent;

            if (cash && receivedCent < _amountDueCent)
            {
                MessageBox.Show(
                    this,
                    "现金实收少于应收金额，还差 ¥" +
                    Money.ToYuan(_amountDueCent - receivedCent).ToString("0.00") + "。",
                    "现金不足",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _received.Focus();
                return;
            }

            PaymentMethod = method;
            AmountReceivedCent = receivedCent;
            ChangeCent = cash ? checked(receivedCent - _amountDueCent) : 0;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
