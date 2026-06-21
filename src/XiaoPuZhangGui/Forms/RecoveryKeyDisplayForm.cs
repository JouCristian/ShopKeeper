using System.Drawing;
using System.Windows.Forms;

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
            ClientSize = new Size(560, 300);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            Label titleLabel = new Label
            {
                Text = "请保存恢复密钥",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(34, 24),
                Size = new Size(480, 40)
            };

            Label noteLabel = new Label
            {
                Text = "恢复密钥只显示一次。请抄写或保存，忘记 PIN 时需要用它重置。",
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new Point(36, 74),
                Size = new Size(480, 48)
            };

            TextBox keyTextBox = new TextBox
            {
                Text = recoveryKey,
                ReadOnly = true,
                Location = new Point(38, 135),
                Size = new Size(480, 38),
                Font = new Font("Consolas", 16F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            Button okButton = new Button
            {
                Text = "我已保存，继续",
                Location = new Point(350, 225),
                Size = new Size(170, 42),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            okButton.FlatAppearance.BorderSize = 0;

            Controls.Add(titleLabel);
            Controls.Add(noteLabel);
            Controls.Add(keyTextBox);
            Controls.Add(okButton);

            AcceptButton = okButton;
        }
    }
}
