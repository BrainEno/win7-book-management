using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Reporting;

namespace Win7BookManagement.Forms
{
    public sealed class BookLabelForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly DataGridView _grid = new DataGridView();
        private readonly AntdUI.Select _paper = new AntdUI.Select();

        public BookLabelForm(ApplicationServices services, Book selectedBook = null)
        {
            _services = services;
            UiTheme.ConfigureForm(this);
            Text = "打印图书标签"; ClientSize = new Size(850, 560); MinimumSize = new Size(680, 450);
            StartPosition = FormStartPosition.CenterParent; BackColor = UiTheme.Background;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text = "添加一本或多本启用图书，填写每本的标签张数。每页一张标签，条码为店内编码（Code 128），不是 ISBN。请先在打印机驱动中设置相同纸张。" }, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 8) };
            var add = UiTheme.CreateAntdButton("添加图书", true); add.Width = 110;
            add.Click += delegate { using (var lookup = new BookLookupDialog(_services)) if (lookup.ShowDialog(this) == DialogResult.OK) AddBook(lookup.SelectedBook); };
            var remove = UiTheme.CreateAntdButton("移除选中", false); remove.Width = 110;
            remove.Click += delegate { if (_grid.CurrentRow != null) _grid.Rows.Remove(_grid.CurrentRow); };
            actions.Controls.Add(add); actions.Controls.Add(remove);
            actions.Controls.Add(new Label { Text = "标签纸", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            _paper.Width = 155; _paper.Items.Add("50 × 30 mm"); _paper.Items.Add("60 × 40 mm"); _paper.SelectedIndex = 0;
            actions.Controls.Add(_paper); root.Controls.Add(actions, 0, 1);
            _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "店内编码", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "书名", ReadOnly = true, FillWeight = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "售价", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Count", HeaderText = "标签张数（可修改）" });
            root.Controls.Add(_grid, 0, 2);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
            var print = UiTheme.CreateAntdButton("选择打印机并打印", true); print.Width = 165; print.Click += delegate { Print(); };
            var close = UiTheme.CreateAntdButton("关闭", false); close.Width = 90; close.Click += delegate { Close(); };
            footer.Controls.Add(print); footer.Controls.Add(close); root.Controls.Add(footer, 0, 3);
            Controls.Add(root); UiTheme.Apply(this);
            Shown += delegate { UiTheme.FitDialogToWorkingArea(this, 24); };
            if (selectedBook != null && selectedBook.IsActive) AddBook(selectedBook);
        }

        private void AddBook(Book book)
        {
            if (book == null) return;
            foreach (DataGridViewRow row in _grid.Rows)
                if ((long)row.Tag == book.Id) { row.Selected = true; _grid.CurrentCell = row.Cells["Count"]; return; }
            if (_grid.Rows.Count >= 100) { MessageBox.Show(this, "一次最多选择 100 种图书。", "提示"); return; }
            var index = _grid.Rows.Add(book.SelfCode, book.Title, book.SalePriceYuan.ToString("0.00"), 1);
            _grid.Rows[index].Tag = book.Id;
        }

        private void Print()
        {
            try
            {
                _grid.EndEdit();
                var books = new List<Book>();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    int count;
                    if (!int.TryParse(Convert.ToString(row.Cells["Count"].Value), out count) || count < 1 || count > 100)
                        throw new InvalidOperationException("每种图书的标签张数必须是 1 到 100 的整数。");
                    var book = _services.Books.GetById((long)row.Tag);
                    if (book == null || !book.IsActive) throw new InvalidOperationException("所选图书已删除或停用，请移除后重新选择。");
                    if (book.SelfCode != Convert.ToString(row.Cells["Code"].Value) ||
                        book.Title != Convert.ToString(row.Cells["Title"].Value) ||
                        book.SalePriceYuan.ToString("0.00") != Convert.ToString(row.Cells["Price"].Value))
                        throw new InvalidOperationException("图书资料已改变，请移除《" + book.Title + "》并重新添加，再核对标签。");
                    if (books.Count + count > 1000) throw new InvalidOperationException("一次最多打印 1000 张标签，请分批打印。");
                    for (var i = 0; i < count; i++) books.Add(book);
                }
                if (books.Count == 0) throw new InvalidOperationException("请先添加需要打印的图书。");
                var wide = _paper.SelectedIndex == 1;
                if (MessageBox.Show(this, "将打印 " + books.Count + " 张图书标签，每页一张。请确认纸张规格和售价，继续选择打印机吗？", "确认标签", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                BookLabelPrinter.Print(this, books, wide ? 60 : 50, wide ? 40 : 30);
            }
            catch (Exception ex) { CrashReporter.Handle(this, ex, "打印图书标签"); }
        }
    }
}
