using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiStyleHelper
    {
        public static readonly Color PageBackground = Color.FromArgb(248, 249, 250);
        public static readonly Color Primary = Color.FromArgb(0, 123, 255);
        public static readonly Color Success = Color.FromArgb(40, 167, 69);
        public static readonly Color WarningBackground = Color.FromArgb(255, 243, 205);
        public static readonly Color Text = Color.FromArgb(33, 37, 41);
        public static readonly Color MutedText = Color.FromArgb(73, 80, 87);

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
            return new Label
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = MutedText,
                BackColor = Color.White,
                Padding = new Padding(12, 0, 0, 0),
                Visible = false
            };
        }

        public static void StyleButton(Button button, Color color)
        {
            button.Height = 40;
            button.BackColor = color;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderSize = 0;
        }
    }
}
