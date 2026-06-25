using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiTheme
    {
        public static readonly Color PrimaryBlue = Color.FromArgb(31, 111, 235);
        public static readonly Color PrimaryBlueHover = Color.FromArgb(23, 94, 204);
        public static readonly Color SidebarDark = Color.FromArgb(36, 48, 63);
        public static readonly Color SidebarDarkHover = Color.FromArgb(45, 61, 80);
        public static readonly Color SidebarSelected = Color.FromArgb(31, 111, 235);
        public static readonly Color PageBackground = Color.FromArgb(244, 247, 251);
        public static readonly Color CardBackground = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(219, 226, 235);
        public static readonly Color TextPrimary = Color.FromArgb(31, 41, 55);
        public static readonly Color TextSecondary = Color.FromArgb(75, 85, 99);
        public static readonly Color SuccessGreen = Color.FromArgb(21, 128, 61);
        public static readonly Color WarningOrange = Color.FromArgb(180, 83, 9);
        public static readonly Color DangerRed = Color.FromArgb(203, 48, 48);
        public static readonly Color InfoCyan = Color.FromArgb(14, 116, 144);
        public static readonly Color MutedGray = Color.FromArgb(107, 114, 128);
        public static readonly Color SoftBlue = Color.FromArgb(232, 240, 254);
        public static readonly Color SoftGreen = Color.FromArgb(230, 246, 238);
        public static readonly Color SoftOrange = Color.FromArgb(255, 247, 237);
        public static readonly Color SoftRed = Color.FromArgb(254, 242, 242);
        public static readonly Color SoftCyan = Color.FromArgb(236, 253, 255);
        public static readonly Color SoftSlate = Color.FromArgb(248, 250, 252);
        public static readonly Color SoftPurple = Color.FromArgb(245, 243, 255);
        public static readonly Color SuccessBorder = Color.FromArgb(187, 247, 208);
        public static readonly Color WarningBorder = Color.FromArgb(253, 186, 116);
        public static readonly Color DangerBorder = Color.FromArgb(252, 165, 165);
        public static readonly Color InfoBorder = Color.FromArgb(165, 243, 252);

        public const string FontFamily = "Microsoft YaHei UI";
        public const int PagePadding = 24;
        public const int CardPadding = 18;
        public const int Gap = 12;
        public const int ButtonHeight = 40;
        public const int InputHeight = 40;
        public const int GridRowHeight = 40;
        public const int GridHeaderHeight = 44;

        public static Font Font(float size)
        {
            return Font(size, FontStyle.Regular);
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font(FontFamily, size, style, GraphicsUnit.Point);
        }

        public static Padding PagePaddingValue
        {
            get { return new Padding(PagePadding); }
        }

        public static Padding CardPaddingValue
        {
            get { return new Padding(CardPadding); }
        }
    }
}
