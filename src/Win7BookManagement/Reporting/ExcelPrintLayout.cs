using System;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Win7BookManagement.Reporting
{
    public static class ExcelPrintLayout
    {
        public static void Apply(
            IWorkbook workbook,
            ISheet sheet,
            string title,
            bool landscape,
            int repeatStartRow,
            int repeatEndRow,
            int lastColumn,
            int lastRow)
        {
            if (workbook == null) throw new ArgumentNullException("workbook");
            if (sheet == null) throw new ArgumentNullException("sheet");

            sheet.FitToPage = true;
            sheet.HorizontallyCenter = true;
            sheet.PrintSetup.Landscape = landscape;
            sheet.PrintSetup.PaperSize = (short)PaperSize.A4;
            sheet.PrintSetup.FitWidth = 1;
            sheet.PrintSetup.FitHeight = 0;

            sheet.SetMargin(MarginType.LeftMargin, 0.28);
            sheet.SetMargin(MarginType.RightMargin, 0.28);
            sheet.SetMargin(MarginType.TopMargin, 0.48);
            sheet.SetMargin(MarginType.BottomMargin, 0.48);
            sheet.SetMargin(MarginType.HeaderMargin, 0.20);
            sheet.SetMargin(MarginType.FooterMargin, 0.20);

            if (repeatStartRow >= 0 && repeatEndRow >= repeatStartRow)
            {
                sheet.RepeatingRows = CellRangeAddress.ValueOf(
                    (repeatStartRow + 1).ToString() + ":" +
                    (repeatEndRow + 1).ToString());
            }

            workbook.SetPrintArea(
                workbook.GetSheetIndex(sheet),
                0,
                Math.Max(0, lastColumn),
                0,
                Math.Max(0, lastRow));

            sheet.Header.Center = string.IsNullOrWhiteSpace(title)
                ? "BOOK DESK"
                : title.Trim();
            sheet.Footer.Left = "BOOK DESK";
            sheet.Footer.Right = "第 &P 页 / 共 &N 页";
        }
    }
}
