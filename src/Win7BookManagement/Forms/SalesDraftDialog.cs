using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class SalesDraftDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();
        private SalesDraftSummary _selected;

        public long SelectedDraftId { get; private set; }

        public SalesDraftDialog(ApplicationServices services)
        {
            _services = services;
            SelectedDraftId = 0;

            UiTheme.ConfigureForm(this);
            Text = "取回挂单";
            StartPosition = FormStartPosition.CenterParent;
            Width = 820;
            Height = 520;
            MinimumSize = new Size(680, 430);
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UiTheme.Background;

            ConfigureGrid();

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

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, 8, 0, 0)
            };
            host.Controls.Add(_grid);
            root.Controls.Add(host, 0, 1);
            root.Controls.Add(CreateFooter(), 0, 2);

            Controls.Add(root);
            UiTheme.Apply(this);

            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                Reload();
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
                Padding = new Padding(18, 14, 18, 12),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "取回挂单",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "挂单保存在本机数据库中，关闭程序后仍会保留。取回后仍会在最终结账时重新检查库存。",
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                Font = UiTheme.Font(8.2F),
                ForeColor = UiTheme.TextSecondary,
                Margin = Padding.Empty
            }, 0, 1);

            return header;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.RowHeight = UiTheme.TableRowHeight;
            _grid.RowHeightHeader = UiTheme.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前没有挂起的销售单";
            _grid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("DraftNo", "挂单号") { Width = "178", MinWidth = "150" },
                new AntdUI.Column("UpdatedAtText", "保存时间") { Width = "150", MinWidth = "132" },
                new AntdUI.Column("ItemCount", "商品项") { Width = "78", MinWidth = "70" },
                new AntdUI.Column("QuantityTotal", "册数") { Width = "72", MinWidth = "64" },
                new AntdUI.Column("TotalYuan", "金额") { Width = "96", MinWidth = "86", DisplayFormat = "0.00" },
                new AntdUI.Column("Note", "备注") { Width = "fill", MinWidth = "150", Ellipsis = true }
            };

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                var row = e.Record as DraftRow;
                _selected = row == null ? null : row.Source;
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                var row = e.Record as DraftRow;
                _selected = row == null ? null : row.Source;
                if (_selected != null)
                    SelectDraft();
            };
        }

        private Control CreateFooter()
        {
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

            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.2F);
            _summary.AutoEllipsis = true;
            footer.Controls.Add(_summary, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            var delete = UiTheme.CreateAntdButton("删除挂单", false);
            delete.Width = 96;
            delete.Margin = new Padding(0, 0, 6, 0);
            delete.Click += delegate { DeleteSelected(); };

            var cancel = UiTheme.CreateAntdButton("取消", false);
            cancel.Width = 86;
            cancel.Margin = new Padding(0, 0, 6, 0);
            cancel.DialogResult = DialogResult.Cancel;

            var recall = UiTheme.CreateAntdButton("取回选中", true);
            recall.Width = 106;
            recall.Click += delegate { SelectDraft(); };

            actions.Controls.Add(delete);
            actions.Controls.Add(cancel);
            actions.Controls.Add(recall);
            footer.Controls.Add(actions, 1, 0);
            return footer;
        }

        private void Reload()
        {
            var values = _services.Sales.GetDraftSummaries();
            var rows = new List<DraftRow>();
            foreach (var value in values)
                rows.Add(new DraftRow(value));

            _selected = null;
            _grid.DataSource = rows;
            _summary.Text = rows.Count == 0
                ? "没有挂单"
                : "共 " + rows.Count + " 个挂单；双击一行也可以直接取回。";
        }

        private void SelectDraft()
        {
            if (_selected == null)
            {
                MessageBox.Show(
                    this,
                    "请先选择一个挂单。",
                    "还没有选择",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                MessageBox.Show(
                    this,
                    "请先选择要删除的挂单。",
                    "还没有选择",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                this,
                "确定删除挂单“" + _selected.DraftNo + "”吗？删除后无法恢复。",
                "删除挂单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _services.Sales.DeleteDraft(_selected.Id);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "删除挂单失败：\r\n" + ex.Message,
                    "无法删除挂单",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private sealed class DraftRow
        {
            public SalesDraftSummary Source { get; private set; }
            public string DraftNo { get { return Source.DraftNo; } }
            public string UpdatedAtText
            {
                get
                {
                    return Source.UpdatedAt == DateTime.MinValue
                        ? "—"
                        : Source.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
                }
            }
            public int ItemCount { get { return Source.ItemCount; } }
            public int QuantityTotal { get { return Source.QuantityTotal; } }
            public decimal TotalYuan { get { return Money.ToYuan(Source.TotalCent); } }
            public string Note
            {
                get
                {
                    return string.IsNullOrWhiteSpace(Source.Note)
                        ? "—"
                        : Source.Note.Trim();
                }
            }

            public DraftRow(SalesDraftSummary source)
            {
                Source = source;
            }
        }
    }
}
