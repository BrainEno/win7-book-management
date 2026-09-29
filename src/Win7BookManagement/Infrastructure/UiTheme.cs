using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    public static class UiTheme
    {
        // Warm neutral palette. Keep the implementation on stock WinForms/GDI so
        // Windows 7 SP1 remains a first-class runtime target.
        public static readonly Color Background = Color.FromArgb(248, 246, 241);
        public static readonly Color Surface = Color.FromArgb(255, 255, 253);
        public static readonly Color SurfaceMuted = Color.FromArgb(250, 248, 244);

        public static readonly Color NavigationSurface = Color.FromArgb(247, 242, 232);
        public static readonly Color NavigationHover = Color.FromArgb(241, 235, 223);
        public static readonly Color NavigationSelected = Color.FromArgb(235, 226, 209);
        public static readonly Color NavigationText = Color.FromArgb(49, 60, 55);

        // Legacy aliases kept for pages that have not yet migrated to the light
        // navigation shell. They intentionally point to the same warm system.
        public static readonly Color Sidebar = NavigationSurface;
        public static readonly Color SidebarHover = NavigationHover;

        public static readonly Color Accent = Color.FromArgb(31, 116, 96);
        public static readonly Color AccentHover = Color.FromArgb(24, 96, 79);
        public static readonly Color AccentSoft = Color.FromArgb(231, 242, 238);
        public static readonly Color TextPrimary = Color.FromArgb(42, 47, 44);
        public static readonly Color TextSecondary = Color.FromArgb(104, 111, 106);
        public static readonly Color Border = Color.FromArgb(225, 220, 210);
        public static readonly Color Success = Color.FromArgb(29, 132, 88);
        public static readonly Color Warning = Color.FromArgb(184, 111, 31);
        public static readonly Color Danger = Color.FromArgb(190, 54, 54);

        // These are logical 96-DPI minimums. WinForms scales them together with
        // the Form because every primary Form uses AutoScaleMode.Dpi.
        public const int InputHeight = 32;
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
            if (root == null)
                return;

            var form = root as Form;
            if (form != null)
                ConfigureForm(form);

            ApplyRecursive(root);
            NormalizeLayout(root);
        }

        public static void ConfigureForm(Form form)
        {
            if (form == null)
                return;

            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.Font = Font(9F);
            form.ForeColor = TextPrimary;

            if (form.BackColor == SystemColors.Control)
                form.BackColor = Background;
        }

        public static FlowLayoutPanel CreateResponsiveToolbar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 54),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Surface,
                Padding = new Padding(12, 8, 12, 8),
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
            if (control == null)
                return;

            var textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Surface;
                textBox.ForeColor = TextPrimary;

                if (!textBox.Multiline)
                {
                    // Native WinForms single-line TextBox does not vertically
                    // center correctly when forced to an arbitrary tall height.
                    // Let the native control choose its text height, then reserve
                    // enough layout room around it through MinimumSize / row metrics.
                    textBox.AutoSize = true;
                    var minimum = Math.Max(InputHeight, textBox.Font.Height + 10);
                    textBox.MinimumSize = new Size(textBox.MinimumSize.Width, minimum);
                }
                return;
            }

            var numeric = control as NumericUpDown;
            if (numeric != null)
            {
                numeric.BorderStyle = BorderStyle.FixedSingle;
                numeric.BackColor = Surface;
                numeric.ForeColor = TextPrimary;
                numeric.MinimumSize = new Size(
                    numeric.MinimumSize.Width,
                    Math.Max(InputHeight, numeric.Font.Height + 10));
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
                combo.MinimumSize = new Size(
                    combo.MinimumSize.Width,
                    Math.Max(InputHeight, combo.Font.Height + 10));
                return;
            }

            var date = control as DateTimePicker;
            if (date != null)
            {
                date.CalendarForeColor = TextPrimary;
                date.CalendarMonthBackground = Surface;
                date.MinimumSize = new Size(
                    date.MinimumSize.Width,
                    Math.Max(InputHeight, date.Font.Height + 10));
            }
        }

        public static void StyleGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;

            var bodyHeight = Math.Max(38, grid.Font.Height + 18);
            var headerHeight = Math.Max(40, grid.ColumnHeadersDefaultCellStyle.Font == null
                ? grid.Font.Height + 20
                : grid.ColumnHeadersDefaultCellStyle.Font.Height + 20);

            grid.RowTemplate.Height = bodyHeight;
            grid.ColumnHeadersHeight = headerHeight;
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
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Cursor = Cursors.Hand;
            button.Font = Font(8.8F, FontStyle.Bold);

            var minimumHeight = Math.Max(ButtonHeight, button.Font.Height + 16);
            button.MinimumSize = new Size(button.MinimumSize.Width, minimumHeight);
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
            if (form == null)
                return;

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
                        StyleButton(
                            button,
                            string.Equals(role, "primary", StringComparison.OrdinalIgnoreCase) ||
                            IsPrimaryAction(button.Text));
                    }
                }

                var grid = control as DataGridView;
                if (grid != null)
                    StyleGrid(grid);

                PrepareInput(control);

                var label = control as Label;
                if (label != null)
                {
                    if (label.ForeColor == SystemColors.ControlText)
                        label.ForeColor = TextPrimary;

                    // Avoid cropped YaHei/CJK glyphs in fixed-height labels.
                    var minimumTextHeight = MeasureSingleLineHeight(label.Font) + label.Padding.Vertical + 4;
                    if (!label.AutoSize)
                    {
                        label.MinimumSize = new Size(label.MinimumSize.Width, minimumTextHeight);
                        if ((label.Dock == DockStyle.Top || label.Dock == DockStyle.Bottom) &&
                            label.Height < minimumTextHeight)
                        {
                            label.Height = minimumTextHeight;
                        }
                    }
                }

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    if (checkBox.ForeColor == SystemColors.ControlText)
                        checkBox.ForeColor = TextSecondary;

                    checkBox.MinimumSize = new Size(
                        checkBox.MinimumSize.Width,
                        Math.Max(checkBox.Font.Height + 8, 26));
                }

                ApplyRecursive(control);
            }
        }

        private static void NormalizeLayout(Control root)
        {
            NormalizeControl(root);

            foreach (Control child in root.Controls)
                NormalizeLayout(child);

            var table = root as TableLayoutPanel;
            if (table != null)
                NormalizeTableRows(table);
        }

        private static void NormalizeControl(Control control)
        {
            var label = control as Label;
            if (label != null && !label.AutoSize)
            {
                var minimum = MeasureSingleLineHeight(label.Font) + label.Padding.Vertical + 4;
                if (label.Height < minimum &&
                    !(label.Parent is TableLayoutPanel))
                {
                    label.Height = minimum;
                }
            }

            var button = control as Button;
            if (button != null)
            {
                var minimum = Math.Max(ButtonHeight, button.Font.Height + 16);
                if (button.Dock != DockStyle.Fill && button.Height < minimum)
                    button.Height = minimum;
            }
        }

        private static void NormalizeTableRows(TableLayoutPanel table)
        {
            if (table.RowCount <= 0)
                return;

            for (var row = 0; row < table.RowCount; row++)
            {
                var controls = table.Controls
                    .Cast<Control>()
                    .Where(control => table.GetRow(control) == row && table.GetRowSpan(control) == 1)
                    .ToArray();

                if (controls.Length == 0)
                    continue;

                var style = table.RowStyles.Count > row ? table.RowStyles[row] : null;
                if (style == null)
                    continue;

                var labelOnly = controls.All(control => control is Label);
                if (labelOnly &&
                    style.SizeType == SizeType.Absolute &&
                    style.Height <= 36F)
                {
                    // Text rows should grow with the actual CJK font metrics.
                    style.SizeType = SizeType.AutoSize;
                    continue;
                }

                if (style.SizeType != SizeType.Absolute || style.Height > 120F)
                    continue;

                var required = 0;
                foreach (var control in controls)
                    required = Math.Max(required, RequiredOuterHeight(control));

                if (required > 0 && style.Height < required)
                    style.Height = required;
            }
        }

        private static int RequiredOuterHeight(Control control)
        {
            if (control == null || !control.Visible)
                return 0;

            var margin = control.Margin.Vertical;

            var label = control as Label;
            if (label != null)
            {
                if (label.AutoSize)
                    return label.PreferredSize.Height + margin;

                return MeasureSingleLineHeight(label.Font) +
                       label.Padding.Vertical + 4 + margin;
            }

            var button = control as Button;
            if (button != null)
                return Math.Max(ButtonHeight, button.Font.Height + 16) + margin;

            var textBox = control as TextBox;
            if (textBox != null && !textBox.Multiline)
                return Math.Max(InputHeight, textBox.PreferredHeight) + margin;

            if (control is ComboBox ||
                control is NumericUpDown ||
                control is DateTimePicker)
            {
                return Math.Max(InputHeight, control.PreferredSize.Height) + margin;
            }

            var checkBox = control as CheckBox;
            if (checkBox != null)
                return Math.Max(26, checkBox.Font.Height + 8) + margin;

            if (control.AutoSize)
                return control.PreferredSize.Height + margin;

            if (control.MinimumSize.Height > 0)
                return control.MinimumSize.Height + margin;

            return 0;
        }

        private static int MeasureSingleLineHeight(System.Drawing.Font font)
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

        private static bool IsPrimaryAction(string text)
        {
            var value = (text ?? "").Trim();
            return value == "保存" ||
                   value == "保存资料" ||
                   value == "新增图书" ||
                   value == "＋ 新增图书" ||
                   value == "新增供应商" ||
                   value == "＋ 新增供应商" ||
                   value == "确认入库" ||
                   value == "结账" ||
                   value == "确认结账" ||
                   value == "确认退货" ||
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

            var installed = FontFamily.Families.Select(family => family.Name).ToArray();
            foreach (var candidate in preferred)
            {
                if (installed.Any(name =>
                    string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }
    }
}
