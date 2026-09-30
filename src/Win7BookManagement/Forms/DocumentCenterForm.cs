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
        private readonly Label _amountChip = new Label();
        private readonly Label _emptyDocuments = new Label();
        private readonly Label _emptyItems = new Label();
        private readonly SplitContainer _split = new SplitContainer();

        public DocumentCenterForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            ConfigureFilters();
            ConfigureGrid(_documents);
            ConfigureGrid(_items);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(CreateSearchSection(), 0, 0);
            root.Controls.Add(CreateDocumentWorkspace(), 0, 1);

            Controls.Add(root);

            _documents.SelectionChanged += delegate { LoadSelectedDetails(); };
            _documents.CellDoubleClick += delegate { LoadSelectedDetails(); };
            Resize += delegate
            {
                ResizeSplit();
                ApplyResponsiveColumns();
            };

            UiTheme.Apply(this);
            Shown += delegate
            {
                ReloadDocuments();
                ResizeSplit();
                ApplyResponsiveColumns();
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

            _search.Font = UiTheme.Font(9.2F);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ReloadDocuments();
                    e.SuppressKeyPress = true;
                }
            };

            _returnButton.Text = "从选中单据发起退货";
            _returnButton.Width = 160;
            _returnButton.Height = UiTheme.ButtonHeight;
            _returnButton.Tag = "primary";
            _returnButton.Click += delegate { StartReturn(); };
        }

        private Control CreateSearchSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (var row = 0; row < 4; row++)
                section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "单据查询",
                AutoSize = true,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            _returnButton.Margin = Padding.Empty;
            section.Controls.Add(_returnButton, 1, 0);

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 58),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 0),
                Padding = Padding.Empty
            };

            filters.Controls.Add(CreateFilterField("类型", _type, 150));
            filters.Controls.Add(CreateFilterField("从", _from, 126));
            filters.Controls.Add(CreateFilterField("到", _to, 126));
            filters.Controls.Add(CreateFilterField("关键词", _search, 250));

            var query = new Button
            {
                Text = "查询",
                Width = 92,
                Height = UiTheme.ButtonHeight,
                Margin = new Padding(0, 18, 0, 0)
            };
            query.Click += delegate { ReloadDocuments(); };
            filters.Controls.Add(query);

            section.Controls.Add(filters, 0, 1);
            section.SetColumnSpan(filters, 2);

            section.Controls.Add(new Label
            {
                Text = "支持按单号、ISBN、书名和备注搜索；采购相关单据还支持供应商名称。退货必须从原销售单或原采购单发起。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 6, 0, 0)
            }, 0, 2);
            section.SetColumnSpan(section.GetControlFromPosition(0, 2), 2);

            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            ConfigureChip(_countChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_amountChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            chips.Controls.Add(_countChip);
            chips.Controls.Add(_amountChip);

            section.Controls.Add(chips, 0, 3);
            section.SetColumnSpan(chips, 2);

            return section;
        }

        private static Control CreateFilterField(string labelText, Control input, int width)
        {
            var field = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                MinimumSize = new Size(width, 0),
                Margin = new Padding(0, 0, 12, 0),
                Padding = Padding.Empty
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            field.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.2F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            input.Dock = DockStyle.Top;
            input.Width = width;
            input.Margin = Padding.Empty;
            field.Controls.Add(input, 0, 1);
            return field;
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
            _split.SplitterDistance = 330;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            // Keep constructor-time panel minimums at WinForms defaults; the
            // real minimums are applied arithmetically after layout.
            _split.Margin = new Padding(0, 10, 0, 0);

            var documentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None
            };
            var documentHeader = new Label
            {
                Text = "单据列表",
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(12, 10, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(9.2F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            _emptyDocuments.Dock = DockStyle.Fill;
            _emptyDocuments.TextAlign = ContentAlignment.MiddleCenter;
            _emptyDocuments.Text = "当前条件下没有找到单据";
            _emptyDocuments.ForeColor = UiTheme.TextSecondary;
            _emptyDocuments.BackColor = UiTheme.Surface;
            _emptyDocuments.Font = UiTheme.Font(9F);

            documentHost.Controls.Add(_documents);
            documentHost.Controls.Add(_emptyDocuments);
            documentHost.Controls.Add(documentHeader);
            _split.Panel1.Controls.Add(documentHost);

            var detailHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None
            };
            _detailTitle.Text = "单据明细";
            _detailTitle.Dock = DockStyle.Top;
            _detailTitle.AutoSize = true;
            _detailTitle.MinimumSize = new Size(0, 42);
            _detailTitle.Padding = new Padding(12, 10, 0, 10);
            _detailTitle.BackColor = UiTheme.Surface;
            _detailTitle.Font = UiTheme.Font(9.2F, FontStyle.Bold);
            _detailTitle.ForeColor = UiTheme.TextPrimary;

            _emptyItems.Dock = DockStyle.Fill;
            _emptyItems.TextAlign = ContentAlignment.MiddleCenter;
            _emptyItems.Text = "选择上方单据后，这里显示书目明细";
            _emptyItems.ForeColor = UiTheme.TextSecondary;
            _emptyItems.BackColor = UiTheme.Surface;
            _emptyItems.Font = UiTheme.Font(9F);

            detailHost.Controls.Add(_items);
            detailHost.Controls.Add(_emptyItems);
            detailHost.Controls.Add(_detailTitle);
            _split.Panel2.Controls.Add(detailHost);

            return _split;
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = UiTheme.Surface;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        private string CurrentKind
        {
            get
            {
                var option = _type.SelectedItem as DocumentOption;
                return option == null ? "sale" : option.Key;
            }
        }

        private void ReloadDocuments()
        {
            if (!IsHandleCreated)
                return;

            if (_to.Value.Date < _from.Value.Date)
            {
                MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _to.Focus();
                return;
            }

            try
            {
                var table = _services.Documents.Search(CurrentKind, _from.Value.Date, _to.Value.Date, _search.Text);
                _documents.DataSource = table;
                HideIdColumn(_documents);
                StyleDocumentColumns();

                var canReturn = CurrentKind == "sale" || CurrentKind == "purchase";
                _returnButton.Enabled = canReturn;

                UpdateSummary(table);
                _emptyDocuments.Visible = _documents.Rows.Count == 0;

                if (_documents.Rows.Count == 0)
                {
                    _items.DataSource = null;
                    _detailTitle.Text = "单据明细";
                    _emptyDocuments.BringToFront();
                    _emptyItems.Visible = true;
                    _emptyItems.BringToFront();
                }
                else
                {
                    _documents.BringToFront();
                    _documents.Rows[0].Selected = true;
                    var first = FirstVisibleColumnIndex(_documents);
                    if (first >= 0)
                        _documents.CurrentCell = _documents.Rows[0].Cells[first];
                    LoadSelectedDetails();
                }

                ApplyResponsiveColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询单据失败：\r\n" + ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateSummary(DataTable table)
        {
            _countChip.Text = "单据  " + table.Rows.Count;

            decimal total = 0m;
            var amountColumn = AmountColumnName();
            if (table.Columns.Contains(amountColumn))
            {
                foreach (DataRow row in table.Rows)
                {
                    if (row[amountColumn] != DBNull.Value)
                        total += Convert.ToDecimal(row[amountColumn]);
                }
            }

            string label;
            switch (CurrentKind)
            {
                case "sale": label = "销售金额"; break;
                case "purchase": label = "采购金额"; break;
                case "sale_return": label = "退款金额"; break;
                default: label = "退货金额"; break;
            }

            _amountChip.Text = label + "  ¥" + total.ToString("0.00");
        }

        private string AmountColumnName()
        {
            switch (CurrentKind)
            {
                case "sale":
                case "purchase":
                    return "金额";
                case "sale_return":
                    return "退款金额";
                default:
                    return "退货金额";
            }
        }

        private void StyleDocumentColumns()
        {
            SetColumnWidth(_documents, "日期", 146);
            SetColumnWidth(_documents, "单号", 170);
            SetColumnWidth(_documents, "退货单号", 170);
            SetColumnWidth(_documents, "原销售单号", 170);
            SetColumnWidth(_documents, "原采购单号", 170);
            SetColumnWidth(_documents, "供应商", 130);
            SetColumnWidth(_documents, "数量", 72);
            SetColumnWidth(_documents, "金额", 92);
            SetColumnWidth(_documents, "退款金额", 92);
            SetColumnWidth(_documents, "退货金额", 92);
            SetColumnWidth(_documents, "状态", 86);

            SetNumericAlignment(_documents, "数量");
            SetNumericAlignment(_documents, "金额");
            SetNumericAlignment(_documents, "退款金额");
            SetNumericAlignment(_documents, "退货金额");

            if (_documents.Columns.Contains("备注"))
            {
                _documents.Columns["备注"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _documents.Columns["备注"].MinimumWidth = 140;
                _documents.Columns["备注"].FillWeight = 180;
            }
        }

        private void StyleItemColumns()
        {
            SetColumnWidth(_items, "ISBN", 132);
            SetColumnWidth(_items, "原数量", 76);
            SetColumnWidth(_items, "已退", 66);
            SetColumnWidth(_items, "可退", 66);
            SetColumnWidth(_items, "退货数量", 82);
            SetColumnWidth(_items, "单价", 88);
            SetColumnWidth(_items, "进价", 88);
            SetColumnWidth(_items, "原售价", 88);
            SetColumnWidth(_items, "原进价", 88);
            SetColumnWidth(_items, "金额", 96);
            SetColumnWidth(_items, "退款金额", 96);
            SetColumnWidth(_items, "退货金额", 96);

            if (_items.Columns.Contains("书名"))
            {
                _items.Columns["书名"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _items.Columns["书名"].MinimumWidth = 190;
                _items.Columns["书名"].FillWeight = 220;
            }

            foreach (var name in new[] { "原数量", "已退", "可退", "退货数量", "单价", "进价", "原售价", "原进价", "金额", "退款金额", "退货金额" })
                SetNumericAlignment(_items, name);
        }

        private static void SetColumnWidth(DataGridView grid, string name, int width)
        {
            if (grid.Columns.Contains(name))
                grid.Columns[name].Width = width;
        }

        private static void SetNumericAlignment(DataGridView grid, string name)
        {
            if (grid.Columns.Contains(name))
                grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;

            SetColumnVisible(_documents, "备注", width >= 940);
            SetColumnVisible(_documents, "供应商", width >= 820);

            SetColumnVisible(_items, "ISBN", width >= 760);
            SetColumnVisible(_items, "已退", width >= 700);

            var first = FirstVisibleColumnIndex(_documents);
            if (_documents.CurrentRow != null &&
                (_documents.CurrentCell == null || !_documents.CurrentCell.OwningColumn.Visible) &&
                first >= 0)
            {
                _documents.CurrentCell = _documents.CurrentRow.Cells[first];
            }
        }

        private static void SetColumnVisible(DataGridView grid, string name, bool visible)
        {
            if (grid.Columns.Contains(name))
                grid.Columns[name].Visible = visible;
        }

        private void ResizeSplit()
        {
            if (_split.Height <= 360)
                return;

            var target = (int)(_split.Height * 0.56);
            const int minimumTopHeight = 180;
            const int minimumBottomHeight = 150;
            var max = _split.Height - minimumBottomHeight - _split.SplitterWidth;
            if (max > minimumTopHeight)
                _split.SplitterDistance = Math.Max(minimumTopHeight, Math.Min(max, target));
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
                    _emptyItems.Visible = true;
                    _emptyItems.BringToFront();
                    return;
                }

                var table = _services.Documents.GetItems(CurrentKind, id);
                _items.DataSource = table;
                StyleItemColumns();
                _detailTitle.Text = "单据明细 · " + _services.Documents.GetDocumentNo(CurrentKind, id);
                _emptyItems.Visible = _items.Rows.Count == 0;

                if (_items.Rows.Count == 0)
                    _emptyItems.BringToFront();
                else
                    _items.BringToFront();

                ApplyResponsiveColumns();
            }
            catch
            {
                _items.DataSource = null;
                _detailTitle.Text = "单据明细";
                _emptyItems.Visible = true;
                _emptyItems.BringToFront();
            }
        }

        private void StartReturn()
        {
            try
            {
                if (CurrentKind != "sale" && CurrentKind != "purchase")
                {
                    MessageBox.Show(this, "退货需要从原销售单或原采购单发起。请先切换到相应单据类型。", "请选择原单据", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                long id;
                if (!TryGetSelectedId(out id))
                {
                    MessageBox.Show(this, "请先选择一张原销售单或采购单。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, "这张单据已经没有可退数量。", "没有可退图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "无法发起退货：\r\n" + ex.Message, "退货准备失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool TryGetSelectedId(out long id)
        {
            id = 0;
            if (_documents.CurrentRow == null || !_documents.Columns.Contains("Id"))
                return false;

            var value = _documents.CurrentRow.Cells["Id"].Value;
            if (value == null || value == DBNull.Value)
                return false;

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
