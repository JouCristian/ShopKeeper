namespace XiaoPuZhangGui.Models
{
    internal sealed class AiActionDraftParseResult
    {
        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public string RawJson { get; set; }

        public AiActionDraft Draft { get; set; }

        public static AiActionDraftParseResult Fail(string message)
        {
            return new AiActionDraftParseResult
            {
                Success = false,
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "AI 没有识别成功，请换一种说法或手动登记。" : message,
                RawJson = string.Empty
            };
        }
    }
}
