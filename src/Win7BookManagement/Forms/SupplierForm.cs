using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class SupplierForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly DataGridView _grid;

        public SupplierForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight };
            var add = new Button { Text = "新增供应商", Width = 100, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            var edit = new Button { Text = "编辑", Width = 72, Height = 28, Margin = new Padding(0, 6, 8, 6) };
            add.Click += delegate { EditSupplier(null); };
            edit.Click += delegate { EditSelected(); };
            toolbar.Controls.Add(add);
            toolbar.Controls.Add(edit);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "供应商", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "联系人", DataPropertyName = "ContactName", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "电话", DataPropertyName = "Phone", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Note", Width = 220 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = "IsActive", Width = 60 });
            _grid.CellDoubleClick += delegate { EditSelected(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            _grid.DataSource = _services.Suppliers.GetAll(true);
        }

        private void EditSelected()
        {
            var supplier = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Supplier;
            if (supplier == null)
            {
                MessageBox.Show(this, "请先选择供应商。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            EditSupplier(supplier);
        }

        private void EditSupplier(Supplier supplier)
        {
            using (var dialog = new SupplierEditDialog(_services, supplier))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }

        private sealed class SupplierEditDialog : Form
        {
            private readonly ApplicationServices _services;
            private readonly Supplier _supplier;
            private readonly TextBox _name = new TextBox();
            private readonly TextBox _contact = new TextBox();
            private readonly TextBox _phone = new TextBox();
            private readonly TextBox _note = new TextBox();
            private readonly CheckBox _active = new CheckBox();

            public SupplierEditDialog(ApplicationServices services, Supplier supplier)
            {
                _services = services;
                _supplier = supplier;
                Text = supplier == null ? "新增供应商" : "编辑供应商";
                StartPosition = FormStartPosition.CenterParent;
                Width = 500;
                Height = 360;
                Font = new Font("Microsoft YaHei", 9F);

                _active.Text = "启用";
                _active.Checked = true;

                var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(18) };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                Add(table, 0, "名称 *", _name);
                Add(table, 1, "联系人", _contact);
                Add(table, 2, "电话", _phone);
                Add(table, 3, "备注", _note);
                Add(table, 4, "状态", _active);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var save = new Button { Text = "保存", Width = 80, Height = 30 };
                var cancel = new Button { Text = "取消", Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
                save.Click += Save;
                buttons.Controls.Add(save);
                buttons.Controls.Add(cancel);
                table.Controls.Add(buttons, 1, 5);

                Controls.Add(table);
                AcceptButton = save;
                CancelButton = cancel;

                if (supplier != null)
                {
                    _name.Text = supplier.Name;
                    _contact.Text = supplier.ContactName;
                    _phone.Text = supplier.Phone;
                    _note.Text = supplier.Note;
                    _active.Checked = supplier.IsActive;
                }
            }

            private static void Add(TableLayoutPanel table, int row, string label, Control control)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(6);
                table.Controls.Add(control, 1, row);
            }

            private void Save(object sender, EventArgs e)
            {
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
                    MessageBox.Show(this, "保存失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
