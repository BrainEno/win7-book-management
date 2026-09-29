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
        private readonly Panel _card = new Panel();
        private readonly Label _stepLabel = new Label();
        private readonly Label _titleLabel = new Label();
        private readonly Label _bodyLabel = new Label();
        private readonly Button _previous = new Button();
        private readonly Button _next = new Button();
        private Rectangle _targetBounds = Rectangle.Empty;
        private int _index;

        public OnboardingGuideForm(MainForm main, ApplicationServices services)
        {
            _main = main;
            _services = services;
            _steps = CreateSteps();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = main.Bounds;
            BackColor = Color.FromArgb(23, 31, 44);
            Opacity = 0.94;
            TopMost = false;
            KeyPreview = true;

            _card.Width = 470;
            _card.Height = 270;
            _card.BackColor = Color.White;
            _card.Padding = new Padding(24);
            Controls.Add(_card);

            _stepLabel.Dock = DockStyle.Top;
            _stepLabel.Height = 24;
            _stepLabel.ForeColor = UiTheme.Accent;
            _stepLabel.Font = UiTheme.Font(8.5F, FontStyle.Bold);

            _titleLabel.Dock = DockStyle.Top;
            _titleLabel.Height = 42;
            _titleLabel.ForeColor = UiTheme.TextPrimary;
            _titleLabel.Font = UiTheme.Font(16F, FontStyle.Bold);

            _bodyLabel.Dock = DockStyle.Top;
            _bodyLabel.Height = 112;
            _bodyLabel.ForeColor = UiTheme.TextSecondary;
            _bodyLabel.Font = UiTheme.Font(9.5F);
            _bodyLabel.AutoEllipsis = true;

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.White
            };

            _next.Text = "下一步";
            _next.Width = 90;
            _next.Height = 32;
            _next.Tag = "primary";
            _next.Click += delegate { NextStep(); };

            _previous.Text = "上一步";
            _previous.Width = 90;
            _previous.Height = 32;
            _previous.Click += delegate { PreviousStep(); };

            var skip = new Button
            {
                Text = "以后再看",
                Width = 92,
                Height = 32
            };
            skip.Click += delegate { FinishGuide(); };

            footer.Controls.Add(_next);
            footer.Controls.Add(_previous);
            footer.Controls.Add(skip);

            _card.Controls.Add(footer);
            _card.Controls.Add(_bodyLabel);
            _card.Controls.Add(_titleLabel);
            _card.Controls.Add(_stepLabel);

            UiTheme.Apply(_card);
            KeyDown += HandleKeyDown;
            Shown += delegate { ShowStep(0); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_targetBounds != Rectangle.Empty)
            {
                using (var pen = new Pen(Color.FromArgb(255, 220, 80), 4F))
                {
                    e.Graphics.DrawRectangle(pen, _targetBounds);
                }

                DrawArrow(e.Graphics);
            }
        }

        private void DrawArrow(Graphics graphics)
        {
            var targetCenter = new Point(
                _targetBounds.Left + _targetBounds.Width / 2,
                _targetBounds.Top + _targetBounds.Height / 2);

            Point start;
            if (_card.Left > targetCenter.X)
                start = new Point(_card.Left - 12, _card.Top + Math.Min(110, _card.Height / 2));
            else
                start = new Point(_card.Right + 12, _card.Top + Math.Min(110, _card.Height / 2));

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

            _stepLabel.Text = "新手引导  " + (_index + 1) + " / " + _steps.Count;
            _titleLabel.Text = step.Title;
            _bodyLabel.Text = step.Body;
            _previous.Enabled = _index > 0;
            _next.Text = _index == _steps.Count - 1 ? "完成" : "下一步";

            UpdateTarget(step.TargetNavigationKey);
            PositionCard();
            Invalidate();
        }

        private void UpdateTarget(string navigationKey)
        {
            _targetBounds = Rectangle.Empty;
            if (string.IsNullOrWhiteSpace(navigationKey)) return;

            var screen = _main.GetNavigationScreenBounds(navigationKey);
            if (screen == Rectangle.Empty) return;

            var topLeft = PointToClient(screen.Location);
            _targetBounds = new Rectangle(topLeft, screen.Size);
            _targetBounds.Inflate(6, 4);
        }

        private void PositionCard()
        {
            if (_targetBounds == Rectangle.Empty)
            {
                _card.Left = Math.Max(24, (ClientSize.Width - _card.Width) / 2);
                _card.Top = Math.Max(24, (ClientSize.Height - _card.Height) / 2);
                return;
            }

            var rightCandidate = _targetBounds.Right + 52;
            if (rightCandidate + _card.Width <= ClientSize.Width - 24)
                _card.Left = rightCandidate;
            else
                _card.Left = Math.Max(24, ClientSize.Width - _card.Width - 24);

            _card.Top = Math.Max(
                24,
                Math.Min(_targetBounds.Top - 34, ClientSize.Height - _card.Height - 24));
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
                    "欢迎使用 BOOK DESK",
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
                    "进货时选供应商，再扫描或搜索图书，填写数量和本次进价，最后点“确认入库”。库存会自动增加，不需要再手工改库存。"),
                new GuideStep(
                    "sales",
                    "sales",
                    "第三步：销售开单",
                    "卖书时直接扫描 ISBN，系统会加入商品并使用当前售价。确认数量无误后点“结账”，库存会自动扣减。未结账就离开页面时，系统会提醒你避免误丢单据。"),
                new GuideStep(
                    "documents",
                    "documents",
                    "第四步：查单据和处理退货",
                    "所有销售、采购和退货都集中在“单据中心”。要退货，不要手工改库存：选中原销售单或采购单，再点“发起退货”，系统会自动限制可退数量。"),
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
