using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiStoredMessage
    {
        public long Id { get; set; }

        public long ConversationId { get; set; }

        public string Role { get; set; }

        public string Content { get; set; }

        public DateTime CreatedAt { get; set; }

        public string MessageType { get; set; }

        public string DataContextType { get; set; }

        public int TokenEstimate { get; set; }
    }
}
