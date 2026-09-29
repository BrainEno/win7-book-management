using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class DocumentCenterForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly ComboBox _type = new ComboBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly TextBox _search = new TextBox();
        private readonly DataGridView _documents = new DataGridView();
        private readonly DataGridView _items = new DataGridView();
        private readonly Button _returnButton = new Button();
        private readonly Label _detailTitle = new Label();
        private readonly Label _countChip = new Label();
        private readonly Label _typeChip = new Label();
        private readonly Label _emptyDetails = new Label();
        private readonly SplitContainer _split = new SplitContainer();

        public DocumentCenterForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            ConfigureFilters();

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

            root.Controls.Add(CreateFilterSection(), 0, 0);
            root.Controls.Add(CreateDocumentWorkspace(), 0, 1);
            root.Controls.Add(CreateFooterHint(), 0, 2);

            Controls.Add(root);

            Resize += delegate { ApplyResponsiveLayout(); };
            UiTheme.Apply(this);

            Shown += delegate
            {
                ReloadDocuments();
                ApplyResponsiveLayout();
                _search.Focus();
            };
        }

        private void ConfigureFilters()
        {
            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Items.Add(new DocumentOption("销售单", "sale"));
            _type.Items.Add(new DocumentOption("采购单", "purchase"));
            _type.Items.Add(new DocumentOption("销售退货", "sale_return"));
            _type.Items.Add(new DocumentOption("采购退货", "purchase_return"));
            _type.SelectedIndex = 0;
            _type.SelectedIndexChanged += delegate { ReloadDocuments(); };

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;

            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ReloadDocuments();
                    e.SuppressKeyPress = true;
                }
            };

            _returnButton.Text = "发起退货";
            _returnButton.Tag = "primary";
            _returnButton.Click += delegate { StartReturn(); };
        }

        private Control CreateFilterSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.FixedSingle
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            section.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = "单据中心",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 4, 0, 8)
            }, 0, 0);

            var headerActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _returnButton.Width = 104;
            _returnButton.Height = UiTheme.ButtonHeight;
            headerActions.Controls.Add(_returnButton);
            header.Controls.Add(headerActions, 1, 0);
            section.Controls.Add(header, 0, 0);

            var filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
                Margin = Padding.Empty
            };
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            filterRow.Controls.Add(CreateFilterLabel("类型"), 0, 0);
            _type.Dock = DockStyle.Fill;
            _type.Margin = new Padding(0, 4, 12, 4);
            filterRow.Controls.Add(_type, 1, 0);

            filterRow.Controls.Add(CreateFilterLabel("开始"), 2, 0);
            _from.Dock = DockStyle.Fill;
            _from.Margin = new Padding(0, 4, 12, 4);
            filterRow.Controls.Add(_from, 3, 0);

            filterRow.Controls.Add(CreateFilterLabel("结束"), 4, 0);
            _to.Dock = DockStyle.Fill;
            _to.Margin = new Padding(0, 4, 0, 4);
            filterRow.Controls.Add(_to, 5, 0);

            section.Controls.Add(filterRow, 0, 1);

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

            searchRow.Controls.Add(CreateFilterLabel("搜索"), 0, 0);
            _search.Dock = DockStyle.Fill;
            _search.Margin = new Padding(0, 4, 10, 4);
            _search.Font = UiTheme.Font(9.5F);
            searchRow.Controls.Add(_search, 1, 0);

            var query = new Button
            {
                Text = "查询",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            query.Click += delegate { ReloadDocuments(); };
            searchRow.Controls.Add(query, 2, 0);
            section.Controls.Add(searchRow, 0, 2);

            var stats = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            ConfigureChip(_typeChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_countChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            stats.Controls.Add(_typeChip);
            stats.Controls.Add(_countChip);
            stats.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "可按单号、ISBN、书名、备注搜索；采购单还支持供应商名称。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(4, 5, 0, 0)
            });
            section.Controls.Add(stats, 0, 3);

            return section;
        }

        private static Label CreateFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F, FontStyle.Bold)
            };
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

        private Control CreateDocumentWorkspace()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.SplitterDistance = 320;
            _split.SplitterWidth = 8;
            _split.Panel1MinSize = 180;
            _split.Panel2MinSize = 150;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            ConfigureGrid(_documents);
            ConfigureGrid(_items);

            _documents.SelectionChanged += delegate { LoadSelectedDetails(); };
            _documents.CellDoubleClick += delegate { LoadSelectedDetails(); };

            var documentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle
            };
            documentHost.Controls.Add(_documents);
            documentHost.Controls.Add(new Label
            {
                Text = "单据列表",
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(12, 11, 0, 0),
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            });
            _split.Panel1.Controls.Add(documentHost);

            var detailHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.FixedSingle
            };

            _detailTitle.Text = "单据明细";
            _detailTitle.Dock = DockStyle.Top;
            _detailTitle.Height = 40;
            _detailTitle.Padding = new Padding(12, 11, 0, 0);
            _detailTitle.BackColor = UiTheme.Surface;
            _detailTitle.Font = UiTheme.Font(9.5F, FontStyle.Bold);
            _detailTitle.ForeColor = UiTheme.TextPrimary;

            _emptyDetails.Dock = DockStyle.Fill;
            _emptyDetails.Text = "选择一张单据后，这里会显示原始商品快照和退货状态。";
            _emptyDetails.TextAlign = ContentAlignment.MiddleCenter;
            _emptyDetails.ForeColor = UiTheme.TextSecondary;
            _emptyDetails.BackColor = UiTheme.Surface;
            _emptyDetails.Font = UiTheme.Font(8.5F);

            detailHost.Controls.Add(_items);
            detailHost.Controls.Add(_emptyDetails);
            detailHost.Controls.Add(_detailTitle);
            _emptyDetails.BringToFront();
            _split.Panel2.Controls.Add(detailHost);

            return _split;
        }

        private Control CreateFooterHint()
        {
            return new Label
            {
                Text = "退货必须从原销售单或采购单发起，系统会保留原单并新建独立退货单；历史快照不会被改写。",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(10, 9, 8, 0),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(8F)
            };
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = UiTheme.Surface;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        }

        private string CurrentKind
        {
            get
            {
                var option = _type.SelectedItem as DocumentOption;
                return option == null ? "sale" : option.Key;
            }
        }

        private string CurrentTypeText
        {
            get
            {
                var option = _type.SelectedItem as DocumentOption;
                return option == null ? "销售单" : option.Text;
            }
        }

        private void ReloadDocuments()
        {
            try
            {
                var table = _services.Documents.Search(CurrentKind, _from.Value.Date, _to.Value.Date, _search.Text);
                _documents.DataSource = table;
                ApplyDocumentColumns();

                _typeChip.Text = CurrentTypeText;
                _countChip.Text = "结果  " + table.Rows.Count;
                _returnButton.Enabled = CurrentKind == "sale" || CurrentKind == "purchase";
                _returnButton.Text = CurrentKind == "purchase" ? "发起采购退货" :
                                     CurrentKind == "sale" ? "发起销售退货" : "当前类型不可退";

                if (_documents.Rows.Count == 0)
                {
                    _items.DataSource = null;
                    _detailTitle.Text = "单据明细";
                    _emptyDetails.Text = "当前条件没有找到单据。";
                    _emptyDetails.Visible = true;
                    _emptyDetails.BringToFront();
                }
                else
                {
                    _documents.Rows[0].Selected = true;
                    var first = FirstVisibleColumnIndex(_documents);
                    if (first >= 0)
                        _documents.CurrentCell = _documents.Rows[0].Cells[first];
                    LoadSelectedDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询单据失败：\r\n" + ex.Message, "请检查查询条件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadSelectedDetails()
        {
            try
            {
                long id;
                if (!TryGetSelectedId(out id))
                {
                    _items.DataSource = null;
                    _detailTitle.Text = "单据明细";
                    _emptyDetails.Visible = true;
                    _emptyDetails.BringToFront();
                    return;
                }

                var table = _services.Documents.GetItems(CurrentKind, id);
                _items.DataSource = table;
                ApplyItemColumns();
                _detailTitle.Text = "单据明细 · " + _services.Documents.GetDocumentNo(CurrentKind, id);
                _emptyDetails.Visible = table.Rows.Count == 0;
                if (_emptyDetails.Visible)
                    _emptyDetails.BringToFront();
                else
                    _items.BringToFront();
            }
            catch
            {
                _items.DataSource = null;
                _detailTitle.Text = "单据明细";
                _emptyDetails.Visible = true;
                _emptyDetails.BringToFront();
            }
        }

        private void ApplyDocumentColumns()
        {
            HideIdColumn(_documents);

            foreach (DataGridViewColumn column in _documents.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.Automatic;
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

                switch (column.Name)
                {
                    case "日期":
                        column.Width = 142;
                        break;
                    case "单号":
                    case "退货单号":
                    case "原销售单号":
                    case "原采购单号":
                        column.Width = 172;
                        break;
                    case "供应商":
                        column.Width = 150;
                        break;
                    case "数量":
                        column.Width = 76;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "金额":
                    case "退款金额":
                    case "退货金额":
                        column.Width = 96;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        column.DefaultCellStyle.Format = "0.00";
                        break;
                    case "状态":
                        column.Width = 92;
                        break;
                    case "备注":
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        column.MinimumWidth = 120;
                        break;
                    default:
                        column.Width = 110;
                        break;
                }
            }

            ApplyResponsiveDocumentColumns();
        }

        private void ApplyItemColumns()
        {
            foreach (DataGridViewColumn column in _items.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

                switch (column.Name)
                {
                    case "ISBN":
                        column.Width = 132;
                        break;
                    case "书名":
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        column.MinimumWidth = 190;
                        break;
                    case "原数量":
                    case "已退":
                    case "可退":
                    case "退货数量":
                        column.Width = 76;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case "单价":
                    case "进价":
                    case "原售价":
                    case "原进价":
                    case "金额":
                    case "退款金额":
                    case "退货金额":
                        column.Width = 94;
                        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        column.DefaultCellStyle.Format = "0.00";
                        break;
                    default:
                        column.Width = 100;
                        break;
                }
            }

            ApplyResponsiveItemColumns();
        }

        private void ApplyResponsiveLayout()
        {
            if (_split.Height > 360)
            {
                var target = (int)(_split.Height * 0.56);
                var maximum = _split.Height - _split.Panel2MinSize - _split.SplitterWidth;
                if (maximum >= _split.Panel1MinSize)
                    _split.SplitterDistance = Math.Max(_split.Panel1MinSize, Math.Min(maximum, target));
            }

            ApplyResponsiveDocumentColumns();
            ApplyResponsiveItemColumns();
        }

        private void ApplyResponsiveDocumentColumns()
        {
            var width = ClientSize.Width;

            SetColumnVisible(_documents, "备注", width >= 900);
            SetColumnVisible(_documents, "供应商", width >= 820);
            SetColumnVisible(_documents, "原销售单号", width >= 780);
            SetColumnVisible(_documents, "原采购单号", width >= 780);
        }

        private void ApplyResponsiveItemColumns()
        {
            var width = ClientSize.Width;
            SetColumnVisible(_items, "ISBN", width >= 700);
            SetColumnVisible(_items, "已退", width >= 760);
        }

        private static void SetColumnVisible(DataGridView grid, string name, bool visible)
        {
            if (grid.Columns.Contains(name))
                grid.Columns[name].Visible = visible;
        }

        private void StartReturn()
        {
            try
            {
                if (CurrentKind != "sale" && CurrentKind != "purchase")
                    return;

                long id;
                if (!TryGetSelectedId(out id))
                {
                    MessageBox.Show(this, "请先选择一张原销售单或采购单。", "还没有选择原单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var no = _services.Documents.GetDocumentNo(CurrentKind, id);
                var lines = _services.Documents.GetReturnableLines(CurrentKind, id);
                var hasReturnable = false;
                foreach (var line in lines)
                {
                    if (line.ReturnableQuantity > 0)
                    {
                        hasReturnable = true;
                        break;
                    }
                }

                if (!hasReturnable)
                {
                    MessageBox.Show(this, "这张单据已经没有可退数量。", "无需退货", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dialog = new ReturnDialog(_services, CurrentKind, id, no))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        ReloadDocuments();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法发起退货：\r\n" + ex.Message, "请检查原单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool TryGetSelectedId(out long id)
        {
            id = 0;
            if (_documents.CurrentRow == null) return false;
            var value = _documents.CurrentRow.Cells["Id"].Value;
            if (value == null || value == DBNull.Value) return false;
            return long.TryParse(Convert.ToString(value), out id);
        }

        private static void HideIdColumn(DataGridView grid)
        {
            if (grid.Columns.Contains("Id"))
                grid.Columns["Id"].Visible = false;
        }

        private static int FirstVisibleColumnIndex(DataGridView grid)
        {
            for (var i = 0; i < grid.Columns.Count; i++)
            {
                if (grid.Columns[i].Visible)
                    return i;
            }

            return -1;
        }

        private sealed class DocumentOption
        {
            public DocumentOption(string text, string key)
            {
                Text = text;
                Key = key;
            }

            public string Text { get; private set; }
            public string Key { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
