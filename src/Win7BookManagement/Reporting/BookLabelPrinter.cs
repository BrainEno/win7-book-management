using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;

namespace Win7BookManagement.Reporting
{
    /// <summary>Code 128-B labels for the exact store code; never fabricate an ISBN.</summary>
    public static class BookLabelPrinter
    {
        // ISO/IEC 15417 Code 128 module widths, indexed by symbol value.
        private static readonly string[] Patterns = {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        public const int QuietModules = 10;
        public const int MinimumDotsPerModule = 2;

        // Return widths with bars first; quiet zones are handled separately.
        public static int[] EncodeStoreCode(string code)
        {
            if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("店内编码为空，请先编辑图书资料。");
            if (code.Length > 100) throw new InvalidOperationException("店内编码过长，无法打印标签。");
            var symbols = new List<int> { 104 }; // Start B
            var checksum = 104;
            for (var i = 0; i < code.Length; i++)
            {
                var ch = code[i];
                if (ch < 32 || ch > 126)
                    throw new InvalidOperationException("店内编码含有不支持的字符。标签仅支持英文、数字和常见半角符号，请先修改店内编码。");
                var value = ch - 32;
                symbols.Add(value);
                checksum += value * (i + 1);
            }
            symbols.Add(checksum % 103);
            symbols.Add(106);
            return symbols.SelectMany(value => Patterns[value].Select(c => c - '0')).ToArray();
        }

        public static int ModuleCount(string code)
        {
            return EncodeStoreCode(code).Sum() + 2 * QuietModules;
        }

        // Width is in hundredths of an inch, printer resolution in dots/inch.
        public static int ModuleDots(string code, float width, int dpi)
        {
            if (width <= 0 || dpi <= 0) return 0;
            return (int)Math.Floor(width * dpi / 100f / ModuleCount(code));
        }

        public static void ValidateFit(Book book, float width, int dpi)
        {
            if (book == null) throw new ArgumentNullException("book");
            if (ModuleDots(book.SelfCode, width, dpi) < MinimumDotsPerModule)
                throw new InvalidOperationException("《" + book.Title + "》的店内编码对当前纸张 / 打印分辨率太长。请换更宽的标签纸或更高分辨率打印机。");
        }

        public static void DrawLabel(Graphics graphics, RectangleF bounds, Book book, int dpi)
        {
            if (bounds.Width <= 0 || bounds.Height < 70) throw new InvalidOperationException("标签纸可打印区域太小，请检查打印机纸张设置。");
            var widths = EncodeStoreCode(book.SelfCode);
            ValidateFit(book, bounds.Width, dpi);
            var dots = ModuleDots(book.SelfCode, bounds.Width, dpi);
            var unit = dots * 100f / dpi;
            var total = (widths.Sum() + QuietModules * 2) * unit;
            var start = bounds.Left + (bounds.Width - total) / 2f;
            // Align edges to whole printer dots; keep ten quiet modules on both sides.
            start = (float)Math.Round(start * dpi / 100f) * 100f / dpi;
            var x = start + QuietModules * unit;
            var barcodeTop = bounds.Top + bounds.Height * 0.38f;
            var barcodeHeight = bounds.Height * 0.40f;
            using (var titleFont = UiTheme.Font(8F, FontStyle.Bold))
            using (var codeFont = UiTheme.Font(7F))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                graphics.DrawString(book.Title ?? "", titleFont, Brushes.Black, new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height * .19f), format);
                graphics.DrawString("售价 ¥" + Money.ToYuan(book.SalePriceCent).ToString("0.00"), codeFont, Brushes.Black, new RectangleF(bounds.Left, bounds.Top + bounds.Height * .19f, bounds.Width, bounds.Height * .18f), format);
                var previous = graphics.SmoothingMode;
                graphics.SmoothingMode = SmoothingMode.None;
                for (var i = 0; i < widths.Length; i++)
                {
                    var w = widths[i] * unit;
                    if (i % 2 == 0) graphics.FillRectangle(Brushes.Black, x, barcodeTop, w, barcodeHeight);
                    x += w;
                }
                graphics.SmoothingMode = previous;
                graphics.DrawString(book.SelfCode, codeFont, Brushes.Black, new RectangleF(bounds.Left, bounds.Top + bounds.Height * .79f, bounds.Width, bounds.Height * .21f), centered);
            }
        }

        public static void Print(IWin32Window owner, IList<Book> books, int widthMm, int heightMm)
        {
            if (books == null || books.Count == 0) throw new InvalidOperationException("请先添加需要打印的图书。");
            if (!((widthMm == 50 && heightMm == 30) || (widthMm == 60 && heightMm == 40)))
                throw new InvalidOperationException("不支持的标签纸规格。");
            // Validate characters for the entire batch before opening the print dialog.
            foreach (var book in books) EncodeStoreCode(book.SelfCode);
            using (var document = new PrintDocument())
            {
                document.DocumentName = "BOOK DESK 图书标签";
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true, AllowSomePages = false, AllowSelection = false })
                {
                    if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                    var page = document.DefaultPageSettings;
                    page.Landscape = false;
                    page.PaperSize = new PaperSize(widthMm + "x" + heightMm + " mm", (int)Math.Round(widthMm * 100 / 25.4), (int)Math.Round(heightMm * 100 / 25.4));
                    page.Margins = new Margins(0, 0, 0, 0);
                    var printable = RectangleF.Intersect(page.PrintableArea, new RectangleF(0, 0, page.PaperSize.Width, page.PaperSize.Height));
                    printable.Inflate(-4, -4);
                    var dpi = page.PrinterResolution.X > 0 ? page.PrinterResolution.X : 203;
                    if (printable.Width <= 0 || printable.Height < 70) throw new InvalidOperationException("打印机的可打印区域太小，请在驱动中选择与标签相同的纸张规格。");
                    foreach (var book in books) ValidateFit(book, printable.Width, dpi);
                    var index = 0;
                    document.BeginPrint += delegate { index = 0; };
                    document.PrintPage += (sender, e) =>
                    {
                        var area = RectangleF.Intersect(e.PageSettings.PrintableArea, new RectangleF(0, 0, e.PageBounds.Width, e.PageBounds.Height));
                        area.Inflate(-4, -4);
                        // Graphics origin is at the physical printer's hard margins.
                        e.Graphics.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
                        DrawLabel(e.Graphics, area, books[index], dpi);
                        index++;
                        e.HasMorePages = index < books.Count;
                    };
                    document.Print();
                }
            }
        }
    }
}
