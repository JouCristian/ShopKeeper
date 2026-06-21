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
    }
}
