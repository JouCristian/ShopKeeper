using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class ResetPinForm : Form
    {
        private readonly TextBox _recoveryKeyTextBox;
        private readonly TextBox _newPinTextBox;
        private readonly TextBox _confirmPinTextBox;

        public ResetPinForm()
        {
            Text = "重置 PIN";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(540, 360);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            Label titleLabel = new Label
            {
                Text = "使用恢复密钥重置 PIN",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(36, 28),
                Size = new Size(450, 42)
            };

            _recoveryKeyTextBox = CreateTextBox(120);
            UiComponentHelper.CenterTextBoxContent(_recoveryKeyTextBox);
            _newPinTextBox = CreatePinTextBox(190);
            _confirmPinTextBox = CreatePinTextBox(250);

            Controls.Add(titleLabel);
            Controls.Add(CreateLabel("恢复密钥", 92));
            Controls.Add(_recoveryKeyTextBox);
            Controls.Add(CreateLabel("新 6 位 PIN", 162));
            Controls.Add(_newPinTextBox);
            Controls.Add(CreateLabel("再次确认 PIN", 222));
            Controls.Add(_confirmPinTextBox);

            Button saveButton = new Button
            {
                Text = "确认重置",
                Location = new Point(330, 305),
                Size = new Size(140, 40),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                BackColor = UiTheme.PrimaryBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;

            Controls.Add(saveButton);
            AcceptButton = saveButton;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            string newPin = _newPinTextBox.Text.Trim();
            string confirmPin = _confirmPinTextBox.Text.Trim();

            if (!AuthService.IsValidPin(newPin))
            {
                MessageBox.Show("新 PIN 必须是 6 位数字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _newPinTextBox.Focus();
                return;
            }

            if (newPin != confirmPin)
            {
                MessageBox.Show("两次输入的新 PIN 不一致。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _confirmPinTextBox.SelectAll();
                _confirmPinTextBox.Focus();
                return;
            }

            if (!AuthService.ResetPinWithRecoveryKey(_recoveryKeyTextBox.Text, newPin))
            {
                MessageBox.Show("恢复密钥不正确，请检查后重新输入。", "重置失败", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _recoveryKeyTextBox.SelectAll();
                _recoveryKeyTextBox.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label CreateLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                Location = new Point(38, top),
                Size = new Size(200, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static TextBox CreateTextBox(int top)
        {
            return new TextBox
            {
                Location = new Point(38, top),
                Size = new Size(450, 32),
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
