using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class LoginForm : Form
    {
        private readonly TextBox _pinTextBox;

        public LoginForm()
        {
            Text = "小铺掌柜 AI智能版登录";
            StartPosition = FormStartPosition.CenterScreen;
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
                Text = "请输入 6 位 PIN",
                Font = UiTheme.Font(22F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(276, 42),
                Size = new Size(250, 42),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label noteLabel = new Label
            {
                Text = "本机离线验证，AI 助手需进入系统后单独联网配置。",
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(278, 88),
                Size = new Size(250, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            PictureBox heroBox = new PictureBox
            {
                Location = new Point(28, -15),
                Size = new Size(200, 340),
                Image = UiAssetHelper.GetIllustration("login_hero", new Size(330, 480)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UiTheme.CardBackground
            };

            _pinTextBox = new TextBox
            {
                Location = new Point(278, 132),
                Size = new Size(238, 38),
                Font = UiTheme.Font(16F),
                MaxLength = 6,
                PasswordChar = '*',
                TextAlign = HorizontalAlignment.Center
            };
            _pinTextBox.KeyPress += PinTextBox_KeyPress;

            Button loginButton = UiComponentHelper.CreatePrimaryButton("登录", 112);
            loginButton.Location = new Point(278, 198);
            loginButton.Click += LoginButton_Click;

            Button resetButton = UiComponentHelper.CreateSecondaryButton("忘记 PIN", 112);
            resetButton.Location = new Point(404, 198);
            resetButton.Click += ResetButton_Click;

            cardPanel.Controls.Add(heroBox);
            cardPanel.Controls.Add(titleLabel);
            cardPanel.Controls.Add(noteLabel);
            cardPanel.Controls.Add(_pinTextBox);
            cardPanel.Controls.Add(loginButton);
            cardPanel.Controls.Add(resetButton);
            Controls.Add(cardPanel);

            AcceptButton = loginButton;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _pinTextBox.Focus();
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            if (AuthService.VerifyPin(_pinTextBox.Text.Trim()))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            MessageBox.Show("PIN 不正确，请重新输入。", "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _pinTextBox.SelectAll();
            _pinTextBox.Focus();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            using (ResetPinForm resetPinForm = new ResetPinForm())
            {
                if (resetPinForm.ShowDialog(this) == DialogResult.OK)
                {
                    MessageBox.Show("PIN 已重置，请使用新 PIN 登录。", "重置成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _pinTextBox.Clear();
                    _pinTextBox.Focus();
                }
            }
        }

        private static void PinTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }
    }
}
