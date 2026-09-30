using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Forms;

namespace Win7BookManagement.Infrastructure
{
    public static class UiLayoutAudit
    {
        private static readonly Size[] Viewports =
        {
            new Size(1024, 768),
            new Size(1366, 768),
            new Size(1920, 1080)
        };

        public static void AssertCoreForms(ApplicationServices services)
        {
            if (services == null)
                throw new ArgumentNullException("services");

            var factories = new[]
            {
                new FormFactory("经营概览", () => new DashboardForm(services, delegate { }, delegate { })),
                new FormFactory("图书资料", () => new BookListForm(services)),
                new FormFactory("新增图书", () => new BookEditForm(services, null)),
                new FormFactory("采购入库", () => new PurchaseForm(services)),
                new FormFactory("销售开单", () => new SalesForm(services)),
                new FormFactory("单据中心", () => new DocumentCenterForm(services)),
                new FormFactory("库存管理", () => new InventoryForm(services)),
                new FormFactory("供应商", () => new SupplierForm(services)),
                new FormFactory("报表", () => new ReportsForm(services)),
                new FormFactory("备份", () => new BackupForm(services)),
                new FormFactory("帮助", () => new HelpForm(services, delegate { }, delegate { })),
                new FormFactory("设置", () => new SettingsForm(services))
            };

            foreach (var factory in factories)
            {
                foreach (var viewport in Viewports)
                {
                    using (var form = factory.Create())
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = Point.Empty;
                        form.ClientSize = viewport;
                        form.CreateControl();
                        UiTheme.Apply(form);
                        form.PerformLayout();

                        var issues = Audit(form);
                        if (issues.Count > 0)
                        {
                            throw new InvalidOperationException(
                                factory.Name + " 在 " + viewport.Width + "x" + viewport.Height +
                                " 布局自检失败：" + string.Join("；", issues.Take(5).ToArray()));
                        }
                    }
                }
            }
        }

        public static IList<string> Audit(Control root)
        {
            var issues = new List<string>();
            if (root == null)
                return issues;

            AuditRecursive(root, root.GetType().Name, issues);
            return issues;
        }

        private static void AuditRecursive(Control control, string path, IList<string> issues)
        {
            if (control == null || issues.Count >= 20)
                return;

            if (control.Width > 0 && control.Height > 0)
                AuditControl(control, path, issues);

            foreach (Control child in control.Controls)
            {
                var name = string.IsNullOrWhiteSpace(child.Name)
                    ? child.GetType().Name
                    : child.Name;
                AuditRecursive(child, path + "/" + name, issues);
            }
        }

        private static void AuditControl(Control control, string path, IList<string> issues)
        {
            var label = control as Label;
            if (label != null && !label.AutoSize && !string.IsNullOrEmpty(label.Text))
            {
                var required = MeasureLine(label.Font) + label.Padding.Vertical + 2;
                if (label.ClientSize.Height < required)
                    issues.Add(path + " 标签高度不足 " + label.ClientSize.Height + "<" + required);
            }

            var button = control as Button;
            if (button != null && !string.IsNullOrEmpty(button.Text))
            {
                var required = MeasureLine(button.Font) + 12;
                if (button.ClientSize.Height < required)
                    issues.Add(path + " 按钮文字可能裁切 " + button.ClientSize.Height + "<" + required);
            }

            var checkBox = control as CheckBox;
            if (checkBox != null && !string.IsNullOrEmpty(checkBox.Text))
            {
                var required = MeasureLine(checkBox.Font) + 6;
                if (checkBox.ClientSize.Height < required)
                    issues.Add(path + " 复选框文字可能裁切 " + checkBox.ClientSize.Height + "<" + required);
            }

            var textBox = control as TextBox;
            if (textBox != null && !textBox.Multiline)
            {
                var required = Math.Max(textBox.PreferredHeight, textBox.Font.Height + 8);
                if (textBox.ClientSize.Height + 4 < required)
                    issues.Add(path + " 输入框高度不足 " + textBox.ClientSize.Height + "<" + required);
            }

            var combo = control as ComboBox;
            if (combo != null)
            {
                var required = combo.PreferredHeight;
                if (combo.ClientSize.Height + 4 < required)
                    issues.Add(path + " 下拉框高度不足 " + combo.ClientSize.Height + "<" + required);
            }

            var numeric = control as NumericUpDown;
            if (numeric != null)
            {
                var required = numeric.PreferredHeight;
                if (numeric.ClientSize.Height + 4 < required)
                    issues.Add(path + " 数字输入框高度不足 " + numeric.ClientSize.Height + "<" + required);
            }

            var date = control as DateTimePicker;
            if (date != null)
            {
                var required = date.PreferredHeight;
                if (date.ClientSize.Height + 4 < required)
                    issues.Add(path + " 日期输入框高度不足 " + date.ClientSize.Height + "<" + required);
            }

            var grid = control as DataGridView;
            if (grid != null)
            {
                var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
                var headerRequired = MeasureLine(headerFont) + 12;
                if (grid.ColumnHeadersHeight < headerRequired)
                    issues.Add(path + " 表头高度不足 " + grid.ColumnHeadersHeight + "<" + headerRequired);

                var rowRequired = MeasureLine(grid.Font) + 10;
                if (grid.RowTemplate.Height < rowRequired)
                    issues.Add(path + " 表格行高不足 " + grid.RowTemplate.Height + "<" + rowRequired);
            }
        }

        private static int MeasureLine(Font font)
        {
            if (font == null)
                return 18;

            return TextRenderer.MeasureText(
                "国Ag",
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding).Height;
        }

        private sealed class FormFactory
        {
            public FormFactory(string name, Func<Form> create)
            {
                Name = name;
                Create = create;
            }

            public string Name { get; private set; }
            public Func<Form> Create { get; private set; }
        }
    }
}
