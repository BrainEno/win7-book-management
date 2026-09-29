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
        private readonly string _sourceDocumentNo;
        private readonly BindingList<ReturnableDocumentLine> _lines;
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _note = new TextBox();
        private readonly Label _lineCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();

        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _returnedColumn;
        private readonly DataGridViewColumn _stockColumn;

        public ReturnDialog(ApplicationServices services, string kind, long sourceDocumentId, string sourceDocumentNo)
        {
            _services = services;
            _kind = kind;
            _sourceDocumentId = sourceDocumentId;
            _sourceDocumentNo = sourceDocumentNo;

            var isSale = kind == "sale";
            if (!isSale && kind != "purchase")
                throw new InvalidOperationException("未知退货来源类型。");

            var sourceLines = _services.Documents.GetReturnableLines(kind, sourceDocumentId);
            _lines = new BindingList<ReturnableDocumentLine>();
            foreach (var line in sourceLines)
                _lines.Add(line);

            UiTheme.ConfigureForm(this);
            Text = (isSale ? "销售退货" : "采购退货") + " - " + sourceDocumentNo;
            StartPosition = FormStartPosition.CenterParent;
            Width = 980;
            Height = 680;
            MinimumSize = new Size(720, 520);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;

            _isbnColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "ISBN",
                DataPropertyName = "Isbn",
                Width = 132,
                ReadOnly = true
            };
            _returnedColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "已退",
                DataPropertyName = "ReturnedQuantity",
                Width = 66,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            _stockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "当前库存",
                DataPropertyName = "CurrentStock",
                Width = 82,
                ReadOnly = true,
                Visible = !isSale,
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

            root.Controls.Add(CreateHeader(isSale), 0, 0);
            root.Controls.Add(CreateQuickActions(isSale), 0, 1);
            root.Controls.Add(CreateGrid(isSale), 0, 2);
            root.Controls.Add(CreateNoteSection(isSale), 0, 3);
            root.Controls.Add(CreateFooter(isSale, out Button confirm, out Button cancel), 0, 4);

            Controls.Add(root);

            AcceptButton = confirm;
            CancelButton = cancel;

            _lines.ListChanged += delegate { UpdateTotal(); };
            Resize += delegate { ApplyResponsiveColumns(); };

            UiTheme.Apply(this);
            UpdateTotal();

            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                ApplyResponsiveColumns();
            };
        }

        private Control CreateHeader(bool isSale)
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 14, 22, 12),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = isSale ? "销售退货" : "采购退货",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "原单号  " + _sourceDocumentNo,
                AutoSize = true,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                ForeColor = UiTheme.Accent,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 1);

            header.Controls.Add(new Label
            {
                Text = isSale
                    ? "只填写本次实际退回的数量。退款金额按原销售价格计算，完成后库存自动加回；历史销售单不会被修改。"
                    : "只填写本次实际退给供应商的数量。金额按原采购进价计算，完成后库存自动扣减；库存不足的图书不能退。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F)
            }, 0, 2);

            return header;
        }

        private Control CreateQuickActions(bool isSale)
        {
            var bar = UiTheme.CreateResponsiveToolbar();
            bar.BackColor = UiTheme.Surface;
            bar.BorderStyle = BorderStyle.FixedSingle;
            bar.Margin = new Padding(0, 10, 0, 10);

            var fill = new Button
            {
                Text = "全部填为最大可退",
                Width = 132,
                Height = UiTheme.ButtonHeight
            };
            var clear = new Button
            {
                Text = "清零本次退货",
                Width = 118,
                Height = UiTheme.ButtonHeight
            };

            fill.Click += delegate { FillMaximum(isSale); };
            clear.Click += delegate { ClearReturnQuantities(); };

            bar.Controls.Add(fill);
            bar.Controls.Add(clear);
            bar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = isSale
                    ? "“最大可退”会自动填入每行尚未退过的数量。"
                    : "采购退货会同时受“尚可退数量”和“当前库存”限制。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 11, 0, 0)
            });

            return bar;
        }

        private Control CreateGrid(bool isSale)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;
            _grid.DataSource = _lines;

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
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "原数量",
                DataPropertyName = "OriginalQuantity",
                Width = 76,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(_returnedColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "可退",
                DataPropertyName = "ReturnableQuantity",
                Width = 66,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = UiTheme.Font(8.8F, FontStyle.Bold)
                }
            });

            if (!isSale)
                _grid.Columns.Add(_stockColumn);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = isSale ? "原售价" : "原进价",
                DataPropertyName = "UnitPriceYuan",
                Width = 86,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "本次退货",
                DataPropertyName = "ReturnQuantity",
                Width = 92,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = UiTheme.AccentSoft
                }
            });

            _grid.CellEndEdit += delegate
            {
                _grid.Refresh();
                UpdateTotal();
            };
            _grid.CellValueChanged += delegate { UpdateTotal(); };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
                MessageBox.Show(this, "本次退货数量请输入 0 或正整数。", "输入格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };
            host.Controls.Add(_grid);
            host.Controls.Add(new Label
            {
                Text = "退货明细",
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(12, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(9.2F, FontStyle.Bold)
            });

            return host;
        }

        private Control CreateNoteSection(bool isSale)
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 72,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 10, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = isSale ? "退货原因 / 备注" : "退货原因 / 备注",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 5, 0, 5);
            section.Controls.Add(_note, 1, 0);

            return section;
        }

        private Control CreateFooter(bool isSale, out Button confirm, out Button cancel)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

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
            _total.Font = UiTheme.Font(13F, FontStyle.Bold);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            cancel = new Button
            {
                Text = "取消",
                Width = 88,
                Height = UiTheme.ButtonHeight,
                DialogResult = DialogResult.Cancel
            };
            confirm = new Button
            {
                Text = "确认退货",
                Width = 108,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            confirm.Click += delegate { Submit(); };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(confirm);

            footer.Controls.Add(_lineCount, 0, 0);
            footer.Controls.Add(_quantityTotal, 1, 0);
            footer.Controls.Add(_total, 2, 0);
            footer.Controls.Add(buttons, 3, 0);

            return footer;
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _isbnColumn.Visible = width >= 780;
            _returnedColumn.Visible = width >= 720;
            if (_kind == "purchase")
                _stockColumn.Visible = width >= 680;
        }

        private void FillMaximum(bool isSale)
        {
            foreach (var line in _lines)
            {
                var max = line.ReturnableQuantity;
                if (!isSale)
                    max = Math.Min(max, line.CurrentStock);
                line.ReturnQuantity = Math.Max(0, max);
            }

            _grid.Refresh();
            UpdateTotal();
        }

        private void ClearReturnQuantities()
        {
            foreach (var line in _lines)
                line.ReturnQuantity = 0;

            _grid.Refresh();
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal amount = 0m;
            var quantity = 0;
            var lineCount = 0;

            foreach (var line in _lines)
            {
                if (line.ReturnQuantity <= 0)
                    continue;

                lineCount += 1;
                quantity += line.ReturnQuantity;
                amount += line.ReturnQuantity * line.UnitPriceYuan;
            }

            _lineCount.Text = "退货项目  " + lineCount;
            _quantityTotal.Text = "退货册数  " + quantity;
            _total.Text = (_kind == "sale" ? "退款金额  ¥" : "退货金额  ¥") + amount.ToString("0.00");
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
                        throw new InvalidOperationException("《" + line.Title + "》的退货数量不能为负数。");

                    if (line.ReturnQuantity == 0)
                        continue;

                    if (line.ReturnQuantity > line.ReturnableQuantity)
                        throw new InvalidOperationException("《" + line.Title + "》最多还可退 " + line.ReturnableQuantity + " 册。");

                    if (_kind == "purchase" && line.ReturnQuantity > line.CurrentStock)
                        throw new InvalidOperationException("《" + line.Title + "》当前库存只有 " + line.CurrentStock + " 册，不能退 " + line.ReturnQuantity + " 册。");

                    inputs.Add(new ReturnLineInput
                    {
                        SourceItemId = line.SourceItemId,
                        Quantity = line.ReturnQuantity
                    });
                }

                if (inputs.Count == 0)
                    throw new InvalidOperationException("请至少填写一项本次退货数量。");

                var confirmation = MessageBox.Show(
                    this,
                    _kind == "sale"
                        ? "确认创建销售退货单并把相应库存加回吗？\r\n原销售单和历史价格不会被修改。"
                        : "确认创建采购退货单并扣减相应库存吗？\r\n原采购单和历史进价不会被修改。",
                    "确认退货",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirmation != DialogResult.Yes)
                    return;

                var returnNo = _kind == "sale"
                    ? _services.Returns.CreateSalesReturn(_sourceDocumentId, inputs, _note.Text)
                    : _services.Returns.CreatePurchaseReturn(_sourceDocumentId, inputs, _note.Text);

                MessageBox.Show(
                    this,
                    "退货完成。\r\n退货单号：" + returnNo,
                    "退货成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "退货失败：\r\n" + ex.Message, "请检查退货内容", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
