using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class DictionaryManagementForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Menu _menu = new AntdUI.Menu();
        private readonly Panel _content = new Panel();
        private readonly Dictionary<string, AntdUI.MenuItem> _items =
            new Dictionary<string, AntdUI.MenuItem>();
        private Control _current;

        public DictionaryManagementForm(ApplicationServices services)
        {
            _services = services;

            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            root.Controls.Add(CreateDictionaryNavigation(), 0, 0);

            _content.Dock = DockStyle.Fill;
            _content.BackColor = UiTheme.Background;
            _content.Padding = new Padding(12, 0, 0, 0);
            root.Controls.Add(_content, 1, 0);

            Controls.Add(root);
            UiTheme.Apply(this);

            Shown += delegate
            {
                ShowProvider("suppliers");
                SelectNavigation("suppliers");
            };
        }

        public static string ShowAddBookCategory(
            IWin32Window owner,
            ApplicationServices services,
            string initialValue)
        {
            using (var dialog = new DictionaryValueEditDialog(
                services,
                DictionaryKeys.BookCategory,
                "图书分类",
                null,
                initialValue))
            {
                return dialog.ShowDialog(owner) == DialogResult.OK &&
                       dialog.SavedValue != null
                    ? dialog.SavedValue.Value
                    : "";
            }
        }

        private Control CreateDictionaryNavigation()
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 14, 12, 10),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.Controls.Add(new Label
            {
                Text = "基础资料",
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 3)
            }, 0, 0);
            header.Controls.Add(new Label
            {
                Text = "统一维护门店长期复用的字典和往来单位。",
                AutoSize = true,
                MaximumSize = new Size(160, 0),
                Font = UiTheme.Font(7.8F),
                ForeColor = UiTheme.TextSecondary,
                Margin = Padding.Empty
            }, 0, 1);

            _menu.Dock = DockStyle.Fill;
            _menu.Mode = AntdUI.TMenuMode.Inline;
            _menu.AutoCollapse = false;
            _menu.Collapsed = false;
            _menu.Unique = true;
            _menu.BackColor = UiTheme.Surface;
            _menu.Padding = new Padding(8, 6, 8, 8);
            _menu.Radius = 6;
            _menu.Gap = 8;
            _menu.itemMargin = 2;
            _menu.IconRatio = 0.95F;
            _menu.FocusMode = AntdUI.TFocusMode.Line;
            _menu.FocusModeAlign = AntdUI.TAlignMini.Left;
            _menu.FocusModeColor = UiTheme.Accent;
            _menu.FocusModeSize = 3;

            AddMenuItem("suppliers", "供应商", "UsergroupAddOutlined");
            AddMenuItem("book_category", "图书分类", "TagsOutlined");
            AddMenuItem("payment_method", "收款方式", "WalletOutlined");

            _menu.SelectChanged += delegate(object sender, AntdUI.MenuSelectEventArgs e)
            {
                if (e == null || e.Value == null) return;
                var key = Convert.ToString(e.Value.Tag);
                if (!string.IsNullOrWhiteSpace(key))
                    ShowProvider(key);
            };

            host.Controls.Add(header, 0, 0);
            host.Controls.Add(_menu, 0, 1);
            return host;
        }

        private void AddMenuItem(string key, string text, string icon)
        {
            var item = new AntdUI.MenuItem(text)
            {
                ID = key,
                Name = key,
                Tag = key,
                IconSvg = icon
            };
            _menu.Items.Add(item);
            _items[key] = item;
        }

        private void SelectNavigation(string key)
        {
            AntdUI.MenuItem item;
            if (!_items.TryGetValue(key, out item)) return;
            try { _menu.Select(item, false); }
            catch { }
        }

        private void ShowProvider(string key)
        {
            if (_current != null)
            {
                _content.Controls.Remove(_current);
                _current.Dispose();
                _current = null;
            }

            if (string.Equals(key, "suppliers", StringComparison.OrdinalIgnoreCase))
            {
                var form = new SupplierForm(_services)
                {
                    TopLevel = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Dock = DockStyle.Fill
                };
                _current = form;
                _content.Controls.Add(form);
                form.Show();
                return;
            }

            if (string.Equals(key, "payment_method", StringComparison.OrdinalIgnoreCase))
            {
                _current = new DictionaryValuePanel(
                    _services,
                    DictionaryKeys.PaymentMethod,
                    "收款方式",
                    "用于零售结账选择。系统默认提供微信、支付宝和现金；现金会在结账时计算实收与找零。");
                _content.Controls.Add(_current);
                return;
            }

            _current = new DictionaryValuePanel(
                _services,
                DictionaryKeys.BookCategory,
                "图书分类",
                "用于图书建档、检索和报表归类。停用分类不会改写已有图书；重命名会同步现有图书资料。");
            _content.Controls.Add(_current);
        }

        private sealed class DictionaryValuePanel : UserControl
        {
            private readonly ApplicationServices _services;
            private readonly string _dictionaryKey;
            private readonly string _displayName;
            private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("搜索字典值或备注");
            private readonly AntdUI.Checkbox _includeInactive = new AntdUI.Checkbox();
            private readonly AntdUI.Table _grid = new AntdUI.Table();
            private readonly Label _summary = new Label();
            private DictionaryValue _selected;

            public DictionaryValuePanel(
                ApplicationServices services,
                string dictionaryKey,
                string displayName,
                string description)
            {
                _services = services;
                _dictionaryKey = dictionaryKey;
                _displayName = displayName;

                Dock = DockStyle.Fill;
                BackColor = UiTheme.Background;
                Margin = Padding.Empty;
                Padding = Padding.Empty;

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

                root.Controls.Add(CreateToolbar(description), 0, 0);

                var host = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(0, 8, 0, 0),
                    Padding = Padding.Empty
                };
                host.Controls.Add(_grid);
                root.Controls.Add(host, 0, 1);

                _summary.Dock = DockStyle.Fill;
                _summary.MinimumSize = new Size(0, 40);
                _summary.TextAlign = ContentAlignment.MiddleLeft;
                _summary.Padding = new Padding(12, 0, 12, 0);
                _summary.BackColor = UiTheme.Surface;
                _summary.ForeColor = UiTheme.TextSecondary;
                _summary.Font = UiTheme.Font(8.2F);
                root.Controls.Add(_summary, 0, 2);

                Controls.Add(root);
                UiTheme.Apply(this);
                Reload();
            }

            private Control CreateToolbar(string description)
            {
                var section = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(14, 12, 14, 12),
                    Margin = Padding.Empty
                };
                section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                section.Controls.Add(new Label
                {
                    Text = _displayName,
                    AutoSize = true,
                    Font = UiTheme.Font(11F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 4)
                }, 0, 0);
                section.Controls.Add(new Label
                {
                    Text = description,
                    AutoSize = true,
                    MaximumSize = new Size(900, 0),
                    Font = UiTheme.Font(8F),
                    ForeColor = UiTheme.TextSecondary,
                    Margin = new Padding(0, 0, 0, 10)
                }, 0, 1);

                var row = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 5,
                    RowCount = 1,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                _search.Dock = DockStyle.Fill;
                _search.Margin = new Padding(0, 0, 8, 0);
                _search.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        Reload();
                        e.SuppressKeyPress = true;
                    }
                };
                row.Controls.Add(_search, 0, 0);

                _includeInactive.Text = "包含停用";
                _includeInactive.AutoSize = true;
                _includeInactive.Margin = new Padding(4, 7, 10, 0);
                _includeInactive.CheckedChanged += delegate { Reload(); };
                row.Controls.Add(_includeInactive, 1, 0);

                var query = UiTheme.CreateAntdButton("查询", false);
                query.Width = 78;
                query.Margin = new Padding(0, 0, 6, 0);
                query.Click += delegate { Reload(); };
                row.Controls.Add(query, 2, 0);

                var add = UiTheme.CreateAntdButton("新增", true);
                add.Width = 82;
                add.IconSvg = "PlusOutlined";
                add.Margin = new Padding(0, 0, 6, 0);
                add.Click += delegate { Edit(null); };
                row.Controls.Add(add, 3, 0);

                var edit = UiTheme.CreateAntdButton("编辑", false);
                edit.Width = 82;
                edit.IconSvg = "EditOutlined";
                edit.Margin = Padding.Empty;
                edit.Click += delegate { Edit(_selected); };
                row.Controls.Add(edit, 4, 0);

                section.Controls.Add(row, 0, 2);
                return section;
            }

            private void ConfigureGrid()
            {
                _grid.Dock = DockStyle.Fill;
                _grid.RowHeight = UiTheme.TableRowHeight;
                _grid.RowHeightHeader = UiTheme.TableHeaderHeight;
                _grid.EnableHeaderResizing = true;
                _grid.ColumnDragSort = false;
                _grid.ShowTip = true;
                _grid.EmptyText = "当前字典还没有可显示的值";
                _grid.Columns = new AntdUI.ColumnCollection
                {
                    new AntdUI.Column("Value", "字典值") { Width = "220", MinWidth = "140", SortOrder = true },
                    new AntdUI.Column("Note", "备注") { Width = "fill", MinWidth = "220", Ellipsis = true },
                    new AntdUI.Column("SortOrder", "排序") { Width = "90", MinWidth = "76", SortOrder = true },
                    new AntdUI.Column("StatusText", "状态") { Width = "86", MinWidth = "76" }
                };
                _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
                {
                    var row = e.Record as DictionaryRow;
                    _selected = row == null ? null : row.Source;
                };
                _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
                {
                    var row = e.Record as DictionaryRow;
                    _selected = row == null ? null : row.Source;
                    if (_selected != null) Edit(_selected);
                };
            }

            private void Reload()
            {
                var values = _services.Dictionaries.Search(
                    _dictionaryKey,
                    _search.Text,
                    _includeInactive.Checked);

                var rows = new List<DictionaryRow>();
                var activeCount = 0;
                foreach (var value in values)
                {
                    rows.Add(new DictionaryRow(value));
                    if (value.IsActive) activeCount++;
                }

                _selected = null;
                _grid.DataSource = rows;
                _summary.Text =
                    "共 " + rows.Count + " 条；启用 " + activeCount +
                    " 条。停用只影响以后选择，历史单据和已有资料中的快照不会被删除。";
            }

            private void Edit(DictionaryValue item)
            {
                using (var dialog = new DictionaryValueEditDialog(
                    _services,
                    _dictionaryKey,
                    _displayName,
                    item,
                    ""))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        Reload();
                }
            }

            private sealed class DictionaryRow
            {
                public DictionaryValue Source { get; private set; }
                public string Value { get { return Source.Value; } }
                public string Note { get { return string.IsNullOrWhiteSpace(Source.Note) ? "—" : Source.Note; } }
                public int SortOrder { get { return Source.SortOrder; } }
                public string StatusText { get { return Source.IsActive ? "启用" : "停用"; } }

                public DictionaryRow(DictionaryValue source)
                {
                    Source = source;
                }
            }
        }

        private sealed class DictionaryValueEditDialog : Form
        {
            private readonly ApplicationServices _services;
            private readonly string _dictionaryKey;
            private readonly string _displayName;
            private readonly DictionaryValue _existing;
            private readonly AntdUI.Input _value = UiTheme.CreateAntdInput("请输入字典值");
            private readonly AntdUI.InputNumber _sortOrder = new AntdUI.InputNumber();
            private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("可选备注");
            private readonly AntdUI.Checkbox _active = new AntdUI.Checkbox();

            public DictionaryValue SavedValue { get; private set; }

            public DictionaryValueEditDialog(
                ApplicationServices services,
                string dictionaryKey,
                string displayName,
                DictionaryValue existing,
                string initialValue)
            {
                _services = services;
                _dictionaryKey = dictionaryKey;
                _displayName = displayName;
                _existing = existing;

                UiTheme.ConfigureForm(this);
                Text = existing == null ? "新增" + displayName : "编辑" + displayName;
                StartPosition = FormStartPosition.CenterParent;
                Width = 620;
                Height = 420;
                MinimumSize = new Size(520, 360);
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = UiTheme.Background;

                _sortOrder.Minimum = -9999;
                _sortOrder.Maximum = 9999;
                _sortOrder.DecimalPlaces = 0;
                _active.Text = "启用，可在新资料中继续选择";
                _active.AutoSize = true;
                _active.Checked = true;
                _note.Multiline = true;
                _note.AutoScroll = true;

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

                root.Controls.Add(new Label
                {
                    Text = existing == null ? "新增" + displayName : "编辑" + displayName,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    BackColor = UiTheme.Surface,
                    ForeColor = UiTheme.TextPrimary,
                    Font = UiTheme.Font(13F, FontStyle.Bold),
                    Padding = new Padding(22, 16, 22, 12)
                }, 0, 0);

                var body = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 4,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 18, 22, 18),
                    Margin = new Padding(0, 8, 0, 0)
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddField(body, 0, "字典值 *", _value);
                AddField(body, 1, "排序", _sortOrder);
                AddField(body, 2, "备注", _note);
                AddField(body, 3, "状态", _active);
                root.Controls.Add(body, 0, 1);

                var footer = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(18, 11, 18, 11),
                    Margin = new Padding(0, 8, 0, 0)
                };
                var save = UiTheme.CreateAntdButton("保存", true);
                save.Width = 96;
                save.Click += Save;
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 90;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                footer.Controls.Add(save);
                footer.Controls.Add(cancel);
                root.Controls.Add(footer, 0, 2);

                Controls.Add(root);

                if (existing != null)
                {
                    _value.Text = existing.Value;
                    _sortOrder.Value = existing.SortOrder;
                    _note.Text = existing.Note;
                    _active.Checked = existing.IsActive;
                }
                else
                {
                    _value.Text = initialValue ?? "";
                }

                UiTheme.Apply(this);
                Shown += delegate
                {
                    UiTheme.FitDialogToWorkingArea(this, 24);
                    _value.Focus();
                    if (!string.IsNullOrWhiteSpace(_value.Text))
                        _value.SelectAll();
                };
            }

            private static void AddField(TableLayoutPanel table, int row, string labelText, Control control)
            {
                table.Controls.Add(new Label
                {
                    Text = labelText,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 12, 0)
                }, 0, row);

                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 5, 0, 5);
                if (row == 2)
                    control.MinimumSize = new Size(0, 72);
                table.Controls.Add(control, 1, row);
            }

            private void Save(object sender, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(_value.Text))
                {
                    MessageBox.Show(
                        this,
                        "请输入" + _displayName + "名称。",
                        "还差一项",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _value.Focus();
                    return;
                }

                try
                {
                    var item = _existing ?? new DictionaryValue();
                    item.DictionaryKey = _dictionaryKey;
                    item.Value = _value.Text;
                    item.SortOrder = Decimal.ToInt32(_sortOrder.Value);
                    item.Note = _note.Text;
                    item.IsActive = _active.Checked;

                    if (_existing == null)
                        item.Id = _services.Dictionaries.Insert(item);
                    else
                        _services.Dictionaries.Update(item);

                    SavedValue = _services.Dictionaries.GetById(item.Id) ?? item;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        "无法保存字典值：\r\n" + ex.Message,
                        "请检查输入",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }
}
