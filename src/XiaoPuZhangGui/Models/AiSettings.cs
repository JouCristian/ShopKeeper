namespace XiaoPuZhangGui.Models
{
    internal sealed class AiSettings
    {
        public bool AiEnabled { get; set; }

        public string AiProvider { get; set; }

        public string AiBaseUrl { get; set; }

        public string AiModel { get; set; }

        public bool HasApiKey { get; set; }

        public string AiApiKey { get; set; }

        public string AiApiKeyMasked { get; set; }

        public string LastConnectionTestTime { get; set; }
    }
}
