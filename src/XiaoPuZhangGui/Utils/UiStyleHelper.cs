using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiStyleHelper
    {
        public static readonly Color PageBackground = UiTheme.PageBackground;
        public static readonly Color Primary = UiTheme.PrimaryBlue;
        public static readonly Color Success = UiTheme.SuccessGreen;
        public static readonly Color WarningBackground = UiTheme.SoftOrange;
        public static readonly Color Text = UiTheme.TextPrimary;
        public static readonly Color MutedText = UiTheme.TextSecondary;

        public static FlowLayoutPanel CreateActionBar(int height)
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = height,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                BackColor = PageBackground,
                Padding = new Padding(0, 8, 0, 4)
            };
        }

        public static Label CreateEmptyLabel(string text)
        {
            return UiComponentHelper.CreateEmptyStateLabel(text, "empty/general");
        }

        public static void StyleButton(Button button, Color color)
        {
            button.Height = UiTheme.ButtonHeight;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = UiTheme.Font(10.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 0;
            UiComponentHelper.ApplyButtonChrome(button, color, Color.Empty);
        }

        public static void StyleSecondaryButton(Button button)
        {
            button.Height = UiTheme.ButtonHeight;
            button.BackColor = Color.White;
            button.ForeColor = UiTheme.TextPrimary;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = UiTheme.Font(10.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = UiTheme.CardBorder;
            UiComponentHelper.ApplyButtonChrome(button, Color.White, UiTheme.CardBorder);
        }

        public static void StyleDangerButton(Button button)
        {
            StyleButton(button, UiTheme.DangerRed);
        }
    }
}
