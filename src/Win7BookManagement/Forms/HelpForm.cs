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
            BackColor = UiTheme.Background;

            var header = UiTheme.CreateCard();
            header.Dock = DockStyle.Top;
            header.Height = 112;
            header.Padding = new Padding(20);

            var title = new Label
            {
                Text = "不会用也没关系：按这条路线操作就行",
                Dock = DockStyle.Top,
                Height = 32,
                Font = UiTheme.Font(14F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            var subtitle = new Label
            {
                Text = "绝大多数日常操作只需要记住：建资料 → 入库 → 销售 → 查单/退货 → 报表 → 备份。",
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = UiTheme.TextSecondary
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = UiTheme.Surface
            };
            var replay = new Button { Text = "重新播放新手引导", Width = 142, Height = 32, Tag = "primary" };
            replay.Click += delegate { if (startGuide != null) startGuide(); };
            var sales = new Button { Text = "去销售开单", Width = 104, Height = 32 };
            sales.Click += delegate { if (navigate != null) navigate("sales"); };
            actions.Controls.Add(replay);
            actions.Controls.Add(sales);

            header.Controls.Add(actions);
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

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
                Text =
@"【第一次使用】
1. 打开“图书资料”，点“新增图书”，至少填写书名；有 ISBN 的书建议一定填写 ISBN。
2. 打开“供应商”建立常用供货方。
3. 打开“采购入库”，选择供应商，扫描 ISBN 或点“选择图书”，填写数量和进价，点“确认入库”。
4. 回到“经营概览”确认库存已经增加。

【每天卖书】
1. 打开“销售开单”。
2. 用扫码枪扫描 ISBN（扫码枪等同键盘输入并回车），也可以点“选择图书”。
3. 检查数量和售价，点“结账”。
4. 系统会自动扣减库存并生成销售单，不需要再去库存页面手工减少数量。

【顾客退货】
1. 打开“单据中心”，选择“销售单”。
2. 找到原销售单，可以按日期、单号、ISBN 或书名搜索。
3. 点“发起退货”，只填写本次真正退回的数量。
4. 系统按原销售价退款并把库存加回，原销售单不会被修改。

【退货给供应商】
1. 打开“单据中心”，选择“采购单”。
2. 找到原采购单后点“发起退货”。
3. 系统会显示原数量、已退、可退和当前库存。
4. 退货后库存自动减少；库存不足时系统会阻止错误操作。

【库存不对怎么办】
不要直接修改数据库，也不要为了修正库存伪造采购或销售。
打开“库存管理” → 选择图书 → “库存调整”，输入增加或减少的数量和原因。
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

            var guideCard = UiTheme.CreateCard();
            guideCard.Dock = DockStyle.Fill;
            guideCard.Padding = new Padding(22);
            guideCard.Controls.Add(guide);

            Controls.Add(guideCard);
            Controls.Add(header);
            UiTheme.Apply(this);
        }
    }
}
