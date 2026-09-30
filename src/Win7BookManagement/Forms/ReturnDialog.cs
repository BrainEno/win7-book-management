using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
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
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("填写退货原因或备注（可选）");
        private readonly Label _lineCount = new Label();
        private readonly Label _quantityTotal = new Label();
        private readonly Label _total = new Label();

        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _returnedColumn;
        private readonly AntdUI.Column _stockColumn;

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
            foreach (var line in sourceLines) _lines.Add(line);

            UiTheme.ConfigureForm(this);
            Text = (isSale ? "销售退货" : "采购退货") + " - " + sourceDocumentNo;
            StartPosition = FormStartPosition.CenterParent;
            Width = 980;
            Height = 680;
            MinimumSize = new Size(720, 520);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;
            KeyPreview = true;

            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "142", ReadOnly = true };
            _returnedColumn = new AntdUI.Column("ReturnedQuantity", "已退") { Width = "72", ReadOnly = true };
            _stockColumn = new AntdUI.Column("CurrentStock", "当前库存") { Width = "92", ReadOnly = true, Visible = !isSale };

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
            root.Controls.Add(CreateNoteSection(), 0, 3);
            root.Controls.Add(CreateFooter(), 0, 4);
            Controls.Add(root);

            _lines.ListChanged += delegate
            {
                _grid.DataSource = _lines;
                UpdateTotal();
            };
            Resize += delegate { ApplyResponsiveColumns(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            UiTheme.Apply(this);
            _grid.DataSource = _lines;
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
                Padding = new Padding(22, 15, 22, 13),
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
                    ? "填写本次实际退回数量。退款按原销售价格计算，完成后库存自动加回；历史销售单保持不变。"
                    : "填写本次实际退给供应商的数量。金额按原采购进价计算，完成后库存自动扣减。",
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
            bar.Margin = new Padding(0, 10, 0, 10);

            var fill = UiTheme.CreateAntdButton("全部填为最大可退", false);
            fill.Width = 140;
            var clear = UiTheme.CreateAntdButton("清零本次退货", false);
            clear.Width = 126;
            fill.Click += delegate { FillMaximum(isSale); };
            clear.Click += delegate { ClearReturnQuantities(); };

            bar.Controls.Add(fill);
            bar.Controls.Add(clear);
            bar.Controls.Add(new Label
            {
                AutoSize = true,
                Text = isSale
                    ? "“最大可退”会填入每行尚未退过的数量。"
                    : "采购退货同时受尚可退数量和当前库存限制。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(14, 12, 0, 0)
            });
            return bar;
        }

        private Control CreateGrid(bool isSale)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.BackColor = UiTheme.Surface;
            _grid.ForeColor = UiTheme.TextPrimary;
            _grid.ColumnBack = UiTheme.NavigationSurface;
            _grid.ColumnFore = UiTheme.TextSecondary;
            _grid.ColumnFont = UiTheme.Font(8.8F, FontStyle.Bold);
            _grid.BorderColor = UiTheme.Border;
            _grid.Radius = 8;
            _grid.RowHeight = 48;
            _grid.RowHeightHeader = 48;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.EditMode = TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "原单据没有可退图书";
            _grid.RowHoverBg = Color.FromArgb(248, 246, 241);
            _grid.RowSelectedBg = UiTheme.AccentSoft;
            _grid.RowSelectedFore = UiTheme.TextPrimary;

            var columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Title", "书名") { Width = "auto", MinWidth = "240", Ellipsis = true, ReadOnly = true },
                new AntdUI.Column("OriginalQuantity", "原数量") { Width = "82", ReadOnly = true },
                _returnedColumn,
                new AntdUI.Column("ReturnableQuantity", "可退") { Width = "72", ReadOnly = true }
            };
            if (!isSale) columns.Add(_stockColumn);
            columns.Add(new AntdUI.Column("UnitPriceYuan", isSale ? "原售价" : "原进价") { Width = "96", ReadOnly = true, DisplayFormat = "0.00" });
            columns.Add(new AntdUI.Column("ReturnQuantity", "本次退货")
            {
                Width = "108",
                ReadOnly = false,
                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }
            });
            columns.Add(_isbnColumn);
            _grid.Columns = columns;

            _grid.CellEndEdit += delegate(object sender, AntdUI.TableEndEditEventArgs e)
            {
                var line = e.Record as ReturnableDocumentLine;
                if (line == null || e.Column == null || e.Column.Key != "ReturnQuantity") return true;

                int quantity;
                if (!int.TryParse(e.Value, out quantity) || quantity < 0)
                {
                    MessageBox.Show(this, "本次退货数量请输入 0 或正整数。", "输入格式不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                if (quantity > line.ReturnableQuantity)
                {
                    MessageBox.Show(this, "《" + line.Title + "》最多还可退 " + line.ReturnableQuantity + " 册。", "超过可退数量", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                if (_kind == "purchase" && quantity > line.CurrentStock)
                {
                    MessageBox.Show(this, "《" + line.Title + "》当前库存只有 " + line.CurrentStock + " 册。", "库存不足", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                line.ReturnQuantity = quantity;
                _grid.Refresh();
                UpdateTotal();
                return true;
            };

            var host = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Margin = Padding.Empty };
            host.Controls.Add(_grid);
            return host;
        }

        private Control CreateNoteSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 11, 16, 11),
                Margin = new Padding(0, 10, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.Controls.Add(new Label
            {
                Text = "退货原因 / 备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);
            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 2, 0, 2);
            section.Controls.Add(_note, 1, 0);
            return section;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 78),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(16, 13, 16, 13),
                Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var metrics = new FlowLayoutPanel
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
            metrics.Controls.Add(_lineCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_total);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(14, 0, 0, 0)
            };
            var cancel = UiTheme.CreateAntdButton("取消", false);
            cancel.Width = 90;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            var confirm = UiTheme.CreateAntdButton("确认退货", true);
            confirm.Width = 112;
            confirm.Click += delegate { Submit(); };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(confirm);

            footer.Controls.Add(metrics, 0, 0);
            footer.Controls.Add(buttons, 1, 0);
            return footer;
        }

        private static void ConfigureSummaryLabel(Label label, bool primary)
        {
            label.AutoSize = true;
            label.ForeColor = primary ? UiTheme.Accent : UiTheme.TextSecondary;
            label.Font = UiTheme.Font(primary ? 12.8F : 8.5F, FontStyle.Bold);
            label.BackColor = primary ? UiTheme.AccentSoft : UiTheme.Surface;
            label.Padding = primary ? new Padding(12, 8, 12, 8) : new Padding(10, 8, 10, 8);
            label.Margin = new Padding(0, 0, 10, 0);
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _isbnColumn.Visible = width >= 780;
            _returnedColumn.Visible = width >= 720;
            if (_kind == "purchase") _stockColumn.Visible = width >= 680;
            _grid.LoadLayout();
        }

        private void FillMaximum(bool isSale)
        {
            foreach (var line in _lines)
            {
                var max = line.ReturnableQuantity;
                if (!isSale) max = Math.Min(max, line.CurrentStock);
                line.ReturnQuantity = Math.Max(0, max);
            }
            _grid.Refresh();
            UpdateTotal();
        }

        private void ClearReturnQuantities()
        {
            foreach (var line in _lines) line.ReturnQuantity = 0;
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
                if (line.ReturnQuantity <= 0) continue;
                lineCount++;
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
                var inputs = new List<ReturnLineInput>();
                foreach (var line in _lines)
                {
                    if (line.ReturnQuantity < 0) throw new InvalidOperationException("《" + line.Title + "》的退货数量不能为负数。");
                    if (line.ReturnQuantity == 0) continue;
                    if (line.ReturnQuantity > line.ReturnableQuantity) throw new InvalidOperationException("《" + line.Title + "》最多还可退 " + line.ReturnableQuantity + " 册。");
                    if (_kind == "purchase" && line.ReturnQuantity > line.CurrentStock)
                        throw new InvalidOperationException("《" + line.Title + "》当前库存只有 " + line.CurrentStock + " 册，不能退 " + line.ReturnQuantity + " 册。");
                    inputs.Add(new ReturnLineInput { SourceItemId = line.SourceItemId, Quantity = line.ReturnQuantity });
                }

                if (inputs.Count == 0) throw new InvalidOperationException("请至少填写一项本次退货数量。");

                var confirmation = MessageBox.Show(
                    this,
                    _kind == "sale"
                        ? "确认创建销售退货单并把相应库存加回吗？\r\n原销售单和历史价格不会被修改。"
                        : "确认创建采购退货单并扣减相应库存吗？\r\n原采购单和历史进价不会被修改。",
                    "确认退货",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirmation != DialogResult.Yes) return;

                var returnNo = _kind == "sale"
                    ? _services.Returns.CreateSalesReturn(_sourceDocumentId, inputs, _note.Text)
                    : _services.Returns.CreatePurchaseReturn(_sourceDocumentId, inputs, _note.Text);

                MessageBox.Show(this, "退货完成。\r\n退货单号：" + returnNo, "退货成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
