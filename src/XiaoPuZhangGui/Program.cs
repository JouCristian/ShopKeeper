using System;
using System.Windows.Forms;
using XiaoPuZhangGui.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool startupCompleted = false;
            try
            {
                StartupService.Initialize();
                startupCompleted = true;
                StartupService.TryRunStartupBackup();

                AppConfig config = AppConfigService.LoadOrCreateDefault();

                if (!config.IsInitialized)
                {
                    using (FirstRunForm firstRunForm = new FirstRunForm())
                    {
                        if (firstRunForm.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                    }

                    Application.Run(new MainForm());
                    return;
                }

                using (LoginForm loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "程序启动失败：\r\n" + ex.Message,
                    "小铺掌柜",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (startupCompleted)
                {
                    StartupService.TryRunExitBackup();
                }
            }
        }
    }
}
