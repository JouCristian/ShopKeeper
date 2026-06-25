using System.IO;
using System.Xml.Linq;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal static class AppConfigService
    {
        public static AppConfig LoadOrCreateDefault()
        {
            AppPaths.EnsureRuntimeDirectories();

            if (!File.Exists(AppPaths.ConfigFilePath))
            {
                AppConfig defaultConfig = CreateDefault();
                Save(defaultConfig);
                return defaultConfig;
            }

            XDocument document = XDocument.Load(AppPaths.ConfigFilePath);
            XElement root = document.Root;

            AppConfig config = new AppConfig
            {
                StoreName = ReadValue(root, "StoreName", "小铺掌柜"),
                DatabasePath = ReadValue(root, "DatabasePath", AppPaths.DefaultDatabasePath),
                BackupPath = ReadValue(root, "BackupPath", AppPaths.BackupDirectory),
                IsInitialized = ReadBool(root, "IsInitialized", false),
                PinHash = ReadValue(root, "PinHash", string.Empty),
                PinSalt = ReadValue(root, "PinSalt", string.Empty),
                RecoveryKeyHash = ReadValue(root, "RecoveryKeyHash", string.Empty),
                RecoveryKeySalt = ReadValue(root, "RecoveryKeySalt", string.Empty),
                AiEnabled = ReadBool(root, "AiEnabled", false),
                AiProvider = ReadValue(root, "AiProvider", "DeepSeek"),
                AiBaseUrl = ReadValue(root, "AiBaseUrl", "https://api.deepseek.com"),
                AiModel = ReadValue(root, "AiModel", "deepseek-v4-flash"),
                AiApiKeyEncrypted = ReadValue(root, "AiApiKeyEncrypted", string.Empty),
                AiApiKeyMasked = ReadValue(root, "AiApiKeyMasked", string.Empty),
                LastConnectionTestTime = ReadValue(root, "LastConnectionTestTime", string.Empty)
            };

            bool changed = NormalizeRuntimePaths(config);
            if (changed)
            {
                Save(config);
            }

            return config;
        }

        public static void Save(AppConfig config)
        {
            AppPaths.EnsureDirectory(AppPaths.RuntimeRoot);
            XDocument document = new XDocument(
                new XElement("AppConfig",
                    new XElement("StoreName", config.StoreName ?? string.Empty),
                    new XElement("DatabasePath", config.DatabasePath ?? string.Empty),
                    new XElement("BackupPath", config.BackupPath ?? string.Empty),
                    new XElement("IsInitialized", config.IsInitialized),
                    new XElement("PinHash", config.PinHash ?? string.Empty),
                    new XElement("PinSalt", config.PinSalt ?? string.Empty),
                    new XElement("RecoveryKeyHash", config.RecoveryKeyHash ?? string.Empty),
                    new XElement("RecoveryKeySalt", config.RecoveryKeySalt ?? string.Empty),
                    new XElement("AiEnabled", config.AiEnabled),
                    new XElement("AiProvider", config.AiProvider ?? string.Empty),
                    new XElement("AiBaseUrl", config.AiBaseUrl ?? string.Empty),
                    new XElement("AiModel", config.AiModel ?? string.Empty),
                    new XElement("AiApiKeyEncrypted", config.AiApiKeyEncrypted ?? string.Empty),
                    new XElement("AiApiKeyMasked", config.AiApiKeyMasked ?? string.Empty),
                    new XElement("LastConnectionTestTime", config.LastConnectionTestTime ?? string.Empty)));

            document.Save(AppPaths.ConfigFilePath);
        }

        public static void UpdateStoreName(string storeName)
        {
            AppConfig config = LoadOrCreateDefault();
            config.StoreName = string.IsNullOrWhiteSpace(storeName) ? "小铺掌柜" : storeName.Trim();
            Save(config);
        }

        private static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                StoreName = "小铺掌柜",
                DatabasePath = AppPaths.DefaultDatabasePath,
                BackupPath = AppPaths.BackupDirectory,
                IsInitialized = false,
                PinHash = string.Empty,
                PinSalt = string.Empty,
                RecoveryKeyHash = string.Empty,
                RecoveryKeySalt = string.Empty,
                AiEnabled = false,
                AiProvider = "DeepSeek",
                AiBaseUrl = "https://api.deepseek.com",
                AiModel = "deepseek-v4-flash",
                AiApiKeyEncrypted = string.Empty,
                AiApiKeyMasked = string.Empty,
                LastConnectionTestTime = string.Empty
            };
        }

        private static bool NormalizeRuntimePaths(AppConfig config)
        {
            bool changed = false;

            if (string.IsNullOrWhiteSpace(config.StoreName))
            {
                config.StoreName = "小铺掌柜";
                changed = true;
            }

            if (config.DatabasePath != AppPaths.DefaultDatabasePath)
            {
                config.DatabasePath = AppPaths.DefaultDatabasePath;
                changed = true;
            }

            if (config.BackupPath != AppPaths.BackupDirectory)
            {
                config.BackupPath = AppPaths.BackupDirectory;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.AiProvider))
            {
                config.AiProvider = "DeepSeek";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.AiBaseUrl))
            {
                config.AiBaseUrl = "https://api.deepseek.com";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.AiModel))
            {
                config.AiModel = "deepseek-v4-flash";
                changed = true;
            }

            return changed;
        }

        private static string ReadValue(XElement root, string elementName, string defaultValue)
        {
            if (root == null)
            {
                return defaultValue;
            }

            XElement element = root.Element(elementName);
            return element == null ? defaultValue : element.Value;
        }

        private static bool ReadBool(XElement root, string elementName, bool defaultValue)
        {
            string value = ReadValue(root, elementName, defaultValue.ToString());
            bool result;
            return bool.TryParse(value, out result) ? result : defaultValue;
        }
    }
}
