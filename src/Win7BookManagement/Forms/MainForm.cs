using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class MainForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly Panel _content;
        private readonly Label _title;

        public MainForm(ApplicationServices services)
        {
            _services = services;

            Text = "简易图书管理系统";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(960, 640);
            Font = new Font("Microsoft YaHei", 9F);

            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 170,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 246, 248)
            };

            var brand = new Label
            {
                Text = "图书管理",
                AutoSize = false,
                Width = 145,
                Height = 48,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold)
            };
            left.Controls.Add(brand);
            left.Controls.Add(CreateNavButton("图书资料", delegate { ShowChild("图书资料", new BookListForm(_services)); }));
            left.Controls.Add(CreateNavButton("供应商", delegate { ShowChild("供应商", new SupplierForm(_services)); }));
            left.Controls.Add(CreateNavButton("采购入库", delegate { ShowChild("采购入库", new PurchaseForm(_services)); }));
            left.Controls.Add(CreateNavButton("销售开单", delegate { ShowChild("销售开单", new SalesForm(_services)); }));
            left.Controls.Add(CreateNavButton("库存管理", delegate { ShowChild("库存管理", new InventoryForm(_services)); }));

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                Padding = new Padding(18, 8, 18, 8)
            };
            _title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "图书资料",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 13F, FontStyle.Bold)
            };
            header.Controls.Add(_title);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.White
            };

            Controls.Add(_content);
            Controls.Add(header);
            Controls.Add(left);

            Shown += delegate { ShowChild("图书资料", new BookListForm(_services)); };
        }

        private Button CreateNavButton(string text, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Width = 145,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 3, 0, 3)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            return button;
        }

        private void ShowChild(string title, Form child)
        {
            for (var i = _content.Controls.Count - 1; i >= 0; i--)
                _content.Controls[i].Dispose();
            _content.Controls.Clear();

            _title.Text = title;
            child.TopLevel = false;
            child.FormBorderStyle = FormBorderStyle.None;
            child.Dock = DockStyle.Fill;
            _content.Controls.Add(child);
            child.Show();
        }
    }
}
