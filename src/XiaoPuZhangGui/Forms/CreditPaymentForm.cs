using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class CreditPaymentForm : Form
    {
        private readonly CreditService _creditService;
        private readonly CreditRecord _record;
        private readonly NumericUpDown _amountNumeric;
        private readonly TextBox _remarkTextBox;

        public CreditPaymentForm(long creditRecordId)
        {
            _creditService = new CreditService();
            _record = _creditService.GetById(creditRecordId);

            Text = "登记还款";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 360);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            Controls.Add(new Label
            {
                Text = "登记还款",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(28, 20),
                Size = new Size(220, 40)
            });

            if (_record == null)
            {
                Controls.Add(new Label { Text = "未找到赊账记录。", Location = new Point(30, 90), Size = new Size(440, 40) });
                return;
            }

            Controls.Add(new Label
            {
                Text = string.Format("欠款人：{0}\r\n原始欠款：{1:N2}    已还：{2:N2}    剩余：{3:N2}",
                    _record.DebtorName,
                    _record.OriginalAmount,
                    _record.PaidAmount,
                    _record.RemainingAmount),
                Location = new Point(32, 76),
                Size = new Size(440, 70),
                ForeColor = Color.FromArgb(73, 80, 87)
            });

            Controls.Add(CreateLabel("本次还款", 36, 166, 90));
            _amountNumeric = new NumericUpDown
            {
                Location = new Point(130, 164),
                Size = new Size(150, 30),
                Minimum = 0,
                Maximum = 9999999,
                DecimalPlaces = 2,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Microsoft YaHei UI", 11F),
                Value = _record.RemainingAmount > 0 && _record.RemainingAmount <= 9999999 ? _record.RemainingAmount : 0
            };
            Controls.Add(_amountNumeric);

            Controls.Add(CreateLabel("还款备注", 36, 216, 90));
            _remarkTextBox = new TextBox
            {
                Location = new Point(130, 214),
                Size = new Size(320, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            Controls.Add(_remarkTextBox);

            Button saveButton = new Button
            {
                Text = "保存还款",
                Location = new Point(300, 292),
                Size = new Size(150, 42),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            string message;
            if (!_creditService.TryRegisterPayment(_record.Id, _amountNumeric.Value, DateTime.Today, _remarkTextBox.Text, out message))
            {
                MessageBox.Show(message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label CreateLabel(string text, int left, int top, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }
    }
}
