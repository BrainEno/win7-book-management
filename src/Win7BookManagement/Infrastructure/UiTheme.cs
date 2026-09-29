using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    public static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(244, 246, 249);
        public static readonly Color Surface = Color.White;
        public static readonly Color Sidebar = Color.FromArgb(24, 34, 49);
        public static readonly Color SidebarHover = Color.FromArgb(37, 51, 72);
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentSoft = Color.FromArgb(232, 240, 254);
        public static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
        public static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
        public static readonly Color Border = Color.FromArgb(221, 226, 234);
        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Warning = Color.FromArgb(217, 119, 6);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);

        private static readonly string FontFamilyName = ResolveFontFamily();

        public static Font Font(float size)
        {
            return Font(size, FontStyle.Regular);
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font(FontFamilyName, size, style, GraphicsUnit.Point);
        }

        public static void Apply(Control root)
        {
            if (root == null) return;

            var form = root as Form;
            if (form != null)
            {
                form.Font = Font(9F);
                if (form.BackColor == SystemColors.Control)
                    form.BackColor = Background;
                form.ForeColor = TextPrimary;
            }

            ApplyRecursive(root);
        }

        public static void StyleGrid(DataGridView grid)
        {
            if (grid == null) return;

            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 36;
            grid.ColumnHeadersHeight = 38;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(247, 248, 250);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Font = Font(9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(247, 248, 250);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 253);
        }

        public static void StyleButton(Button button, bool primary)
        {
            if (button == null) return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Cursor = Cursors.Hand;
            button.Font = Font(9F, FontStyle.Bold);

            if (primary)
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Accent;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = TextPrimary;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(247, 248, 250);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(239, 242, 246);
            }
        }

        public static Panel CreateCard()
        {
            return new Panel
            {
                BackColor = Surface,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 14, 14),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static void ApplyRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                var button = control as Button;
                if (button != null)
                {
                    var role = Convert.ToString(button.Tag);
                    if (string.Equals(role, "nav", StringComparison.OrdinalIgnoreCase))
                    {
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderSize = 0;
                        button.Cursor = Cursors.Hand;
                    }
                    else
                    {
                        StyleButton(button,
                            string.Equals(role, "primary", StringComparison.OrdinalIgnoreCase) ||
                            IsPrimaryAction(button.Text));
                    }
                }

                var grid = control as DataGridView;
                if (grid != null) StyleGrid(grid);

                var textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.BackColor = Surface;
                    textBox.ForeColor = TextPrimary;
                }

                var combo = control as ComboBox;
                if (combo != null)
                {
                    combo.BackColor = Surface;
                    combo.ForeColor = TextPrimary;
                    combo.FlatStyle = FlatStyle.Flat;
                }

                var numeric = control as NumericUpDown;
                if (numeric != null)
                {
                    numeric.BackColor = Surface;
                    numeric.ForeColor = TextPrimary;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                }

                var label = control as Label;
                if (label != null && label.ForeColor == SystemColors.ControlText)
                    label.ForeColor = TextPrimary;

                var checkBox = control as CheckBox;
                if (checkBox != null)
                    checkBox.ForeColor = TextSecondary;

                ApplyRecursive(control);
            }
        }

        private static bool IsPrimaryAction(string text)
        {
            var value = (text ?? "").Trim();
            return value == "保存" ||
                   value == "新增图书" ||
                   value == "新增供应商" ||
                   value == "确认入库" ||
                   value == "结账" ||
                   value == "导出 Excel" ||
                   value == "确认";
        }

        private static string ResolveFontFamily()
        {
            var preferred = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            var installed = FontFamily.Families.Select(f => f.Name).ToArray();

            foreach (var candidate in preferred)
            {
                if (installed.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }

            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }
    }
}
