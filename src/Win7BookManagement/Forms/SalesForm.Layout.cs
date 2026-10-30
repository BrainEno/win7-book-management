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
private Control CreateInputSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 18),
                ColumnCount = 8,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 9, 14, 9),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "扫码 / 搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _isbn.Dock = DockStyle.None;
            _isbn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _isbn.Tag = "toolbar-input";
            _isbn.Margin = new Padding(0, 0, 8, 0);
            _isbn.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddByIsbn();
                    e.SuppressKeyPress = true;
                }
            };
            section.Controls.Add(_isbn, 1, 0);

            var add = UiTheme.CreateAntdButton("加入", true);
            add.Width = 78;
            add.Anchor = AnchorStyles.Left;
            add.Tag = "toolbar-action";
            add.Margin = new Padding(0, 0, 6, 0);
            add.Click += delegate { AddByIsbn(); };
            section.Controls.Add(add, 2, 0);

            var pick = UiTheme.CreateAntdButton("选择图书", false);
            pick.Width = 92;
            pick.Anchor = AnchorStyles.Left;
            pick.Tag = "toolbar-action";
            pick.Margin = new Padding(0, 0, 6, 0);
            pick.Click += delegate { PickBook(); };
            section.Controls.Add(pick, 3, 0);

            var newOrder = UiTheme.CreateAntdButton("新单", false);
            newOrder.Width = 68;
            newOrder.Anchor = AnchorStyles.Left;
            newOrder.Tag = "toolbar-action";
            newOrder.Margin = new Padding(0, 0, 6, 0);
            newOrder.Click += delegate { StartNewOrder(); };
            _shortcutTips.SetToolTip(newOrder, "Ctrl+N 新建空白销售单");
            section.Controls.Add(newOrder, 4, 0);

            var hold = UiTheme.CreateAntdButton("挂单", false);
            hold.Width = 70;
            hold.Anchor = AnchorStyles.Left;
            hold.Tag = "toolbar-action";
            hold.Margin = new Padding(0, 0, 6, 0);
            hold.Click += delegate { HoldCurrentOrder(); };
            _shortcutTips.SetToolTip(hold, "F5 挂单并持久保存");
            section.Controls.Add(hold, 5, 0);

            var recall = UiTheme.CreateAntdButton("取单", false);
            recall.Width = 70;
            recall.Anchor = AnchorStyles.Left;
            recall.Tag = "toolbar-action";
            recall.Margin = new Padding(0, 0, 6, 0);
            recall.Click += delegate { RecallDraft(); };
            _shortcutTips.SetToolTip(recall, "F6 从已保存挂单中取回");
            section.Controls.Add(recall, 6, 0);

            var clear = UiTheme.CreateAntdButton("清空", false);
            clear.Width = 68;
            clear.Anchor = AnchorStyles.Left;
            clear.Tag = "toolbar-action";
            clear.Click += delegate { ClearCartWithConfirmation(); };
            section.Controls.Add(clear, 7, 0);

            return section;
        }

private Control CreateCartSection()
        {
            ConfigureGrid();

            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, 6, 0, 0)
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 5, 12, 5),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "销售明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "数量 / 折扣可编辑",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(8, 6, 12, 0)
            }, 1, 0);

            var remove = UiTheme.CreateAntdButton("移除选中", false);
            remove.Width = 92;
            remove.Click += delegate { RemoveSelected(); };
            header.Controls.Add(remove, 2, 0);
            var reprint = UiTheme.CreateAntdButton("重打上一张", false);
            reprint.Width = 112; reprint.Margin = new Padding(6, 0, 0, 0);
            reprint.Click += delegate
            {
                if (_lastReceipt == null) { MessageBox.Show(this, "本次打开页面后还没有已结账小票。", "没有可重打的小票", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                try { _services.Receipts.Print(this, _lastReceipt); }
                catch (Exception ex) { CrashReporter.Handle(this, ex, "重打小票"); }
            };
            header.Controls.Add(reprint, 3, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };

            _emptyState.Dock = DockStyle.Fill;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前销售单为空\r\n请扫码、搜索或选择图书";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.Font = UiTheme.Font(9F);
            _emptyState.BackColor = UiTheme.Surface;

            content.Controls.Add(_grid);
            content.Controls.Add(_emptyState);
            _emptyState.BringToFront();

            host.Controls.Add(header, 0, 0);
            host.Controls.Add(content, 0, 1);
            return host;
        }

private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.RowHeight = 44;
            _grid.RowHeightHeader = 42;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.EditMode = AntdUI.TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前销售单还没有商品";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _titleColumn,
                _quantityColumn,
                _priceColumn,
                _discountColumn,
                _discountedUnitColumn,
                _lineTotalColumn,
                _stockColumn,
                _authorColumn,
                _isbnColumn,
                _selfCodeColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "sales-lines-v3-discount");

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedRow = e.Record as SalesCartRow;
            };
            _grid.CellEndEdit += HandleCellEndEdit;
        }

private Control CreateSettlementSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, UiTheme.InputHeight + 14),
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 6, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = new Padding(0, 0, 18, 0);
            section.Controls.Add(_note, 1, 0);

            section.Controls.Add(new Label
            {
                Text = "整单折扣 %",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            }, 2, 0);

            _orderDiscount.Dock = DockStyle.None;
            _orderDiscount.Anchor = AnchorStyles.Left;
            _orderDiscount.Margin = Padding.Empty;
            section.Controls.Add(_orderDiscount, 3, 0);
            return section;
        }

private Control CreateTotalsSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 62),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(14, 9, 14, 9),
                Margin = new Padding(0, 8, 0, 0)
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var metrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty
            };

            ConfigureSummaryLabel(_itemCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_originalTotal, false);
            ConfigureSummaryLabel(_discountTotal, false);
            ConfigureSummaryLabel(_total, true);
            metrics.Controls.Add(_itemCount);
            metrics.Controls.Add(_quantityTotal);
            metrics.Controls.Add(_originalTotal);
            metrics.Controls.Add(_discountTotal);
            metrics.Controls.Add(_total);

            var submit = UiTheme.CreateAntdButton("确认结账", true);
            submit.Width = 112;
            submit.Margin = new Padding(10, 0, 0, 0);
            submit.Click += delegate { Submit(); };
            _shortcutTips.SetToolTip(submit, "Ctrl+Enter 打开收款结算");

            section.Controls.Add(metrics, 0, 0);
            section.Controls.Add(submit, 1, 0);
            return section;
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

private void ApplyResponsiveColumns()
        {
            var width = _grid.ClientSize.Width > 0 ? _grid.ClientSize.Width : ClientSize.Width;
            _selfCodeColumn.Visible = width >= 1400;
            _authorColumn.Visible = width >= 1150;
            _isbnColumn.Visible = width >= 950;
            _stockColumn.Visible = width >= 760;
            _grid.LoadLayout();
        }
    }
}
