using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class InventoryCheckItem
    {
        public long Id { get; set; }

        public long InventoryCheckId { get; set; }

        public long ProductId { get; set; }

        public string ProductNameSnapshot { get; set; }

        public string CategoryName { get; set; }

        public decimal SystemStock { get; set; }

        public decimal ActualStock { get; set; }

        public decimal DifferenceQuantity { get; set; }

        public decimal CostPriceSnapshot { get; set; }

        public decimal DifferenceAmount { get; set; }

        public string Reason { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
