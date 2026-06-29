namespace XiaoPuZhangGui.Models
{
    internal static class AiActionTypes
    {
        public const string PurchaseIn = "purchase_in";
        public const string SaleRecord = "sale_record";
        public const string InventoryAdjust = "inventory_adjust";
        public const string CreditRegister = "credit_register";
        public const string ProductPriceUpdate = "product_price_update";
        public const string DeleteOrUndoRequest = "delete_or_undo_request";
        public const string Unknown = "unknown";
        public const string MultiAction = "multi_action";
    }

    internal static class AiActionDraftStatus
    {
        public const string Pending = "pending";
        public const string Editing = "editing";
        public const string Confirmed = "confirmed";
        public const string Cancelled = "cancelled";
        public const string Executed = "executed";
        public const string Failed = "failed";
    }

    internal static class AiActionRiskLevels
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
    }
}
