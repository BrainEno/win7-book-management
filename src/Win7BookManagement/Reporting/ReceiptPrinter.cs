using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;
using Win7BookManagement.Database;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Repositories;

namespace Win7BookManagement.Reporting
{
    public sealed class ReceiptPrinter
    {
        private readonly DatabaseConnectionFactory _factory;
        private readonly SettingsRepository _settings;
        public ReceiptPrinter(DatabaseConnectionFactory factory, SettingsRepository settings) { _factory = factory; _settings = settings; }

        public SalesReceipt Load(string orderNo)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT order_no,sold_at,payment_method,subtotal_cent,line_discount_cent,order_discount_cent,
total_cent,amount_received_cent,change_cent,note FROM sales_orders WHERE order_no=@no;";
                command.Parameters.AddWithValue("@no", orderNo);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("找不到刚结账的销售单，无法打印小票。");
                    var receipt = new SalesReceipt
                    {
                        StoreName = _settings.GetReportStoreName(), OrderNo = reader.GetString(0),
                        SoldAt = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture), PaymentMethod = reader.GetString(2),
                        SubtotalCent = reader.GetInt64(3), DiscountCent = reader.GetInt64(4) + reader.GetInt64(5),
                        TotalCent = reader.GetInt64(6), ReceivedCent = reader.GetInt64(7), ChangeCent = reader.GetInt64(8), Note = reader.GetString(9)
                    };
                    reader.Close();
                    using (var items = connection.CreateCommand())
                    {
                        items.CommandText = @"SELECT i.title_snapshot,i.quantity,i.unit_price_cent,i.line_total_cent FROM sales_order_items i
JOIN sales_orders o ON o.id=i.sales_order_id WHERE o.order_no=@no ORDER BY i.id;";
                        items.Parameters.AddWithValue("@no", orderNo);
                        using (var r = items.ExecuteReader())
                            while (r.Read()) receipt.Lines.Add(new SalesReceiptLine { Title = r.GetString(0), Quantity = r.GetInt32(1), UnitPriceCent = r.GetInt64(2), TotalCent = r.GetInt64(3) });
                    }
                    return receipt;
                }
            }
        }

        public void Print(IWin32Window owner, SalesReceipt receipt)
        {
            var widthMm = _settings.GetInt("receipt_width_mm", 80);
            if (widthMm != 58 && widthMm != 80) widthMm = 80;
            using (var document = new PrintDocument())
            {
                document.DocumentName = "BOOK DESK " + receipt.OrderNo;
                document.DefaultPageSettings.PaperSize = new PaperSize("Thermal" + widthMm, (int)(widthMm / 25.4 * 100), 3000);
                document.DefaultPageSettings.Margins = new Margins(12, 12, 12, 12);
                var index = 0; var firstPage = true;
                document.PrintPage += (sender, e) =>
                {
                    var graphics = e.Graphics; var left = e.MarginBounds.Left; var right = e.MarginBounds.Right; var y = e.MarginBounds.Top;
                    var width = e.MarginBounds.Width;
                    using (var font = UiTheme.Font(8.5F)) using (var bold = UiTheme.Font(9F, FontStyle.Bold))
                    {
                        Func<string, Font, int> draw = (text, f) =>
                        {
                            var size = graphics.MeasureString(text, f, width);
                            graphics.DrawString(text, f, Brushes.Black, new RectangleF(left, y, width, size.Height + 2));
                            y += (int)Math.Ceiling(size.Height) + 3;
                            return y;
                        };
                        if (firstPage)
                        {
                            draw(receipt.StoreName, bold); draw("销售小票", bold);
                            draw("单号：" + receipt.OrderNo, font); draw("时间：" + receipt.SoldAt.ToString("yyyy-MM-dd HH:mm:ss"), font);
                            draw(new string('-', 32), font); firstPage = false;
                        }
                        while (index < receipt.Lines.Count)
                        {
                            if (y + 48 > e.MarginBounds.Bottom) { e.HasMorePages = true; return; }
                            var line = receipt.Lines[index++];
                            draw(line.Title, font);
                            draw("  " + line.Quantity + " x " + Money.ToYuan(line.UnitPriceCent).ToString("0.00") + "  = " + Money.ToYuan(line.TotalCent).ToString("0.00"), font);
                        }
                        draw(new string('-', 32), font);
                        draw("原金额：" + Money.ToYuan(receipt.SubtotalCent).ToString("0.00"), font);
                        draw("优惠：" + Money.ToYuan(receipt.DiscountCent).ToString("0.00"), font);
                        draw("应收：" + Money.ToYuan(receipt.TotalCent).ToString("0.00"), bold);
                        draw("收款方式：" + receipt.PaymentMethod, font);
                        draw("实收：" + Money.ToYuan(receipt.ReceivedCent).ToString("0.00") + "  找零：" + Money.ToYuan(receipt.ChangeCent).ToString("0.00"), font);
                        if (!string.IsNullOrWhiteSpace(receipt.Note)) draw("备注：" + receipt.Note, font);
                        draw("谢谢惠顾，请保留小票。", font);
                        e.HasMorePages = false;
                    }
                };
                using (var dialog = new PrintDialog { Document = document, UseEXDialog = true })
                    if (dialog.ShowDialog(owner) == DialogResult.OK) document.Print();
            }
        }
    }
}
