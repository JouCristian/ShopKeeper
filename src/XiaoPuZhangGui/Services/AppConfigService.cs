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
                RecoveryKeySalt = ReadValue(root, "RecoveryKeySalt", string.Empty)
            };

            if (string.IsNullOrWhiteSpace(config.StoreName))
            {
                config.StoreName = "小铺掌柜";
            }

            if (string.IsNullOrWhiteSpace(config.DatabasePath))
            {
                config.DatabasePath = AppPaths.DefaultDatabasePath;
            }

            if (string.IsNullOrWhiteSpace(config.BackupPath))
            {
                config.BackupPath = AppPaths.BackupDirectory;
            }

            return config;
        }

        public static void Save(AppConfig config)
        {
            XDocument document = new XDocument(
                new XElement("AppConfig",
                    new XElement("StoreName", config.StoreName ?? string.Empty),
                    new XElement("DatabasePath", config.DatabasePath ?? string.Empty),
                    new XElement("BackupPath", config.BackupPath ?? string.Empty),
                    new XElement("IsInitialized", config.IsInitialized),
                    new XElement("PinHash", config.PinHash ?? string.Empty),
                    new XElement("PinSalt", config.PinSalt ?? string.Empty),
                    new XElement("RecoveryKeyHash", config.RecoveryKeyHash ?? string.Empty),
                    new XElement("RecoveryKeySalt", config.RecoveryKeySalt ?? string.Empty)));

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
                RecoveryKeySalt = string.Empty
            };
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
