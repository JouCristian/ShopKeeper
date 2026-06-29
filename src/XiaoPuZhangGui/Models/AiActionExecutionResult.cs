namespace XiaoPuZhangGui.Models
{
    internal sealed class AiActionExecutionResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public long BusinessRecordId { get; set; }

        public string BusinessRecordType { get; set; }
    }
}
