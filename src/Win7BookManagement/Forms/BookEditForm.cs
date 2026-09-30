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

        private readonly AntdUI.Input _selfCode = ModernUi.CreateInput();
        private readonly AntdUI.Input _isbn = ModernUi.CreateInput();
        private readonly AntdUI.Input _title = ModernUi.CreateInput();
        private readonly AntdUI.Input _author = ModernUi.CreateInput();
        private readonly AntdUI.Input _publisher = ModernUi.CreateInput();
        private readonly AntdUI.Input _category = ModernUi.CreateInput();
        private readonly AntdUI.Input _publicationYear = ModernUi.CreateInput();
        private readonly AntdUI.Input _edition = ModernUi.CreateInput();
        private readonly AntdUI.Input _binding = ModernUi.CreateInput();
        private readonly AntdUI.Input _shelfCode = ModernUi.CreateInput();
        private readonly TextBox _note = new TextBox();

        private readonly NumericUpDown _retailPrice = new NumericUpDown();
        private readonly NumericUpDown _defaultPurchasePrice = new NumericUpDown();
        private readonly CheckBox _active = new CheckBox();

        private readonly ErrorProvider _errors = new ErrorProvider();
        private readonly ToolTip _tips = new ToolTip();

        public BookEditForm(ApplicationServices services, Book book)
        {
            _services = services;
            _book = book;

            UiTheme.ConfigureForm(this);
            Text = book == null ? "新增图书资料" : "编辑图书资料";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1040;
            Height = 760;
            MinimumSize = new Size(760, 560);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;

            ConfigureMoney(_retailPrice);
            ConfigureMoney(_defaultPurchasePrice);

            _selfCode.Enabled = false;
            _selfCode.Text = book == null ? "保存后自动生成" : "";

            _active.Text = "启用此图书，可用于新的采购和销售";
            _active.AutoSize = true;
            _active.Checked = true;
            _active.Padding = new Padding(0, 8, 0, 0);

            _note.Multiline = true;
            _note.ScrollBars = ScrollBars.Vertical;

            var header = CreateHeader();
            var footer = CreateFooter(out Button saveButton, out Button cancelButton);
            var body = CreateBody();

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            _errors.ContainerControl = this;
            _errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;

            if (book != null)
                LoadBook(book);

            UiTheme.Apply(this);

            Shown += delegate
            {
                UiTheme.FitDialogToWorkingArea(this, 24);
                if (book == null)
                    _title.Focus();
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
                Padding = new Padding(24, 16, 24, 14),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = _book == null ? "建立图书资料" : "修改图书资料",
                AutoSize = true,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            };

            var hint = new Label
            {
                Text = "面向自出版物与独立出版场景：书名为唯一必填项，ISBN 与出版社均可留空；店内编码由系统自动生成。",
                AutoSize = true,
                MaximumSize = new Size(820, 0),
                Font = UiTheme.Font(8.5F),
                ForeColor = UiTheme.TextSecondary,
                Margin = Padding.Empty
            };

            header.Controls.Add(title, 0, 0);
            header.Controls.Add(hint, 0, 1);
            return header;
        }

        private Control CreateFooter(out Button save, out Button cancel)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                MinimumSize = new Size(0, 70),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(24, 12, 24, 12),
                Margin = Padding.Empty
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var hint = new Label
            {
                Text = "零售价格同时作为图书定价与开单默认售价；默认进价只用于采购预填，历史单据不会被改写。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                AutoEllipsis = true
            };

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };

            cancel = new Button
            {
                Text = "取消",
                Width = 94,
                Height = UiTheme.ButtonHeight,
                DialogResult = DialogResult.Cancel
            };
            save = new Button
            {
                Text = "保存资料",
                Width = 108,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            save.Click += Save;

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            footer.Controls.Add(hint, 0, 0);
            footer.Controls.Add(buttons, 1, 0);

            return footer;
        }

        private Control CreateBody()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiTheme.Background,
                Padding = new Padding(18, 16, 18, 16)
            };

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 4; i++)
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            stack.Controls.Add(CreateIdentitySection(), 0, 0);
            stack.Controls.Add(CreateClassificationSection(), 0, 1);
            stack.Controls.Add(CreatePricingSection(), 0, 2);
            stack.Controls.Add(CreateNoteSection(), 0, 3);

            host.Controls.Add(stack);
            return host;
        }

        private Control CreateIdentitySection()
        {
            var grid = CreateTwoColumnGrid(4);

            var titleField = CreateField("书名 *", _title, "建议录入封面主书名，便于开单、退货和报表检索。");
            grid.Controls.Add(titleField, 0, 0);
            grid.SetColumnSpan(titleField, 2);

            AddPair(
                grid,
                1,
                CreateField("作者", _author, "可填写作者、编者或主要责任者。"),
                CreateField("零售价格（元）", _retailPrice, "同时作为资料中的定价与销售开单默认售价。"));

            AddPair(
                grid,
                2,
                CreateField("ISBN（可选）", _isbn, "自出版物可以没有 ISBN；填写后可用于扫码和检索。"),
                CreateField("分类", _category, "例如 文学 / 社科 / 艺术 / 自出版。"));

            AddPair(
                grid,
                3,
                CreateField("店内编码", _selfCode, "新建时无需填写，保存后系统自动生成。"),
                CreateField("默认货架位", _shelfCode, "例如 A-03-2，方便找书和盘点。"));

            return WrapSection("核心资料", "把最常用、最影响销售与查找的字段放在最上方。", grid);
        }

        private Control CreateClassificationSection()
        {
            var grid = CreateTwoColumnGrid(2);

            AddPair(
                grid,
                0,
                CreateField("出版社（可选）", _publisher, "自出版物可留空；如有发行或出版主体可填写。"),
                CreateField("出版年 / 日期（可选）", _publicationYear, "可填写 2026、2026-09 等简洁文本。"));

            AddPair(
                grid,
                1,
                CreateField("版次（可选）", _edition, "低频字段，可留空。"),
                CreateField("装帧（可选）", _binding, "低频字段，例如 精装 / 平装，也可留空。"));

            return WrapSection("更多出版信息（可选）", "低频信息下沉，不阻碍快速建立自出版物资料。", grid);
        }

        private Control CreatePricingSection()
        {
            var grid = CreateTwoColumnGrid(1);

            AddPair(
                grid,
                0,
                CreateField("默认进价（元）", _defaultPurchasePrice, "新增采购行时自动预填，仍可在采购单内修改。"),
                CreateField("状态", _active, "停用后不参与新的采购和销售，历史单据仍保留。"));

            return WrapSection("采购与状态", "销售价格已经放到核心资料区；这里只保留经营辅助信息。", grid);
        }

        private Control CreateNoteSection()
        {
            var grid = CreateTwoColumnGrid(1);
            var noteField = CreateField("备注", _note, "可记录签名本、轻微瑕疵、陈列提醒等不影响库存计算的信息。", 104);
            grid.Controls.Add(noteField, 0, 0);
            grid.SetColumnSpan(noteField, 2);

            return WrapSection("经营备注", "只记录对日常经营真正有帮助的补充信息。", grid);
        }

        private static TableLayoutPanel CreateTwoColumnGrid(int rows)
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = rows,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (var i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return grid;
        }

        private static void AddPair(TableLayoutPanel table, int row, Control left, Control right)
        {
            table.Controls.Add(left, 0, row);
            table.Controls.Add(right, 1, row);
        }

        private Control WrapSection(string titleText, string subtitleText, Control content)
        {
            var section = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 0, 0, 10),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = titleText,
                AutoSize = true,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 3)
            };
            var subtitle = new Label
            {
                Text = subtitleText,
                AutoSize = true,
                Font = UiTheme.Font(8F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 0, 0, 10)
            };

            section.Controls.Add(title, 0, 0);
            section.Controls.Add(subtitle, 0, 1);
            section.Controls.Add(content, 0, 2);
            return section;
        }

        private Control CreateField(string labelText, Control input, string toolTip)
        {
            return CreateField(labelText, input, toolTip, 64);
        }

        private Control CreateField(string labelText, Control input, string toolTip, int height)
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
                Margin = new Padding(0, 0, 12, 8),
                Padding = Padding.Empty
            };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(8.6F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 4)
            };

            var multiline = input as TextBox;
            if (multiline != null && multiline.Multiline)
            {
                input.Dock = DockStyle.Fill;
                input.MinimumSize = new Size(0, Math.Max(76, height - 34));
            }
            else
            {
                input.Dock = DockStyle.Top;
            }
            input.Margin = new Padding(0, 0, 0, 2);

            field.Controls.Add(label, 0, 0);
            field.Controls.Add(input, 0, 1);

            if (!string.IsNullOrWhiteSpace(toolTip))
            {
                _tips.SetToolTip(label, toolTip);
                _tips.SetToolTip(input, toolTip);
                _tips.SetToolTip(field, toolTip);
            }

            return field;
        }

        private static void ConfigureMoney(NumericUpDown control)
        {
            control.DecimalPlaces = 2;
            control.Maximum = 1000000m;
            control.ThousandsSeparator = true;
            control.TextAlign = HorizontalAlignment.Right;
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
            var retailCent = book.SalePriceCent > 0 ? book.SalePriceCent : book.ListPriceCent;
            _retailPrice.Value = Math.Min(_retailPrice.Maximum, Money.ToYuan(retailCent));
            _defaultPurchasePrice.Value = Math.Min(_defaultPurchasePrice.Maximum, Money.ToYuan(book.DefaultPurchasePriceCent));
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
                var retailPriceCent = Money.FromYuan(_retailPrice.Value);
                target.ListPriceCent = retailPriceCent;
                target.DefaultPurchasePriceCent = Money.FromYuan(_defaultPurchasePrice.Value);
                target.SalePriceCent = retailPriceCent;
                target.IsActive = _active.Checked;

                if (_book == null)
                    _services.Books.Insert(target);
                else
                    _services.Books.Update(target);

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
