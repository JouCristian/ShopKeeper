using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiConversation
    {
        public AiConversation()
        {
            Messages = new List<AiStoredMessage>();
        }

        public long Id { get; set; }

        public string Title { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Model { get; set; }

        public bool IsArchived { get; set; }

        public string Summary { get; set; }

        public IList<AiStoredMessage> Messages { get; private set; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Title) ? "新对话" : Title;
        }
    }
}
