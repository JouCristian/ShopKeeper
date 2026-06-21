using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class PurchaseItem
    {
        public long Id { get; set; }

        public long PurchaseRecordId { get; set; }

        public long ProductId { get; set; }

        public string ProductNameSnapshot { get; set; }

        public decimal Quantity { get; set; }

        public decimal PurchasePrice { get; set; }

        public decimal LineTotal { get; set; }

        public DateTime? ProductionDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string ExpiryDateText
        {
            get { return ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : string.Empty; }
        }
    }
}
