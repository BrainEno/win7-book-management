using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookLookupDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly TextBox _search = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _summary = new Label();

        private readonly DataGridViewColumn _selfCodeColumn;
        private readonly DataGridViewColumn _isbnColumn;
        private readonly DataGridViewColumn _authorColumn;
        private readonly DataGridViewColumn _shelfColumn;
        private readonly DataGridViewColumn _priceColumn;

        public Book SelectedBook { get; private set; }

        public BookLookupDialog(ApplicationServices services)
        {
            _services = services;
            UiTheme.ConfigureForm(this);

            Text = "选择图书";
            StartPosition = FormStartPosition.CenterParent;
            Width = 920;
            Height = 620;
            MinimumSize = new Size(680, 460);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;

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
            _shelfColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "货架位",
                DataPropertyName = "ShelfCode",
                Width = 86
            };
            _priceColumn = new DataGridViewTextBoxColumn
            {
                HeaderText = "零售价",
                DataPropertyName = "SalePriceYuan",
                Width = 82,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "0.00",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };

            var header = CreateHeader();
            var searchSection = CreateSearchSection();
            var gridHost = CreateGridSection();
            var footer = CreateFooter(out Button select, out Button cancel);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(searchSection, 0, 1);
            root.Controls.Add(gridHost, 0, 2);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);

            AcceptButton = select;
            CancelButton = cancel;

            Resize += delegate { ApplyResponsiveColumns(); };

            UiTheme.Apply(this);
            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                Reload();
                ApplyResponsiveColumns();
                _search.Focus();
            };
        }

        private Control CreateHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(20, 14, 20, 12),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "查找并选择图书",
                AutoSize = true,
                Font = UiTheme.Font(12.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "支持店内编码、ISBN、书名、作者、出版社、分类、出版年、版次、装帧、货架位和备注。双击结果可直接选择。",
                AutoSize = true,
                MaximumSize = new Size(850, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            }, 0, 1);

            return header;
        }

        private Control CreateSearchSection()
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 66),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0, 10, 0, 10),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));

            section.Controls.Add(new Label
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
            section.Controls.Add(_search, 1, 0);

            var searchButton = new Button
            {
                Text = "查询",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            searchButton.Click += delegate { Reload(); };
            section.Controls.Add(searchButton, 2, 0);

            return section;
        }

        private Control CreateGridSection()
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
                MinimumWidth = 210,
                FillWeight = 220
            });
            _grid.Columns.Add(_authorColumn);
            _grid.Columns.Add(_shelfColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "库存",
                DataPropertyName = "StockQuantity",
                Width = 70,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });
            _grid.Columns.Add(_priceColumn);
            _grid.CellDoubleClick += delegate { Choose(); };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                BorderStyle = BorderStyle.None,
                Margin = Padding.Empty
            };
            host.Controls.Add(_grid);
            host.Controls.Add(new Label
            {
                Text = "搜索结果",
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(12, 10, 0, 10),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(9F, FontStyle.Bold)
            });
            return host;
        }

        private Control CreateFooter(out Button select, out Button cancel)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 70),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 11, 16, 11),
                Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _summary.Dock = DockStyle.Fill;
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.ForeColor = UiTheme.TextSecondary;
            _summary.Font = UiTheme.Font(8.5F);
            footer.Controls.Add(_summary, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            cancel = new Button
            {
                Text = "取消",
                Width = 88,
                Height = UiTheme.ButtonHeight,
                DialogResult = DialogResult.Cancel
            };
            select = new Button
            {
                Text = "选择图书",
                Width = 104,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            select.Click += delegate { Choose(); };

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(select);
            footer.Controls.Add(buttons, 1, 0);

            return footer;
        }

        private void Reload()
        {
            var result = _services.Books.Search(_search.Text, false);
            _grid.DataSource = result;
            _summary.Text = "找到 " + result.Count + " 条启用图书资料 · 可双击一行直接选择";

            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                var firstVisible = FirstVisibleColumnIndex();
                if (firstVisible >= 0)
                    _grid.CurrentCell = _grid.Rows[0].Cells[firstVisible];
            }
        }

        private void ApplyResponsiveColumns()
        {
            var width = ClientSize.Width;
            _selfCodeColumn.Visible = width >= 840;
            _shelfColumn.Visible = width >= 760;
            _authorColumn.Visible = width >= 700;
            _priceColumn.Visible = width >= 650;
            _isbnColumn.Visible = width >= 580;
        }

        private int FirstVisibleColumnIndex()
        {
            for (var i = 0; i < _grid.Columns.Count; i++)
            {
                if (_grid.Columns[i].Visible)
                    return i;
            }

            return -1;
        }

        private void Choose()
        {
            var book = _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as Book;
            if (book == null)
            {
                MessageBox.Show(this, "请先选择图书。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedBook = book;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
