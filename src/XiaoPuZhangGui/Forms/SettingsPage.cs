using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class SettingsPage : UserControl
    {
        private readonly TextBox _storeNameTextBox;
        private readonly Label _databasePathLabel;
        private readonly Label _backupPathLabel;
        private readonly Label _initializedLabel;
        private readonly Label _versionLabel;

        public SettingsPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "系统设置",
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

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 5,
                Height = 250,
                BackColor = Color.White,
                Padding = new Padding(24),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _storeNameTextBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 12F) };
            _databasePathLabel = CreateValueLabel();
            _backupPathLabel = CreateValueLabel();
            _initializedLabel = CreateValueLabel();
            _versionLabel = CreateValueLabel();

            AddRow(table, 0, "店铺名称", _storeNameTextBox);
            AddRow(table, 1, "数据库路径", _databasePathLabel);
            AddRow(table, 2, "备份路径", _backupPathLabel);
            AddRow(table, 3, "是否已初始化", _initializedLabel);
            AddRow(table, 4, "程序版本", _versionLabel);

            Button saveButton = CreateActionButton("保存店铺名称", Color.FromArgb(0, 123, 255));
            saveButton.Click += SaveButton_Click;

            Button openBackupButton = CreateActionButton("打开备份目录", Color.FromArgb(40, 167, 69));
            openBackupButton.Click += OpenBackupButton_Click;

            Button regenerateButton = CreateActionButton("重新生成恢复密钥", Color.FromArgb(220, 53, 69));
            regenerateButton.Click += RegenerateButton_Click;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(0, 18, 0, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            actions.Controls.Add(saveButton);
            actions.Controls.Add(openBackupButton);
            actions.Controls.Add(regenerateButton);

            Label noteLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 60,
                Text = "PIN 和恢复密钥不会明文显示。重新生成恢复密钥后，旧恢复密钥将立即失效。",
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft
            };

            contentPanel.Controls.Add(noteLabel);
            contentPanel.Controls.Add(actions);
            contentPanel.Controls.Add(table);

            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            LoadConfig();
        }

        private void LoadConfig()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            _storeNameTextBox.Text = config.StoreName;
            _databasePathLabel.Text = config.DatabasePath;
            _backupPathLabel.Text = config.BackupPath;
            _initializedLabel.Text = config.IsInitialized ? "是" : "否";
            _versionLabel.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            AppConfigService.UpdateStoreName(_storeNameTextBox.Text);
            LoadConfig();
            MessageBox.Show("店铺名称已保存。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenBackupButton_Click(object sender, EventArgs e)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            if (!Directory.Exists(config.BackupPath))
            {
                Directory.CreateDirectory(config.BackupPath);
            }

            Process.Start(config.BackupPath);
        }

        private void RegenerateButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "重新生成恢复密钥后，旧恢复密钥将失效。确定要继续吗？",
                "确认重新生成",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            string recoveryKey = AuthService.RegenerateRecoveryKey();
            using (RecoveryKeyDisplayForm recoveryKeyForm = new RecoveryKeyDisplayForm(recoveryKey))
            {
                recoveryKeyForm.ShowDialog(this);
            }
        }

        private static void AddRow(TableLayoutPanel table, int rowIndex, string labelText, Control valueControl)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(73, 80, 87)
            };

            table.Controls.Add(label, 0, rowIndex);
            table.Controls.Add(valueControl, 1, rowIndex);
        }

        private static Label CreateValueLabel()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(33, 37, 41)
            };
        }

        private static Button CreateActionButton(string text, Color color)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(170, 42),
                Margin = new Padding(0, 0, 12, 0),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
