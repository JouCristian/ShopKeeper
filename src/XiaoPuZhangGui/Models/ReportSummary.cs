using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class ReportSummary
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public decimal SalesReceivable { get; set; }

        public decimal SalesPaid { get; set; }

        public decimal NewCredit { get; set; }

        public decimal CreditCollected { get; set; }

        public decimal OutstandingCredit { get; set; }

        public decimal ProductCost { get; set; }

        public decimal GrossProfit { get; set; }

        public decimal ScrapLoss { get; set; }

        public decimal NetProfit { get; set; }

        public int SalesOrderCount { get; set; }

        public decimal SoldQuantity { get; set; }

        public decimal PurchaseTotal { get; set; }
    }
}
