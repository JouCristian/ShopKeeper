using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class RecoveryKeyDisplayForm : Form
    {
        public RecoveryKeyDisplayForm(string recoveryKey)
        {
            Text = "恢复密钥";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(640, 360);
            Font = UiTheme.Font(11F);
            BackColor = UiTheme.PageBackground;

            Panel cardPanel = UiComponentHelper.CreateCardPanel(new Padding(24));
            cardPanel.Location = new Point(24, 24);
            cardPanel.Size = new Size(592, 312);

            Label titleLabel = new Label
            {
                Text = "请保存恢复密钥",
                Font = UiTheme.Font(22F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(258, 28),
                Size = new Size(280, 42)
            };

            Label noteLabel = new Label
            {
                Text = "恢复密钥只显示一次。请抄写或保存，忘记 PIN 时需要用它重置。",
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(260, 76),
                Size = new Size(280, 52)
            };

            PictureBox heroBox = new PictureBox
            {
                Location = new Point(22, 22),
                Size = new Size(210, 270),
                Image = UiAssetHelper.GetIllustration("recovery_key_hero", new Size(410, 540)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UiTheme.CardBackground
            };

            TextBox keyTextBox = new TextBox
            {
                Text = recoveryKey,
                ReadOnly = true,
                Location = new Point(260, 144),
                Size = new Size(280, 38),
                Font = new Font("Consolas", 16F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            UiComponentHelper.CenterTextBoxContent(keyTextBox);

            Button copyButton = UiComponentHelper.CreateSecondaryButton("复制密钥", 118);
            copyButton.Location = new Point(260, 218);
            copyButton.Click += delegate
            {
                try
                {
                    Clipboard.SetText(recoveryKey);
                    MessageBox.Show("恢复密钥已复制。", "复制密钥", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch
                {
                    MessageBox.Show("复制失败，请手动选中密钥后复制。", "复制密钥", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            Button okButton = UiComponentHelper.CreatePrimaryButton("我已保存，继续", 150);
            okButton.Location = new Point(390, 218);
            okButton.DialogResult = DialogResult.OK;

            cardPanel.Controls.Add(heroBox);
            cardPanel.Controls.Add(titleLabel);
            cardPanel.Controls.Add(noteLabel);
            cardPanel.Controls.Add(keyTextBox);
            cardPanel.Controls.Add(copyButton);
            cardPanel.Controls.Add(okButton);
            Controls.Add(cardPanel);

            AcceptButton = okButton;
        }
    }
}
