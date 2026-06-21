using System;
using System.IO;

namespace XiaoPuZhangGui.Utils
{
    internal static class AppPaths
    {
        public static string BaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string ConfigFilePath
        {
            get { return Path.Combine(BaseDirectory, "app.config.xml"); }
        }

        public static string DatabaseDirectory
        {
            get { return Path.Combine(BaseDirectory, "database"); }
        }

        public static string DefaultDatabasePath
        {
            get { return Path.Combine(DatabaseDirectory, "shop.db"); }
        }

        public static string BackupDirectory
        {
            get { return Path.Combine(BaseDirectory, "backups"); }
        }

        public static string SchemaFilePath
        {
            get { return Path.Combine(BaseDirectory, "Database", "schema.sql"); }
        }

        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
