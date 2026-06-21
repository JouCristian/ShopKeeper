using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class PlaceholderPage : UserControl
    {
        public PlaceholderPage(string title, string description)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 20, 28, 28),
                BackColor = BackColor
            };

            Label descriptionLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 120,
                Text = description,
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Regular),
                ForeColor = Color.FromArgb(73, 80, 87),
                TextAlign = ContentAlignment.TopLeft
            };

            Label stageLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 52,
                Text = "第一阶段占位页面，后续迭代再接入完整业务。",
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft
            };

            contentPanel.Controls.Add(stageLabel);
            contentPanel.Controls.Add(descriptionLabel);

            Controls.Add(contentPanel);
            Controls.Add(titleLabel);
        }
    }
}
