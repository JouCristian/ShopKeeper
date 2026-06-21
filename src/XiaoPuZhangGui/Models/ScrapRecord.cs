using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class ScrapRecord
    {
        public long Id { get; set; }

        public string ScrapNo { get; set; }

        public DateTime ScrapDate { get; set; }

        public long ProductId { get; set; }

        public string ProductNameSnapshot { get; set; }

        public decimal Quantity { get; set; }

        public decimal CostPriceSnapshot { get; set; }

        public decimal LossAmount { get; set; }

        public string Reason { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
