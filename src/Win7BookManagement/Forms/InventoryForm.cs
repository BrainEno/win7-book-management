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
        private readonly TextBox _search = new TextBox();
        private readonly CheckBox _lowOnly = new CheckBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _summary = new Label();
        private readonly Label _resultChip = new Label();
        private readonly Label _lowChip = new Label();
        private readonly Label _stockChip = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _categoryColumn;
        private readonly DataGridViewColumn _shelfColumn;

        public InventoryForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _selfCodeColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "店内编码",
                DataPropertyName = "SelfCode",
                Width = 100
            };
            _isbnColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "ISBN",
                DataPropertyName = "Isbn",
                Width = 132
            };
            _authorColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "作者",
                DataPropertyName = "Author",
                Width = 118
            };
            _categoryColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "分类",
                DataPropertyName = "Category",
                Width = 92
            };
            _shelfColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "货架位",
                DataPropertyName = "ShelfCode",
                Width = 88
            };

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
            _summary.MinimumSize = new Size(0, 36);
            _summary.Padding = new Padding(10, 8, 8, 8);
            _summary.BackColor = UiTheme.Surface;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8F);
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
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "库存查询",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            var adjustButton = new Button
            {
                Text = "库存调整",
                Width = 102,
                Height = UiTheme.ButtonHeight,
                Tag = "primary",
                Margin = Padding.Empty
            };
            adjustButton.Click += delegate { AdjustSelected(); };
            section.Controls.Add(adjustButton, 1, 0);

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 2, 0, 0)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            searchRow.Controls.Add(new Label
            {
                Text = "综合搜索",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            }, 0, 0);

            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 4, 10, 4);
            _search.Font = UiTheme.Font(9.5F);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };
            searchRow.Controls.Add(_search, 1, 0);

            var query = new Button
            {
                Text = "查询",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 8, 4)
            };
            query.Click += delegate { Reload(); };
            searchRow.Controls.Add(query, 2, 0);

            _lowOnly.Text = "仅看低库存";
            _lowOnly.AutoSize = true;
            _lowOnly.Dock = DockStyle.Fill;
            _lowOnly.TextAlign = ContentAlignment.MiddleLeft;
            _lowOnly.Margin = new Padding(8, 0, 0, 0);
            _lowOnly.CheckedChanged += delegate { Reload(); };
            searchRow.Controls.Add(_lowOnly, 3, 0);

            section.Controls.Add(searchRow, 0, 1);
            section.SetColumnSpan(searchRow, 2);

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 0)
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
                Text = "库存只能通过采购、销售、退货或有记录的库存调整发生变化。",
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
            label.Padding = new Padding(9, 5, 9, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8F, FontStyle.Bold);
        }

        private Control CreateContentSection()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel2;
            // Do not assign large Panel*MinSize values before the SplitContainer
            // has been laid out. WinForms validates them against the default
            // 150px constructor size and can throw before the page is shown.
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            ConfigureGrid();

            var gridHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None
            };
            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(new Label
            {
                Text = "库存表格",
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(12, 10, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(9F, FontStyle.Bold)
            });

            _split.Panel1.Controls.Add(gridHost);
            _split.Panel2.Controls.Add(CreateDetailPanel());
            return _split;
        }

        private Control CreateDetailPanel()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(14)
            };

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

            var adjust = new Button
            {
                Text = "库存调整",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                Tag = "primary"
            };
            adjust.Click += delegate { AdjustSelected(); };
            header.Controls.Add(adjust, 1, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0, 8, 0, 0)
            };

            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = UiTheme.Surface
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
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
            var row = table.RowCount;
            table.RowCount += 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            table.Controls.Add(new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 46),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 8, 0),
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

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = UiTheme.Surface;

            _grid.Columns.Add(_selfCodeColumn);
            _grid.Columns.Add(_isbnColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "书名",
                DataPropertyName = "Title",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 190,
                FillWeight = 220
            });
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_categoryColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "当前库存",
                DataPropertyName = "StockQuantity",
                Width = 88,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = UiTheme.Font(8.8F, FontStyle.Bold)
                }
            });

            _grid.CellFormatting += HighlightLowStock;
            _grid.CellDoubleClick += delegate { AdjustSelected(); };
            _grid.SelectionChanged += delegate { ShowSelectedDetails(); };
        }

        private void Reload()
        {
            var threshold = _services.Settings.GetLowStockThreshold();
            var books = _services.Books.Search(_search.Text, false);
            if (_lowOnly.Checked)
                books = books.Where(book => book.StockQuantity <= threshold).ToList();

            _grid.DataSource = books;

            var lowCount = 0;
            long stockTotal = 0;
            foreach (var book in books)
            {
                stockTotal += book.StockQuantity;
                if (book.StockQuantity <= threshold)
                    lowCount += 1;
            }

            _resultChip.Text = "结果  " + books.Count;
            _lowChip.Text = "低库存  " + lowCount;
            _stockChip.Text = "合计库存  " + stockTotal + " 册";
            _summary.Text = "显示 " + books.Count + " 个启用品种 · 低库存阈值 ≤ " + threshold + " 册 · 双击图书也可发起库存调整";

            ShowSelectedDetails();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Width <= 0)
                return;

            var showDetails = ClientSize.Width >= UiTheme.WideBreakpoint;
            _split.Panel2Collapsed = !showDetails;

            if (showDetails)
            {
                var desiredRightWidth = Math.Min(360, Math.Max(305, _split.Width / 3));
                var distance = _split.Width - desiredRightWidth - _split.SplitterWidth;
                const int minimumLeftWidth = 420;
                const int minimumRightWidth = 300;
                var maximumDistance = _split.Width - minimumRightWidth - _split.SplitterWidth;
                if (distance >= minimumLeftWidth && maximumDistance >= minimumLeftWidth)
                    _split.SplitterDistance = Math.Min(distance, maximumDistance);
            }

            var gridWidth = showDetails ? _split.Panel1.ClientSize.Width : ClientSize.Width;
            _selfCodeColumn.Visible = gridWidth >= 930;
            _categoryColumn.Visible = gridWidth >= 840;
            _shelfColumn.Visible = gridWidth >= 730;
            _authorColumn.Visible = gridWidth >= 650;
            _isbnColumn.Visible = gridWidth >= 570;
        }

        private void HighlightLowStock(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var book = _grid.Rows[e.RowIndex].DataBoundItem as Book;
            if (book == null) return;

            var low = book.StockQuantity <= _services.Settings.GetLowStockThreshold();
            _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = low ? UiTheme.Warning : UiTheme.TextPrimary;
            _grid.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = low ? UiTheme.Warning : UiTheme.TextPrimary;
        }

        private void ShowSelectedDetails()
        {
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                foreach (var value in _detailValues.Values)
                    value.Text = "—";
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
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                MessageBox.Show(this, "请先选择要调整库存的图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new AdjustmentDialog(book))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    var no = _services.Inventory.Adjust(book.Id, dialog.Delta, dialog.Note);
                    MessageBox.Show(
                        this,
                        "库存调整完成。\r\n流水号：" + no,
                        "调整成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
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
            private readonly ComboBox _direction = new ComboBox();
            private readonly NumericUpDown _quantity = new NumericUpDown();
            private readonly TextBox _note = new TextBox();
            private readonly Label _preview = new Label();

            public int Delta
            {
                get
                {
                    var amount = Decimal.ToInt32(_quantity.Value);
                    return _direction.SelectedIndex == 1 ? -amount : amount;
                }
            }

            public string Note
            {
                get { return _note.Text; }
            }

            public AdjustmentDialog(Book book)
            {
                _book = book;

                UiTheme.ConfigureForm(this);
                Text = "库存调整";
                StartPosition = FormStartPosition.CenterParent;
                Width = 600;
                Height = 470;
                MinimumSize = new Size(520, 420);
                BackColor = UiTheme.Background;
                ShowInTaskbar = false;
                MinimizeBox = false;

                _direction.DropDownStyle = ComboBoxStyle.DropDownList;
                _direction.Items.Add("增加库存");
                _direction.Items.Add("减少库存");
                _direction.SelectedIndex = 0;

                _quantity.Minimum = 1;
                _quantity.Maximum = 1000000;
                _quantity.Value = 1;
                _quantity.TextAlign = HorizontalAlignment.Right;

                _note.Multiline = true;
                _note.ScrollBars = ScrollBars.Vertical;

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
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                AddDialogLabel(body, 0, "调整方向 *");
                _direction.Dock = DockStyle.Fill;
                _direction.Margin = new Padding(8, 7, 0, 7);
                body.Controls.Add(_direction, 1, 0);

                AddDialogLabel(body, 1, "调整数量 *");
                _quantity.Dock = DockStyle.Fill;
                _quantity.Margin = new Padding(8, 7, 0, 7);
                body.Controls.Add(_quantity, 1, 1);

                body.Controls.Add(new Label
                {
                    Text = "调整后",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold)
                }, 0, 2);

                _preview.Dock = DockStyle.Fill;
                _preview.TextAlign = ContentAlignment.MiddleLeft;
                _preview.Padding = new Padding(8, 0, 0, 0);
                _preview.Font = UiTheme.Font(10F, FontStyle.Bold);
                body.Controls.Add(_preview, 1, 2);

                body.Controls.Add(new Label
                {
                    Text = "调整原因 *",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Padding = Padding.Empty,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold)
                }, 0, 3);

                _note.Dock = DockStyle.Fill;
                _note.Margin = new Padding(8, 7, 0, 7);
                body.Controls.Add(_note, 1, 3);

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    MinimumSize = new Size(0, 68),
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 11, 22, 11)
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

                var buttons = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = Padding.Empty
                };
                var cancel = new Button
                {
                    Text = "取消",
                    Width = 88,
                    Height = UiTheme.ButtonHeight,
                    DialogResult = DialogResult.Cancel
                };
                var ok = new Button
                {
                    Text = "确认调整",
                    Width = 104,
                    Height = UiTheme.ButtonHeight,
                    Tag = "primary"
                };
                ok.Click += Confirm;
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                footer.Controls.Add(buttons, 1, 0);

                Controls.Add(body);
                Controls.Add(footer);
                Controls.Add(header);

                AcceptButton = ok;
                CancelButton = cancel;

                _direction.SelectedIndexChanged += delegate { UpdatePreview(); };
                _quantity.ValueChanged += delegate { UpdatePreview(); };

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
                    MessageBox.Show(
                        this,
                        "减少后的库存不能小于 0。当前库存只有 " + _book.StockQuantity + " 册，请修改调整数量。",
                        "数量过大",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _quantity.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(Note))
                {
                    MessageBox.Show(
                        this,
                        "请填写调整原因，例如“盘点差异”“破损报废”或“录入修正”。",
                        "请填写原因",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _note.Focus();
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
