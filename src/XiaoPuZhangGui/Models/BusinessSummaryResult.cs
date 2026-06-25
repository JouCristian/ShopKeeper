namespace XiaoPuZhangGui.Models
{
    internal sealed class BusinessSummaryResult
    {
        public bool Success { get; set; }

        public string Title { get; set; }

        public string Period { get; set; }

        public string SummaryText { get; set; }

        public string JsonText { get; set; }

        public string ErrorMessage { get; set; }

        public static BusinessSummaryResult Fail(string title, string errorMessage)
        {
            return new BusinessSummaryResult
            {
                Success = false,
                Title = title,
                Period = string.Empty,
                SummaryText = string.Empty,
                JsonText = string.Empty,
                ErrorMessage = errorMessage
            };
        }
    }
}
