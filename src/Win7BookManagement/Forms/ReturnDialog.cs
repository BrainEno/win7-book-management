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
        private readonly Label _quantityTotal = new Label();
        private readonly Label _amountTotal = new Label();

        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _originalColumn;
        private readonly DataGridViewColumn _returnedColumn;
        private readonly DataGridViewColumn _currentStockColumn;

        public ReturnDialog(ApplicationServices services, string kind, long sourceDocumentId, string sourceDocumentNo)
        {
            _services = services;
            _kind = kind;
            _sourceDocumentId = sourceDocumentId;
            _sourceDocumentNo = sourceDocumentNo ?? "";

            var isSale = kind == "sale";
            if (!isSale && kind != "purchase")
                throw new InvalidOperationException("未知退货来源类型。");

            var sourceLines = _services.Documents.GetReturnableLines(kind, sourceDocumentId);
            _lines = new BindingList<ReturnableDocumentLine>();
            foreach (var line in sourceLines)
                _lines.Add(line);

            UiTheme.ConfigureForm(this);
            Text = isSale ? "销售退货" : "采购退货";
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
            _originalColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "原数量",
                DataPropertyName = "OriginalQuantity",
                Width = 76,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            _returnedColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "已退",
                DataPropertyName = "ReturnedQuantity",
                Width = 68,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            _currentStockColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "当前库存",
                DataPropertyName = "CurrentStock",
                Width = 86,
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
                RowCount = 4,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateHeader(isSale), 0, 0);
            root.Controls.Add(CreateGridSection(isSale), 0, 1);
            root.Controls.Add(CreateReasonSection(), 0, 2);
            root.Controls.Add(CreateFooter(isSale, out Button confirm, out Button cancel), 0, 3);

            Controls.Add(root);
            AcceptButton = confirm;
            CancelButton = cancel;

            Resize += delegate { ApplyResponsiveColumns(); };
            UiTheme.Apply(this);
            UpdateTotals();

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
                Padding = new Padding(20, 14, 20, 12),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = isSale ? "从原销售单创建退货" : "从原采购单创建退货",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "原单号  " + _sourceDocumentNo,
                AutoSize = true,
                Font = UiTheme.Font(9F, FontStyle.Bold),
                ForeColor = UiTheme.Accent,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 1);

            header.Controls.Add(new Label
            {
                Text = isSale
                    ? "只填写本次实际退回的数量。退款价格来自原销售单快照，提交后库存自动加回，原销售单不会被修改。"
                    : "只填写本次实际退给供应商的数量。退货价格来自原采购单快照，提交后库存自动扣减，库存不足时系统会阻止提交。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Font = UiTheme.Font(8.3F),
                ForeColor = UiTheme.TextSecondary,
                Margin = Padding.Empty
            }, 0, 2);

            return header;
        }

        private Control CreateGridSection(bool isSale)
        {
            ConfigureGrid(isSale);

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 10, 0, 0)
            };

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 5, 12, 5),
                Margin = Padding.Empty
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            toolbar.Controls.Add(new Label
            {
                Text = "退货明细",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            var fillAll = new Button
            {
                Text = "全部填入可退数量",
                Width = 128,
                Height = UiTheme.ButtonHeight
            };
            var clear = new Button
            {
                Text = "清空本次数量",
                Width = 112,
                Height = UiTheme.ButtonHeight
            };
            fillAll.Click += delegate { FillReturnableQuantities(); };
            clear.Click += delegate { ClearReturnQuantities(); };
            actions.Controls.Add(fillAll);
            actions.Controls.Add(clear);
            toolbar.Controls.Add(actions, 1, 0);

            host.Controls.Add(_grid);
            host.Controls.Add(toolbar);
            return host;
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

            _grid.Columns.Add(_isbnColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 210,
                FillWeight = 220,
                ReadOnly = true
            });
            _grid.Columns.Add(_originalColumn);
            _grid.Columns.Add(_returnedColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "可退",
                DataPropertyName = "ReturnableQuantity",
                Width = 68,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    ForeColor = UiTheme.Accent
                }
            });
            if (!isSale)
                _grid.Columns.Add(_currentStockColumn);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = isSale ? "原售价" : "原进价",
                DataPropertyName = "UnitPriceYuan",
                Width = 88,
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
                Width = 94,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            _grid.CellEndEdit += delegate { UpdateTotals(); };
            _grid.CellValueChanged += delegate { UpdateTotals(); };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
                MessageBox.Show(this, "本次退货数量请输入整数。", "输入格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private Control CreateReasonSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 9, 14, 9),
                Margin = new Padding(0, 10, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            section.Controls.Add(new Label
            {
                Text = "退货原因",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 3, 0, 3);
            section.Controls.Add(_note, 1, 0);

            return section;
        }

        private Control CreateFooter(bool isSale, out Button confirm, out Button cancel)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 78,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 0),
                BorderStyle = BorderStyle.FixedSingle
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _quantityTotal.Dock = DockStyle.Fill;
            _quantityTotal.TextAlign = ContentAlignment.MiddleLeft;
            _quantityTotal.ForeColor = UiTheme.TextSecondary;
            _quantityTotal.Font = UiTheme.Font(8.5F, FontStyle.Bold);

            _amountTotal.Dock = DockStyle.Fill;
            _amountTotal.TextAlign = ContentAlignment.MiddleLeft;
            _amountTotal.ForeColor = UiTheme.Accent;
            _amountTotal.Font = UiTheme.Font(13F, FontStyle.Bold);

            cancel = new Button
            {
                Text = "取消",
                Width = 90,
                Height = UiTheme.ButtonHeight,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(0, 4, 8, 4)
            };
            confirm = new Button
            {
                Text = isSale ? "确认销售退货" : "确认采购退货",
                Width = 128,
                Height = UiTheme.ButtonHeight,
                Tag = "primary",
                Margin = new Padding(0, 4, 0, 4)
            };
            confirm.Click += delegate { Submit(); };

            footer.Controls.Add(_quantityTotal, 0, 0);
            footer.Controls.Add(_amountTotal, 1, 0);
            footer.Controls.Add(cancel, 2, 0);
            footer.Controls.Add(confirm, 3, 0);

            return footer;
        }

        private void FillReturnableQuantities()
        {
            foreach (var line in _lines)
            {
                var quantity = line.ReturnableQuantity;
                if (_kind == "purchase")
                    quantity = Math.Min(quantity, line.CurrentStock);
                line.ReturnQuantity = Math.Max(0, quantity);
            }

            _grid.Refresh();
            UpdateTotals();
        }

        private void ClearReturnQuantities()
        {
            foreach (var line in _lines)
                line.ReturnQuantity = 0;

            _grid.Refresh();
            UpdateTotals();
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _isbnColumn.Visible = width >= 760;
            _returnedColumn.Visible = width >= 700;
            _originalColumn.Visible = width >= 640;
            if (_kind == "purchase")
                _currentStockColumn.Visible = width >= 820;
        }

        private void UpdateTotals()
        {
            decimal amount = 0m;
            var quantity = 0;
            foreach (var line in _lines)
            {
                if (line.ReturnQuantity <= 0)
                    continue;

                quantity += line.ReturnQuantity;
                amount += line.ReturnQuantity * line.UnitPriceYuan;
            }

            _quantityTotal.Text = "本次退货  " + quantity + " 册";
            _amountTotal.Text = (_kind == "sale" ? "退款金额  ¥" : "退货金额  ¥") + amount.ToString("0.00");
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
                        throw new InvalidOperationException("《" + line.Title + "》当前库存只有 " + line.CurrentStock + " 册，无法退给供应商 " + line.ReturnQuantity + " 册。");

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
                    "退货完成。\r\n退货单号：" + returnNo + "\r\n原单保持不变，库存流水已同步写入。",
                    "退货成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "退货失败：\r\n" + ex.Message, "请检查退货数量", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
