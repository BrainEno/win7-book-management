using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AntdUI;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class SupplierForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly AntdUI.Input _search = UiTheme.CreateAntdInput("按供应商、联系人、电话或备注搜索");
        private readonly CheckBox _includeInactive = new CheckBox();
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _resultChip = new Label();
        private readonly Label _activeChip = new Label();
        private readonly Label _inactiveChip = new Label();
        private readonly Label _summary = new Label();
        private readonly SplitContainer _split = new SplitContainer();
        private readonly Dictionary<string, Label> _detailValues = new Dictionary<string, Label>();

        private readonly AntdUI.Column _contactColumn;
        private readonly AntdUI.Column _phoneColumn;
        private readonly AntdUI.Column _noteColumn;
        private readonly AntdUI.Column _activeColumn;
        private Supplier _selectedSupplier;

        public SupplierForm(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            _contactColumn = new AntdUI.Column("ContactName", "联系人") { Width = "132" };
            _phoneColumn = new AntdUI.Column("Phone", "电话") { Width = "150" };
            _noteColumn = new AntdUI.Column("Note", "备注") { Width = "auto", MinWidth = "180", Ellipsis = true };
            _activeColumn = new AntdUI.Column("IsActive", "启用") { Width = "66" };

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
                Text = "供应商资料",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 5, 0, 8)
            }, 0, 0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };
            var add = UiTheme.CreateAntdButton("＋ 新增供应商", true);
            add.Width = 126;
            var edit = UiTheme.CreateAntdButton("编辑资料", false);
            edit.Width = 102;
            add.Click += delegate { EditSupplier(null); };
            edit.Click += delegate { EditSelected(); };
            actions.Controls.Add(add);
            actions.Controls.Add(edit);
            section.Controls.Add(actions, 1, 0);

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

            _includeInactive.Text = "包含停用";
            _includeInactive.AutoSize = true;
            _includeInactive.Anchor = AnchorStyles.Left;
            _includeInactive.Margin = new Padding(8, 11, 0, 0);
            _includeInactive.CheckedChanged += delegate { Reload(); };
            searchRow.Controls.Add(_includeInactive, 3, 0);

            section.Controls.Add(searchRow, 0, 1);
            section.SetColumnSpan(searchRow, 2);

            var chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 9, 0, 0)
            };
            ConfigureChip(_resultChip, UiTheme.AccentSoft, UiTheme.Accent);
            ConfigureChip(_activeChip, Color.FromArgb(233, 244, 236), UiTheme.Success);
            ConfigureChip(_inactiveChip, UiTheme.SurfaceMuted, UiTheme.TextSecondary);
            chips.Controls.Add(_resultChip);
            chips.Controls.Add(_activeChip);
            chips.Controls.Add(_inactiveChip);
            chips.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "停用供应商不会进入新的采购单，历史单据保持不变。",
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(5, 5, 0, 0)
            });
            section.Controls.Add(chips, 0, 2);
            section.SetColumnSpan(chips, 2);
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

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface };
            gridHost.Controls.Add(_grid);
            _split.Panel1.Controls.Add(gridHost);
            _split.Panel2.Controls.Add(CreateDetailPanel());
            return _split;
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.BackColor = UiTheme.Surface;
            _grid.ForeColor = UiTheme.TextPrimary;
            _grid.ColumnBack = UiTheme.NavigationSurface;
            _grid.ColumnFore = UiTheme.TextSecondary;
            _grid.ColumnFont = UiTheme.Font(8.8F, FontStyle.Bold);
            _grid.BorderColor = UiTheme.Border;
            _grid.Radius = 8;
            _grid.RowHeight = 46;
            _grid.RowHeightHeader = 46;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = true;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有符合条件的供应商";
            _grid.RowHoverBg = Color.FromArgb(248, 246, 241);
            _grid.RowSelectedBg = UiTheme.AccentSoft;
            _grid.RowSelectedFore = UiTheme.TextPrimary;
            _grid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("Name", "供应商") { Width = "auto", MinWidth = "210", Ellipsis = true },
                _contactColumn,
                _phoneColumn,
                _noteColumn,
                _activeColumn
            };
            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedSupplier = e.Record as Supplier;
                ShowSelectedDetails();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedSupplier = e.Record as Supplier;
                EditSelected();
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
                Text = "供应商详情",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            var edit = UiTheme.CreateAntdButton("编辑资料", false);
            edit.Width = 102;
            edit.Click += delegate { EditSelected(); };
            header.Controls.Add(edit, 1, 0);

            var details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 0,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddDetailRow(details, "名称", "name");
            AddDetailRow(details, "联系人", "contact");
            AddDetailRow(details, "电话", "phone");
            AddDetailRow(details, "状态", "status");
            AddDetailRow(details, "备注", "note", 90);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Surface, Padding = new Padding(0, 8, 0, 0) };
            scroll.Controls.Add(details);
            host.Controls.Add(scroll);
            host.Controls.Add(header);
            return host;
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key)
        {
            AddDetailRow(table, labelText, key, 44);
        }

        private void AddDetailRow(TableLayoutPanel table, string labelText, string key, int height)
        {
            var row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, height),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 12, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F, FontStyle.Bold)
            }, 0, row);

            var value = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, Math.Max(40, height - 6)),
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
            var suppliers = _services.Suppliers.GetAll(_includeInactive.Checked);
            var keyword = (_search.Text ?? "").Trim();
            if (keyword.Length > 0)
            {
                suppliers = suppliers.Where(supplier =>
                    Contains(supplier.Name, keyword) ||
                    Contains(supplier.ContactName, keyword) ||
                    Contains(supplier.Phone, keyword) ||
                    Contains(supplier.Note, keyword)).ToList();
            }

            _selectedSupplier = null;
            _grid.DataSource = suppliers;

            var activeCount = suppliers.Count(s => s.IsActive);
            var inactiveCount = suppliers.Count - activeCount;
            _resultChip.Text = "结果  " + suppliers.Count;
            _activeChip.Text = "启用  " + activeCount;
            _inactiveChip.Text = "停用  " + inactiveCount;
            _inactiveChip.Visible = _includeInactive.Checked || inactiveCount > 0;
            _summary.Text = "显示 " + suppliers.Count + " 家供应商 · 双击可编辑 · 表头可拖动调整宽度";
            ShowSelectedDetails();
        }

        private static bool Contains(string value, string keyword)
        {
            return (value ?? "").IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0;
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
            _noteColumn.Visible = gridWidth >= 900;
            _phoneColumn.Visible = gridWidth >= 690;
            _contactColumn.Visible = gridWidth >= 600;
            _activeColumn.Visible = gridWidth >= 520;
            _grid.LoadLayout();
        }

        private void ShowSelectedDetails()
        {
            var supplier = _selectedSupplier;
            if (supplier == null)
            {
                foreach (var value in _detailValues.Values) value.Text = "—";
                return;
            }

            _detailValues["name"].Text = EmptyAsDash(supplier.Name);
            _detailValues["contact"].Text = EmptyAsDash(supplier.ContactName);
            _detailValues["phone"].Text = EmptyAsDash(supplier.Phone);
            _detailValues["status"].Text = supplier.IsActive ? "启用" : "停用";
            _detailValues["status"].ForeColor = supplier.IsActive ? UiTheme.Success : UiTheme.TextSecondary;
            _detailValues["note"].Text = EmptyAsDash(supplier.Note);
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private void EditSelected()
        {
            if (_selectedSupplier == null)
            {
                MessageBox.Show(this, "请先选择一条供应商资料。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            EditSupplier(_selectedSupplier);
        }

        private void EditSupplier(Supplier supplier)
        {
            using (var dialog = new SupplierEditDialog(_services, supplier))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) Reload();
            }
        }

        private sealed class SupplierEditDialog : Form
        {
            private readonly ApplicationServices _services;
            private readonly Supplier _supplier;
            private readonly AntdUI.Input _name = UiTheme.CreateAntdInput("供应商名称");
            private readonly AntdUI.Input _contact = UiTheme.CreateAntdInput("联系人（可选）");
            private readonly AntdUI.Input _phone = UiTheme.CreateAntdInput("电话（可选）");
            private readonly AntdUI.Input _note = UiTheme.CreateAntdInput("备注（可选）");
            private readonly CheckBox _active = new CheckBox();
            private readonly ErrorProvider _errors = new ErrorProvider();

            public SupplierEditDialog(ApplicationServices services, Supplier supplier)
            {
                _services = services;
                _supplier = supplier;
                UiTheme.ConfigureForm(this);
                Text = supplier == null ? "新增供应商" : "编辑供应商";
                StartPosition = FormStartPosition.CenterParent;
                Width = 760;
                Height = 550;
                MinimumSize = new Size(600, 470);
                BackColor = UiTheme.Background;
                ShowInTaskbar = false;
                MinimizeBox = false;
                KeyPreview = true;

                _active.Text = "启用此供应商，可用于新的采购入库";
                _active.AutoSize = true;
                _active.Checked = true;
                _active.Padding = new Padding(0, 8, 0, 0);

                var header = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 15, 22, 13)
                };
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                header.Controls.Add(new Label
                {
                    Text = supplier == null ? "建立供应商资料" : "修改供应商资料",
                    AutoSize = true,
                    Font = UiTheme.Font(13F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary,
                    Margin = new Padding(0, 0, 0, 5)
                }, 0, 0);
                header.Controls.Add(new Label
                {
                    Text = "供应商名称为必填；停用不会删除任何历史采购记录。",
                    AutoSize = true,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8.5F)
                }, 0, 1);

                var bodyHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = UiTheme.Background, Padding = new Padding(18, 16, 18, 16) };
                var section = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 2,
                    RowCount = 4,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(16, 14, 16, 14)
                };
                section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

                var nameField = CreateField("供应商名称 *", _name, 76);
                section.Controls.Add(nameField, 0, 0);
                section.SetColumnSpan(nameField, 2);
                section.Controls.Add(CreateField("联系人", _contact, 76), 0, 1);
                section.Controls.Add(CreateField("电话", _phone, 76), 1, 1);
                var noteField = CreateField("备注", _note, 76);
                section.Controls.Add(noteField, 0, 2);
                section.SetColumnSpan(noteField, 2);
                var statusField = CreateField("状态", _active, 68);
                section.Controls.Add(statusField, 0, 3);
                section.SetColumnSpan(statusField, 2);
                bodyHost.Controls.Add(section);

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    AutoSize = true,
                    MinimumSize = new Size(0, 72),
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = UiTheme.Surface,
                    Padding = new Padding(22, 12, 22, 12)
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                footer.Controls.Add(new Label
                {
                    Text = "联系方式可按实际经营需要填写。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = UiTheme.TextSecondary,
                    Font = UiTheme.Font(8F)
                }, 0, 0);

                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty };
                var cancel = UiTheme.CreateAntdButton("取消", false);
                cancel.Width = 90;
                cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
                var save = UiTheme.CreateAntdButton("保存资料", true);
                save.Width = 110;
                save.Click += Save;
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(save);
                footer.Controls.Add(buttons, 1, 0);

                Controls.Add(bodyHost);
                Controls.Add(footer);
                Controls.Add(header);

                _errors.ContainerControl = this;
                _errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;

                if (supplier != null)
                {
                    _name.Text = supplier.Name;
                    _contact.Text = supplier.ContactName;
                    _phone.Text = supplier.Phone;
                    _note.Text = supplier.Note;
                    _active.Checked = supplier.IsActive;
                }

                KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                };

                UiTheme.Apply(this);
                Shown += delegate
                {
                    UiTheme.FitDialogToWorkingArea(this, 24);
                    _name.Focus();
                };
            }

            private static Control CreateField(string labelText, Control input, int height)
            {
                var field = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(0, height),
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = UiTheme.Surface,
                    Margin = new Padding(0, 0, 12, 10)
                };
                field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                field.Controls.Add(new Label
                {
                    Text = labelText,
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    ForeColor = UiTheme.TextPrimary,
                    Font = UiTheme.Font(8.6F, FontStyle.Bold),
                    Margin = new Padding(0, 0, 0, 4)
                }, 0, 0);
                input.Dock = DockStyle.Top;
                input.Margin = new Padding(0, 0, 0, 2);
                field.Controls.Add(input, 0, 1);
                return field;
            }

            private void Save(object sender, EventArgs e)
            {
                _errors.Clear();
                if (string.IsNullOrWhiteSpace(_name.Text))
                {
                    _errors.SetError(_name, "请输入供应商名称。");
                    _name.Focus();
                    MessageBox.Show(this, "请先填写供应商名称，再保存资料。", "还差一项", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    var target = _supplier ?? new Supplier();
                    target.Name = _name.Text;
                    target.ContactName = _contact.Text;
                    target.Phone = _phone.Text;
                    target.Note = _note.Text;
                    target.IsActive = _active.Checked;

                    if (_supplier == null) _services.Suppliers.Insert(target);
                    else _services.Suppliers.Update(target);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "保存供应商资料失败：\r\n" + ex.Message, "请检查输入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
