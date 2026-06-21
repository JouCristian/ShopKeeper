using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class ExpiringProductReportItem
    {
        public string ProductName { get; set; }

        public string BatchCode { get; set; }

        public decimal QuantityRemaining { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int DaysRemaining { get; set; }

        public string StatusText
        {
            get { return DaysRemaining < 0 ? "已过期" : "临期"; }
        }
    }
}
