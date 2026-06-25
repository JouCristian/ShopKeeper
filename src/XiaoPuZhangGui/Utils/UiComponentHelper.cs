using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiComponentHelper
    {
        public static Button CreatePrimaryButton(string text, int width)
        {
            return CreateButton(text, width, UiTheme.PrimaryBlue, Color.White, Color.Empty);
        }

        public static Button CreateSecondaryButton(string text, int width)
        {
            return CreateButton(text, width, Color.White, UiTheme.TextPrimary, UiTheme.CardBorder);
        }

        public static Button CreateSuccessButton(string text, int width)
        {
            return CreateButton(text, width, UiTheme.SuccessGreen, Color.White, Color.Empty);
        }

        public static Button CreateInfoButton(string text, int width)
        {
            return CreateButton(text, width, UiTheme.InfoCyan, Color.White, Color.Empty);
        }

        public static Button CreateWarningButton(string text, int width)
        {
            return CreateButton(text, width, UiTheme.WarningOrange, Color.White, Color.Empty);
        }

        public static Button CreateDangerButton(string text, int width)
        {
            return CreateButton(text, width, UiTheme.DangerRed, Color.White, Color.Empty);
        }

        public static Button CreateButton(string text, int width, Color backColor, Color foreColor, Color borderColor)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = UiTheme.ButtonHeight,
                Margin = new Padding(0, 0, UiTheme.Gap, UiTheme.Gap),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.Font(10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = borderColor == Color.Empty ? 0 : 1;
            if (borderColor != Color.Empty)
            {
                button.FlatAppearance.BorderColor = borderColor;
            }

            ApplyButtonChrome(button, backColor, borderColor);
            return button;
        }

        public static Panel CreateCardPanel()
        {
            return CreateCardPanel(UiTheme.CardPaddingValue);
        }

        public static Panel CreateCardPanel(Padding padding)
        {
            return CreateCardPanel(padding, UiTheme.CardBackground, UiTheme.CardBorder);
        }

        public static Panel CreateCardPanel(Padding padding, Color backColor, Color borderColor)
        {
            return new ThemedCardPanel
            {
                BackColor = backColor,
                BorderColor = borderColor,
                Padding = padding
            };
        }

        public static void ApplyButtonChrome(Button button, Color backColor, Color borderColor)
        {
            if (button == null)
            {
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = Lighten(backColor, 16);
            button.FlatAppearance.MouseDownBackColor = Darken(backColor, 10);
            if (borderColor != Color.Empty)
            {
                button.FlatAppearance.BorderColor = borderColor;
            }
        }

        public static Color Lighten(Color color, int amount)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(255, color.R + amount),
                Math.Min(255, color.G + amount),
                Math.Min(255, color.B + amount));
        }

        public static Color Darken(Color color, int amount)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(0, color.R - amount),
                Math.Max(0, color.G - amount),
                Math.Max(0, color.B - amount));
        }

        public static Label CreatePageTitle(string title)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                Height = 56,
                Text = title,
                Font = UiTheme.Font(31F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        public static Label CreatePageSubtitle(string text)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = text,
                Font = UiTheme.Font(11.5F),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        public static Label CreateEmptyStateLabel(string text, string iconName)
        {
            return new EmptyStateLabel(text, iconName)
            {
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.CardBackground,
                Font = UiTheme.Font(10.5F),
                Visible = false
            };
        }

        public static Label CreateIconTextLabel(string text, string iconName, int iconSize)
        {
            return CreateIconTextLabel(text, iconName, iconSize, Color.Empty);
        }

        public static Label CreateIconTextLabel(string text, string iconName, int iconSize, Color iconColor)
        {
            return new IconTextLabel(text, iconName, iconSize)
            {
                ForeColor = UiTheme.TextSecondary,
                BackColor = UiTheme.CardBackground,
                Font = UiTheme.Font(10.5F, FontStyle.Bold),
                IconColor = iconColor
            };
        }

        public static void CenterButtonIcon(Button button)
        {
            button.Padding = Padding.Empty;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        public static void NormalizeFilterBar(FlowLayoutPanel panel)
        {
            if (panel == null || !ContainsInputControl(panel))
            {
                return;
            }

            panel.AutoScroll = false;
            panel.Padding = new Padding(0, 16, 0, 0);

            foreach (Control control in panel.Controls)
            {
                if (control is Label)
                {
                    control.Height = UiTheme.ButtonHeight;
                    control.Margin = new Padding(0, CenterOffset(control.Height), 6, 0);
                    ((Label)control).TextAlign = ContentAlignment.MiddleLeft;
                    control.ForeColor = UiTheme.TextSecondary;
                    continue;
                }

                if (control is Button)
                {
                    control.Height = UiTheme.ButtonHeight;
                    control.Margin = new Padding(0, CenterOffset(control.Height), 12, 0);
                    ApplyButtonChrome((Button)control, control.BackColor, Color.Empty);
                    continue;
                }

                if (control is TextBox || control is ComboBox || control is NumericUpDown || control is DateTimePicker)
                {
                    ApplyInputMetric(control);
                    control.Margin = new Padding(0, CenterOffset(control.Height), 12, 0);
                }
            }
        }

        public static void NormalizeControlMetrics(Control root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Control control in root.Controls)
            {
                ApplyControlMetric(control);
                NormalizeControlMetrics(control);
            }
        }

        private static void ApplyControlMetric(Control control)
        {
            FlowLayoutPanel flowLayoutPanel = control as FlowLayoutPanel;
            if (flowLayoutPanel != null)
            {
                NormalizeFilterBar(flowLayoutPanel);
            }

            if (control is Button)
            {
                if (object.Equals(control.Tag, "KeepSize"))
                {
                    ApplyButtonChrome((Button)control, control.BackColor, Color.Empty);
                    return;
                }

                control.Height = UiTheme.ButtonHeight;
                ApplyButtonChrome((Button)control, control.BackColor, Color.Empty);
                return;
            }

            if (control is TextBox)
            {
                TextBox textBox = (TextBox)control;
                if (!textBox.Multiline)
                {
                    ApplyInputMetric(textBox);
                }

                return;
            }

            if (control is ComboBox || control is NumericUpDown || control is DateTimePicker)
            {
                ApplyInputMetric(control);
            }
        }

        private static void ApplyInputMetric(Control control)
        {
            TextBox textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.AutoSize = false;
                CenterTextBoxContent(textBox);
            }

            control.Height = UiTheme.InputHeight;
        }

        public static void CenterTextBoxContent(TextBox textBox)
        {
            if (textBox == null || textBox.Multiline || textBox.PasswordChar != '\0' || textBox.UseSystemPasswordChar)
            {
                return;
            }

            textBox.AutoSize = false;
            textBox.Multiline = true;
            textBox.AcceptsReturn = false;
            textBox.WordWrap = false;
            textBox.ScrollBars = ScrollBars.None;
            textBox.BorderStyle = BorderStyle.Fixed3D;

            EventHandler updateEditRect = delegate { ApplyCenteredEditRectangle(textBox); };
            textBox.HandleCreated += updateEditRect;
            textBox.Resize += updateEditRect;
            textBox.FontChanged += updateEditRect;
            textBox.KeyPress += SuppressSingleLineTextBoxReturn;
            ApplyCenteredEditRectangle(textBox);
        }

        private static void SuppressSingleLineTextBoxReturn(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                e.Handled = true;
            }
        }

        private static void ApplyCenteredEditRectangle(TextBox textBox)
        {
            if (textBox == null || !textBox.IsHandleCreated)
            {
                return;
            }

            int textHeight = TextRenderer.MeasureText("田", textBox.Font).Height;
            int top = Math.Max(1, (textBox.ClientSize.Height - textHeight) / 2);
            RECT rect = new RECT
            {
                Left = 4,
                Top = top,
                Right = Math.Max(4, textBox.ClientSize.Width - 4),
                Bottom = Math.Min(textBox.ClientSize.Height - 1, top + textHeight + 2)
            };

            SendMessage(textBox.Handle, EM_SETRECTNP, IntPtr.Zero, ref rect);
        }

        private static bool ContainsInputControl(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (control is TextBox || control is ComboBox || control is NumericUpDown || control is DateTimePicker)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CenterOffset(int controlHeight)
        {
            return Math.Max(0, (UiTheme.ButtonHeight - controlHeight) / 2);
        }

        private const int EM_SETRECTNP = 0x00B4;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static Panel CreatePageHeader(string title, string subtitle, Control action, string illustrationName)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = string.IsNullOrEmpty(illustrationName) ? 112 : 176,
                BackColor = UiTheme.CardBackground,
                Padding = string.IsNullOrEmpty(illustrationName)
                    ? new Padding(28, 13, 28, 13)
                    : new Padding(28, 18, 28, 18)
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = string.IsNullOrEmpty(illustrationName) ? 2 : 3,
                RowCount = 1,
                BackColor = UiTheme.CardBackground
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, action == null ? 1F : Math.Max(action.Width + 18, 136)));
            if (!string.IsNullOrEmpty(illustrationName))
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430F));
            }

            Panel textPanel = CreateHeaderTextPanel(title, subtitle);
            layout.Controls.Add(textPanel, 0, 0);

            if (action != null)
            {
                action.Dock = DockStyle.Fill;
                action.Margin = new Padding(0, 18, 16, 18);
                layout.Controls.Add(action, 1, 0);
            }

            if (!string.IsNullOrEmpty(illustrationName))
            {
                PictureBox picture = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = UiAssetHelper.GetIllustration(illustrationName, new Size(860, 360)),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = UiTheme.CardBackground,
                    Margin = new Padding(12, 0, 0, 0)
                };
                layout.Controls.Add(picture, 2, 0);
            }

            panel.Controls.Add(layout);
            return panel;
        }

        public static Panel CreatePageHeader(string title, string subtitle, string illustrationName)
        {
            return CreatePageHeader(title, subtitle, null, illustrationName);
        }

        private static Panel CreateHeaderTextPanel(string title, string subtitle)
        {
            Panel textPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.CardBackground
            };

            Label titleLabel = CreatePageTitle(title);
            Label subtitleLabel = CreatePageSubtitle(subtitle);
            titleLabel.Dock = DockStyle.None;
            subtitleLabel.Dock = DockStyle.None;

            textPanel.Controls.Add(titleLabel);
            textPanel.Controls.Add(subtitleLabel);
            EventHandler arrangeText = delegate
            {
                int availableWidth = Math.Max(0, textPanel.ClientSize.Width);
                int blockHeight = titleLabel.Height + subtitleLabel.Height;
                int top = Math.Max(0, (textPanel.ClientSize.Height - blockHeight) / 2);

                titleLabel.Location = new Point(0, top);
                titleLabel.Width = availableWidth;
                subtitleLabel.Location = new Point(0, titleLabel.Bottom);
                subtitleLabel.Width = availableWidth;
            };
            textPanel.Resize += arrangeText;
            arrangeText(textPanel, EventArgs.Empty);

            return textPanel;
        }
    }

    internal sealed class ThemedCardPanel : Panel
    {
        public ThemedCardPanel()
        {
            DoubleBuffered = true;
            BorderStyle = BorderStyle.None;
            BorderColor = UiTheme.CardBorder;
        }

        public Color BorderColor { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(BorderColor))
            {
                Rectangle bounds = ClientRectangle;
                bounds.Width -= 1;
                bounds.Height -= 1;
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }
    }

    internal sealed class EmptyStateLabel : Label
    {
        private readonly string _illustrationName;

        public EmptyStateLabel(string text, string illustrationName)
        {
            Text = text;
            _illustrationName = illustrationName;
            TextAlign = ContentAlignment.MiddleCenter;
            DoubleBuffered = true;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                BringToFront();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush background = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            int availableWidth = Math.Max(1, Width - 48);
            int availableHeight = Math.Max(1, Height - 36);
            int targetWidth = Math.Max(180, Math.Min(360, availableWidth * 2 / 3));
            int targetHeight = Math.Max(120, Math.Min(220, availableHeight * 2 / 3));
            Image illustration = UiAssetHelper.GetIllustration(_illustrationName, new Size(targetWidth, targetHeight));

            int imageWidth = illustration == null ? targetWidth : illustration.Width;
            int imageHeight = illustration == null ? targetHeight : illustration.Height;
            int blockHeight = imageHeight + 14 + 28 + 24;
            int top = Math.Max(12, (Height - blockHeight) / 2);
            int imageLeft = Math.Max(0, (Width - imageWidth) / 2);

            if (illustration != null)
            {
                e.Graphics.DrawImage(illustration, new Rectangle(imageLeft, top, imageWidth, imageHeight));
            }

            Rectangle titleBounds = new Rectangle(24, top + imageHeight + 14, Math.Max(0, Width - 48), 30);
            using (Font titleFont = UiTheme.Font(11.5F, FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    Text,
                    titleFont,
                    titleBounds,
                    UiTheme.TextPrimary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            Rectangle hintBounds = new Rectangle(24, titleBounds.Bottom + 2, Math.Max(0, Width - 48), 24);
            TextRenderer.DrawText(
                e.Graphics,
                "可通过上方操作开始录入或刷新数据",
                Font,
                hintBounds,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class IconTextLabel : Label
    {
        private readonly string _iconName;
        private readonly int _iconSize;

        public Color IconColor { get; set; }

        public IconTextLabel(string text, string iconName, int iconSize)
        {
            Text = text;
            _iconName = iconName;
            _iconSize = iconSize;
            TextAlign = ContentAlignment.MiddleLeft;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush background = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            int iconX = 12;
            int iconY = Math.Max(0, (Height - _iconSize) / 2);
            Image icon = IconColor == Color.Empty
                ? UiAssetHelper.GetIcon(_iconName, _iconSize)
                : UiAssetHelper.GetIcon(_iconName, _iconSize, IconColor);
            if (icon != null)
            {
                e.Graphics.DrawImage(icon, new Rectangle(iconX, iconY, _iconSize, _iconSize));
            }

            int textX = iconX + _iconSize + 10;
            Rectangle textBounds = new Rectangle(textX, 0, Math.Max(0, Width - textX - 8), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
