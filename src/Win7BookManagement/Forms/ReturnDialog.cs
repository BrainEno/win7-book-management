using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class ReturnDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly string _kind;
        private readonly long _sourceDocumentId;
        private readonly BindingList<ReturnableDocumentLine> _lines;
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _note = new TextBox();
        private readonly Label _total = new Label();

        public ReturnDialog(ApplicationServices services, string kind, long sourceDocumentId, string sourceDocumentNo)
        {
            _services = services;
            _kind = kind;
            _sourceDocumentId = sourceDocumentId;

            var isSale = kind == "sale";
            if (!isSale && kind != "purchase")
                throw new InvalidOperationException("未知退货来源类型。");

            var sourceLines = _services.Documents.GetReturnableLines(kind, sourceDocumentId);
            _lines = new BindingList<ReturnableDocumentLine>();
            foreach (var line in sourceLines)
                _lines.Add(line);

            Text = (isSale ? "销售退货" : "采购退货") + " - " + sourceDocumentNo;
            StartPosition = FormStartPosition.CenterParent;
            Width = 880;
            Height = 600;
            MinimumSize = new Size(760, 500);
            BackColor = UiTheme.Background;

            var info = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 12, 18, 8)
            };
            var title = new Label
            {
                Text = isSale ? "从原销售单选择要退回的图书" : "从原采购单选择要退给供应商的图书",
                Dock = DockStyle.Top,
                Height = 28,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            var hint = new Label
            {
                Text = isSale
                    ? "本次退货会按原销售价格退款，并把库存加回。已退过的数量不能重复退。"
                    : "本次退货会按原采购进价计算，并扣减库存；当前库存不足时不能退给供应商。",
                Dock = DockStyle.Top,
                Height = 38,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F)
            };
            info.Controls.Add(hint);
            info.Controls.Add(title);

            ConfigureGrid(isSale);

            var notePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                ColumnCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 8, 12, 6)
            };
            notePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            notePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            notePanel.Controls.Add(new Label
            {
                Text = "退货原因",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, 0, 0);
            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(8, 3, 0, 3);
            notePanel.Controls.Add(_note, 1, 0);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0, 12, 12, 10)
            };
            var confirm = new Button
            {
                Text = "确认退货",
                Width = 104,
                Height = 34,
                Tag = "primary"
            };
            var cancel = new Button
            {
                Text = "取消",
                Width = 90,
                Height = 34,
                DialogResult = DialogResult.Cancel
            };
            _total.AutoSize = true;
            _total.Margin = new Padding(12, 8, 18, 0);
            _total.Font = UiTheme.Font(11F, FontStyle.Bold);
            _total.ForeColor = UiTheme.TextPrimary;

            confirm.Click += delegate { Submit(); };
            footer.Controls.Add(confirm);
            footer.Controls.Add(cancel);
            footer.Controls.Add(_total);

            Controls.Add(_grid);
            Controls.Add(notePanel);
            Controls.Add(footer);
            Controls.Add(info);

            AcceptButton = confirm;
            CancelButton = cancel;
            UiTheme.Apply(this);
            UpdateTotal();
        }

        private void ConfigureGrid(bool isSale)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.DataSource = _lines;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ISBN",
                DataPropertyName = "Isbn",
                Width = 130,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "原数量",
                DataPropertyName = "OriginalQuantity",
                Width = 72,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "已退",
                DataPropertyName = "ReturnedQuantity",
                Width = 64,
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "可退",
                DataPropertyName = "ReturnableQuantity",
                Width = 64,
                ReadOnly = true
            });

            if (!isSale)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "当前库存",
                    DataPropertyName = "CurrentStock",
                    Width = 82,
                    ReadOnly = true
                });
            }

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = isSale ? "原售价" : "原进价",
                DataPropertyName = "UnitPriceYuan",
                Width = 82,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "0.00" }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "本次退货",
                DataPropertyName = "ReturnQuantity",
                Width = 88
            });

            _grid.CellValueChanged += delegate { UpdateTotal(); };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
        }

        private void UpdateTotal()
        {
            decimal amount = 0m;
            var quantity = 0;
            foreach (var line in _lines)
            {
                if (line.ReturnQuantity <= 0) continue;
                quantity += line.ReturnQuantity;
                amount += line.ReturnQuantity * line.UnitPriceYuan;
            }
            _total.Text = "本次 " + quantity + " 册 · ¥" + amount.ToString("0.00");
        }

        private void Submit()
        {
            try
            {
                _grid.EndEdit();

                var inputs = new List<ReturnLineInput>();
                foreach (var line in _lines)
                {
                    if (line.ReturnQuantity < 0)
                        throw new InvalidOperationException(line.Title + " 的退货数量不能为负数。");

                    if (line.ReturnQuantity == 0) continue;

                    if (line.ReturnQuantity > line.ReturnableQuantity)
                        throw new InvalidOperationException(line.Title + " 最多还可退 " + line.ReturnableQuantity + " 册。");

                    if (_kind == "purchase" && line.ReturnQuantity > line.CurrentStock)
                        throw new InvalidOperationException(line.Title + " 当前库存只有 " + line.CurrentStock + " 册。");

                    inputs.Add(new ReturnLineInput
                    {
                        SourceItemId = line.SourceItemId,
                        Quantity = line.ReturnQuantity
                    });
                }

                if (inputs.Count == 0)
                    throw new InvalidOperationException("请至少填写一项本次退货数量。");

                var returnNo = _kind == "sale"
                    ? _services.Returns.CreateSalesReturn(_sourceDocumentId, inputs, _note.Text)
                    : _services.Returns.CreatePurchaseReturn(_sourceDocumentId, inputs, _note.Text);

                MessageBox.Show(
                    this,
                    "退货完成。退货单号：" + returnNo,
                    "完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "退货失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
