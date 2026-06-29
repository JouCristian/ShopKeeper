using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiIntentResult
    {
        public const string RouteQuery = "query";
        public const string RouteAnalysis = "analysis";
        public const string RouteAction = "action";
        public const string RouteChat = "chat";
        public const string RouteUnknown = "unknown";

        public AiIntentResult()
        {
            IntentKeys = new List<string>();
            RouteType = RouteChat;
            AnalysisKey = string.Empty;
            QueryKind = string.Empty;
            FollowUpQuestion = string.Empty;
        }

        public IList<string> IntentKeys { get; private set; }

        public string RouteType { get; set; }

        public string AnalysisKey { get; set; }

        public string QueryKind { get; set; }

        public decimal QueryConfidence { get; set; }

        public decimal AnalysisConfidence { get; set; }

        public decimal ActionConfidence { get; set; }

        public decimal ChatConfidence { get; set; }

        public string FollowUpQuestion { get; set; }

        public bool HasBusinessContext
        {
            get { return IntentKeys.Count > 0; }
        }

        public bool IsAction
        {
            get { return RouteType == RouteAction && ActionConfidence >= 0.75m; }
        }
    }
}
