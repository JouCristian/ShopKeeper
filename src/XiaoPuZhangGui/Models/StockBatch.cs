using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class StockBatch
    {
        public long Id { get; set; }

        public long ProductId { get; set; }

        public string BatchCode { get; set; }

        public string SourceType { get; set; }

        public long SourceId { get; set; }

        public decimal QuantityIn { get; set; }

        public decimal QuantityRemaining { get; set; }

        public decimal PurchasePrice { get; set; }

        public DateTime? ProductionDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
