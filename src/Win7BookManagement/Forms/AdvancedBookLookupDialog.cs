using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class AdvancedBookLookupDialog : Form
    {
        private readonly ApplicationServices _services;
        private readonly Action<Book> _addToDocument;

        private readonly AntdUI.Input _selfCode = UiTheme.CreateAntdInput("店内编码");
        private readonly AntdUI.Input _isbn = UiTheme.CreateAntdInput("ISBN");
        private readonly AntdUI.Input _author = UiTheme.CreateAntdInput("作者");
        private readonly AntdUI.Input _title = UiTheme.CreateAntdInput("书名");
        private readonly AntdUI.Input _publisher = UiTheme.CreateAntdInput("出版社");
        private readonly AntdUI.Select _category = new AntdUI.Select();
        private readonly List<string> _categoryValues = new List<string>();

        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label();

        private Book _selected;

        public Book SelectedBook { get; private set; }

        public AdvancedBookLookupDialog(
            ApplicationServices services,
            string initialIdentifier,
            Action<Book> addToDocument)
        {
            _services = services;
            _addToDocument = addToDocument;

            UiTheme.ConfigureForm(this);
            Text = "高级图书查找";
            StartPosition = FormStartPosition.CenterParent;
            Width = BookDeskUiSpec.PurchaseAdvancedLookupWidth;
            Height = BookDeskUiSpec.PurchaseAdvancedLookupHeight;
            MinimumSize = new Size(
                BookDeskUiSpec.PurchaseAdvancedLookupMinimumWidth,
                BookDeskUiSpec.PurchaseAdvancedLookupMinimumHeight);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            KeyPreview = true;

            ConfigureCategoryFilter();
            ConfigureInitialIdentifier(initialIdentifier);

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

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateFilterBar(), 0, 1);
            root.Controls.Add(CreateGrid(), 0, 2);
            root.Controls.Add(CreateFooter(), 0, 3);
            Controls.Add(root);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                if (e.KeyCode == Keys.Enter &&
                    !(_grid.Focused || _grid.ContainsFocus))
                {
                    Reload();
                    e.SuppressKeyPress = true;
                }
            };

            UiTheme.Apply(this);
            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 18);
                Reload();
                FocusInitialFilter();
            };
        }

        private void ConfigureCategoryFilter()
        {
            _category.DropDownArrow = true;

            _categoryValues.Clear();
            _categoryValues.Add("");
            _category.Items.Add("全部分类");

            foreach (var value in _services.Books.GetActiveCategories())
            {
                _categoryValues.Add(value);
                _category.Items.Add(value);
            }

            _category.SelectedIndex = 0;
        }

        private void ConfigureInitialIdentifier(string initialIdentifier)
        {
            var value = (initialIdentifier ?? "").Trim();
            if (value.Length == 0) return;

            if (LooksLikeIsbn(value))
                _isbn.Text = value;
            else
                _selfCode.Text = value;
        }

        private static bool LooksLikeIsbn(string value)
        {
            var digits = 0;
            foreach (var ch in value)
            {
                if (char.IsDigit(ch))
                {
                    digits++;
                    continue;
                }

                if (ch == '-' || char.IsWhiteSpace(ch))
                    continue;

                return false;
            }

            return digits == 10 || digits == 13;
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
                Padding = new Padding(20, 16, 20, 13),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "选择图书（高级查找）",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "可组合店内编码、ISBN、作者、书名、出版社和分类进行精确查找。双击结果可直接加入采购明细。",
                AutoSize = true,
                MaximumSize = new Size(1120, 0),
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.4F),
                Margin = Padding.Empty
            }, 0, 1);

            return header;
        }

        private Control CreateFilterBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = BookDeskUiSpec.PurchaseAdvancedLookupFilterHeight,
                ColumnCount = 6,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 13, 14, 13),
                Margin = new Padding(0, 8, 0, 8)
            };

            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));

            AddFilter(bar, _selfCode, 0);
            AddFilter(bar, _isbn, 1);
            AddFilter(bar, _author, 2);
            AddFilter(bar, _title, 3);
            AddFilter(bar, _publisher, 4);

            _category.Dock = DockStyle.Fill;
            _category.Height = BookDeskUiSpec.PurchaseFieldStandardHeight;
            _category.MinimumSize = new Size(0, BookDeskUiSpec.PurchaseFieldStandardHeight);
            _category.Margin = new Padding(6, 3, 0, 3);
            bar.Controls.Add(_category, 5, 0);

            AttachEnterSearch(_selfCode);
            AttachEnterSearch(_isbn);
            AttachEnterSearch(_author);
            AttachEnterSearch(_title);
            AttachEnterSearch(_publisher);

            return bar;
        }

        private static void AddFilter(TableLayoutPanel bar, Control control, int column)
        {
            control.Dock = DockStyle.Fill;
            control.Height = BookDeskUiSpec.PurchaseFieldStandardHeight;
            control.MinimumSize = new Size(0, BookDeskUiSpec.PurchaseFieldStandardHeight);
            control.Margin = new Padding(column == 0 ? 0 : 6, 3, 6, 3);
            bar.Controls.Add(control, column, 0);
        }

        private void AttachEnterSearch(AntdUI.Input input)
        {
            input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                Reload();
                e.SuppressKeyPress = true;
            };
        }

        private Control CreateGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.RowHeight = BookDeskUiSpec.PurchaseAdvancedLookupRowHeight;
            _grid.RowHeightHeader = BookDeskUiSpec.PurchaseAdvancedLookupHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.ShowTip = true;
            _grid.EmptyText = "没有找到匹配的启用图书资料";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("SelfCode", "店内编码") { Width = "118", MinWidth = "96" },
                new AntdUI.Column("Title", "书名") { Width = "fill", MinWidth = "180", MaxWidth = "360", Ellipsis = true },
                new AntdUI.Column("Isbn", "ISBN") { Width = "138", MinWidth = "112" },
                new AntdUI.Column("Author", "作者") { Width = "150", MinWidth = "110", Ellipsis = true },
                new AntdUI.Column("Publisher", "出版社") { Width = "150", MinWidth = "110", Ellipsis = true },
                new AntdUI.Column("Category", "分类") { Width = "102", MinWidth = "82" },
                new AntdUI.Column("SalePriceYuan", "售价") { Width = "86", MinWidth = "76", DisplayFormat = "0.00" },
                new AntdUI.Column("PublicationYear", "出版年") { Width = "92", MinWidth = "78" },
                new AntdUI.Column("StockQuantity", "库存") { Width = "72", MinWidth = "64" }
            };

            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as Book;
                UpdateSummary();
            };
            _grid.CellDoubleClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selected = e.Record as Book;
                AddSelectedAndClose();
            };

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 0, 14, 0),
                Margin = Padding.Empty
            };
            host.Controls.Add(_grid);
            return host;
        }

        private Control CreateFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 72),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 11, 14, 11),
                Margin = new Padding(0, 8, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            var search = UiTheme.CreateAntdButton("查询", false);
            search.Width = 88;
            search.Click += delegate { Reload(); };

            var create = UiTheme.CreateAntdButton("新增资料", false);
            create.Width = 108;
            create.Click += delegate { CreateNewBook(); };

            var clear = UiTheme.CreateAntdButton("清除", false);
            clear.Width = 88;
            clear.Click += delegate { ClearFilters(); };

            left.Controls.Add(search);
            left.Controls.Add(create);
            left.Controls.Add(clear);

            var right = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            if (_addToDocument != null)
            {
                var add = UiTheme.CreateAntdButton("添加到单据", true);
                add.Width = 126;
                add.Click += delegate { AddSelectedWithoutClosing(); };
                right.Controls.Add(add);
            }

            var confirm = UiTheme.CreateAntdButton("确定", true);
            confirm.Width = 88;
            confirm.Click += delegate { AddSelectedAndClose(); };

            var cancel = UiTheme.CreateAntdButton("取消", false);
            cancel.Width = 88;
            cancel.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            right.Controls.Add(confirm);
            right.Controls.Add(cancel);

            footer.Controls.Add(left, 0, 0);
            footer.Controls.Add(right, 1, 0);
            return footer;
        }

        private BookSearchCriteria BuildCriteria()
        {
            var categoryIndex = _category.SelectedIndex;
            var category = categoryIndex >= 0 && categoryIndex < _categoryValues.Count
                ? _categoryValues[categoryIndex]
                : "";

            return new BookSearchCriteria
            {
                SelfCode = _selfCode.Text,
                Isbn = _isbn.Text,
                Author = _author.Text,
                Title = _title.Text,
                Publisher = _publisher.Text,
                Category = category
            };
        }

        private void Reload()
        {
            var result = _services.Books.SearchAdvanced(BuildCriteria());
            _selected = result.Count > 0 ? result[0] : null;
            _grid.DataSource = result;

            if (_selected != null)
                _grid.SetSelected(_selected, false);

            _summary.Tag = result.Count;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            var count = _summary.Tag is int ? (int)_summary.Tag : 0;
            if (_selected == null)
            {
                _summary.Text = "找到 " + count + " 条启用图书资料";
                return;
            }

            _summary.Text =
                "找到 " + count + " 条 · 当前：" +
                _selected.SelfCode + "  《" + _selected.Title + "》";
        }

        private void ClearFilters()
        {
            _selfCode.Text = "";
            _isbn.Text = "";
            _author.Text = "";
            _title.Text = "";
            _publisher.Text = "";
            _category.SelectedIndex = 0;
            Reload();
            _selfCode.Focus();
        }

        private void FocusInitialFilter()
        {
            if (!string.IsNullOrWhiteSpace(_isbn.Text))
                _isbn.Focus();
            else if (!string.IsNullOrWhiteSpace(_selfCode.Text))
                _selfCode.Focus();
            else
                _title.Focus();
        }

        private void CreateNewBook()
        {
            using (var editor = new BookEditForm(_services, null))
            {
                if (editor.ShowDialog(this) != DialogResult.OK || editor.SavedBook == null)
                    return;

                var saved = editor.SavedBook;
                _selfCode.Text = saved.SelfCode;
                _isbn.Text = "";
                _author.Text = "";
                _title.Text = "";
                _publisher.Text = "";
                _category.SelectedIndex = 0;
                Reload();

                foreach (var book in _services.Books.SearchAdvanced(BuildCriteria()))
                {
                    if (book.Id != saved.Id) continue;
                    _selected = book;
                    _grid.SetSelected(book, false);
                    break;
                }

                _summary.Text =
                    "已新建《" + saved.Title + "》，可直接添加到采购单。";
            }
        }

        private void AddSelectedWithoutClosing()
        {
            if (!EnsureSelection()) return;

            _addToDocument(_selected);
            _summary.Text =
                "已加入《" + _selected.Title + "》；可继续查找并添加其他图书。";
        }

        private void AddSelectedAndClose()
        {
            if (!EnsureSelection()) return;

            SelectedBook = _selected;
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool EnsureSelection()
        {
            if (_selected != null) return true;

            MessageBox.Show(
                this,
                "请先选择一条图书资料。",
                "还没有选择图书",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }
    }
}
