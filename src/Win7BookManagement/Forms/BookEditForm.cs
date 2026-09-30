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

        private readonly AntdUI.Input _selfCode = new AntdUI.Input();
        private readonly AntdUI.Input _isbn = new AntdUI.Input();
        private readonly AntdUI.Input _title = new AntdUI.Input();
        private readonly AntdUI.Input _author = new AntdUI.Input();
        private readonly AntdUI.Input _publisher = new AntdUI.Input();
        private readonly AntdUI.Input _category = new AntdUI.Input();
        private readonly AntdUI.Input _publicationYear = new AntdUI.Input();
        private readonly AntdUI.Input _edition = new AntdUI.Input();
        private readonly AntdUI.Input _binding = new AntdUI.Input();
        private readonly AntdUI.Input _shelfCode = new AntdUI.Input();
        private readonly TextBox _note = new TextBox();

        // The user sees one selling price. The legacy database still keeps
        // list_price_cent and sale_price_cent for backward compatibility; both
        // are written with the same value from this field.
        private readonly AntdUI.InputNumber _price = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber _defaultPurchasePrice = new AntdUI.InputNumber();
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
            Width = 980;
            Height = 730;
            MinimumSize = new Size(740, 540);
            BackColor = UiTheme.Background;
            ShowInTaskbar = false;
            MinimizeBox = false;

            ConfigureMoney(_price);
            ConfigureMoney(_defaultPurchasePrice);

            _selfCode.ReadOnly = true;
            _selfCode.PlaceholderText = "系统自动生成";
            _selfCode.TabStop = false;
            _selfCode.Text = book == null ? "保存后自动生成" : "";

            _active.Text = "启用此图书，可用于新的采购和销售";
            _active.AutoSize = true;
            _active.Checked = true;
            _active.Padding = new Padding(0, 8, 0, 0);

            _note.Multiline = true;
            _note.ScrollBars = ScrollBars.Vertical;

            Button saveButton;
            Button cancelButton;
            var header = CreateHeader();
            var footer = CreateFooter(out saveButton, out cancelButton);
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
                Padding = new Padding(24, 17, 24, 15),
                Margin = Padding.Empty
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = _book == null ? "建立图书资料" : "修改图书资料",
                AutoSize = true,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 6)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "书名是唯一必填业务信息。ISBN、出版社、版次和装帧均可留空；店内编码由系统自动生成。",
                AutoSize = true,
                MaximumSize = new Size(850, 0),
                Font = UiTheme.Font(8.7F),
                ForeColor = UiTheme.TextSecondary,
                Margin = Padding.Empty
            }, 0, 1);

            return header;
        }

        private Control CreateFooter(out Button save, out Button cancel)
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                MinimumSize = new Size(0, 72),
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
                Text = "销售价格同时作为图书定价；默认进价只用于新建采购行预填，不改写历史单据。",
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
                Width = 96,
                Height = UiTheme.ButtonHeight,
                DialogResult = DialogResult.Cancel
            };
            save = new Button
            {
                Text = "保存资料",
                Width = 112,
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

            stack.Controls.Add(CreatePrimarySection(), 0, 0);
            stack.Controls.Add(CreateClassificationSection(), 0, 1);
            stack.Controls.Add(CreateOperationsSection(), 0, 2);
            stack.Controls.Add(CreateNoteSection(), 0, 3);
            host.Controls.Add(stack);
            return host;
        }

        private Control CreatePrimarySection()
        {
            var grid = CreateTwoColumnGrid(3);

            var titleField = CreateField(
                "书名 *",
                _title,
                "图书最重要的识别信息，也是唯一必填的业务字段。");
            grid.Controls.Add(titleField, 0, 0);
            grid.SetColumnSpan(titleField, 2);

            AddPair(
                grid,
                1,
                CreateField("作者 / 责任者", _author, "作者、编者、艺术家或其他主要责任者。"),
                CreateField("销售价格（元）", _price, "同时作为图书定价和默认零售价。"));

            AddPair(
                grid,
                2,
                CreateField("店内商品编码", _selfCode, "系统自动生成 BK-000001 形式的唯一编码，与 ISBN 完全解耦。"),
                CreateField("ISBN（可选）", _isbn, "自出版物或无 ISBN 商品可以直接留空。"));

            return WrapSection(
                "核心信息",
                "日常检索和开单最常用的信息放在最前面。",
                grid);
        }

        private Control CreateClassificationSection()
        {
            var grid = CreateTwoColumnGrid(3);

            AddPair(
                grid,
                0,
                CreateField("分类", _category, "例如 文学 / 社科 / 艺术 / 自出版。"),
                CreateField("默认货架位", _shelfCode, "例如 A-03-2，便于找书和盘点。"));

            AddPair(
                grid,
                1,
                CreateField("出版社（可选）", _publisher, "自出版物可以留空。"),
                CreateField("出版年 / 日期（可选）", _publicationYear, "可填写 2026、2026-09 等简洁文本。"));

            AddPair(
                grid,
                2,
                CreateField("版次（可选）", _edition, "低频信息，不影响销售、库存和检索主流程。"),
                CreateField("装帧（可选）", _binding, "例如 精装 / 平装；没有需要可留空。"));

            return WrapSection(
                "归类与出版信息",
                "这些字段用于整理和陈列，不阻止无 ISBN、自出版或小批量出版物建档。",
                grid);
        }

        private Control CreateOperationsSection()
        {
            var grid = CreateTwoColumnGrid(1);
            AddPair(
                grid,
                0,
                CreateField("默认进价（元）", _defaultPurchasePrice, "采购入库时自动预填，本次采购仍可单独修改。"),
                CreateField("资料状态", _active, "停用后不参与新的采购和销售，历史单据仍保留。"));

            return WrapSection(
                "经营设置",
                "只保留会直接影响门店日常操作的设置。",
                grid);
        }

        private Control CreateNoteSection()
        {
            var grid = CreateTwoColumnGrid(1);
            var noteField = CreateField(
                "备注",
                _note,
                "可记录签名本、编号版、轻微瑕疵、陈列提醒等信息。",
                112);
            grid.Controls.Add(noteField, 0, 0);
            grid.SetColumnSpan(noteField, 2);

            return WrapSection("经营备注", "记录自出版物或特殊版本的补充信息。", grid);
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
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(0, 0, 0, 11),
                BorderStyle = BorderStyle.None
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            section.Controls.Add(new Label
            {
                Text = titleText,
                AutoSize = true,
                Font = UiTheme.Font(10.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 3)
            }, 0, 0);

            section.Controls.Add(new Label
            {
                Text = subtitleText,
                AutoSize = true,
                Font = UiTheme.Font(8.2F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 0, 0, 11)
            }, 0, 1);

            section.Controls.Add(content, 0, 2);
            return section;
        }

        private Control CreateField(string labelText, Control input, string toolTip)
        {
            return CreateField(labelText, input, toolTip, 70);
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
                Margin = new Padding(0, 0, 14, 10),
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
                Font = UiTheme.Font(8.7F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            };

            var multiline = input as TextBox;
            if (multiline != null && multiline.Multiline)
            {
                input.Dock = DockStyle.Fill;
                input.MinimumSize = new Size(0, Math.Max(80, height - 34));
            }
            else
            {
                input.Dock = DockStyle.Top;
                input.MinimumSize = new Size(0, 38);
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

        private static void ConfigureMoney(AntdUI.InputNumber control)
        {
            control.DecimalPlaces = 2;
            control.Maximum = 1000000m;
            control.ThousandsSeparator = true;
            control.MinimumSize = new Size(0, 38);
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

            var displayPrice = book.SalePriceCent > 0
                ? Money.ToYuan(book.SalePriceCent)
                : Money.ToYuan(book.ListPriceCent);
            _price.Value = Math.Min(_price.Maximum ?? displayPrice, displayPrice);
            _defaultPurchasePrice.Value = Math.Min(
                _defaultPurchasePrice.Maximum ?? Money.ToYuan(book.DefaultPurchasePriceCent),
                Money.ToYuan(book.DefaultPurchasePriceCent));
            _active.Checked = book.IsActive;
        }

        private void Save(object sender, EventArgs e)
        {
            _errors.Clear();

            if (string.IsNullOrWhiteSpace(_title.Text))
            {
                _errors.SetError(_title, "请输入书名。");
                _title.Focus();
                MessageBox.Show(
                    this,
                    "请先填写书名，再保存图书资料。",
                    "还差一项",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                var target = _book ?? new Book();
                target.SelfCode = _book == null ? "" : (_selfCode.Text ?? "").Trim();
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

                var priceCent = Money.FromYuan(_price.Value);
                target.ListPriceCent = priceCent;
                target.SalePriceCent = priceCent;
                target.DefaultPurchasePriceCent = Money.FromYuan(_defaultPurchasePrice.Value);
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
                MessageBox.Show(
                    this,
                    "无法保存图书资料：\r\n" + ex.Message,
                    "请检查输入",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
