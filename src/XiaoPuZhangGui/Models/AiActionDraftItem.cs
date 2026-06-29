using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiActionDraftItem
    {
        public AiActionDraftItem()
        {
            MissingFields = new List<string>();
            Warnings = new List<string>();
            CandidateProductNames = new List<string>();
            Status = AiActionDraftStatus.Pending;
            ActionType = AiActionTypes.Unknown;
            RiskLevel = AiActionRiskLevels.Low;
            Unit = "件";
        }

        public string Id { get; set; }

        public string DraftId { get; set; }

        public int ItemIndex { get; set; }

        public string ActionType { get; set; }

        public string ProductName { get; set; }

        public string ProductSpec { get; set; }

        public string Category { get; set; }

        public decimal? Quantity { get; set; }

        public string Unit { get; set; }

        public decimal? PurchasePrice { get; set; }

        public decimal? SalePrice { get; set; }

        public DateTime? ProductionDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public bool? ShelfLifeEnabled { get; set; }

        public int? ShelfLifeDays { get; set; }

        public string CustomerName { get; set; }

        public decimal? CreditAmount { get; set; }

        public decimal? ActualReceivedAmount { get; set; }

        public decimal? InventoryAdjustQuantity { get; set; }

        public decimal? PriceChangeOldValue { get; set; }

        public decimal? PriceChangeNewValue { get; set; }

        public IList<string> MissingFields { get; set; }

        public IList<string> Warnings { get; set; }

        public decimal Confidence { get; set; }

        public string Status { get; set; }

        public string RiskLevel { get; set; }

        public long? MatchedProductId { get; set; }

        public string MatchedProductName { get; set; }

        public bool IsNewProduct { get; set; }

        public IList<string> CandidateProductNames { get; set; }

        public string Notes { get; set; }
    }
}
