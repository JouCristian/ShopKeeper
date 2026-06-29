using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class AiQuickQuestionsForm : Form
    {
        private readonly List<TextBox> _textBoxes = new List<TextBox>();
        private readonly IList<string> _defaultQuestions;

        public AiQuickQuestionsForm(IList<string> currentQuestions, IList<string> defaultQuestions)
        {
            _defaultQuestions = defaultQuestions;
            Text = "编辑快捷问题";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 460);
            BackColor = Color.White;
            Font = UiTheme.Font(10.5F);

            Label title = new Label
            {
                Text = "快捷问题",
                Location = new Point(28, 22),
                Size = new Size(480, 30),
                Font = UiTheme.Font(15F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            Controls.Add(title);

            Label hint = new Label
            {
                Text = "每行一个常用问题，保存后会显示在 AI 对话顶部。最多保留 8 个。",
                Location = new Point(28, 58),
                Size = new Size(500, 28),
                ForeColor = UiTheme.TextSecondary
            };
            Controls.Add(hint);

            int top = 98;
            for (int index = 0; index < 8; index++)
            {
                Label number = new Label
                {
                    Text = (index + 1) + ".",
                    Location = new Point(28, top + 3),
                    Size = new Size(28, 30),
                    ForeColor = UiTheme.TextSecondary,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Controls.Add(number);

                TextBox textBox = new TextBox
                {
                    Location = new Point(58, top),
                    Size = new Size(470, 30),
                    Font = UiTheme.Font(10.5F)
                };
                if (currentQuestions != null && index < currentQuestions.Count)
                {
                    textBox.Text = currentQuestions[index];
                }

                UiComponentHelper.CenterTextBoxContent(textBox);
                _textBoxes.Add(textBox);
                Controls.Add(textBox);
                top += 36;
            }

            Button resetButton = UiComponentHelper.CreateSecondaryButton("恢复默认", 104);
            resetButton.Location = new Point(28, 400);
            resetButton.Click += delegate { FillDefaults(); };
            Controls.Add(resetButton);

            Button saveButton = UiComponentHelper.CreatePrimaryButton("保存", 96);
            saveButton.Location = new Point(328, 400);
            saveButton.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(saveButton);

            Button cancelButton = UiComponentHelper.CreateSecondaryButton("取消", 96);
            cancelButton.Location = new Point(432, 400);
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        public IList<string> Questions
        {
            get
            {
                List<string> questions = new List<string>();
                foreach (TextBox textBox in _textBoxes)
                {
                    string text = (textBox.Text ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        questions.Add(text);
                    }
                }

                return questions;
            }
        }

        private void FillDefaults()
        {
            for (int index = 0; index < _textBoxes.Count; index++)
            {
                _textBoxes[index].Text = _defaultQuestions != null && index < _defaultQuestions.Count
                    ? _defaultQuestions[index]
                    : string.Empty;
            }
        }
    }
}
