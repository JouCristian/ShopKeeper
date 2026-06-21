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

        public static string ProjectRoot
        {
            get
            {
#if DEBUG
                return FindProjectRoot();
#else
                return BaseDirectory;
#endif
            }
        }

        public static string RuntimeRoot
        {
            get
            {
#if DEBUG
                return Path.Combine(ProjectRoot, ".runtime");
#else
                return Path.Combine(BaseDirectory, "data");
#endif
            }
        }

        public static string ConfigFilePath
        {
            get { return Path.Combine(RuntimeRoot, "app.config.xml"); }
        }

        public static string DatabaseDirectory
        {
            get { return Path.Combine(RuntimeRoot, "database"); }
        }

        public static string DefaultDatabasePath
        {
            get { return Path.Combine(DatabaseDirectory, "shop.db"); }
        }

        public static string BackupDirectory
        {
            get { return Path.Combine(RuntimeRoot, "backups"); }
        }

        public static string ExportDirectory
        {
            get { return Path.Combine(RuntimeRoot, "exports"); }
        }

        public static string SchemaFilePath
        {
            get { return Path.Combine(BaseDirectory, "Database", "schema.sql"); }
        }

        public static string RuntimeMode
        {
            get
            {
#if DEBUG
                return "Debug";
#else
                return "Release";
#endif
            }
        }

        public static void EnsureRuntimeDirectories()
        {
            EnsureDirectory(RuntimeRoot);
            EnsureDirectory(DatabaseDirectory);
            EnsureDirectory(BackupDirectory);
            EnsureDirectory(ExportDirectory);
            CopyLegacyRuntimeFilesIfNeeded();
        }

        public static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static string FindProjectRoot()
        {
            DirectoryInfo current = new DirectoryInfo(BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "XiaoPuZhangGui.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            current = new DirectoryInfo(BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return BaseDirectory;
        }

        private static void CopyLegacyRuntimeFilesIfNeeded()
        {
#if DEBUG
            CopyFileIfMissing(
                Path.Combine(BaseDirectory, "app.config.xml"),
                ConfigFilePath);

            CopyFileIfMissing(
                Path.Combine(BaseDirectory, "database", "shop.db"),
                DefaultDatabasePath);

            CopyDirectoryIfExists(
                Path.Combine(BaseDirectory, "backups"),
                BackupDirectory);

            CopyDirectoryIfExists(
                Path.Combine(BaseDirectory, "exports"),
                ExportDirectory);
#endif
        }

        private static void CopyFileIfMissing(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath) || File.Exists(targetPath))
            {
                return;
            }

            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                EnsureDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, false);
        }

        private static void CopyDirectoryIfExists(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            EnsureDirectory(targetDirectory);
            foreach (string sourceFile in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = sourceFile.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFile = Path.Combine(targetDirectory, relativePath);
                CopyFileIfMissing(sourceFile, targetFile);
            }
        }
    }
}
