using System;
using System.Globalization;

namespace Win7BookManagement.Infrastructure
{
    public static class Money
    {
        public static long FromYuan(decimal yuan)
        {
            return checked((long)Math.Round(yuan * 100m, 0, MidpointRounding.AwayFromZero));
        }

        public static decimal ToYuan(long cents)
        {
            return cents / 100m;
        }

        public static string Format(long cents)
        {
            return ToYuan(cents).ToString("0.00", CultureInfo.CurrentCulture);
        }
    }
}
