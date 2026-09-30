using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class InventoryForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("按编码、ISBN、书名、作者、分类或货架位搜索");
        private readonly AntdUI.Checkbox _lowOnly = new AntdUI.Checkbox();
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();
        private readonly Label _resultChip = new Label();
        private readonly Label _lowChip = new Label();
        private readonly Label _stockChip = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private readonly AntdUI.Column _selfCodeColumn;
        private readonly AntdUI.Column _isbnColumn;
        private readonly AntdUI.Column _authorColumn;
        private readonly AntdUI.Column _categoryColumn;
        private readonly AntdUI.Column _shelfColumn;
        private Book _selectedBook;

        public InventoryForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new AntdUI.Column("SelfCode", "店内编码") { Width = "112" };
            _isbnColumn = new AntdUI.Column("Isbn", "ISBN") { Width = "146" };
            _authorColumn = new AntdUI.Column("Author", "作者") { Width = "120" };
            _categoryColumn = new AntdUI.Column("Category", "分类") { Width = "96" };
            _shelfColumn = new AntdUI.Column("ShelfCode", "货架位") { Width = "92" };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(CreateSearchSection(), 0, 0);
            root.Controls.Add(CreateContentSection(), 0, 1);

            _summary.AutoSize = true;
            _summary.Dock = DockStyle.Fill;
            _summary.MinimumSize = new Size(0, 38);
            _summary.Padding = new Padding(12, 8, 8, 8);
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.2F);
            root.Controls.Add(_summary, 0, 2);

            Controls.Add(root);
            Resize += delegate { ApplyResponsiveLayout(); };
            Shown += delegate
            {
                Reload();
                ApplyResponsiveLayout();
                _search.Focus();
            };
            UiTheme.Apply(this);
        }

        private Control CreateSearchSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 15, 18, 15),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "库存管理",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            var adjustButton = UiTheme.CreateAntdButton("库存调整", true);
            adjustButton.Width = 108;
            adjustButton.Click += delegate { AdjustSelected(); };
            section.Controls.Add(adjustButton, 1, 0);

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 4, 0, 0)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            searchRow.Controls.Add(new Label
            {
                Text = "综合搜索",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 14, 0)
            }, 0, 0);

            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 3, 10, 3);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            searchRow.Controls.Add(_search, 1, 0);

            var query = UiTheme.CreateAntdButton("查询", true);
            query.Width = 96;
            query.Margin = new Padding(0, 3, 8, 3);
            query.Click += delegate { Reload(); };
            searchRow.Controls.Add(query, 2, 0);

            _lowOnly.Text = "仅看低库存";
            _lowOnly.AutoSize = true;
            _lowOnly.Anchor = AnchorStyles.Left;
            _lowOnly.Margin = new Padding(8, 11, 0, 0);
            _lowOnly.CheckedChanged += delegate(object sender, AntdUI.BoolEventArgs e) { Reload(); };
            searchRow.Controls.Add(_lowOnly, 3, 0);

            section.Controls.Add(searchRow, 0, 1);
            section.SetColumnSpan(searchRow, 2);

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 9, 0, 0)
            };
            ConfigureChip(_resultChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_lowChip, Color.FromArgb(252, 241, 226), UiTheme.Warning);
            ConfigureChip(_stockChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            stats.Controls.Add(_resultChip);
            stats.Controls.Add(_lowChip);
            stats.Controls.Add(_stockChip);
            stats.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "库存变化必须来自业务单据或带原因的库存调整。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(5, 5, 0, 0)
            });
            section.Controls.Add(stats, 0, 2);
            section.SetColumnSpan(stats, 2);
            return section;
        }

        private static void ConfigureChip(Label label, Color backColor, Color foreColor)
        {
            label.AutoSize = true;
            label.Padding = new Padding(10, 5, 10, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8.2F, FontStyle.Bold);
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            ConfigureGrid();

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            gridHost.Controls.Add(_grid);
            _split.Panel1.Controls.Add(gridHost);
            _split.Panel2.Controls.Add(CreateDetailPanel());
            return _split;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;

            _grid.RowHeight = 46;
            _grid.RowHeightHeader = 46;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有符合条件的库存记录";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _selfCodeColumn,
                _isbnColumn,
                new AntdUI.Column("Title", "书名") { Width = "auto", MinWidth = "230", Ellipsis = true },
                _authorColumn,
                _categoryColumn,
                _shelfColumn,
                new AntdUI.Column("StockQuantity", "当前库存") { Width = "96" }
            };
            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                ShowSelectedDetails();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedBook = e.Record as Book;
                AdjustSelected();
            };
            _grid.SetRowStyle += delegate(object sender, AntdUI.TableSetRowStyleEventArgs e)
            {
                var book = e.Record as Book;
                if (book != null && book.StockQuantity <= _services.Settings.GetLowStockThreshold())
                    return new AntdUI.Table.CellStyleInfo { ForeColor = UiTheme.Warning };
                return null;
            };
        }

        private Control CreateDetailPanel()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(14) };
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 48),
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new Label
            {
                Text = "库存详情",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var adjust = UiTheme.CreateAntdButton("库存调整", true);
            adjust.Width = 108;
            adjust.Click += delegate { AdjustSelected(); };
            header.Controls.Add(adjust, 1, 0);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Surface, Padding = new Padding(0, 8, 0, 0) };
            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = UiTheme.Surface
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddDetailRow(details, "书名", "title");
            AddDetailRow(details, "店内编码", "selfCode");
            AddDetailRow(details, "ISBN", "isbn");
            AddDetailRow(details, "作者", "author");
            AddDetailRow(details, "分类", "category");
            AddDetailRow(details, "货架位", "shelf");
            AddDetailRow(details, "当前库存", "stock");
            AddDetailRow(details, "低库存阈值", "threshold");
            AddDetailRow(details, "库存状态", "stockState");
            scroll.Controls.Add(details);
            host.Controls.Add(scroll);
            host.Controls.Add(header);
            return host;
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 46),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F, FontStyle.Bold)
            }, 0, row);
            var value = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(9, 0, 9, 0),
                Margin = new Padding(0, 3, 0, 3),
                BackColor = UiTheme.SurfaceMuted,
                ForeColor = UiTheme.TextPrimary,
                AutoEllipsis = true,
                Font = UiTheme.Font(8.5F)
            };
            table.Controls.Add(value, 1, row);
            _detailValues[key] = value;
        }

        private void Reload()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var books = _services.Books.Search(_search.Text, false);
            if (_lowOnly.Checked) books = books.Where(book => book.StockQuantity <= threshold).ToList();

            _selectedBook = null;
            _grid.DataSource = books;

            var lowCount = 0;
            long stockTotal = 0;
            foreach (var book in books)
            {
                stockTotal += book.StockQuantity;
                if (book.StockQuantity <= threshold) lowCount++;
            }

            _resultChip.Text = "结果  " + books.Count;
            _lowChip.Text = "低库存  " + lowCount;
            _stockChip.Text = "合计库存  " + stockTotal + " 册";
            _summary.Text = "显示 " + books.Count + " 个启用品种 · 低库存阈值 ≤ " + threshold + " 册 · 表头可拖动调整宽度";
            ShowSelectedDetails();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0) return;
            var showDetails = ClientSize.Width >= UiTheme.WideBreakpoint;
            _split.Panel2Collapsed = !showDetails;
            if (showDetails)
            {
                var desiredRightWidth = Math.Min(360, Math.Max(305, _split.Width / 3));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                var maximumDistance = _split.Width - 300 - _split.SplitterWidth;
                if (distance >= 420 && maximumDistance >= 420)
                    _split.SplitterDistance = Math.Min(distance, maximumDistance);
            }

            var gridWidth = showDetails ? _split.Panel1.ClientSize.Width : ClientSize.Width;
            _selfCodeColumn.Visible = gridWidth >= 930;
            _categoryColumn.Visible = gridWidth >= 840;
            _shelfColumn.Visible = gridWidth >= 730;
            _authorColumn.Visible = gridWidth >= 650;
            _isbnColumn.Visible = gridWidth >= 570;
            _grid.LoadLayout();
        }

        private void ShowSelectedDetails()
        {
            var book = _selectedBook;
            if (book == null)
            {
                foreach (var value in _detailValues.Values) value.Text = "—";
                return;
            }

            var threshold = _services.Settings.GetLowStockThreshold();
            var low = book.StockQuantity <= threshold;
            _detailValues["title"].Text = EmptyAsDash(book.Title);
            _detailValues["selfCode"].Text = EmptyAsDash(book.SelfCode);
            _detailValues["isbn"].Text = EmptyAsDash(book.Isbn);
            _detailValues["author"].Text = EmptyAsDash(book.Author);
            _detailValues["category"].Text = EmptyAsDash(book.Category);
            _detailValues["shelf"].Text = EmptyAsDash(book.ShelfCode);
            _detailValues["stock"].Text = book.StockQuantity + " 册";
            _detailValues["threshold"].Text = "≤ " + threshold + " 册";
            _detailValues["stockState"].Text = low ? "低库存，需要关注" : "库存正常";
            _detailValues["stockState"].ForeColor = low ? UiTheme.Warning : UiTheme.Success;
        }

        private static string EmptyAsDash(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
        }

        private void AdjustSelected()
        {
            if (_selectedBook == null)
            {
                MessageBox.Show(this, "请先选择要调整库存的图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new AdjustmentDialog(_selectedBook))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var no = _services.Inventory.Adjust(_selectedBook.Id, dialog.Delta, dialog.Note);
                    MessageBox.Show(this, "库存调整完成。\r\n流水号：" + no, "调整成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Reload();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "调整失败：\r\n" + ex.Message, "请检查调整内容", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private sealed class AdjustmentDialog : Form
        {
            private readonly Book _book;
            private readonly AntdUI.Select _direction = new AntdUI.Select();
            private readonly AntdUI.InputNumber _quantity = new AntdUI.InputNumber();
            private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("例如：盘点差异、破损报废、录入修正");
            private readonly Label _preview = new Label();

            public int Delta
            {
                get
                {
                    var amount = Decimal.ToInt32(_quantity.Value);
                    return _direction.SelectedIndex == 1 ? -amount : amount;
                }
            }

            public string Note { get { return _note.Text; } }

            public AdjustmentDialog(Book book)
            {
                _book = book;
                UiTheme.ConfigureForm(this);
                Text = "库存调整";
                StartPosition = FormStartPosition.CenterParent;
                Width = 600;
                Height = 450;
                MinimumSize = new Size(520, 400);
                BackColor = UiTheme.Background;
                ShowInTaskbar = false;
                MinimizeBox = false;
                KeyPreview = true;

                _direction.Items.Add("增加库存");
                _direction.Items.Add("减少库存");
                _direction.SelectedIndex = 0;
                _direction.DropDownArrow = true;

                _quantity.Minimum = 1;
                _quantity.Maximum = 1000000;
                _quantity.Value = 1;

                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 14, 22, 12)
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.Controls.Add(new Label
                {
                    Text = book.Title,
                    AutoSize = true,
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 5)
                }, 0, 0);
                header.Controls.Add(new Label
                {
                    Text = "当前库存 " + book.StockQuantity + " 册" +
                           (string.IsNullOrWhiteSpace(book.ShelfCode) ? "" : " · 货架位 " + book.ShelfCode),
                    AutoSize = true,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F)
                }, 0, 1);

                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(22, 18, 22, 14),
                    BackColor = UiTheme.Surface
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddDialogLabel(body, 0, "调整方向 *");
                _direction.Dock = DockStyle.Fill;
                _direction.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_direction, 1, 0);

                AddDialogLabel(body, 1, "调整数量 *");
                _quantity.Dock = DockStyle.Fill;
                _quantity.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_quantity, 1, 1);

                AddDialogLabel(body, 2, "调整后");
                _preview.Dock = DockStyle.Fill;
                _preview.TextAlign = ContentAlignment.MiddleLeft;
                _preview.Padding = new Padding(8, 0, 0, 0);
                _preview.Font = UiTheme.Font(10F, FontStyle.Bold);
                body.Controls.Add(_preview, 1, 2);

                AddDialogLabel(body, 3, "调整原因 *");
                _note.Dock = DockStyle.Fill;
                _note.Margin = new Padding(8, 6, 0, 6);
                body.Controls.Add(_note, 1, 3);

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    MinimumSize = new Size(0, 70),
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 12, 22, 12)
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                footer.Controls.Add(new Label
                {
                    Text = "每次调整都会写入库存流水；不能直接改写历史流水。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8F)
                }, 0, 0);

                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 90;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                var ok = UiTheme.CreateAntdButton("确认调整", true);
                ok.Width = 110;
                ok.Click += Confirm;
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                footer.Controls.Add(buttons, 1, 0);

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                _direction.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e) { UpdatePreview(); };
                _quantity.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e) { UpdatePreview(); };
                KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                };

                UiTheme.Apply(this);
                UpdatePreview();
                Shown += delegate { UiTheme.FitDialogToWorkingArea(this, 24); };
            }

            private static void AddDialogLabel(TableLayoutPanel body, int row, string text)
            {
                body.Controls.Add(new Label
                {
                    Text = text,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold)
                }, 0, row);
            }

            private void UpdatePreview()
            {
                var after = (long)_book.StockQuantity + Delta;
                _preview.Text = after + " 册";
                _preview.ForeColor = after < 0 ? UiTheme.Danger : UiTheme.Accent;
            }

            private void Confirm(object sender, EventArgs e)
            {
                var after = (long)_book.StockQuantity + Delta;
                if (after < 0)
                {
                    MessageBox.Show(this, "减少后的库存不能小于 0。当前库存只有 " + _book.StockQuantity + " 册，请修改调整数量。", "数量过大", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _quantity.Focus();
                    return;
                }
                if (string.IsNullOrWhiteSpace(Note))
                {
                    MessageBox.Show(this, "请填写调整原因，例如“盘点差异”“破损报废”或“录入修正”。", "请填写原因", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _note.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
