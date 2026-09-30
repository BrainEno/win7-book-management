using System.Drawing;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// Compatibility boundary between business forms and AntdUI.
    /// Keep third-party details here so Win7/net48 fallback remains controllable.
    /// </summary>
    public static class ModernUi
    {
        public static AntdUI.Input CreateInput()
        {
            return new AntdUI.Input
            {
                Font = UiTheme.Font(9.5F),
                MinimumSize = new Size(0, 42),
                Margin = Padding.Empty,
                TabStop = true
            };
        }

        public static AntdUI.Button CreateButton(string text, bool primary)
        {
            var button = new AntdUI.Button
            {
                Text = text ?? "",
                Font = UiTheme.Font(9F, primary ? FontStyle.Bold : FontStyle.Regular),
                MinimumSize = new Size(92, 40),
                Margin = Padding.Empty,
                TabStop = true
            };

            if (primary)
                button.Type = AntdUI.TTypeMini.Primary;

            return button;
        }

        public static void PolishBusinessGrid(DataGridView grid, bool editable)
        {
            if (grid == null) return;

            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            grid.ColumnHeadersHeight = editable ? 46 : 44;
            grid.RowTemplate.Height = editable ? 46 : 42;
            grid.RowTemplate.Resizable = DataGridViewTriState.False;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.BackgroundColor = UiTheme.Surface;
            grid.GridColor = UiTheme.Border;
            grid.DefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
        }

        public static void PreferTitleFill(DataGridViewColumn titleColumn, int minimumWidth)
        {
            if (titleColumn == null) return;
            titleColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            titleColumn.MinimumWidth = minimumWidth;
            titleColumn.FillWeight = 190F;
            titleColumn.Resizable = DataGridViewTriState.True;
        }

        public static void MakeColumnResizable(DataGridViewColumn column, int minimumWidth)
        {
            if (column == null) return;
            column.MinimumWidth = minimumWidth;
            column.Resizable = DataGridViewTriState.True;
        }
    }
}
