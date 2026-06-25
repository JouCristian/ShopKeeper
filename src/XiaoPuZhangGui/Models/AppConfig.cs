namespace XiaoPuZhangGui.Models
{
    internal sealed class AppConfig
    {
        public string StoreName { get; set; }

        public string DatabasePath { get; set; }

        public string BackupPath { get; set; }

        public bool IsInitialized { get; set; }

        public string PinHash { get; set; }

        public string PinSalt { get; set; }

        public string RecoveryKeyHash { get; set; }

        public string RecoveryKeySalt { get; set; }

        public bool AiEnabled { get; set; }

        public string AiProvider { get; set; }

        public string AiBaseUrl { get; set; }

        public string AiModel { get; set; }

        public string AiApiKeyEncrypted { get; set; }

        public string AiApiKeyMasked { get; set; }

        public string LastConnectionTestTime { get; set; }
    }
}
