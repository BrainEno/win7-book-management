using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class ReportsForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly ComboBox _type = new ComboBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly DataGridView _grid = new DataGridView();
        private DataTable _current;

        public ReportsForm(ApplicationServices services)
        {
            _services = services;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 54,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };

            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Width = 160;
            _type.Margin = new Padding(0, 9, 8, 8);
            _type.Items.Add(new ReportOption("销售明细", "sales"));
            _type.Items.Add(new ReportOption("销售退货明细", "sales_return"));
            _type.Items.Add(new ReportOption("采购明细", "purchase"));
            _type.Items.Add(new ReportOption("采购退货明细", "purchase_return"));
            _type.Items.Add(new ReportOption("库存变动明细", "movement"));
            _type.Items.Add(new ReportOption("指定日期库存快照", "snapshot"));
            _type.SelectedIndex = 0;
            _type.SelectedIndexChanged += delegate { UpdateDateControls(); };

            _from.Format = DateTimePickerFormat.Short;
            _to.Format = DateTimePickerFormat.Short;
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;
            _from.Width = 105;
            _to.Width = 105;
            _from.Margin = new Padding(0, 9, 6, 8);
            _to.Margin = new Padding(0, 9, 8, 8);

            var query = new Button { Text = "查询", Width = 72, Height = 30, Margin = new Padding(0, 7, 8, 7) };
            var export = new Button { Text = "导出 Excel", Width = 96, Height = 30, Margin = new Padding(0, 7, 8, 7), Tag = "primary" };
            query.Click += delegate { Query(); };
            export.Click += delegate { Export(); };

            toolbar.Controls.Add(_type);
            toolbar.Controls.Add(new Label { Text = "开始日期", AutoSize = true, Margin = new Padding(8, 14, 6, 0) });
            toolbar.Controls.Add(_from);
            toolbar.Controls.Add(new Label { Text = "结束/快照日期", AutoSize = true, Margin = new Padding(8, 14, 6, 0) });
            toolbar.Controls.Add(_to);
            toolbar.Controls.Add(query);
            toolbar.Controls.Add(export);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoGenerateColumns = true;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Shown += delegate { Query(); };
        }

        private void UpdateDateControls()
        {
            var option = _type.SelectedItem as ReportOption;
            _from.Enabled = option == null || option.Key != "snapshot";
        }

        private void Query()
        {
            try
            {
                var option = _type.SelectedItem as ReportOption;
                if (option == null) return;

                switch (option.Key)
                {
                    case "sales":
                        _current = _services.Reports.SalesDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "sales_return":
                        _current = _services.Reports.SalesReturnDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "purchase":
                        _current = _services.Reports.PurchaseDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "purchase_return":
                        _current = _services.Reports.PurchaseReturnDetail(_from.Value.Date, _to.Value.Date);
                        break;
                    case "movement":
                        _current = _services.Reports.InventoryMovements(_from.Value.Date, _to.Value.Date);
                        break;
                    case "snapshot":
                        _current = _services.Reports.InventorySnapshot(_to.Value.Date);
                        break;
                    default:
                        throw new InvalidOperationException("未知报表类型。");
                }

                _grid.DataSource = _current;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Export()
        {
            try
            {
                if (_current == null) Query();
                if (_current == null) return;

                var option = _type.SelectedItem as ReportOption;
                var title = option == null ? "报表" : option.Text;
                var fileName = title + "_" + _to.Value.ToString("yyyyMMdd") + ".xlsx";

                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                    dialog.DefaultExt = "xlsx";
                    dialog.AddExtension = true;
                    dialog.InitialDirectory = AppPaths.ExportDirectory;
                    dialog.FileName = fileName;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    _services.Excel.Export(_current, dialog.FileName, title);
                    MessageBox.Show(this, "已导出：" + dialog.FileName, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class ReportOption
        {
            public ReportOption(string text, string key)
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
