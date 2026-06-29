using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiPurchaseDraft
    {
        public string ProductName { get; set; }

        public string Specification { get; set; }

        public string CategoryName { get; set; }

        public decimal? Quantity { get; set; }

        public string QuantityUnit { get; set; }

        public decimal? PackageCount { get; set; }

        public decimal? UnitsPerPackage { get; set; }

        public decimal? PurchasePrice { get; set; }

        public decimal? SalePrice { get; set; }

        public DateTime PurchaseDate { get; set; }

        public bool? RequiresExpiry { get; set; }

        public DateTime? ProductionDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string Remark { get; set; }

        public long? MatchedProductId { get; set; }

        public bool ShouldCreateProduct { get; set; }

        public AiPurchaseDraft()
        {
            PurchaseDate = DateTime.Today;
            QuantityUnit = "件";
            Remark = string.Empty;
        }
    }
}
