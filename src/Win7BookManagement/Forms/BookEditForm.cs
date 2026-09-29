using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Forms
{
    public sealed class BookEditForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Book _book;
        private readonly TextBox _isbn = new TextBox();
        private readonly TextBox _title = new TextBox();
        private readonly TextBox _author = new TextBox();
        private readonly TextBox _publisher = new TextBox();
        private readonly TextBox _category = new TextBox();
        private readonly NumericUpDown _listPrice = new NumericUpDown();
        private readonly NumericUpDown _salePrice = new NumericUpDown();
        private readonly CheckBox _active = new CheckBox();

        public BookEditForm(ApplicationServices services, Book book)
        {
            _services = services;
            _book = book;

            Text = book == null ? "新增图书" : "编辑图书";
            StartPosition = FormStartPosition.CenterParent;
            Width = 600;
            Height = 500;
            MinimumSize = new Size(560, 460);
            BackColor = UiTheme.Background;

            _listPrice.DecimalPlaces = 2;
            _listPrice.Maximum = 1000000m;
            _salePrice.DecimalPlaces = 2;
            _salePrice.Maximum = 1000000m;
            _active.Text = "启用";
            _active.Checked = true;

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(24),
                BackColor = UiTheme.Surface
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(table, 0, "ISBN", _isbn);
            AddRow(table, 1, "书名 *", _title);
            AddRow(table, 2, "作者", _author);
            AddRow(table, 3, "出版社", _publisher);
            AddRow(table, 4, "分类", _category);
            AddRow(table, 5, "定价（元）", _listPrice);
            AddRow(table, 6, "售价（元）", _salePrice);
            AddRow(table, 7, "状态", _active);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = UiTheme.Surface
            };
            var save = new Button { Text = "保存", Width = 90, Height = 32, Tag = "primary" };
            var cancel = new Button { Text = "取消", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
            save.Click += Save;
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            table.Controls.Add(buttons, 1, 8);

            Controls.Add(table);
            AcceptButton = save;
            CancelButton = cancel;

            if (book != null)
                LoadBook(book);

            UiTheme.Apply(this);
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            table.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, 0, row);
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(8, 6, 6, 6);
            table.Controls.Add(control, 1, row);
        }

        private void LoadBook(Book book)
        {
            _isbn.Text = book.Isbn;
            _title.Text = book.Title;
            _author.Text = book.Author;
            _publisher.Text = book.Publisher;
            _category.Text = book.Category;
            _listPrice.Value = Math.Min(_listPrice.Maximum, Money.ToYuan(book.ListPriceCent));
            _salePrice.Value = Math.Min(_salePrice.Maximum, Money.ToYuan(book.SalePriceCent));
            _active.Checked = book.IsActive;
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                var target = _book ?? new Book();
                target.Isbn = _isbn.Text;
                target.Title = _title.Text;
                target.Author = _author.Text;
                target.Publisher = _publisher.Text;
                target.Category = _category.Text;
                target.ListPriceCent = Money.FromYuan(_listPrice.Value);
                target.SalePriceCent = Money.FromYuan(_salePrice.Value);
                target.IsActive = _active.Checked;

                if (_book == null) _services.Books.Insert(target);
                else _services.Books.Update(target);

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
