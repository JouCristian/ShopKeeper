using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal static class StartupService
    {
        public static void Initialize()
        {
            AppPaths.EnsureRuntimeDirectories();
            AppConfig config = AppConfigService.LoadOrCreateDefault();

            AppPaths.EnsureDirectory(AppPaths.DatabaseDirectory);
            AppPaths.EnsureDirectory(config.BackupPath);
            AppPaths.EnsureDirectory(AppPaths.ExportDirectory);

            DatabaseService.Initialize(config.DatabasePath);
        }

        public static void TryRunStartupBackup()
        {
            try
            {
                new BackupService().CreateStartupAutomaticBackupIfNeeded();
            }
            catch
            {
            }
        }

        public static void TryRunExitBackup()
        {
            try
            {
                new BackupService().CreateExitAutomaticBackup();
            }
            catch
            {
            }
        }
    }
}
