using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class RenameConversationForm : Form
    {
        private readonly TextBox _titleTextBox;

        public RenameConversationForm(string currentTitle)
        {
            Text = "重命名对话";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 180);
            BackColor = Color.White;
            Font = UiTheme.Font(11F);

            Controls.Add(new Label
            {
                Text = "对话标题",
                Location = new Point(28, 26),
                Size = new Size(110, 30),
                TextAlign = ContentAlignment.MiddleLeft
            });

            _titleTextBox = new TextBox
            {
                Text = currentTitle ?? string.Empty,
                Location = new Point(28, 64),
                Size = new Size(360, 34),
                Font = UiTheme.Font(11F)
            };
            UiComponentHelper.CenterTextBoxContent(_titleTextBox);
            Controls.Add(_titleTextBox);

            Button saveButton = UiComponentHelper.CreatePrimaryButton("保存", 96);
            saveButton.Location = new Point(188, 120);
            saveButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(_titleTextBox.Text))
                {
                    _titleTextBox.Focus();
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            Button cancelButton = UiComponentHelper.CreateSecondaryButton("取消", 96);
            cancelButton.Location = new Point(292, 120);
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(saveButton);
            Controls.Add(cancelButton);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        public string NewTitle
        {
            get { return _titleTextBox.Text.Trim(); }
        }
    }
}
