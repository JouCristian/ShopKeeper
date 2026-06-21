using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class Product
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public long CategoryId { get; set; }

        public string CategoryName { get; set; }

        public string Barcode { get; set; }

        public string Specification { get; set; }

        public decimal DefaultPrice { get; set; }

        public decimal CurrentStock { get; set; }

        public decimal AverageCost { get; set; }

        public decimal MinStockAlert { get; set; }

        public bool RequiresExpiry { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string Status { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string RequiresExpiryText
        {
            get { return RequiresExpiry ? "是" : "否"; }
        }

        public string ExpiryDateText
        {
            get { return ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : string.Empty; }
        }

        public string StatusActionText
        {
            get { return Status == "停用" ? "启用" : "停用"; }
        }
    }
}
