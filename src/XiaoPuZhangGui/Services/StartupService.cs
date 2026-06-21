using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal static class StartupService
    {
        public static void Initialize()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();

            AppPaths.EnsureDirectory(AppPaths.DatabaseDirectory);
            AppPaths.EnsureDirectory(config.BackupPath);

            DatabaseService.Initialize(config.DatabasePath);
        }
    }
}
