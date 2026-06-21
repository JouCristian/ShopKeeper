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
                BackupPath = ReadValue(root, "BackupPath", AppPaths.BackupDirectory)
            };

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
                    new XElement("BackupPath", config.BackupPath ?? string.Empty)));

            document.Save(AppPaths.ConfigFilePath);
        }

        private static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                StoreName = "小铺掌柜",
                DatabasePath = AppPaths.DefaultDatabasePath,
                BackupPath = AppPaths.BackupDirectory
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
    }
}
