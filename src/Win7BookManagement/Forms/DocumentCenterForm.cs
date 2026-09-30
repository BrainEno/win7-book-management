using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class DocumentCenterForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Select _type = new AntdUI.Select();
        private readonly List<DocumentOption> _options = new List<DocumentOption>();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("单号、ISBN、书名、备注或供应商");
        private readonly AntdUI.Table _documents = new AntdUI.Table();
        private readonly AntdUI.Table _items = new AntdUI.Table();
        private readonly AntdUI.Button _returnButton;
        private readonly Dictionary<string, AntdUI.Column> _documentColumns = new Dictionary<string, AntdUI.Column>();
        private readonly Dictionary<string, AntdUI.Column> _itemColumns = new Dictionary<string, AntdUI.Column>();
        private readonly Label _detailTitle = new Label();
        private readonly Label _countChip = new Label();
        private readonly Label _amountChip = new Label();
        private readonly Label _emptyDocuments = new Label();
        private readonly Label _emptyItems = new Label();
        private readonly SplitContainer _split = new SplitContainer();

        private object _selectedDocumentRecord;

        public DocumentCenterForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            _returnButton = UiTheme.CreateAntdButton("从选中单据发起退货", true);
            _returnButton.Width = 168;

            ConfigureFilters();
            ConfigureTable(_documents, "当前条件下没有找到单据");
            ConfigureTable(_items, "选择上方单据后，这里显示书目明细");

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

            _documents.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedDocumentRecord = e.Record;
                LoadSelectedDetails();
            };
            _documents.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedDocumentRecord = e.Record;
                LoadSelectedDetails();
            };
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
            _options.Add(new DocumentOption("销售单", "sale"));
            _options.Add(new DocumentOption("采购单", "purchase"));
            _options.Add(new DocumentOption("销售退货", "sale_return"));
            _options.Add(new DocumentOption("采购退货", "purchase_return"));
            foreach (var option in _options) _type.Items.Add(option.Text);
            _type.SelectedIndex = 0;
            _type.DropDownArrow = true;

            _type.SelectedIndexChanged += delegate(object sender, AntdUI.IntEventArgs e) { ReloadDocuments(); };

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
            _returnButton.Click += delegate { StartReturn(); };
        }

        private string CurrentKind
        {
            get
            {
                var index = _type.SelectedIndex;
                return index >= 0 && index < _options.Count ? _options[index].Key : "sale";
            }
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
                Padding = new Padding(18, 15, 18, 15),
                Margin = Padding.Empty
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = "单据中心",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            _returnButton.Margin = Padding.Empty;
            section.Controls.Add(_returnButton, 1, 0);

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 62,
                MinimumSize = new Size(0, 62),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            filters.SizeChanged += delegate { ResizeFilterFlow(filters); };
            filters.Controls.Add(CreateFilterField("类型", _type, 160));
            filters.Controls.Add(CreateFilterField("从", _from, 132));
            filters.Controls.Add(CreateFilterField("到", _to, 132));
            filters.Controls.Add(CreateFilterField("关键词", _search, 270));

            var query = UiTheme.CreateAntdButton("查询", true);
            query.Width = 96;
            query.Margin = new Padding(0, 19, 0, 0);
            query.Click += delegate { ReloadDocuments(); };
            filters.Controls.Add(query);

            section.Controls.Add(filters, 0, 1);
            section.SetColumnSpan(filters, 2);

            var hint = new Label
            {
                Text = "支持按单号、ISBN、书名和备注搜索；采购单还支持供应商。退货必须从原单据发起。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(0, 6, 0, 0)
            };
            section.Controls.Add(hint, 0, 2);
            section.SetColumnSpan(hint, 2);

            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
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

        private static void ResizeFilterFlow(FlowLayoutPanel filters)
        {
            if (filters == null || filters.ClientSize.Width <= 0) return;
            var preferred = filters.GetPreferredSize(new Size(filters.ClientSize.Width, 0));
            var nextHeight = Math.Max(62, preferred.Height);
            if (filters.Height != nextHeight) filters.Height = nextHeight;
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
                Margin = new Padding(0, 0, 12, 0)
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
            label.Padding = new Padding(10, 5, 10, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(8.2F, FontStyle.Bold);
        }

        private Control CreateDocumentWorkspace()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.SplitterDistance = 330;
            _split.SplitterWidth = 8;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, 10, 0, 0);

            var documentHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            _emptyDocuments.Dock = DockStyle.Fill;
            _emptyDocuments.TextAlign = ContentAlignment.MiddleCenter;
            _emptyDocuments.Text = "当前条件下没有找到单据";
            _emptyDocuments.ForeColor = UiTheme.TextSecondary;
            _emptyDocuments.BackColor = UiTheme.Surface;
            _emptyDocuments.Font = UiTheme.Font(9F);
            documentHost.Controls.Add(_documents);
            documentHost.Controls.Add(_emptyDocuments);
            _split.Panel1.Controls.Add(documentHost);

            var detailHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
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

        private static void ConfigureTable(AntdUI.Table table, string emptyText)
        {
            table.Dock = DockStyle.Fill;

            table.RowHeight = 46;
            table.RowHeightHeader = 46;
            table.EnableHeaderResizing = true;
            table.ColumnDragSort = true;
            table.ShowTip = true;
            table.EmptyText = emptyText;

        }

        private void ReloadDocuments()
        {
            if (!IsHandleCreated) return;

            if (_to.Value.Date < _from.Value.Date)
            {
                MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _to.Focus();
                return;
            }

            try
            {
                var table = _services.Documents.Search(CurrentKind, _from.Value.Date, _to.Value.Date, _search.Text);
                _selectedDocumentRecord = null;
                BuildColumns(table, _documents, _documentColumns, true);
                _documents.DataSource = table;

                var canReturn = CurrentKind == "sale" || CurrentKind == "purchase";
                _returnButton.Enabled = canReturn;

                UpdateSummary(table);
                _emptyDocuments.Visible = table.Rows.Count == 0;

                if (table.Rows.Count == 0)
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
                    _selectedDocumentRecord = table.Rows[0];
                    _documents.SetSelected(table.Rows[0], false);
                    LoadSelectedDetails();
                }

                ApplyResponsiveColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询单据失败：\r\n" + ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void BuildColumns(
            DataTable source,
            AntdUI.Table target,
            Dictionary<string, AntdUI.Column> map,
            bool documentTable)
        {
            map.Clear();
            var collection = new AntdUI.ColumnCollection();
            if (source != null)
            {
                foreach (DataColumn dataColumn in source.Columns)
                {
                    var name = dataColumn.ColumnName;
                    var column = new AntdUI.Column(name, name)
                    {
                        Width = PreferredWidth(name),
                        Ellipsis = name == "书名" || name == "备注"
                    };
                    if (name == "Id") column.Visible = false;
                    if (name == "书名") { column.Width = "auto"; column.MinWidth = "210"; }
                    if (name == "备注") { column.Width = "auto"; column.MinWidth = "160"; }
                    if (name.Contains("金额") || name.Contains("价")) column.DisplayFormat = "0.00";
                    map[name] = column;
                    collection.Add(column);
                }
            }
            target.Columns = collection;
        }

        private static string PreferredWidth(string name)
        {
            if (name == "日期") return "150";
            if (name.Contains("单号")) return "172";
            if (name == "供应商") return "136";
            if (name == "ISBN") return "142";
            if (name == "店内编码") return "112";
            if (name == "状态") return "90";
            if (name.Contains("数量") || name == "已退" || name == "可退") return "86";
            if (name.Contains("金额")) return "108";
            if (name.Contains("价")) return "96";
            return "120";
        }

        private void UpdateSummary(DataTable table)
        {
            _countChip.Text = "单据  " + table.Rows.Count;
            decimal total = 0m;
            var amountColumn = AmountColumnName();
            if (table.Columns.Contains(amountColumn))
            {
                foreach (DataRow row in table.Rows)
                    if (row[amountColumn] != DBNull.Value) total += Convert.ToDecimal(row[amountColumn]);
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
                case "purchase": return "金额";
                case "sale_return": return "退款金额";
                default: return "退货金额";
            }
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            SetVisible(_documentColumns, "备注", width >= 940);
            SetVisible(_documentColumns, "供应商", width >= 820);
            SetVisible(_itemColumns, "ISBN", width >= 760);
            SetVisible(_itemColumns, "已退", width >= 700);
            _documents.LoadLayout();
            _items.LoadLayout();
        }

        private static void SetVisible(Dictionary<string, AntdUI.Column> map, string name, bool visible)
        {
            AntdUI.Column column;
            if (map.TryGetValue(name, out column)) column.Visible = visible;
        }

        private void ResizeSplit()
        {
            if (_split.Height <= 360) return;
            var target = (int)(_split.Height * 0.56);
            var max = _split.Height - 150 - _split.SplitterWidth;
            if (max > 180) _split.SplitterDistance = Math.Max(180, Math.Min(max, target));
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
                BuildColumns(table, _items, _itemColumns, false);
                _items.DataSource = table;
                _detailTitle.Text = "单据明细 · " + _services.Documents.GetDocumentNo(CurrentKind, id);
                _emptyItems.Visible = table.Rows.Count == 0;
                if (_emptyItems.Visible) _emptyItems.BringToFront();
                else _items.BringToFront();
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
                    if (line.ReturnableQuantity > 0) { hasReturnable = true; break; }
                }

                if (!hasReturnable)
                {
                    MessageBox.Show(this, "这张单据已经没有可退数量。", "没有可退图书", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dialog = new ReturnDialog(_services, CurrentKind, id, no))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) ReloadDocuments();
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
            if (_selectedDocumentRecord == null) return false;

            var row = _selectedDocumentRecord as DataRow;
            if (row != null)
            {
                if (!row.Table.Columns.Contains("Id") || row["Id"] == DBNull.Value) return false;
                return long.TryParse(Convert.ToString(row["Id"]), out id);
            }

            var view = _selectedDocumentRecord as DataRowView;
            if (view != null)
            {
                if (!view.DataView.Table.Columns.Contains("Id") || view["Id"] == DBNull.Value) return false;
                return long.TryParse(Convert.ToString(view["Id"]), out id);
            }

            return false;
        }

        private sealed class DocumentOption
        {
            public DocumentOption(string text, string key) { Text = text; Key = key; }
            public string Text { get; private set; }
            public string Key { get; private set; }
        }
    }
}
