namespace XiaoPuZhangGui.Models
{
    internal sealed class AiChatMessage
    {
        public string Role { get; set; }

        public string Content { get; set; }

        public static AiChatMessage System(string content)
        {
            return Create("system", content);
        }

        public static AiChatMessage User(string content)
        {
            return Create("user", content);
        }

        public static AiChatMessage Assistant(string content)
        {
            return Create("assistant", content);
        }

        private static AiChatMessage Create(string role, string content)
        {
            return new AiChatMessage
            {
                Role = role,
                Content = content ?? string.Empty
            };
        }
    }
}
