namespace XiaoPuZhangGui.Models
{
    internal sealed class AiPurchaseExecutionResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public long ProductId { get; set; }

        public long PurchaseRecordId { get; set; }

        public bool CreatedProduct { get; set; }
    }
}
