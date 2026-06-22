using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class SettingsPage : UserControl
    {
        private readonly BackupService _backupService;
        private readonly TextBox _storeNameTextBox;
        private readonly Label _databasePathLabel;
        private readonly Label _configPathLabel;
        private readonly Label _backupPathLabel;
        private readonly Label _exportPathLabel;
        private readonly Label _initializedLabel;
        private readonly Label _versionLabel;
        private ListBox _recentBackupListBox;

        public SettingsPage()
        {
            _backupService = new BackupService();

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
                AutoScroll = true,
                BackColor = BackColor
            };

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 7,
                Height = 330,
                BackColor = Color.White,
                Padding = new Padding(24),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _storeNameTextBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 12F) };
            _databasePathLabel = CreateValueLabel();
            _configPathLabel = CreateValueLabel();
            _backupPathLabel = CreateValueLabel();
            _exportPathLabel = CreateValueLabel();
            _initializedLabel = CreateValueLabel();
            _versionLabel = CreateValueLabel();

            AddRow(table, 0, "店铺名称", _storeNameTextBox);
            AddRow(table, 1, "数据库路径", _databasePathLabel);
            AddRow(table, 2, "配置文件路径", _configPathLabel);
            AddRow(table, 3, "备份目录", _backupPathLabel);
            AddRow(table, 4, "导出目录", _exportPathLabel);
            AddRow(table, 5, "是否已初始化", _initializedLabel);
            AddRow(table, 6, "程序版本", _versionLabel);

            FlowLayoutPanel actions = BuildActionPanel();
            Panel recentBackupPanel = BuildRecentBackupPanel();

            Label noteLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                Text = "PIN 和恢复密钥不会明文显示。备份包包含本机经营数据，请妥善保存。",
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft
            };

            contentPanel.Controls.Add(noteLabel);
            contentPanel.Controls.Add(recentBackupPanel);
            contentPanel.Controls.Add(actions);
            contentPanel.Controls.Add(table);

            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            LoadConfig();
        }

        private FlowLayoutPanel BuildActionPanel()
        {
            Button saveButton = CreateActionButton("保存店铺名称", Color.FromArgb(0, 123, 255));
            saveButton.Click += SaveButton_Click;

            Button backupButton = CreateActionButton("立即备份", Color.FromArgb(40, 167, 69));
            backupButton.Click += BackupButton_Click;

            Button backupToButton = CreateActionButton("备份到其他位置", Color.FromArgb(23, 162, 184));
            backupToButton.Click += BackupToButton_Click;

            Button restoreButton = CreateActionButton("从备份恢复", Color.FromArgb(255, 193, 7));
            restoreButton.ForeColor = Color.FromArgb(33, 37, 41);
            restoreButton.Click += RestoreButton_Click;

            Button openBackupButton = CreateActionButton("打开备份目录", Color.FromArgb(108, 117, 125));
            openBackupButton.Click += OpenBackupButton_Click;

            Button openExportButton = CreateActionButton("打开导出目录", Color.FromArgb(108, 117, 125));
            openExportButton.Click += OpenExportButton_Click;

            Button regenerateButton = CreateActionButton("重新生成恢复密钥", Color.FromArgb(220, 53, 69));
            regenerateButton.Click += RegenerateButton_Click;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 132,
                Padding = new Padding(0, 18, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor
            };
            actions.Controls.Add(saveButton);
            actions.Controls.Add(backupButton);
            actions.Controls.Add(backupToButton);
            actions.Controls.Add(restoreButton);
            actions.Controls.Add(openBackupButton);
            actions.Controls.Add(openExportButton);
            actions.Controls.Add(regenerateButton);
            return actions;
        }

        private Panel BuildRecentBackupPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 190,
                BackColor = Color.White,
                Padding = new Padding(18, 12, 18, 14)
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "最近备份",
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _recentBackupListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F),
                IntegralHeight = false
            };

            panel.Controls.Add(_recentBackupListBox);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private void LoadConfig()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            _storeNameTextBox.Text = config.StoreName;
            _databasePathLabel.Text = config.DatabasePath;
            _configPathLabel.Text = AppPaths.ConfigFilePath;
            _backupPathLabel.Text = config.BackupPath;
            _exportPathLabel.Text = AppPaths.ExportDirectory;
            _initializedLabel.Text = config.IsInitialized ? "是" : "否";
            _versionLabel.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LoadRecentBackups();
        }

        private void LoadRecentBackups()
        {
            _recentBackupListBox.Items.Clear();

            foreach (BackupFileInfo backup in _backupService.GetRecentBackups(10))
            {
                _recentBackupListBox.Items.Add(backup.DisplayText);
            }

            if (_recentBackupListBox.Items.Count == 0)
            {
                _recentBackupListBox.Items.Add("暂无备份文件");
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            AppConfigService.UpdateStoreName(_storeNameTextBox.Text);
            LoadConfig();
            MessageBox.Show("店铺名称已保存。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BackupButton_Click(object sender, EventArgs e)
        {
            try
            {
                BackupResult result = _backupService.CreateManualBackup();
                LoadRecentBackups();
                MessageBox.Show("备份成功：\r\n" + result.FilePath, "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("备份失败：\r\n" + ex.Message, "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BackupToButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择备份保存位置";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    BackupResult result = _backupService.CreateManualBackupTo(dialog.SelectedPath);
                    LoadRecentBackups();
                    MessageBox.Show("备份成功：\r\n" + result.FilePath, "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("备份失败：\r\n" + ex.Message, "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            DialogResult confirm = MessageBox.Show(
                "恢复会覆盖当前数据，系统会先自动备份当前数据，确认继续吗？",
                "确认从备份恢复",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择备份文件";
                dialog.Filter = "备份包或数据库|*.zip;*.db|备份包 (*.zip)|*.zip|SQLite 数据库 (*.db)|*.db";
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    RestoreResult result = _backupService.RestoreFromBackup(dialog.FileName);
                    LoadConfig();
                    MessageBox.Show(
                        "恢复完成，请重启软件后继续使用。\r\n恢复前备份：\r\n" + result.PreRestoreBackupPath,
                        "恢复完成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("恢复失败，当前数据未主动删除：\r\n" + ex.Message, "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenBackupButton_Click(object sender, EventArgs e)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            OpenDirectory(config.BackupPath);
        }

        private void OpenExportButton_Click(object sender, EventArgs e)
        {
            OpenDirectory(AppPaths.ExportDirectory);
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

        private static void OpenDirectory(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Process.Start(directory);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败：\r\n" + ex.Message, "打开目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                Size = new Size(156, 42),
                Margin = new Padding(0, 0, 12, 10),
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
