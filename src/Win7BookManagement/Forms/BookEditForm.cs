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
        private readonly TextBox _selfCode = new TextBox();
        private readonly TextBox _isbn = new TextBox();
        private readonly TextBox _title = new TextBox();
        private readonly TextBox _author = new TextBox();
        private readonly TextBox _publisher = new TextBox();
        private readonly TextBox _category = new TextBox();
        private readonly TextBox _publicationYear = new TextBox();
        private readonly TextBox _edition = new TextBox();
        private readonly TextBox _binding = new TextBox();
        private readonly TextBox _shelfCode = new TextBox();
        private readonly TextBox _note = new TextBox();
        private readonly NumericUpDown _listPrice = new NumericUpDown();
        private readonly NumericUpDown _defaultPurchasePrice = new NumericUpDown();
        private readonly NumericUpDown _salePrice = new NumericUpDown();
        private readonly CheckBox _active = new CheckBox();
        private readonly ErrorProvider _errors = new ErrorProvider();

        public BookEditForm(ApplicationServices services, Book book)
        {
            _services = services;
            _book = book;

            UiTheme.ConfigureForm(this);
            Text = book == null ? "新增图书资料" : "编辑图书资料";
            StartPosition = FormStartPosition.CenterParent;
            Width = 760;
            Height = 650;
            MinimumSize = new Size(640, 520);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;

            ConfigureMoney(_listPrice);
            ConfigureMoney(_defaultPurchasePrice);
            ConfigureMoney(_salePrice);

            _active.Text = "启用此图书，可用于采购和销售";
            _active.AutoSize = true;
            _active.Checked = true;

            _note.Multiline = true;
            _note.ScrollBars = ScrollBars.Vertical;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 86,
                BackColor = UiTheme.Surface,
                Padding = new Padding(24, 16, 24, 8)
            };
            var headerTitle = new Label
            {
                Text = book == null ? "建立图书资料" : "修改图书资料",
                Dock = DockStyle.Top,
                Height = 30,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            var headerHint = new Label
            {
                Text = "只需填写日常经营真正会用到的信息；带 * 的项目必须填写。库存数量请通过采购、销售或库存调整维护。",
                Dock = DockStyle.Top,
                Height = 26,
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextSecondary
            };
            header.Controls.Add(headerHint);
            header.Controls.Add(headerTitle);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                ColumnCount = 2,
                BackColor = UiTheme.Surface,
                Padding = new Padding(24, 12, 24, 12)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var footerHint = new Label
            {
                Text = "提示：默认进价只用于下次采购时预填，不会改写任何历史采购单。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F)
            };
            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            var cancel = new Button { Text = "取消", Width = 92, Height = 36, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "保存资料", Width = 104, Height = 36, Tag = "primary" };
            save.Click += Save;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            footer.Controls.Add(footerHint, 0, 0);
            footer.Controls.Add(buttons, 1, 0);

            var bodyHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Background,
                Padding = new Padding(20, 18, 20, 18)
            };

            var formGrid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 9,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty
            };
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (var row = 0; row < 9; row++)
                formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AddPair(formGrid, 0,
                CreateField("店内编码", _selfCode, "可选。用于书店内部查找，例如 BK-001。"),
                CreateField("ISBN", _isbn, "可扫码录入；同一个 ISBN 在本轻量版中只能对应一条图书资料。"));

            var titleField = CreateField("书名 *", _title, "建议录入封面主书名，方便销售、退货和报表检索。");
            formGrid.Controls.Add(titleField, 0, 1);
            formGrid.SetColumnSpan(titleField, 2);

            AddPair(formGrid, 2,
                CreateField("作者", _author, null),
                CreateField("出版社", _publisher, null));

            AddPair(formGrid, 3,
                CreateField("分类", _category, "例如 文学 / 社科 / 艺术。"),
                CreateField("默认货架位", _shelfCode, "例如 A-03-2，方便找书和盘点。"));

            AddPair(formGrid, 4,
                CreateField("出版年 / 日期", _publicationYear, "可填写 2026、2026-09 等简洁文本。"),
                CreateField("版次", _edition, "例如 1版1印。"));

            AddPair(formGrid, 5,
                CreateField("装帧", _binding, "例如 精装 / 平装。"),
                CreateField("状态", _active, "停用后不参与新采购和新销售，但历史单据仍保留。"));

            AddPair(formGrid, 6,
                CreateField("定价（元）", _listPrice, "出版社标价，可为 0。"),
                CreateField("默认进价（元）", _defaultPurchasePrice, "新增采购行时自动预填，仍可在采购单里修改。"));

            var saleField = CreateField("零售价（元）", _salePrice, "销售开单时默认带出，可在开单时调整。");
            formGrid.Controls.Add(saleField, 0, 7);
            formGrid.SetColumnSpan(saleField, 2);

            var noteField = CreateField("备注", _note, "记录签名本、轻微瑕疵、陈列提醒等不影响库存计算的信息。", 136, 72);
            formGrid.Controls.Add(noteField, 0, 8);
            formGrid.SetColumnSpan(noteField, 2);

            bodyHost.Controls.Add(formGrid);

            Controls.Add(bodyHost);
            Controls.Add(footer);
            Controls.Add(header);

            AcceptButton = save;
            CancelButton = cancel;
            _errors.ContainerControl = this;
            _errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;

            if (book != null)
                LoadBook(book);

            UiTheme.Apply(this);
            Shown += delegate
            {
                if (book == null) _title.Focus();
            };
        }

        private static void ConfigureMoney(NumericUpDown control)
        {
            control.DecimalPlaces = 2;
            control.Maximum = 1000000m;
            control.ThousandsSeparator = true;
            control.TextAlign = HorizontalAlignment.Right;
        }

        private static void AddPair(TableLayoutPanel table, int row, Control left, Control right)
        {
            table.Controls.Add(left, 0, row);
            table.Controls.Add(right, 1, row);
        }

        private static Control CreateField(string labelText, Control input, string helpText, int height = 86, int inputHeight = 34)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = height,
                MinimumSize = new Size(0, height),
                RowCount = helpText == null ? 2 : 3,
                ColumnCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 10, 14, 8),
                Margin = new Padding(0, 0, 12, 12)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, inputHeight));
            if (helpText != null)
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8.8F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 2, 0, 2);

            panel.Controls.Add(label, 0, 0);
            panel.Controls.Add(input, 0, 1);

            if (helpText != null)
            {
                panel.Controls.Add(new Label
                {
                    Text = helpText,
                    Dock = DockStyle.Fill,
                    Font = UiTheme.Font(7.5F),
                    ForeColor = UiTheme.TextSecondary,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 2);
            }

            return panel;
        }

        private void LoadBook(Book book)
        {
            _selfCode.Text = book.SelfCode;
            _isbn.Text = book.Isbn;
            _title.Text = book.Title;
            _author.Text = book.Author;
            _publisher.Text = book.Publisher;
            _category.Text = book.Category;
            _publicationYear.Text = book.PublicationYear;
            _edition.Text = book.Edition;
            _binding.Text = book.Binding;
            _shelfCode.Text = book.ShelfCode;
            _note.Text = book.Note;
            _listPrice.Value = Math.Min(_listPrice.Maximum, Money.ToYuan(book.ListPriceCent));
            _defaultPurchasePrice.Value = Math.Min(_defaultPurchasePrice.Maximum, Money.ToYuan(book.DefaultPurchasePriceCent));
            _salePrice.Value = Math.Min(_salePrice.Maximum, Money.ToYuan(book.SalePriceCent));
            _active.Checked = book.IsActive;
        }

        private void Save(object sender, EventArgs e)
        {
            _errors.Clear();
            if (string.IsNullOrWhiteSpace(_title.Text))
            {
                _errors.SetError(_title, "请输入书名。");
                _title.Focus();
                MessageBox.Show(this, "请先填写书名，再保存图书资料。", "还差一项", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var target = _book ?? new Book();
                target.SelfCode = _selfCode.Text;
                target.Isbn = _isbn.Text;
                target.Title = _title.Text;
                target.Author = _author.Text;
                target.Publisher = _publisher.Text;
                target.Category = _category.Text;
                target.PublicationYear = _publicationYear.Text;
                target.Edition = _edition.Text;
                target.Binding = _binding.Text;
                target.ShelfCode = _shelfCode.Text;
                target.Note = _note.Text;
                target.ListPriceCent = Money.FromYuan(_listPrice.Value);
                target.DefaultPurchasePriceCent = Money.FromYuan(_defaultPurchasePrice.Value);
                target.SalePriceCent = Money.FromYuan(_salePrice.Value);
                target.IsActive = _active.Checked;

                if (_book == null) _services.Books.Insert(target);
                else _services.Books.Update(target);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法保存图书资料：\r\n" + ex.Message, "请检查输入", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
