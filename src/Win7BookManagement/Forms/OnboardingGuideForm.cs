using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class OnboardingGuideForm : Form
    {
        private readonly MainForm _main;
        private readonly ApplicationServices _services;
        private readonly IList<GuideStep> _steps;

        private readonly TableLayoutPanel _card = new TableLayoutPanel();
        private readonly Label _stepLabel = new Label();
        private readonly Label _titleLabel = new Label();
        private readonly Label _bodyLabel = new Label();
        private readonly AntdUI.Button _previous = UiTheme.CreateAntdButton("上一步", false);
        private readonly AntdUI.Button _next = UiTheme.CreateAntdButton("下一步", true);

        private Rectangle _targetBounds = Rectangle.Empty;
        private Bitmap _backgroundSnapshot;
        private int _index;

        public OnboardingGuideForm(MainForm main, ApplicationServices services)
        {
            _main = main;
            _services = services;
            _steps = CreateSteps();

            UiTheme.ConfigureForm(this);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = main.RectangleToScreen(main.ClientRectangle);
            BackColor = UiTheme.Background;
            Opacity = 1.0;
            TopMost = false;
            KeyPreview = true;

            ConfigureCard();
            Controls.Add(_card);

            UiTheme.Apply(_card);

            KeyDown += HandleKeyDown;
            Shown += delegate
            {
                // Keep the overlay on the client area only. The real interface
                // is rendered into a snapshot below the translucent dim layer,
                // so users can still see the page they are learning.
                Bounds = _main.RectangleToScreen(_main.ClientRectangle);
                RefreshBackgroundSnapshot();
                ResizeCard();
                ShowStep(0);
            };
            Resize += delegate
            {
                RefreshBackgroundSnapshot();
                ResizeCard();
                PositionCard();
                Invalidate();
            };
        }

        private void ConfigureCard()
        {
            _card.ColumnCount = 1;
            _card.RowCount = 4;
            _card.BackColor = UiTheme.Surface;
            _card.Padding = new Padding(24, 20, 24, 18);
            _card.Margin = Padding.Empty;
            _card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _card.BorderStyle = BorderStyle.FixedSingle;

            _stepLabel.AutoSize = true;
            _stepLabel.Dock = DockStyle.Top;
            _stepLabel.ForeColor = UiTheme.Accent;
            _stepLabel.Font = UiTheme.Font(8.5F, FontStyle.Bold);
            _stepLabel.Margin = new Padding(0, 0, 0, 6);

            _titleLabel.AutoSize = true;
            _titleLabel.Dock = DockStyle.Top;
            _titleLabel.ForeColor = UiTheme.TextPrimary;
            _titleLabel.Font = UiTheme.Font(15F, FontStyle.Bold);
            _titleLabel.Margin = new Padding(0, 0, 0, 10);

            _bodyLabel.AutoSize = true;
            _bodyLabel.Dock = DockStyle.Top;
            _bodyLabel.ForeColor = UiTheme.TextSecondary;
            _bodyLabel.Font = UiTheme.Font(9.2F);
            _bodyLabel.Margin = new Padding(0, 0, 0, 16);

            var footer = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _next.Text = "下一步";
            _next.Width = 92;
            _next.Height = UiTheme.ButtonHeight;
            _next.Tag = "primary";
            _next.Click += delegate { NextStep(); };

            _previous.Text = "上一步";
            _previous.Width = 92;
            _previous.Height = UiTheme.ButtonHeight;
            _previous.Click += delegate { PreviousStep(); };

            var skip = UiTheme.CreateAntdButton("跳过", false);
            skip.Width = 96;
            skip.Click += delegate { FinishGuide(); };

            footer.Controls.Add(_next);
            footer.Controls.Add(_previous);
            footer.Controls.Add(skip);

            _card.Controls.Add(_stepLabel, 0, 0);
            _card.Controls.Add(_titleLabel, 0, 1);
            _card.Controls.Add(_bodyLabel, 0, 2);
            _card.Controls.Add(footer, 0, 3);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_backgroundSnapshot != null)
            {
                e.Graphics.DrawImage(
                    _backgroundSnapshot,
                    new Rectangle(Point.Empty, ClientSize),
                    new Rectangle(Point.Empty, _backgroundSnapshot.Size),
                    GraphicsUnit.Pixel);
            }

            // A moderate 42% dim keeps non-target content readable instead of
            // covering the application with the previous nearly-opaque layer.
            using (var dim = new SolidBrush(Color.FromArgb(108, 14, 22, 34)))
                e.Graphics.FillRectangle(dim, ClientRectangle);

            if (_targetBounds == Rectangle.Empty)
                return;

            var spotlight = _targetBounds;
            spotlight.Inflate(8, 6);
            spotlight.Intersect(ClientRectangle);

            if (_backgroundSnapshot != null &&
                spotlight.Width > 0 &&
                spotlight.Height > 0)
            {
                e.Graphics.DrawImage(
                    _backgroundSnapshot,
                    spotlight,
                    spotlight,
                    GraphicsUnit.Pixel);
            }

            using (var pen = new Pen(UiTheme.Accent, 3F))
                e.Graphics.DrawRectangle(pen, spotlight);

            DrawArrow(e.Graphics);
        }

        private void DrawArrow(Graphics graphics)
        {
            var targetCenter = new Point(
                _targetBounds.Left + _targetBounds.Width / 2,
                _targetBounds.Top + _targetBounds.Height / 2);

            Point start;
            if (_card.Left > targetCenter.X)
                start = new Point(_card.Left - 12, _card.Top + Math.Min(120, _card.Height / 2));
            else
                start = new Point(_card.Right + 12, _card.Top + Math.Min(120, _card.Height / 2));

            using (var pen = new Pen(Color.FromArgb(255, 220, 80), 5F))
            {
                pen.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                graphics.DrawLine(pen, start, targetCenter);
            }
        }

        private void ShowStep(int index)
        {
            _index = Math.Max(0, Math.Min(index, _steps.Count - 1));
            var step = _steps[_index];

            if (!string.IsNullOrWhiteSpace(step.NavigationKey))
                _main.Navigate(step.NavigationKey);

            _main.PerformLayout();
            _main.Update();
            RefreshBackgroundSnapshot();

            _stepLabel.Text = "新手引导  " + (_index + 1) + " / " + _steps.Count;
            _titleLabel.Text = step.Title;
            _bodyLabel.Text = step.Body;
            _previous.Enabled = _index > 0;
            _next.Text = _index == _steps.Count - 1 ? "完成" : "下一步";

            ResizeCard();
            UpdateTarget(step.TargetNavigationKey);
            PositionCard();
            Invalidate();
        }

        private void RefreshBackgroundSnapshot()
        {
            if (_main == null ||
                _main.IsDisposed ||
                _main.ClientSize.Width <= 0 ||
                _main.ClientSize.Height <= 0)
                return;

            Bitmap next = null;
            try
            {
                next = new Bitmap(
                    _main.ClientSize.Width,
                    _main.ClientSize.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                _main.DrawToBitmap(
                    next,
                    new Rectangle(Point.Empty, _main.ClientSize));

                var old = _backgroundSnapshot;
                _backgroundSnapshot = next;
                next = null;
                if (old != null) old.Dispose();
            }
            catch
            {
                if (next != null) next.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _backgroundSnapshot != null)
            {
                _backgroundSnapshot.Dispose();
                _backgroundSnapshot = null;
            }
            base.Dispose(disposing);
        }

        private void ResizeCard()
        {
            if (ClientSize.Width <= 0)
                return;

            var cardWidth = Math.Min(540, Math.Max(420, ClientSize.Width - 96));
            var contentWidth = Math.Max(320, cardWidth - _card.Padding.Horizontal);

            _card.Width = cardWidth;
            _titleLabel.MaximumSize = new Size(contentWidth, 0);
            _bodyLabel.MaximumSize = new Size(contentWidth, 0);

            _card.PerformLayout();

            var preferred = _card.GetPreferredSize(new Size(cardWidth, 0));
            var maxHeight = Math.Max(280, ClientSize.Height - 48);
            _card.Height = Math.Min(maxHeight, Math.Max(280, preferred.Height));
        }

        private void UpdateTarget(string navigationKey)
        {
            _targetBounds = Rectangle.Empty;
            if (string.IsNullOrWhiteSpace(navigationKey))
                return;

            var screen = _main.GetNavigationScreenBounds(navigationKey);
            if (screen == Rectangle.Empty)
                return;

            var topLeft = PointToClient(screen.Location);
            _targetBounds = new Rectangle(topLeft, screen.Size);
            _targetBounds.Inflate(6, 4);
        }

        private void PositionCard()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            if (_targetBounds == Rectangle.Empty)
            {
                _card.Left = Math.Max(24, (ClientSize.Width - _card.Width) / 2);
                _card.Top = Math.Max(24, (ClientSize.Height - _card.Height) / 2);
                return;
            }

            var rightCandidate = _targetBounds.Right + 48;
            if (rightCandidate + _card.Width <= ClientSize.Width - 24)
                _card.Left = rightCandidate;
            else
                _card.Left = Math.Max(24, ClientSize.Width - _card.Width - 24);

            _card.Top = Math.Max(
                24,
                Math.Min(
                    _targetBounds.Top - 28,
                    ClientSize.Height - _card.Height - 24));
        }

        private void NextStep()
        {
            if (_index >= _steps.Count - 1)
            {
                FinishGuide();
                return;
            }

            ShowStep(_index + 1);
        }

        private void PreviousStep()
        {
            if (_index > 0)
                ShowStep(_index - 1);
        }

        private void FinishGuide()
        {
            _services.Settings.SetOnboardingCompleted(true);
            Close();
        }

        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter)
            {
                NextStep();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                PreviousStep();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                FinishGuide();
            }
        }

        private static IList<GuideStep> CreateSteps()
        {
            return new List<GuideStep>
            {
                new GuideStep(
                    null,
                    null,
                    "欢迎使用 BOOK",
                    "第一次使用不需要记住所有功能。最简单的顺序是：先建立图书资料 → 做采购入库 → 日常销售开单 → 需要时从单据中心退货 → 定期导出报表和备份。接下来会逐项指给你看。"),
                new GuideStep(
                    "books",
                    "books",
                    "第一步：建立图书资料",
                    "这里保存 ISBN、书名、作者、出版社和售价。新书第一次进入系统，先在这里建资料；以后扫描 ISBN 才能在采购和销售里快速找到它。"),
                new GuideStep(
                    "purchase",
                    "purchase",
                    "第二步：采购入库",
                    "进货时可以选择供应商，也可以先不指定供应商。扫描或搜索图书，填写数量和本次进价，最后点“确认入库”。库存会自动增加。"),
                new GuideStep(
                    "sales",
                    "sales",
                    "第三步：销售开单",
                    "卖书时直接扫描 ISBN，系统会加入商品并使用当前售价。确认数量无误后点“确认结账”，库存会自动扣减。未结账就离开页面时，系统会提醒你避免误丢单据。"),
                new GuideStep(
                    "documents",
                    "documents",
                    "第四步：查单据和处理退货",
                    "所有销售、采购和退货都集中在“单据中心”。要退货，不要手工改库存：选中原销售单或采购单，再从原单发起退货，系统会自动限制可退数量。"),
                new GuideStep(
                    "reports",
                    "reports",
                    "第五步：查报表和导出 Excel",
                    "这里可以按日期查询销售、采购、退货、库存变动，也能导出某一天结束时的历史库存。导出的 .xlsx 不要求电脑安装 Excel。"),
                new GuideStep(
                    "backup",
                    "backup",
                    "最后一步：记得备份",
                    "这套系统完全离线，数据库就在本机。建议每天营业结束后创建一次备份，并把备份复制到 U 盘或另一块硬盘。这样即使电脑损坏，也能恢复数据。")
            };
        }

        private sealed class GuideStep
        {
            public GuideStep(string navigationKey, string targetNavigationKey, string title, string body)
            {
                NavigationKey = navigationKey;
                TargetNavigationKey = targetNavigationKey;
                Title = title;
                Body = body;
            }

            public string NavigationKey { get; private set; }
            public string TargetNavigationKey { get; private set; }
            public string Title { get; private set; }
            public string Body { get; private set; }
        }
    }
}
