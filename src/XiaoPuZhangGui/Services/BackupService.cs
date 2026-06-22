using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class BackupService
    {
        private const int AutomaticBackupRetention = 60;
        private const string AutomaticBackupType = "自动备份";
        private const string ManualBackupType = "手动备份";
        private const string PreRestoreBackupType = "恢复前备份";

        private readonly string _databasePath;
        private readonly string _configPath;
        private readonly string _backupDirectory;

        public BackupService()
            : this(AppPaths.DefaultDatabasePath, AppPaths.ConfigFilePath, AppPaths.BackupDirectory)
        {
        }

        internal BackupService(string databasePath, string configPath, string backupDirectory)
        {
            _databasePath = databasePath;
            _configPath = configPath;
            _backupDirectory = backupDirectory;
        }

        public BackupResult CreateManualBackup()
        {
            return CreateBackup(ManualBackupType, _backupDirectory, false);
        }

        public BackupResult CreateManualBackupTo(string targetDirectory)
        {
            return CreateBackup(ManualBackupType, targetDirectory, false);
        }

        public BackupResult CreatePreRestoreBackup()
        {
            return CreateBackup(PreRestoreBackupType, _backupDirectory, false);
        }

        public BackupResult CreateExitAutomaticBackup()
        {
            BackupResult result = CreateBackup(AutomaticBackupType, _backupDirectory, true);
            CleanupAutomaticBackups();
            return result;
        }

        public BackupResult CreateStartupAutomaticBackupIfNeeded()
        {
            EnsureDirectory(_backupDirectory);
            string today = DateTime.Now.ToString("yyyyMMdd");
            bool alreadyBackedUp = Directory.GetFiles(_backupDirectory, "*.zip")
                .Any(path => Path.GetFileName(path).Contains(AutomaticBackupType) && Path.GetFileName(path).Contains(today));

            if (alreadyBackedUp)
            {
                return BackupResult.Skipped("今天已经完成首次启动自动备份。");
            }

            BackupResult result = CreateBackup(AutomaticBackupType, _backupDirectory, true);
            CleanupAutomaticBackups();
            return result;
        }

        public RestoreResult RestoreFromBackup(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("没有找到要恢复的备份文件。", sourcePath);
            }

            BackupResult preRestoreBackup = CreatePreRestoreBackup();
            string tempRoot = Path.Combine(Path.GetTempPath(), "XiaoPuZhangGuiRestore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            string restoreDatabasePath = null;
            string restoreConfigPath = null;

            try
            {
                string extension = Path.GetExtension(sourcePath);
                if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ExtractRestoreFiles(sourcePath, tempRoot, out restoreDatabasePath, out restoreConfigPath);
                }
                else if (string.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase))
                {
                    restoreDatabasePath = Path.Combine(tempRoot, "shop.db");
                    File.Copy(sourcePath, restoreDatabasePath, true);
                }
                else
                {
                    throw new InvalidOperationException("仅支持从 .zip 备份包或单独 .db 数据库文件恢复。");
                }

                if (string.IsNullOrWhiteSpace(restoreDatabasePath) || !File.Exists(restoreDatabasePath))
                {
                    throw new InvalidOperationException("备份文件中没有找到 database/shop.db。");
                }

                ValidateSqliteDatabase(restoreDatabasePath);
                ReplaceRuntimeFiles(restoreDatabasePath, restoreConfigPath);

                return new RestoreResult
                {
                    SourcePath = sourcePath,
                    PreRestoreBackupPath = preRestoreBackup.FilePath,
                    RestoredDatabase = true,
                    RestoredConfig = !string.IsNullOrWhiteSpace(restoreConfigPath) && File.Exists(restoreConfigPath)
                };
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        public IList<BackupFileInfo> GetRecentBackups(int limit)
        {
            EnsureDirectory(_backupDirectory);
            return Directory.GetFiles(_backupDirectory, "*.zip")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTime)
                .Take(limit)
                .Select(file => new BackupFileInfo
                {
                    FileName = file.Name,
                    FullPath = file.FullName,
                    LastWriteTime = file.LastWriteTime,
                    Size = file.Length
                })
                .ToList();
        }

        private BackupResult CreateBackup(string backupType, string targetDirectory, bool isAutomatic)
        {
            EnsureDirectory(targetDirectory);

            string filePath = CreateUniqueBackupPath(targetDirectory, backupType);
            using (FileStream fileStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                if (File.Exists(_databasePath))
                {
                    AddFile(archive, _databasePath, "database/shop.db");
                }

                if (File.Exists(_configPath))
                {
                    AddFile(archive, _configPath, "app.config.xml");
                }

                AddText(archive, "backup_info.txt", BuildBackupInfo(backupType));
            }

            return new BackupResult
            {
                FilePath = filePath,
                BackupType = backupType,
                IsAutomatic = isAutomatic,
                IsSkipped = false
            };
        }

        private void CleanupAutomaticBackups()
        {
            EnsureDirectory(_backupDirectory);
            List<FileInfo> automaticBackups = Directory.GetFiles(_backupDirectory, "*.zip")
                .Select(path => new FileInfo(path))
                .Where(file => file.Name.Contains(AutomaticBackupType))
                .OrderByDescending(file => file.LastWriteTime)
                .ToList();

            foreach (FileInfo oldBackup in automaticBackups.Skip(AutomaticBackupRetention))
            {
                try
                {
                    oldBackup.Delete();
                }
                catch
                {
                    // 自动清理失败不应影响业务使用。
                }
            }
        }

        private void ExtractRestoreFiles(string sourcePath, string tempRoot, out string databasePath, out string configPath)
        {
            databasePath = null;
            configPath = null;

            using (FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string normalizedName = entry.FullName.Replace('\\', '/');
                    if (string.Equals(normalizedName, "database/shop.db", StringComparison.OrdinalIgnoreCase))
                    {
                        databasePath = Path.Combine(tempRoot, "shop.db");
                        ExtractEntry(entry, databasePath);
                    }
                    else if (string.Equals(normalizedName, "app.config.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        configPath = Path.Combine(tempRoot, "app.config.xml");
                        ExtractEntry(entry, configPath);
                    }
                }
            }
        }

        private void ReplaceRuntimeFiles(string restoreDatabasePath, string restoreConfigPath)
        {
            string rollbackDirectory = Path.Combine(Path.GetTempPath(), "XiaoPuZhangGuiRollback_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rollbackDirectory);

            string rollbackDatabasePath = Path.Combine(rollbackDirectory, "shop.db");
            string rollbackConfigPath = Path.Combine(rollbackDirectory, "app.config.xml");
            bool hasDatabaseRollback = File.Exists(_databasePath);
            bool hasConfigRollback = File.Exists(_configPath);

            try
            {
                if (hasDatabaseRollback)
                {
                    File.Copy(_databasePath, rollbackDatabasePath, true);
                }

                if (hasConfigRollback)
                {
                    File.Copy(_configPath, rollbackConfigPath, true);
                }

                EnsureDirectory(Path.GetDirectoryName(_databasePath));
                File.Copy(restoreDatabasePath, _databasePath, true);

                if (!string.IsNullOrWhiteSpace(restoreConfigPath) && File.Exists(restoreConfigPath))
                {
                    EnsureDirectory(Path.GetDirectoryName(_configPath));
                    File.Copy(restoreConfigPath, _configPath, true);
                }
            }
            catch
            {
                TryRestoreRollback(rollbackDatabasePath, _databasePath, hasDatabaseRollback);
                TryRestoreRollback(rollbackConfigPath, _configPath, hasConfigRollback);
                throw;
            }
            finally
            {
                TryDeleteDirectory(rollbackDirectory);
            }
        }

        private static void ValidateSqliteDatabase(string databasePath)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3,
                ForeignKeys = true
            };

            using (SQLiteConnection connection = new SQLiteConnection(builder.ToString()))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' LIMIT 1;";
                command.ExecuteScalar();
            }
        }

        private string BuildBackupInfo(string backupType)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("软件名称：小铺掌柜");
            builder.AppendLine("备份时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("备份类型：" + backupType);
            builder.AppendLine("数据库路径：" + _databasePath);
            builder.AppendLine("配置路径：" + _configPath);
            return builder.ToString();
        }

        private static string CreateUniqueBackupPath(string targetDirectory, string backupType)
        {
            string safeType = backupType.Replace(" ", string.Empty);
            string baseName = string.Format("小铺掌柜_备份_{0}_{1}", safeType, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string filePath = Path.Combine(targetDirectory, baseName + ".zip");
            int suffix = 1;

            while (File.Exists(filePath))
            {
                filePath = Path.Combine(targetDirectory, baseName + "_" + suffix.ToString("00") + ".zip");
                suffix++;
            }

            return filePath;
        }

        private static void AddFile(ZipArchive archive, string sourcePath, string entryName)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Stream target = entry.Open())
            {
                source.CopyTo(target);
            }
        }

        private static void AddText(ZipArchive archive, string entryName, string text)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(text);
            }
        }

        private static void ExtractEntry(ZipArchiveEntry entry, string targetPath)
        {
            EnsureDirectory(Path.GetDirectoryName(targetPath));
            using (Stream source = entry.Open())
            using (FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
            {
                source.CopyTo(target);
            }
        }

        private static void TryRestoreRollback(string rollbackPath, string targetPath, bool hasRollback)
        {
            try
            {
                if (hasRollback && File.Exists(rollbackPath))
                {
                    EnsureDirectory(Path.GetDirectoryName(targetPath));
                    File.Copy(rollbackPath, targetPath, true);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }

        private static void EnsureDirectory(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    internal sealed class BackupResult
    {
        public string FilePath { get; set; }

        public string BackupType { get; set; }

        public bool IsAutomatic { get; set; }

        public bool IsSkipped { get; set; }

        public string Message { get; set; }

        public static BackupResult Skipped(string message)
        {
            return new BackupResult
            {
                IsSkipped = true,
                Message = message
            };
        }
    }

    internal sealed class RestoreResult
    {
        public string SourcePath { get; set; }

        public string PreRestoreBackupPath { get; set; }

        public bool RestoredDatabase { get; set; }

        public bool RestoredConfig { get; set; }
    }

    internal sealed class BackupFileInfo
    {
        public string FileName { get; set; }

        public string FullPath { get; set; }

        public DateTime LastWriteTime { get; set; }

        public long Size { get; set; }

        public string DisplayText
        {
            get
            {
                return string.Format("{0}  {1:yyyy-MM-dd HH:mm:ss}  {2}", FileName, LastWriteTime, FormatSize(Size));
            }
        }

        private static string FormatSize(long size)
        {
            if (size >= 1024 * 1024)
            {
                return (size / 1024m / 1024m).ToString("0.00") + " MB";
            }

            return (size / 1024m).ToString("0.00") + " KB";
        }
    }
}
