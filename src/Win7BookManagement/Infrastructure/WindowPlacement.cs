using System;
using System.Drawing;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// Keeps the application shell inside the working area of the monitor it
    /// opens on.
    ///
    /// Windows 7 bookstores still run 1024x768 and 1280x1024 panels, and a
    /// 1366x768 laptop at 125% or a 1080p screen at 150% leaves less usable
    /// space than the historical 1600x900 shell. The preferred size therefore
    /// must never become a minimum that cannot fit on screen: the minimum is
    /// clamped to the working area before it is applied.
    ///
    /// The rectangle math is intentionally pure so --self-test can verify the
    /// small-screen behaviour on any build machine, including GitHub Actions
    /// runners that do not have a 1024x768 monitor attached.
    /// </summary>
    public static class WindowPlacement
    {
        /// <summary>
        /// Minimum size reduced until it fits inside the working area. A
        /// positive size is always returned so WinForms never receives an
        /// empty minimum.
        /// </summary>
        public static Size ClampMinimum(Size workingArea, Size minimumSize)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0)
                throw new ArgumentOutOfRangeException("workingArea", "工作区尺寸必须大于零。");

            return new Size(
                Math.Max(1, Math.Min(minimumSize.Width, workingArea.Width)),
                Math.Max(1, Math.Min(minimumSize.Height, workingArea.Height)));
        }

        /// <summary>
        /// Window bounds for the given working area: the preferred size is
        /// used when it fits, otherwise the window is reduced to the area and
        /// centred on it. Coordinates honour non-zero / negative monitor
        /// origins (secondary displays to the left of the primary one).
        /// </summary>
        public static Rectangle FitBounds(Rectangle workingArea, Size preferredSize, Size minimumSize)
        {
            if (workingArea.Width <= 0 || workingArea.Height <= 0)
                throw new ArgumentOutOfRangeException("workingArea", "工作区尺寸必须大于零。");

            var requestedWidth = Math.Max(1, Math.Max(preferredSize.Width, minimumSize.Width));
            var requestedHeight = Math.Max(1, Math.Max(preferredSize.Height, minimumSize.Height));

            var width = Math.Min(requestedWidth, workingArea.Width);
            var height = Math.Min(requestedHeight, workingArea.Height);
            var x = workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2);
            var y = workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2);

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Applies the preferred size on the monitor under the mouse pointer,
        /// clamped to that monitor's working area. Called before the shell is
        /// shown so the first frame already fits (Win7 small screens must not
        /// open with the bottom action bar outside the display).
        /// </summary>
        public static void Apply(Form form, Size preferredSize, Size minimumSize)
        {
            if (form == null)
                return;

            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            var minimum = ClampMinimum(area.Size, minimumSize);

            form.StartPosition = FormStartPosition.Manual;
            form.MinimumSize = minimum;
            form.Bounds = FitBounds(area, preferredSize, minimum);
        }

        /// <summary>
        /// Re-clamps an already visible window after DPI changes, monitor
        /// changes or when the shell was restored larger than the current
        /// working area. The window is also pulled back on screen if it was
        /// moved partly outside the display.
        /// </summary>
        public static void ClampToWorkingArea(Form form)
        {
            if (form == null || form.WindowState != FormWindowState.Normal)
                return;

            var area = Screen.FromControl(form).WorkingArea;
            var minimum = ClampMinimum(area.Size, form.MinimumSize);
            if (form.MinimumSize != minimum)
                form.MinimumSize = minimum;

            var width = Math.Min(form.Width, area.Width);
            var height = Math.Min(form.Height, area.Height);
            width = Math.Max(minimum.Width, width);
            height = Math.Max(minimum.Height, height);

            var x = Math.Min(Math.Max(form.Left, area.Left), Math.Max(area.Left, area.Right - width));
            var y = Math.Min(Math.Max(form.Top, area.Top), Math.Max(area.Top, area.Bottom - height));

            var target = new Rectangle(x, y, width, height);
            if (form.Bounds != target)
                form.Bounds = target;
        }
    }
}