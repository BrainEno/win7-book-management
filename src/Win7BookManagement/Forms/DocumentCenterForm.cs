using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class DocumentCenterForm : Form, IUiSpecPage
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Select _type = new AntdUI.Select();
        private readonly List<DocumentOption> _options = new List<DocumentOption>();
        private readonly AntdUI.DatePicker _from = new AntdUI.DatePicker();
        private readonly AntdUI.DatePicker _to = new AntdUI.DatePicker();
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

        private UiSpecSectionPanel _filterSection;
        private TableLayoutPanel _actionRow;
        private TableLayoutPanel _filterRow;
        private TableLayoutPanel _typeField;
        private TableLayoutPanel _fromField;
        private TableLayoutPanel _toField;
        private TableLayoutPanel _searchField;
        private Label _filterHint;
        private FlowLayoutPanel _chips;
        private AntdUI.Button _queryButton;
        private UiSpecSectionPanel _documentSurface;
        private UiSpecSectionPanel _detailSurface;
        private UiSpecProfile _profile = BookDeskUiSpec.Standard;

        private object _selectedDocumentRecord;

        public DocumentCenterForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;
            _returnButton = UiTheme.CreateAntdButton("从选中单据发起退货", true);
            _returnButton.IconSvg = "RollbackOutlined";
            _returnButton.Width = BookDeskUiSpec.DocumentsTopActionWidth;

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
            ApplyUiSpecProfile(BookDeskUiSpec.Standard);
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

            _from.Format = "yyyy-MM-dd";
            _to.Format = "yyyy-MM-dd";
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

        private DateTime FromDate { get { return (_from.Value ?? DateTime.Today).Date; } }
        private DateTime ToDate { get { return (_to.Value ?? DateTime.Today).Date; } }

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
            _filterSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.DocumentsFilterStandardHeight),
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Surface,
                Padding = new Padding(BookDeskUiSpec.Standard.ToolbarPadding),
                Margin = Padding.Empty
            };
            _filterSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _filterSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _filterSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _filterSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _filterSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _actionRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.Standard.ControlHeight),
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _returnButton.Anchor = AnchorStyles.Right;
            _returnButton.Margin = Padding.Empty;
            _actionRow.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
            _actionRow.Controls.Add(_returnButton, 1, 0);

            _filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 1,
                Margin = new Padding(0, 8, 0, 0),
                Padding = Padding.Empty
            };
            _filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BookDeskUiSpec.DocumentsTypeStandardWidth));
            _filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BookDeskUiSpec.DocumentsDateStandardWidth));
            _filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BookDeskUiSpec.DocumentsDateStandardWidth));
            _filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _typeField = CreateFilterField("类型", _type);
            _fromField = CreateFilterField("从", _from);
            _toField = CreateFilterField("到", _to);
            _searchField = CreateFilterField("关键词", _search);

            _filterRow.Controls.Add(_typeField, 0, 0);
            _filterRow.Controls.Add(_fromField, 1, 0);
            _filterRow.Controls.Add(_toField, 2, 0);
            _filterRow.Controls.Add(_searchField, 3, 0);

            _queryButton = UiTheme.CreateAntdButton("查询", true);
            _queryButton.Width = BookDeskUiSpec.DocumentsQueryWidth;
            _queryButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _queryButton.Margin = new Padding(0, 22, 0, 0);
            _queryButton.Click += delegate { ReloadDocuments(); };
            _filterRow.Controls.Add(_queryButton, 4, 0);

            _filterHint = new Label
            {
                Text = "支持按单号、ISBN、书名和备注搜索；采购单还支持供应商。退货必须从原单据发起。",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints),
                Margin = new Padding(0, 10, 0, 0)
            };

            _chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 10, 0, 0),
                Padding = Padding.Empty
            };
            ConfigureChip(_countChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_amountChip, Color.FromArgb(255, 247, 230), UiTheme.Warning);
            _chips.Controls.Add(_countChip);
            _chips.Controls.Add(_amountChip);

            _filterSection.Controls.Add(_actionRow, 0, 0);
            _filterSection.Controls.Add(_filterRow, 0, 1);
            _filterSection.Controls.Add(_filterHint, 0, 2);
            _filterSection.Controls.Add(_chips, 0, 3);
            return _filterSection;
        }

        private static TableLayoutPanel CreateFilterField(string labelText, Control input)
        {
            var field = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, BookDeskUiSpec.DocumentsFilterFieldGap, 0),
                Padding = Padding.Empty
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);
            input.Dock = DockStyle.Fill;
            input.Margin = Padding.Empty;
            field.Controls.Add(input, 0, 1);
            return field;
        }

        private static void ConfigureChip(Label label, Color backColor, Color foreColor)
        {
            label.AutoSize = false;
            label.Height = BookDeskUiSpec.Standard.MetricHeight;
            label.Padding = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Margin = new Padding(0, 0, 10, 0);
            label.BackColor = backColor;
            label.ForeColor = foreColor;
            label.Font = UiTheme.Font(BookDeskUiSpec.Standard.BodyFontPoints, FontStyle.Bold);
        }

        private Control CreateDocumentWorkspace()
        {
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.SplitterDistance = BookDeskUiSpec.DocumentsListReferenceHeight;
            _split.SplitterWidth = BookDeskUiSpec.Standard.SectionGap;
            _split.IsSplitterFixed = true;
            _split.BackColor = UiTheme.Background;
            _split.Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0);

            _documentSurface = new UiSpecSectionPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _documentSurface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _documentSurface.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _emptyDocuments.Dock = DockStyle.Fill;
            _emptyDocuments.TextAlign = ContentAlignment.MiddleCenter;
            _emptyDocuments.Text = "当前条件下没有找到单据";
            _emptyDocuments.ForeColor = UiTheme.TextSecondary;
            _emptyDocuments.BackColor = UiTheme.Surface;
            _emptyDocuments.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);
            _documentSurface.Controls.Add(_documents, 0, 0);
            _documentSurface.Controls.Add(_emptyDocuments, 0, 0);

            _detailSurface = new UiSpecSectionPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _detailSurface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _detailSurface.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _detailSurface.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _detailTitle.Text = "单据明细";
            _detailTitle.Dock = DockStyle.Fill;
            _detailTitle.AutoSize = false;
            _detailTitle.MinimumSize = new Size(0, BookDeskUiSpec.DocumentsDetailTitleHeight);
            _detailTitle.Padding = new Padding(14, 0, 0, 0);
            _detailTitle.TextAlign = ContentAlignment.MiddleLeft;
            _detailTitle.BackColor = UiTheme.Surface;
            _detailTitle.Font = UiTheme.Font(BookDeskUiSpec.PixelFontToPoints(16), FontStyle.Bold);
            _detailTitle.ForeColor = UiTheme.TextPrimary;

            var itemHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _emptyItems.Dock = DockStyle.Fill;
            _emptyItems.TextAlign = ContentAlignment.MiddleCenter;
            _emptyItems.Text = "选择上方单据后，这里显示书目明细";
            _emptyItems.ForeColor = UiTheme.TextSecondary;
            _emptyItems.BackColor = UiTheme.Surface;
            _emptyItems.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);
            itemHost.Controls.Add(_items);
            itemHost.Controls.Add(_emptyItems);

            _detailSurface.Controls.Add(_detailTitle, 0, 0);
            _detailSurface.Controls.Add(itemHost, 0, 1);

            _split.Panel1.Controls.Add(_documentSurface);
            _split.Panel2.Controls.Add(_detailSurface);
            return _split;
        }

        private static void ConfigureTable(AntdUI.Table table, string emptyText)
        {
            table.Dock = DockStyle.Fill;
            table.RowHeight = BookDeskUiSpec.Standard.TableRowHeight;
            table.RowHeightHeader = BookDeskUiSpec.Standard.TableHeaderHeight;
            table.EnableHeaderResizing = true;
            table.ColumnDragSort = true;
            table.ShowTip = true;
            table.EmptyText = emptyText;
        }

        private void ReloadDocuments()
        {
            if (!IsHandleCreated) return;

            if (ToDate < FromDate)
            {
                MessageBox.Show(this, "结束日期不能早于开始日期，请重新选择日期范围。", "日期范围不正确", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _to.Focus();
                return;
            }

            try
            {
                var table = _services.Documents.Search(CurrentKind, FromDate, ToDate, _search.Text);
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

        private void BuildColumns(
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
                        Width = PreferredWidth(name, documentTable, _profile),
                        Ellipsis = name == "书名" || name == "备注"
                    };
                    if (name == "Id") column.Visible = false;
                    if (name == "书名")
                    {
                        column.Width = "fill";
                        column.MinWidth = _profile.IsCompact ? "180" : "220";
                        column.MaxWidth = "420";
                    }
                    if (name == "备注")
                    {
                        column.Width = "fill";
                        column.MinWidth = _profile.IsCompact ? "120" : "160";
                    }
                    if (name.Contains("金额") || name.Contains("价"))
                        column.DisplayFormat = "0.00";
                    map[name] = column;
                    collection.Add(column);
                }
            }
            target.Columns = collection;
        }

        private static string PreferredWidth(
            string name,
            bool documentTable,
            UiSpecProfile profile)
        {
            var compact = profile != null && profile.IsCompact;

            if (documentTable)
            {
                if (name == "日期") return compact ? "180" : "220";
                if (name == "单号") return compact ? "220" : "270";
                if (name == "数量") return compact ? "90" : "140";
                if (name == "金额") return compact ? "120" : "180";
                if (name == "状态") return compact ? "120" : "180";
                if (name == "供应商") return compact ? "120" : "150";
                if (name == "备注") return "fill";
            }
            else
            {
                if (name == "ISBN") return compact ? "160" : "220";
                if (name == "书名") return "fill";
                if (name == "原数量") return compact ? "90" : "128";
                if (name == "已退") return compact ? "86" : "124";
                if (name == "可退") return compact ? "86" : "128";
                if (name == "单价" || name == "进价") return compact ? "110" : "158";
                if (name == "金额") return compact ? "110" : "160";
            }

            if (name.Contains("单号")) return compact ? "150" : "172";
            if (name == "供应商") return compact ? "120" : "136";
            if (name == "ISBN") return compact ? "130" : "160";
            if (name == "状态") return compact ? "90" : "110";
            if (name.Contains("数量") || name == "已退" || name == "可退")
                return compact ? "86" : "100";
            if (name.Contains("金额")) return compact ? "110" : "128";
            if (name.Contains("价")) return compact ? "100" : "118";
            return compact ? "100" : "120";
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

        public void ApplyUiSpecProfile(UiSpecProfile profile)
        {
            _profile = profile ?? BookDeskUiSpec.Standard;
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveColumns()
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            var profile = _profile ?? BookDeskUiSpec.Standard;
            var compact = profile.IsCompact;
            var controlHeight = profile.ControlHeight;

            if (_filterSection != null)
            {
                _filterSection.Padding = new Padding(profile.ToolbarPadding);
                _filterSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.DocumentsFilterCompactHeight
                        : BookDeskUiSpec.DocumentsFilterStandardHeight);
            }

            if (_actionRow != null)
                _actionRow.MinimumSize = new Size(0, controlHeight);

            SetButton(_returnButton, BookDeskUiSpec.DocumentsTopActionWidth, profile);
            SetButton(
                _queryButton,
                compact
                    ? BookDeskUiSpec.DocumentsCompactQueryWidth
                    : BookDeskUiSpec.DocumentsQueryWidth,
                profile);

            if (_filterRow != null && _filterRow.ColumnStyles.Count >= 5)
            {
                _filterRow.ColumnStyles[0].Width = compact
                    ? BookDeskUiSpec.DocumentsTypeCompactWidth
                    : BookDeskUiSpec.DocumentsTypeStandardWidth;
                _filterRow.ColumnStyles[1].Width = compact
                    ? BookDeskUiSpec.DocumentsDateCompactWidth
                    : BookDeskUiSpec.DocumentsDateStandardWidth;
                _filterRow.ColumnStyles[2].Width = compact
                    ? BookDeskUiSpec.DocumentsDateCompactWidth
                    : BookDeskUiSpec.DocumentsDateStandardWidth;
                _filterRow.Margin = new Padding(0, compact ? 6 : 8, 0, 0);
            }

            SetFilterControl(_type, controlHeight, profile);
            SetFilterControl(_from, controlHeight, profile);
            SetFilterControl(_to, controlHeight, profile);
            SetFilterControl(_search, controlHeight, profile);
            _search.MinimumSize = new Size(
                compact
                    ? BookDeskUiSpec.DocumentsSearchCompactMinimumWidth
                    : BookDeskUiSpec.DocumentsSearchMinimumWidth,
                controlHeight);

            if (_queryButton != null)
                _queryButton.Margin = new Padding(0, compact ? 20 : 22, 0, 0);

            if (_filterHint != null)
            {
                _filterHint.Font = UiTheme.Font(profile.SecondaryFontPoints);
                _filterHint.Margin = new Padding(0, compact ? 7 : 10, 0, 0);
                _filterHint.AutoEllipsis = compact;
                _filterHint.MaximumSize = compact
                    ? new Size(Math.Max(1, ClientSize.Width - 40), 22)
                    : Size.Empty;
            }

            if (_chips != null)
                _chips.Margin = new Padding(0, compact ? 7 : 10, 0, 0);
            ResizeChip(_countChip, 94, profile.MetricHeight, profile);
            ResizeChip(_amountChip, 166, profile.MetricHeight, profile);

            _split.Margin = new Padding(0, profile.SectionGap, 0, 0);
            _split.SplitterWidth = profile.SectionGap;

            _documents.RowHeightHeader = profile.TableHeaderHeight;
            _documents.RowHeight = profile.TableRowHeight;
            _documents.Font = UiTheme.Font(profile.TableFontPoints);
            _items.RowHeightHeader = profile.TableHeaderHeight;
            _items.RowHeight = profile.TableRowHeight;
            _items.Font = UiTheme.Font(profile.TableFontPoints);
            _detailTitle.MinimumSize = new Size(0, BookDeskUiSpec.DocumentsDetailTitleHeight);
            _detailTitle.Font = UiTheme.Font(BookDeskUiSpec.PixelFontToPoints(16), FontStyle.Bold);

            ApplyDynamicColumnWidths(_documentColumns, true, profile);
            ApplyDynamicColumnWidths(_itemColumns, false, profile);

            foreach (var pair in _documentColumns)
                pair.Value.Visible = !string.Equals(pair.Key, "Id", StringComparison.Ordinal);
            foreach (var pair in _itemColumns)
                pair.Value.Visible = !string.Equals(pair.Key, "Id", StringComparison.Ordinal);

            _documents.LoadLayout();
            _items.LoadLayout();
            ResizeSplit();
        }

        private static void SetFilterControl(Control control, int height, UiSpecProfile profile)
        {
            if (control == null)
                return;
            control.Height = height;
            control.MinimumSize = new Size(control.MinimumSize.Width, height);
            control.Font = UiTheme.Font(profile.BodyFontPoints);
        }

        private static void SetButton(AntdUI.Button button, int width, UiSpecProfile profile)
        {
            if (button == null)
                return;
            button.Width = width;
            button.Height = profile.ControlHeight;
            button.MinimumSize = new Size(width, profile.ControlHeight);
            button.Font = UiTheme.Font(profile.BodyFontPoints);
        }

        private static void ResizeChip(
            Label label,
            int width,
            int height,
            UiSpecProfile profile)
        {
            if (label == null)
                return;
            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
        }

        private static void ApplyDynamicColumnWidths(
            Dictionary<string, AntdUI.Column> map,
            bool documentTable,
            UiSpecProfile profile)
        {
            foreach (var pair in map)
            {
                var name = pair.Key;
                var column = pair.Value;
                if (string.Equals(name, "Id", StringComparison.Ordinal))
                    continue;

                column.Width = PreferredWidth(name, documentTable, profile);
                if (name == "书名")
                {
                    column.Width = "fill";
                    column.MinWidth = profile.IsCompact ? "180" : "220";
                    column.MaxWidth = "420";
                }
                else if (name == "备注")
                {
                    column.Width = "fill";
                    column.MinWidth = profile.IsCompact ? "120" : "160";
                }
            }
        }

        private void ResizeSplit()
        {
            if (_split.Height <= 0)
                return;

            var available = _split.Height - _split.SplitterWidth;
            var minimumList = BookDeskUiSpec.DocumentsListMinimumHeight;
            var minimumDetails = BookDeskUiSpec.DocumentsDetailMinimumHeight;

            if (available <= minimumList + minimumDetails)
                return;

            var target = (int)Math.Round(available * 0.57);
            var maximum = available - minimumDetails;
            _split.SplitterDistance = Math.Max(
                minimumList,
                Math.Min(maximum, target));
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
