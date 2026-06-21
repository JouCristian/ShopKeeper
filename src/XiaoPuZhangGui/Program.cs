using System;
using System.Windows.Forms;
using XiaoPuZhangGui.Forms;
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

            try
            {
                StartupService.Initialize();
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
        }
    }
}
