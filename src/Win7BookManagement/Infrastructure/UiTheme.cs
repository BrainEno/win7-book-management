using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    public static class UiTheme
    {
        // Warm neutral palette inspired by the Flutter edition, while keeping a
        // conservative WinForms/GDI implementation that remains safe on Win7.
        public static readonly Color Background = Color.FromArgb(248, 246, 241);
        public static readonly Color Surface = Color.FromArgb(255, 255, 253);
        public static readonly Color SurfaceMuted = Color.FromArgb(250, 248, 244);
        public static readonly Color Sidebar = Color.FromArgb(27, 39, 37);
        public static readonly Color SidebarHover = Color.FromArgb(42, 57, 53);
        public static readonly Color Accent = Color.FromArgb(31, 116, 96);
        public static readonly Color AccentHover = Color.FromArgb(24, 96, 79);
        public static readonly Color AccentSoft = Color.FromArgb(231, 242, 238);
        public static readonly Color TextPrimary = Color.FromArgb(42, 47, 44);
        public static readonly Color TextSecondary = Color.FromArgb(104, 111, 106);
        public static readonly Color Border = Color.FromArgb(225, 220, 210);
        public static readonly Color Success = Color.FromArgb(29, 132, 88);
        public static readonly Color Warning = Color.FromArgb(184, 111, 31);
        public static readonly Color Danger = Color.FromArgb(190, 54, 54);

        public const int InputHeight = 38;
        public const int ButtonHeight = 38;
        public const int CompactBreakpoint = 980;
        public const int WideBreakpoint = 1180;

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
                ConfigureForm(form);

            ApplyRecursive(root);
        }

        public static void ConfigureForm(Form form)
        {
            if (form == null) return;

            if (form.AutoScaleMode != AutoScaleMode.Dpi)
            {
                form.AutoScaleDimensions = new SizeF(96F, 96F);
                form.AutoScaleMode = AutoScaleMode.Dpi;
            }

            form.Font = Font(9F);
            form.ForeColor = TextPrimary;
            if (form.BackColor == SystemColors.Control)
                form.BackColor = Background;
        }

        public static FlowLayoutPanel CreateResponsiveToolbar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 52),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Surface,
                Padding = new Padding(12, 7, 12, 7),
                Margin = Padding.Empty
            };
        }

        public static Panel CreateSection(int padding)
        {
            return new Panel
            {
                BackColor = Surface,
                Padding = new Padding(padding),
                Margin = Padding.Empty,
                BorderStyle = BorderStyle.FixedSingle
            };
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

        public static void PrepareInput(Control control)
        {
            if (control == null) return;

            var textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Surface;
                textBox.ForeColor = TextPrimary;
                if (!textBox.Multiline)
                {
                    textBox.AutoSize = false;
                    textBox.Height = Math.Max(textBox.Height, InputHeight);
                }
                return;
            }

            var numeric = control as NumericUpDown;
            if (numeric != null)
            {
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Surface;
                numeric.ForeColor = TextPrimary;
                numeric.Height = Math.Max(numeric.Height, InputHeight);
                return;
            }

            var combo = control as ComboBox;
            if (combo != null)
            {
                combo.BackColor = Surface;
                combo.ForeColor = TextPrimary;
                combo.FlatStyle = FlatStyle.Flat;
                combo.IntegralHeight = false;
                combo.DropDownHeight = 240;
            }
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
            grid.RowTemplate.Height = 38;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceMuted;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Font = Font(8.8F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceMuted;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = AccentSoft;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 251, 248);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Resizable = DataGridViewTriState.False;
        }

        public static void StyleButton(Button button, bool primary)
        {
            if (button == null) return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Cursor = Cursors.Hand;
            button.Font = Font(8.8F, FontStyle.Bold);
            button.MinimumSize = new Size(0, ButtonHeight);
            button.Padding = new Padding(10, 0, 10, 0);

            if (primary)
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Accent;
                button.FlatAppearance.MouseOverBackColor = AccentHover;
                button.FlatAppearance.MouseDownBackColor = AccentHover;
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = TextPrimary;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = SurfaceMuted;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(242, 239, 233);
            }
        }

        public static void FitDialogToWorkingArea(Form form, int margin)
        {
            if (form == null) return;

            var area = Screen.FromControl(form).WorkingArea;
            var maxWidth = Math.Max(640, area.Width - margin * 2);
            var maxHeight = Math.Max(500, area.Height - margin * 2);

            if (form.Width > maxWidth)
                form.Width = maxWidth;
            if (form.Height > maxHeight)
                form.Height = maxHeight;
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
                if (grid != null)
                    StyleGrid(grid);

                PrepareInput(control);

                var label = control as Label;
                if (label != null && label.ForeColor == SystemColors.ControlText)
                    label.ForeColor = TextPrimary;

                var date = control as DateTimePicker;
                if (date != null)
                {
                    date.CalendarForeColor = TextPrimary;
                    date.CalendarMonthBackground = Surface;
                }

                var checkBox = control as CheckBox;
                if (checkBox != null && checkBox.ForeColor == SystemColors.ControlText)
                    checkBox.ForeColor = TextSecondary;

                ApplyRecursive(control);
            }
        }

        private static bool IsPrimaryAction(string text)
        {
            var value = (text ?? "").Trim();
            return value == "保存" ||
                   value == "保存资料" ||
                   value == "新增图书" ||
                   value == "新增供应商" ||
                   value == "确认入库" ||
                   value == "结账" ||
                   value == "确认结账" ||
                   value == "导出 Excel" ||
                   value == "确认";
        }

        private static string ResolveFontFamily()
        {
            var preferred = new[]
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "Segoe UI",
                "SimSun"
            };
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
