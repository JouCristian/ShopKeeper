using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiActionDraft
    {
        public AiActionDraft()
        {
            Id = Guid.NewGuid().ToString("N");
            Items = new List<AiActionDraftItem>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            Status = AiActionDraftStatus.Pending;
            ActionType = AiActionTypes.Unknown;
            RiskLevel = AiActionRiskLevels.Low;
        }

        public string Id { get; set; }

        public long ConversationId { get; set; }

        public string ActionType { get; set; }

        public string Title { get; set; }

        public string Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string SourceUserMessage { get; set; }

        public string RawAiJson { get; set; }

        public decimal Confidence { get; set; }

        public string RiskLevel { get; set; }

        public bool NeedUserClarification { get; set; }

        public string ClarificationQuestion { get; set; }

        public IList<AiActionDraftItem> Items { get; set; }
    }
}
