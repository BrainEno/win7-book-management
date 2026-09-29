using System;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Forms
{
    public sealed class HelpForm : Form
    {
        public HelpForm(ApplicationServices services, Action startGuide, Action<string> navigate)
        {
            UiTheme.ConfigureForm(this);
            BackColor = UiTheme.Background;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(CreateHeader(startGuide, navigate), 0, 0);
            root.Controls.Add(CreateGuideCard(), 0, 1);

            Controls.Add(root);
            UiTheme.Apply(this);
        }

        private static Control CreateHeader(Action startGuide, Action<string> navigate)
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiTheme.Surface,
                Padding = new Padding(18, 15, 18, 14),
                Margin = new Padding(0, 0, 0, 12),
                BorderStyle = BorderStyle.None
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            header.Controls.Add(new Label
            {
                Text = "使用帮助",
                AutoSize = true,
                Font = UiTheme.Font(13F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Margin = new Padding(0, 0, 0, 5)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Text = "最常用的路线只有六步：建资料 → 入库 → 销售 → 查单 / 退货 → 报表 → 备份。",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Font = UiTheme.Font(8.6F),
                ForeColor = UiTheme.TextSecondary,
                Margin = new Padding(0, 0, 0, 10)
            }, 0, 1);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty
            };

            var replay = new Button
            {
                Text = "重新播放新手引导",
                Width = 148,
                Height = UiTheme.ButtonHeight,
                Tag = "primary"
            };
            replay.Click += delegate
            {
                if (startGuide != null)
                    startGuide();
            };

            var sales = new Button
            {
                Text = "去销售开单",
                Width = 108,
                Height = UiTheme.ButtonHeight
            };
            sales.Click += delegate
            {
                if (navigate != null)
                    navigate("sales");
            };

            actions.Controls.Add(replay);
            actions.Controls.Add(sales);
            header.Controls.Add(actions, 0, 2);
            return header;
        }

        private static Control CreateGuideCard()
        {
            var guide = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(9.5F),
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                Text =
@"【第一次使用】
1. 打开“图书资料”，点“新增图书”，至少填写书名；有 ISBN 的书建议一定填写 ISBN。
2. 打开“供应商”建立常用供货方。
3. 打开“采购入库”，选择供应商，扫描 ISBN 或点“选择图书”，填写数量和进价，点“确认入库”。
4. 回到“经营概览”确认库存已经增加。

【每天卖书】
1. 打开“销售开单”。
2. 用扫码枪扫描 ISBN（扫码枪等同键盘输入并回车），也可以点“选择图书”。
3. 检查数量和售价，点“确认结账”。
4. 系统会自动扣减库存并生成销售单，不需要再去库存页面手工减少数量。

【顾客退货】
1. 打开“单据中心”，选择“销售单”。
2. 找到原销售单，可以按日期、单号、ISBN 或书名搜索。
3. 点“从选中单据发起退货”，只填写本次真正退回的数量。
4. 系统按原销售价退款并把库存加回，原销售单不会被修改。

【退货给供应商】
1. 打开“单据中心”，选择“采购单”。
2. 找到原采购单后发起退货。
3. 系统会显示原数量、已退、可退和当前库存。
4. 退货后库存自动减少；库存不足时系统会阻止错误操作。

【库存不对怎么办】
不要直接修改数据库，也不要为了修正库存伪造采购或销售。
打开“库存管理” → 选择图书 → “库存调整”，明确选择增加或减少，填写数量和原因。
系统会留下完整库存流水。

【报表和 Excel】
“报表与导出”可以按日期查询销售、采购、销售退货、采购退货和库存变动。
“指定日期库存快照”表示所选日期当天结束时的库存。
点“导出 Excel”即可生成 .xlsx，目标电脑不需要安装 Microsoft Excel。

【每天关店前】
建议打开“备份与恢复” → “创建备份”。
把备份文件再复制到 U 盘或另一块硬盘。电脑硬盘坏掉时，只要备份还在，数据就能恢复。

【最重要的三条】
• 销售和采购完成后，库存会自动变化，不要重复手工调整。
• 退货必须从原单据发起，不要用库存调整代替正常退货。
• 数据都在本机，定期备份比任何功能都重要。"
            };

            var card = UiTheme.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(20);
            card.Margin = Padding.Empty;
            card.Controls.Add(guide);
            return card;
        }
    }
}
