using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

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
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(680, 460);
            Font = UiTheme.Font(11F);
            BackColor = UiTheme.PageBackground;

            AppConfig config = AppConfigService.LoadOrCreateDefault();

            Panel cardPanel = UiComponentHelper.CreateCardPanel(new Padding(26));
            cardPanel.Location = new Point(24, 24);
            cardPanel.Size = new Size(632, 412);

            PictureBox heroBox = new PictureBox
            {
                Location = new Point(350, 25),
                Size = new Size(260, 310),
                Image = UiAssetHelper.GetIllustration("first_run_hero", new Size(500, 600)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UiTheme.CardBackground
            };

            Label titleLabel = new Label
            {
                Text = "欢迎使用小铺掌柜",
                Font = UiTheme.Font(22F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(28, 24),
                Size = new Size(330, 42)
            };

            Label noteLabel = new Label
            {
                Text = "请先设置店铺名称和 6 位数字 PIN。所有信息仅保存在本机。",
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(30, 72),
                Size = new Size(320, 48)
            };

            _storeNameTextBox = CreateTextBox(160);
            UiComponentHelper.CenterTextBoxContent(_storeNameTextBox);
            _storeNameTextBox.Text = config.StoreName;

            _pinTextBox = CreatePinTextBox(228);
            _confirmPinTextBox = CreatePinTextBox(296);

            cardPanel.Controls.Add(heroBox);
            cardPanel.Controls.Add(titleLabel);
            cardPanel.Controls.Add(noteLabel);
            cardPanel.Controls.Add(CreateLabel("店铺名称", 132));
            cardPanel.Controls.Add(_storeNameTextBox);
            cardPanel.Controls.Add(CreateLabel("设置 6 位 PIN", 200));
            cardPanel.Controls.Add(_pinTextBox);
            cardPanel.Controls.Add(CreateLabel("再次确认 PIN", 268));
            cardPanel.Controls.Add(_confirmPinTextBox);

            Button initializeButton = UiComponentHelper.CreatePrimaryButton("完成初始化", 150);
            initializeButton.Location = new Point(418, 344);
            initializeButton.Click += InitializeButton_Click;

            cardPanel.Controls.Add(initializeButton);
            Controls.Add(cardPanel);
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
                Location = new Point(30, top),
                Size = new Size(140, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };
        }

        private static TextBox CreateTextBox(int top)
        {
            return new TextBox
            {
                Location = new Point(30, top),
                Size = new Size(300, 32),
                Font = UiTheme.Font(12F)
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
