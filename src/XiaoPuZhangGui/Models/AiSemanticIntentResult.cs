using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiSemanticIntentResult
    {
        public AiSemanticIntentResult()
        {
            ConversationMode = string.Empty;
            IntentType = string.Empty;
            SemanticTask = string.Empty;
            RouteType = AiIntentResult.RouteUnknown;
            QueryKind = string.Empty;
            AnalysisKey = string.Empty;
            ActionType = string.Empty;
            SubjectText = string.Empty;
            ProductName = string.Empty;
            CategoryName = string.Empty;
            CustomerName = string.Empty;
            TimeRange = string.Empty;
            NormalizedText = string.Empty;
            ClarificationQuestion = string.Empty;
            RawJson = string.Empty;
            RiskLevel = "low";
            ShortReason = string.Empty;
            RequiredData = new List<string>();
            ActionUnit = string.Empty;
            ActionNote = string.Empty;
            Confidence = 0m;
            Success = false;
            ErrorMessage = string.Empty;
        }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; }

        public string ConversationMode { get; set; }

        public string IntentType { get; set; }

        public string SemanticTask { get; set; }

        public string RouteType { get; set; }

        public string QueryKind { get; set; }

        public string AnalysisKey { get; set; }

        public string ActionType { get; set; }

        public string SubjectText { get; set; }

        public string ProductName { get; set; }

        public string CategoryName { get; set; }

        public string CustomerName { get; set; }

        public string TimeRange { get; set; }

        public string NormalizedText { get; set; }

        public decimal Confidence { get; set; }

        public bool NeedsClarification { get; set; }

        public string ClarificationQuestion { get; set; }

        public string RawJson { get; set; }

        public IList<string> RequiredData { get; private set; }

        public bool IsWriteAction { get; set; }

        public bool NeedsConfirmation { get; set; }

        public string RiskLevel { get; set; }

        public string ShortReason { get; set; }

        public decimal ActionQuantity { get; set; }

        public decimal ActionAmount { get; set; }

        public decimal ActionPrice { get; set; }

        public decimal ActionPriceDelta { get; set; }

        public string ActionUnit { get; set; }

        public string ActionNote { get; set; }

        public AiIntentResult ToIntentResult()
        {
            AiIntentResult intent = new AiIntentResult
            {
                RouteType = NormalizeRouteType(RouteType),
                QueryKind = QueryKind ?? string.Empty,
                AnalysisKey = AnalysisKey ?? string.Empty,
                FollowUpQuestion = ClarificationQuestion ?? string.Empty
            };

            if (intent.RouteType == AiIntentResult.RouteQuery)
            {
                intent.QueryConfidence = Confidence <= 0m ? 0.88m : Confidence;
            }
            else if (intent.RouteType == AiIntentResult.RouteAnalysis)
            {
                intent.AnalysisConfidence = Confidence <= 0m ? 0.88m : Confidence;
            }
            else if (intent.RouteType == AiIntentResult.RouteAction)
            {
                intent.ActionConfidence = IsWriteAction || NeedsConfirmation
                    ? Math.Max(0.88m, Confidence)
                    : (Confidence <= 0m ? 0.88m : Confidence);
            }
            else if (intent.RouteType == AiIntentResult.RouteChat)
            {
                intent.ChatConfidence = Confidence <= 0m ? 0.7m : Confidence;
            }

            AddIntentKeys(intent);
            return intent;
        }

        public string BuildSubjectText(string originalText)
        {
            if (!string.IsNullOrWhiteSpace(NormalizedText))
            {
                return NormalizedText.Trim();
            }

            if (!string.IsNullOrWhiteSpace(CategoryName))
            {
                if (QueryKind == "restock_advice")
                {
                    return CategoryName.Trim() + " 补货建议";
                }

                if (QueryKind == "new_product_advice")
                {
                    return CategoryName.Trim() + " 新品拓展建议";
                }

                if (QueryKind == "category_stock" || QueryKind == "category_low_stock")
                {
                    return CategoryName.Trim() + " 库存有哪些";
                }

                return CategoryName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(ProductName))
            {
                return ProductName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(SubjectText))
            {
                return SubjectText.Trim();
            }

            return originalText ?? string.Empty;
        }

        private void AddIntentKeys(AiIntentResult intent)
        {
            if (intent == null)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(intent.AnalysisKey) ? intent.QueryKind : intent.AnalysisKey;
            foreach (string intentKey in MapIntentKeys(key, TimeRange))
            {
                intent.IntentKeys.Add(intentKey);
            }
        }

        private static IEnumerable<string> MapIntentKeys(string key, string timeRange)
        {
            string value = (key ?? string.Empty).Trim();
            string range = (timeRange ?? string.Empty).Trim();
            if (value == "today" || range == "today")
            {
                yield return "today";
            }
            else if (value == "yesterday" || range == "yesterday")
            {
                yield return "yesterday";
            }
            else if (value == "week" || range == "week")
            {
                yield return "week";
            }
            else if (value == "month" || range == "month")
            {
                yield return "month";
            }

            if (value == "inventoryRisk" || value == "inventory" || value == "inventory_health" || value == "restock_advice" || value == "new_product_advice" || value == "category_stock" || value == "low_stock")
            {
                yield return "inventorySnapshot";
                yield return "inventoryRisk";
            }

            if (value == "hotSlow" || value == "hot_slow" || value == "hot_slow_products")
            {
                yield return "hotSlow";
            }

            if (value == "credit" || value == "credit_customers")
            {
                yield return "credit";
            }
        }

        private static string NormalizeRouteType(string routeType)
        {
            string value = (routeType ?? string.Empty).Trim();
            if (value == AiIntentResult.RouteQuery
                || value == AiIntentResult.RouteAnalysis
                || value == AiIntentResult.RouteAction
                || value == AiIntentResult.RouteChat
                || value == AiIntentResult.RouteUnknown)
            {
                return value;
            }

            return AiIntentResult.RouteUnknown;
        }
    }
}
