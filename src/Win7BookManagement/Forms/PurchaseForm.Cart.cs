using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed partial class PurchaseForm
    {
internal sealed class PurchaseCartRow
        {
            public int Index { get; set; }
            public long BookId { get; set; }
            public string SelfCode { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public string Publisher { get; set; }
            public string ShelfCode { get; set; }
            public int CurrentStock { get; set; }
            public int Quantity { get; set; }
            public decimal BaseUnitCostYuan { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal OrderDiscountPercent { get; set; }

            public decimal DiscountedUnitCostYuan
            {
                get
                {
                    var baseCent = Money.FromYuan(BaseUnitCostYuan);
                    return Money.ToYuan(
                        ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent)));
                }
            }

            public decimal LineTotalYuan
            {
                get
                {
                    var baseCent = Money.FromYuan(BaseUnitCostYuan);
                    var lineCent = ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent));
                    var finalCent = ApplyBasisPoints(lineCent, PercentToBasisPoints(OrderDiscountPercent));
                    return Money.ToYuan(checked((long)Quantity * finalCent));
                }
            }
        }
    }
}
