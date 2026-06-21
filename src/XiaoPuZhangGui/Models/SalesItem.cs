using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class SalesItem
    {
        public long Id { get; set; }

        public long SalesOrderId { get; set; }

        public long ProductId { get; set; }

        public string ProductNameSnapshot { get; set; }

        public decimal Quantity { get; set; }

        public decimal SalePriceSnapshot { get; set; }

        public decimal CostPriceSnapshot { get; set; }

        public decimal LineAmount { get; set; }

        public decimal LineCost { get; set; }

        public decimal LineProfit { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
