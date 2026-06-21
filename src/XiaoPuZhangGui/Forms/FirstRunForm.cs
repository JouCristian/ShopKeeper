using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class FirstRunForm : Form
    {
        private readonly TextBox _storeNameTextBox;
        private readonly TextBox _pinTextBox;
        private readonly TextBox _confirmPinTextBox;

        public FirstRunForm()
        {
            Text = "首次使用初始化";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 420);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            AppConfig config = AppConfigService.LoadOrCreateDefault();

            Label titleLabel = new Label
            {
                Text = "欢迎使用小铺掌柜",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(36, 28),
                Size = new Size(440, 44)
            };

            Label noteLabel = new Label
            {
                Text = "请先设置店铺名称和 6 位数字 PIN。所有信息仅保存在本机。",
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new Point(38, 80),
                Size = new Size(430, 48)
            };

            _storeNameTextBox = CreateTextBox(170);
            _storeNameTextBox.Text = config.StoreName;

            _pinTextBox = CreatePinTextBox(235);
            _confirmPinTextBox = CreatePinTextBox(300);

            Controls.Add(titleLabel);
            Controls.Add(noteLabel);
            Controls.Add(CreateLabel("店铺名称", 145));
            Controls.Add(_storeNameTextBox);
            Controls.Add(CreateLabel("设置 6 位 PIN", 210));
            Controls.Add(_pinTextBox);
            Controls.Add(CreateLabel("再次确认 PIN", 275));
            Controls.Add(_confirmPinTextBox);

            Button initializeButton = new Button
            {
                Text = "完成初始化",
                Location = new Point(300, 360),
                Size = new Size(160, 42),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            initializeButton.FlatAppearance.BorderSize = 0;
            initializeButton.Click += InitializeButton_Click;

            Controls.Add(initializeButton);
            AcceptButton = initializeButton;
        }

        private void InitializeButton_Click(object sender, EventArgs e)
        {
            string pin = _pinTextBox.Text.Trim();
            string confirmPin = _confirmPinTextBox.Text.Trim();

            if (!AuthService.IsValidPin(pin))
            {
                MessageBox.Show("PIN 必须是 6 位数字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _pinTextBox.Focus();
                return;
            }

            if (pin != confirmPin)
            {
                MessageBox.Show("两次输入的 PIN 不一致，请重新输入。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _confirmPinTextBox.SelectAll();
                _confirmPinTextBox.Focus();
                return;
            }

            string recoveryKey = AuthService.InitializeFirstRun(_storeNameTextBox.Text, pin);
            using (RecoveryKeyDisplayForm recoveryKeyForm = new RecoveryKeyDisplayForm(recoveryKey))
            {
                recoveryKeyForm.ShowDialog(this);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label CreateLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                Location = new Point(40, top),
                Size = new Size(140, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static TextBox CreateTextBox(int top)
        {
            return new TextBox
            {
                Location = new Point(180, top),
                Size = new Size(280, 30),
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular)
            };
        }

        private static TextBox CreatePinTextBox(int top)
        {
            TextBox textBox = CreateTextBox(top);
            textBox.MaxLength = 6;
            textBox.PasswordChar = '*';
            textBox.KeyPress += PinTextBox_KeyPress;
            return textBox;
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
