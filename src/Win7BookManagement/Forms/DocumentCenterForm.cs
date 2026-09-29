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

        public DocumentCenterForm(ApplicationServices services)
        {
            _services = services;
            BackColor = UiTheme.Background;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 9, 12, 8)
            };

            _type.DropDownStyle = ComboBoxStyle.DropDownList;
            _type.Width = 130;
            _type.Margin = new Padding(0, 5, 8, 5);
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
            _from.Width = 105;
            _to.Width = 105;
            _from.Margin = new Padding(0, 5, 6, 5);
            _to.Margin = new Padding(0, 5, 8, 5);

            _search.Width = 210;
            _search.Margin = new Padding(4, 5, 8, 5);
            _search.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ReloadDocuments();
                    e.SuppressKeyPress = true;
                }
            };

            var query = new Button { Text = "查询", Width = 72, Height = 32, Margin = new Padding(0, 3, 8, 3) };
            query.Click += delegate { ReloadDocuments(); };

            _returnButton.Text = "发起退货";
            _returnButton.Width = 92;
            _returnButton.Height = 32;
            _returnButton.Margin = new Padding(0, 3, 8, 3);
            _returnButton.Tag = "primary";
            _returnButton.Click += delegate { StartReturn(); };

            toolbar.Controls.Add(_type);
            toolbar.Controls.Add(new Label { Text = "从", AutoSize = true, Margin = new Padding(4, 11, 5, 0), ForeColor = UiTheme.TextSecondary });
            toolbar.Controls.Add(_from);
            toolbar.Controls.Add(new Label { Text = "到", AutoSize = true, Margin = new Padding(2, 11, 5, 0), ForeColor = UiTheme.TextSecondary });
            toolbar.Controls.Add(_to);
            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(query);
            toolbar.Controls.Add(_returnButton);

            var hint = new Label
            {
                Text = "可按单号、ISBN、书名、备注搜索；采购单还支持供应商名称。",
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 7, 0, 0),
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(8F)
            };

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 310,
                SplitterWidth = 6,
                BackColor = UiTheme.Background,
                Panel1MinSize = 180,
                Panel2MinSize = 150
            };

            ConfigureGrid(_documents);
            ConfigureGrid(_items);
            _documents.SelectionChanged += delegate { LoadSelectedDetails(); };
            _documents.CellDoubleClick += delegate { LoadSelectedDetails(); };

            var documentHeader = new Label
            {
                Text = "单据列表",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(8, 7, 0, 0),
                BackColor = UiTheme.Surface,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            split.Panel1.BackColor = UiTheme.Surface;
            split.Panel1.Controls.Add(_documents);
            split.Panel1.Controls.Add(documentHeader);

            _detailTitle.Text = "单据明细";
            _detailTitle.Dock = DockStyle.Top;
            _detailTitle.Height = 34;
            _detailTitle.Padding = new Padding(8, 7, 0, 0);
            _detailTitle.BackColor = UiTheme.Surface;
            _detailTitle.Font = UiTheme.Font(9.5F, FontStyle.Bold);
            _detailTitle.ForeColor = UiTheme.TextPrimary;
            split.Panel2.BackColor = UiTheme.Surface;
            split.Panel2.Controls.Add(_items);
            split.Panel2.Controls.Add(_detailTitle);

            Controls.Add(split);
            Controls.Add(hint);
            Controls.Add(toolbar);

            UiTheme.Apply(this);
            Shown += delegate { ReloadDocuments(); };
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
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
            try
            {
                var table = _services.Documents.Search(CurrentKind, _from.Value.Date, _to.Value.Date, _search.Text);
                _documents.DataSource = table;
                HideIdColumn(_documents);
                _returnButton.Enabled = CurrentKind == "sale" || CurrentKind == "purchase";

                if (_documents.Rows.Count == 0)
                {
                    _items.DataSource = null;
                    _detailTitle.Text = "单据明细";
                }
                else
                {
                    _documents.Rows[0].Selected = true;
                    _documents.CurrentCell = _documents.Rows[0].Cells[FirstVisibleColumnIndex(_documents)];
                    LoadSelectedDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "查询单据失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadSelectedDetails()
        {
            try
            {
                long id;
                if (!TryGetSelectedId(out id)) return;

                var table = _services.Documents.GetItems(CurrentKind, id);
                _items.DataSource = table;
                _detailTitle.Text = "单据明细 · " + _services.Documents.GetDocumentNo(CurrentKind, id);
            }
            catch
            {
                _items.DataSource = null;
                _detailTitle.Text = "单据明细";
            }
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
                    MessageBox.Show(this, "这张单据已经没有可退数量。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "无法发起退货：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (grid.Columns[i].Visible) return i;
            return 0;
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
