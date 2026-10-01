using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class PurchaseHistoryDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly long? _currentDocumentId;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("采购单号 / 供应商 / ISBN / 书名");
        private readonly AntdUI.Select _statusFilter = new AntdUI.Select();
        private readonly string[] _statusKeys = { "", PurchaseService.DraftStatus, PurchaseService.ReviewedStatus };
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();
        private readonly List<PurchaseDocumentSummary> _display = new List<PurchaseDocumentSummary>();
        private IList<PurchaseDocumentSummary> _all = new List<PurchaseDocumentSummary>();
        private PurchaseDocumentSummary _selected;
        private AntdUI.Button _copyButton;
        private AntdUI.Button _deleteDraftButton;

        public long? SelectedDocumentId { get; private set; }
        public bool DeletedCurrentDocument { get; private set; }
        public bool CopyAsNewRequested { get; private set; }

        public PurchaseHistoryDialog(ApplicationServices services, long? currentDocumentId)
        {
            _services = services;
            _currentDocumentId = currentDocumentId;

            _statusFilter.Items.Add("全部状态");
            _statusFilter.Items.Add("草稿");
            _statusFilter.Items.Add("已复核");
            _statusFilter.SelectedIndex = 0;
            _statusFilter.DropDownArrow = true;
            _statusFilter.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e)
            {
                if (IsHandleCreated) ApplyFilter();
            };

            UiTheme.ConfigureForm(this);
            Text = "采购历史单据";
            StartPosition = FormStartPosition.CenterParent;
            Width = 980;
            Height = 650;
            MinimumSize = new Size(720, 480);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;
            KeyPreview = true;

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
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateSearch(), 0, 1);
            root.Controls.Add(CreateGrid(), 0, 2);
            root.Controls.Add(CreateFooter(), 0, 3);
            Controls.Add(root);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Escape) return;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            UiTheme.Apply(this);
            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                LoadHistory(currentDocumentId);
                _search.Focus();
            };
        }

        private Control CreateHeader()
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = UiTheme.Surface,
                Padding = new Padding(22, 16, 22, 14),
                Margin = Padding.Empty
            };

            var title = new Label
            {
                Text = "采购历史单据",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(22, 16)
            };
            var hint = new Label
            {
                Text = "草稿可以继续编辑；已复核单据默认只读，需要修改时请先反复核。",
                AutoSize = true,
                Font = UiTheme.Font(8.2F),
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(22, 44)
            };
            host.Controls.Add(title);
            host.Controls.Add(hint);
            host.Height = 76;
            return host;
        }

        private Control CreateSearch()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 10, 18, 10),
                Margin = new Padding(0, 8, 0, 8)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            row.Controls.Add(new Label
            {
                Text = "筛选",
                AutoSize = false,
                Width = 54,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Left,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary
            }, 0, 0);

            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 2, 10, 2);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                ApplyFilter();
                e.SuppressKeyPress = true;
            };
            row.Controls.Add(_search, 1, 0);

            _statusFilter.Dock = DockStyle.Fill;
            _statusFilter.Margin = new Padding(0, 2, 10, 2);
            row.Controls.Add(_statusFilter, 2, 0);

            var query = UiTheme.CreateAntdButton("查询", true);
            query.Width = 92;
            query.Margin = new Padding(0, 2, 0, 2);
            query.Click += delegate { ApplyFilter(); };
            row.Controls.Add(query, 3, 0);
            return row;
        }

        private Control CreateGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.RowHeight = 44;
            _grid.RowHeightHeader = 44;
            _grid.EnableHeaderResizing = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有采购历史单据";
            _grid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("PurchaseDate", "采购日期") { Width = "112", ReadOnly = true },
                new AntdUI.Column("OrderNo", "采购单号") { Width = "150", ReadOnly = true },
                new AntdUI.Column("SupplierName", "供应商") { Width = "fill", MinWidth = "150", Ellipsis = true, ReadOnly = true },
                new AntdUI.Column("Quantity", "册数") { Width = "76", ReadOnly = true },
                new AntdUI.Column("TotalYuan", "金额（元）") { Width = "110", DisplayFormat = "0.00", ReadOnly = true },
                new AntdUI.Column("StatusText", "状态") { Width = "86", ReadOnly = true },
                new AntdUI.Column("UpdatedAtText", "最后保存") { Width = "142", ReadOnly = true }
            };

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as PurchaseDocumentSummary;
                UpdateActionState();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as PurchaseDocumentSummary;
                Choose();
            };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            host.Controls.Add(_grid);
            return host;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 12, 18, 12),
                Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.4F);
            footer.Controls.Add(_summary, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _copyButton = UiTheme.CreateAntdButton("复制为新单", false);
            _copyButton.Width = 112;
            _copyButton.Enabled = false;
            _copyButton.Click += delegate { CopySelectedAsNew(); };

            _deleteDraftButton = UiTheme.CreateAntdButton("删除草稿", false);
            _deleteDraftButton.Width = 104;
            _deleteDraftButton.Enabled = false;
            _deleteDraftButton.Click += delegate { DeleteSelectedDraft(); };

            var cancel = UiTheme.CreateAntdButton("取消", false);
            cancel.Width = 88;
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var open = UiTheme.CreateAntdButton("打开单据", true);
            open.Width = 108;
            open.Click += delegate { Choose(); };

            buttons.Controls.Add(_copyButton);
            buttons.Controls.Add(_deleteDraftButton);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(open);
            footer.Controls.Add(buttons, 1, 0);
            return footer;
        }

        private void LoadHistory(long? currentDocumentId)
        {
            ApplyFilter();

            if (!currentDocumentId.HasValue) return;
            foreach (var item in _display)
            {
                if (item.Id != currentDocumentId.Value) continue;
                _selected = item;
                _grid.SetSelected(item, false);
                UpdateActionState();
                break;
            }
        }

        private void ApplyFilter()
        {
            var keyword = (_search.Text ?? "").Trim();
            var statusIndex = _statusFilter.SelectedIndex;
            var status = statusIndex >= 0 && statusIndex < _statusKeys.Length
                ? _statusKeys[statusIndex]
                : "";

            _all = _services.Purchases.SearchHistory(keyword, status, 500);
            _display.Clear();
            foreach (var item in _all)
                _display.Add(item);

            _selected = _display.Count > 0 ? _display[0] : null;
            _grid.DataSource = _display;
            if (_selected != null) _grid.SetSelected(_selected, false);
            _summary.Text = "共 " + _display.Count + " 张匹配采购单 · 支持单号 / 供应商 / ISBN / 书名";
            UpdateActionState();
        }

        private void UpdateActionState()
        {
            if (_copyButton != null)
                _copyButton.Enabled = _selected != null;

            if (_deleteDraftButton == null) return;
            _deleteDraftButton.Enabled =
                _selected != null &&
                string.Equals(
                    _selected.Status,
                    PurchaseService.DraftStatus,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteSelectedDraft()
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "请先选择一张采购草稿。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.Equals(_selected.Status, PurchaseService.DraftStatus, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "只有从未正式入库的草稿才能删除。已复核单据必须保留历史。", "不能删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    this,
                    "确定删除采购草稿“" + _selected.OrderNo + "”吗？\r\n\r\n删除后无法恢复；如果该单曾经复核产生过库存流水，系统会自动拒绝删除。",
                    "删除采购草稿",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                var deletedId = _selected.Id;
                var orderNo = _services.Purchases.DeleteDraft(deletedId);
                if (_currentDocumentId.HasValue && _currentDocumentId.Value == deletedId)
                    DeletedCurrentDocument = true;

                ApplyFilter();

                MessageBox.Show(
                    this,
                    "采购草稿“" + orderNo + "”已删除。",
                    "草稿已删除",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "删除采购草稿失败：\r\n" + ex.Message,
                    "无法删除",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void CopySelectedAsNew()
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "请先选择一张采购单。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedDocumentId = _selected.Id;
            CopyAsNewRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Choose()
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "请先选择一张采购单。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedDocumentId = _selected.Id;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
