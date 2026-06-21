using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class LoginForm : Form
    {
        private readonly TextBox _pinTextBox;

        public LoginForm()
        {
            Text = "小铺掌柜登录";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 290);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            Label titleLabel = new Label
            {
                Text = "请输入 6 位 PIN",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(40, 34),
                Size = new Size(340, 46),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _pinTextBox = new TextBox
            {
                Location = new Point(95, 105),
                Size = new Size(240, 36),
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Regular),
                MaxLength = 6,
                PasswordChar = '*',
                TextAlign = HorizontalAlignment.Center
            };
            _pinTextBox.KeyPress += PinTextBox_KeyPress;

            Button loginButton = new Button
            {
                Text = "登录",
                Location = new Point(95, 165),
                Size = new Size(110, 42),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            loginButton.FlatAppearance.BorderSize = 0;
            loginButton.Click += LoginButton_Click;

            Button resetButton = new Button
            {
                Text = "忘记 PIN",
                Location = new Point(225, 165),
                Size = new Size(110, 42),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular)
            };
            resetButton.Click += ResetButton_Click;

            Controls.Add(titleLabel);
            Controls.Add(_pinTextBox);
            Controls.Add(loginButton);
            Controls.Add(resetButton);

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
