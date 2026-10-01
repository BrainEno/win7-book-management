using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    public interface IUiSpecPage
    {
        void ApplyUiSpecProfile(UiSpecProfile profile);
    }

    /// <summary>
    /// Machine-readable layout metrics derived from docs/ui-spec.json.
    ///
    /// The spec uses logical pixels at 96 DPI. WinForms forms remain
    /// AutoScaleMode.Dpi, so these values describe layout density rather than
    /// a custom whole-window scale factor.
    /// </summary>
    public sealed class UiSpecProfile
    {
        public string Name { get; private set; }
        public int SidebarWidth { get; private set; }
        public int PageHeaderHeight { get; private set; }
        public int ContentPaddingX { get; private set; }
        public int ContentPaddingY { get; private set; }
        public int SectionGap { get; private set; }
        public int ControlHeight { get; private set; }
        public int ControlGap { get; private set; }
        public int ButtonHorizontalPadding { get; private set; }
        public int TableHeaderHeight { get; private set; }
        public int TableRowHeight { get; private set; }
        public int ToolbarPadding { get; private set; }
        public int MetricHeight { get; private set; }
        public float PageTitleFontPoints { get; private set; }
        public float BodyFontPoints { get; private set; }
        public float TableFontPoints { get; private set; }
        public float SecondaryFontPoints { get; private set; }

        public bool IsCompact
        {
            get { return string.Equals(Name, "compact", StringComparison.Ordinal); }
        }

        public UiSpecProfile(
            string name,
            int sidebarWidth,
            int pageHeaderHeight,
            int contentPaddingX,
            int contentPaddingY,
            int sectionGap,
            int controlHeight,
            int controlGap,
            int buttonHorizontalPadding,
            int tableHeaderHeight,
            int tableRowHeight,
            int toolbarPadding,
            int metricHeight,
            int pageTitleFontPx,
            int bodyFontPx,
            int tableFontPx,
            int secondaryFontPx)
        {
            Name = name;
            SidebarWidth = sidebarWidth;
            PageHeaderHeight = pageHeaderHeight;
            ContentPaddingX = contentPaddingX;
            ContentPaddingY = contentPaddingY;
            SectionGap = sectionGap;
            ControlHeight = controlHeight;
            ControlGap = controlGap;
            ButtonHorizontalPadding = buttonHorizontalPadding;
            TableHeaderHeight = tableHeaderHeight;
            TableRowHeight = tableRowHeight;
            ToolbarPadding = toolbarPadding;
            MetricHeight = metricHeight;
            PageTitleFontPoints = BookDeskUiSpec.PixelFontToPoints(pageTitleFontPx);
            BodyFontPoints = BookDeskUiSpec.PixelFontToPoints(bodyFontPx);
            TableFontPoints = BookDeskUiSpec.PixelFontToPoints(tableFontPx);
            SecondaryFontPoints = BookDeskUiSpec.PixelFontToPoints(secondaryFontPx);
        }
    }


    public sealed class UiSpecSectionPanel : TableLayoutPanel
    {
        public UiSpecSectionPanel()
        {
            DoubleBuffered = true;
            BackColor = UiTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var scale = DeviceDpi > 0 ? DeviceDpi / 96F : 1F;
            var radius = Math.Max(2F, 8F * scale);
            var borderWidth = Math.Max(1F, scale);
            var inset = borderWidth / 2F;
            var rect = new RectangleF(
                inset,
                inset,
                Math.Max(1F, ClientSize.Width - borderWidth - 1F),
                Math.Max(1F, ClientSize.Height - borderWidth - 1F));

            using (var path = CreateRoundedRectangle(rect, radius))
            using (var pen = new Pen(UiTheme.Border, borderWidth))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var diameter = radius * 2F;
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180F, 90F);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270F, 90F);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0F, 90F);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }
    }

    public static class BookDeskUiSpec
    {
        public const int ReferenceRasterWidth = 1586;
        public const int ReferenceRasterHeight = 992;

        public const int MinimumClientWidth = 1280;
        public const int MinimumClientHeight = 720;

        public const int ShellStatusHeight = 28;
        public const int SidebarFooterHeight = 72;
        public const int OfflineBadgeWidth = 148;
        public const int OfflineBadgeHeight = 36;
        public const int PageTitleIconSize = 22;

        public const int BookToolbarStandardHeight = 138;
        public const int BookToolbarCompactHeight = 116;
        public const int BookSearchLabelWidth = 56;
        public const int BookSearchMinimumWidth = 360;

        public const int PurchaseToolbarStandardHeight = 148;
        public const int PurchaseToolbarCompactHeight = 126;
        public const int PurchaseSupplierLabelWidth = 72;
        public const int PurchaseSupplierStandardWidth = 304;
        public const int PurchaseSupplierCompactWidth = 240;
        public const int PurchaseScanLabelWidth = 94;
        public const int PurchaseSearchMinimumWidth = 400;
        public const int PurchaseSearchCompactMinimumWidth = 360;
        public const int PurchaseCartHeaderHeight = 52;
        public const int PurchaseNoteStandardHeight = 48;
        public const int PurchaseNoteCompactHeight = 42;
        public const int PurchaseSummaryStandardHeight = 72;
        public const int PurchaseSummaryCompactHeight = 62;
        public const int PurchaseConfirmWidth = 146;
        public const int PurchaseConfirmHeight = 40;

        public const int InventoryToolbarStandardHeight = 136;
        public const int InventoryToolbarCompactHeight = 112;
        public const int InventorySearchLabelWidth = 80;
        public const int InventorySearchMinimumWidth = 380;
        public const int InventoryQueryWidth = 72;
        public const int InventoryAdjustWidth = 118;
        public const int InventoryLowOnlyWidth = 120;
        public const int InventoryDetailStandardWidth = 330;
        public const int InventoryDetailCompactWidth = 286;
        public const int InventoryDetailMaxWidth = 360;
        public const int InventorySplitStackThreshold = 1020;
        public const int InventoryDetailRowHeight = 64;
        public const int InventoryDetailLabelWidth = 88;

        public static readonly UiSpecProfile Compact = new UiSpecProfile(
            "compact",
            208,
            56,
            10,
            8,
            8,
            34,
            6,
            10,
            38,
            42,
            10,
            30,
            18,
            13,
            12,
            11);

        public static readonly UiSpecProfile Standard = new UiSpecProfile(
            "standard",
            228,
            64,
            12,
            12,
            12,
            36,
            8,
            14,
            42,
            46,
            14,
            34,
            20,
            14,
            13,
            12);

        public static readonly UiSpecProfile Expanded = new UiSpecProfile(
            "expanded",
            232,
            64,
            16,
            14,
            12,
            36,
            8,
            14,
            42,
            46,
            14,
            34,
            20,
            14,
            13,
            12);

        public static UiSpecProfile Resolve(int clientWidth, int clientHeight)
        {
            if (clientWidth <= 1440 || clientHeight <= 800)
                return Compact;

            if (clientWidth >= 2200)
                return Expanded;

            return Standard;
        }

        public static float PixelFontToPoints(int pixels)
        {
            return pixels * 72F / 96F;
        }
    }
}
