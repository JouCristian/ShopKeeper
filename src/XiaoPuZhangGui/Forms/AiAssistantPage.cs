using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class AiAssistantPage : UserControl
    {
        private readonly NetworkStatusService _networkStatusService;
        private readonly AiSettingsService _settingsService;
        private readonly BusinessSummaryService _businessSummaryService;
        private readonly Action<NetworkStatusResult> _networkStatusChanged;
        private readonly Action _aiSettingsChanged;
        private NetworkStatusResult _networkStatus;
        private Label _messageLabel;
        private string _lastSuccessfulConnectionTestTime;

        public AiAssistantPage(
            NetworkStatusService networkStatusService,
            AiSettingsService settingsService,
            NetworkStatusResult networkStatus,
            Action<NetworkStatusResult> networkStatusChanged,
            Action aiSettingsChanged)
        {
            _networkStatusService = networkStatusService;
            _settingsService = settingsService;
            _businessSummaryService = new BusinessSummaryService();
            _networkStatus = networkStatus ?? NetworkStatusResult.Unknown();
            _networkStatusChanged = networkStatusChanged;
            _aiSettingsChanged = aiSettingsChanged;

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = UiTheme.Font(11F);

            Render();
        }

        private void Render()
        {
            Controls.Clear();

            Panel page = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = UiTheme.PagePaddingValue,
                BackColor = BackColor
            };

            Panel header = UiComponentHelper.CreatePageHeader(
                "小铺掌柜 AI智能版 · AI 助手",
                "联网后可接入 DeepSeek API，用本地经营摘要生成建议、简报和报告。",
                null,
                "headers/report");

            Panel content = UiComponentHelper.CreateCardPanel(new Padding(24));
            content.Dock = DockStyle.Fill;

            page.Controls.Add(content);
            page.Controls.Add(header);
            Controls.Add(page);

            AiSettings settings = _settingsService.Load();
            if (!_networkStatus.IsNetworkAvailable)
            {
                RenderOfflineState(content);
                return;
            }

            if (!settings.AiEnabled || !settings.HasApiKey)
            {
                RenderConfigurationState(content, settings);
                return;
            }

            RenderChatState(content, settings);
        }

        private void RenderOfflineState(Control content)
        {
            content.Controls.Clear();

            Label title = new Label
            {
                Text = "当前未联网",
                Dock = DockStyle.Top,
                Height = 52,
                Font = UiTheme.Font(22F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            Label description = new Label
            {
                Text = "AI 助手需要联网后才能使用。\r\n小铺掌柜的本地销售、入库、库存、赊账、报表和备份功能仍可正常使用。",
                Dock = DockStyle.Top,
                Height = 82,
                Font = UiTheme.Font(12F),
                ForeColor = UiTheme.TextSecondary
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = UiTheme.CardBackground
            };

            Button retryButton = UiComponentHelper.CreatePrimaryButton("重新检测网络", 132);
            retryButton.Click += async delegate { await RefreshNetworkAsync(); };
            actions.Controls.Add(retryButton);

            _messageLabel = CreateMessageLabel(_networkStatus.Message);

            content.Controls.Add(_messageLabel);
            content.Controls.Add(actions);
            content.Controls.Add(description);
            content.Controls.Add(title);
        }

        private void RenderConfigurationState(Control content, AiSettings settings)
        {
            content.Controls.Clear();

            Label title = new Label
            {
                Text = "接入 DeepSeek API",
                Dock = DockStyle.Top,
                Height = 46,
                Font = UiTheme.Font(20F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            TableLayoutPanel form = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 250,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = UiTheme.CardBackground,
                Padding = new Padding(0, 12, 0, 0)
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            ComboBox providerComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.Font(11F),
                Dock = DockStyle.Fill
            };
            providerComboBox.Items.Add("DeepSeek");
            providerComboBox.SelectedIndex = 0;

            TextBox baseUrlTextBox = CreateInputTextBox(settings.AiBaseUrl);
            TextBox apiKeyTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(11F),
                PasswordChar = '*',
                UseSystemPasswordChar = true
            };
            apiKeyTextBox.PasswordChar = '*';
            apiKeyTextBox.UseSystemPasswordChar = true;

            CheckBox showKeyCheckBox = new CheckBox
            {
                Text = "显示 API Key",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.TextSecondary
            };
            showKeyCheckBox.CheckedChanged += delegate { apiKeyTextBox.UseSystemPasswordChar = !showKeyCheckBox.Checked; };

            ComboBox modelComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.Font(11F),
                Dock = DockStyle.Fill
            };
            modelComboBox.Items.Add("deepseek-v4-flash");
            modelComboBox.Items.Add("deepseek-v4-pro");
            modelComboBox.SelectedItem = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
            if (modelComboBox.SelectedIndex < 0)
            {
                modelComboBox.SelectedIndex = 0;
            }

            Label savedKeyLabel = new Label
            {
                Text = settings.HasApiKey ? "已保存 Key：" + settings.AiApiKeyMasked : "尚未保存 API Key",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };

            AddFormRow(form, 0, "API 服务商", providerComboBox);
            AddFormRow(form, 1, "API 地址", baseUrlTextBox);
            AddFormRow(form, 2, "API Key", apiKeyTextBox);
            AddFormRow(form, 3, string.Empty, showKeyCheckBox);
            AddFormRow(form, 4, "模型", modelComboBox);
            AddFormRow(form, 5, string.Empty, savedKeyLabel);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = UiTheme.CardBackground
            };

            Button testButton = UiComponentHelper.CreateSecondaryButton("测试连接", 112);
            Button saveButton = UiComponentHelper.CreatePrimaryButton("保存并启用", 124);
            Button clearButton = UiComponentHelper.CreateDangerButton("清除配置", 112);
            actions.Controls.Add(testButton);
            actions.Controls.Add(saveButton);
            actions.Controls.Add(clearButton);

            _messageLabel = CreateMessageLabel("网络可用，请填写 DeepSeek API 配置。");

            testButton.Click += async delegate
            {
                await TestConnectionAsync(baseUrlTextBox.Text, modelComboBox.Text, apiKeyTextBox.Text);
            };

            saveButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(apiKeyTextBox.Text) && !settings.HasApiKey)
                {
                    SetMessage("请先填写 API Key。", UiTheme.DangerRed);
                    return;
                }

                AiSettings updated = new AiSettings
                {
                    AiEnabled = true,
                    AiProvider = providerComboBox.Text,
                    AiBaseUrl = baseUrlTextBox.Text,
                AiModel = modelComboBox.Text,
                    LastConnectionTestTime = string.IsNullOrWhiteSpace(_lastSuccessfulConnectionTestTime)
                        ? settings.LastConnectionTestTime
                        : _lastSuccessfulConnectionTestTime
                };
                _settingsService.Save(updated, apiKeyTextBox.Text);
                _aiSettingsChanged();
                Render();
            };

            clearButton.Click += delegate
            {
                _settingsService.Clear();
                _aiSettingsChanged();
                Render();
            };

            content.Controls.Add(_messageLabel);
            content.Controls.Add(actions);
            content.Controls.Add(form);
            content.Controls.Add(title);
        }

        private void RenderChatState(Control content, AiSettings settings)
        {
            content.Controls.Clear();

            Panel top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = UiTheme.CardBackground
            };

            AiAvatar avatar = new AiAvatar
            {
                Location = new Point(0, 10),
                Size = new Size(54, 54)
            };

            Label nameLabel = new Label
            {
                Text = "小铺经营助手",
                Location = new Point(70, 8),
                Size = new Size(260, 30),
                Font = UiTheme.Font(16F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };

            Label statusLabel = new Label
            {
                Text = "当前模型：" + settings.AiModel + "    网络状态：" + (_networkStatus.IsNetworkAvailable ? "可用" : "不可用"),
                Location = new Point(70, 42),
                Size = new Size(640, 26),
                ForeColor = UiTheme.TextSecondary
            };

            top.Controls.Add(avatar);
            top.Controls.Add(nameLabel);
            top.Controls.Add(statusLabel);

            FlowLayoutPanel suggestions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 96,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.CardBackground
            };
            AddSuggestionButton(suggestions, "分析今日收入", _businessSummaryService.BuildTodaySummary);
            AddSuggestionButton(suggestions, "生成本周经营小结", _businessSummaryService.BuildWeekSummary);
            AddSuggestionButton(suggestions, "生成本月经营月报", _businessSummaryService.BuildMonthSummary);
            AddSuggestionButton(suggestions, "查看库存补货建议", _businessSummaryService.BuildInventoryRiskSummary);
            AddSuggestionButton(suggestions, "查看赊账客户提醒", _businessSummaryService.BuildCreditRiskSummary);
            AddSuggestionButton(suggestions, "分析热销与滞销商品", _businessSummaryService.BuildHotAndSlowProductsSummary);

            FlowLayoutPanel chatPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = UiTheme.SoftSlate,
                Padding = new Padding(14)
            };
            AddChatBubble(chatPanel, "你好，我是小铺经营助手。你可以点上方建议问题，后续我会基于本地经营摘要帮你生成分析建议。", false);

            Panel inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = UiTheme.CardBackground,
                Padding = new Padding(0, 10, 0, 0)
            };

            TextBox inputTextBox = new TextBox
            {
                Location = new Point(0, 10),
                Size = new Size(620, 40),
                Font = UiTheme.Font(11F)
            };
            UiComponentHelper.CenterTextBoxContent(inputTextBox);

            Button sendButton = UiComponentHelper.CreatePrimaryButton("发送", 96);
            sendButton.Location = new Point(636, 10);
            inputPanel.Resize += delegate
            {
                sendButton.Left = Math.Max(0, inputPanel.ClientSize.Width - sendButton.Width);
                inputTextBox.Width = Math.Max(220, sendButton.Left - 14);
            };
            sendButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(inputTextBox.Text))
                {
                    return;
                }

                AddChatBubble(chatPanel, inputTextBox.Text.Trim(), true);
                AddChatBubble(chatPanel, "本阶段已完成 AI 对话窗口骨架。下一阶段会把本地经营摘要发送给 DeepSeek 生成正式回复。", false);
                inputTextBox.Clear();
            };

            inputPanel.Controls.Add(inputTextBox);
            inputPanel.Controls.Add(sendButton);

            content.Controls.Add(chatPanel);
            content.Controls.Add(inputPanel);
            content.Controls.Add(suggestions);
            content.Controls.Add(top);
        }

        private async Task RefreshNetworkAsync()
        {
            SetMessage("正在检测网络...", UiTheme.TextSecondary);
            NetworkStatusResult result = await _networkStatusService.CheckAsync();
            _networkStatus = result;
            if (_networkStatusChanged != null)
            {
                _networkStatusChanged(result);
            }

            Render();
        }

        private async Task TestConnectionAsync(string baseUrl, string model, string apiKeyFromInput)
        {
            SetMessage("正在测试连接...", UiTheme.TextSecondary);
            string apiKey = string.IsNullOrWhiteSpace(apiKeyFromInput) ? _settingsService.GetApiKey() : apiKeyFromInput.Trim();
            DeepSeekClient client = new DeepSeekClient(baseUrl, model, apiKey);
            DeepSeekConnectionResult result = await client.TestConnectionAsync();
            SetMessage(result.Message, result.Success ? UiTheme.SuccessGreen : UiTheme.DangerRed);
            if (result.Success)
            {
                DateTime now = DateTime.Now;
                _lastSuccessfulConnectionTestTime = now.ToString("yyyy-MM-dd HH:mm:ss");
                _settingsService.UpdateLastConnectionTestTime(now);
                _aiSettingsChanged();
            }
        }

        private static TextBox CreateInputTextBox(string text)
        {
            TextBox textBox = new TextBox
            {
                Text = text ?? string.Empty,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(11F)
            };
            UiComponentHelper.CenterTextBoxContent(textBox);
            return textBox;
        }

        private static Label CreateMessageLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };
        }

        private static void AddFormRow(TableLayoutPanel form, int row, string labelText, Control valueControl)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };

            form.Controls.Add(label, 0, row);
            form.Controls.Add(valueControl, 1, row);
        }

        private void AddSuggestionButton(FlowLayoutPanel panel, string text, Func<string> summaryFactory)
        {
            Button button = UiComponentHelper.CreateSecondaryButton(text, 150);
            button.Click += delegate
            {
                Control chatControl = FindChatPanel(panel.Parent);
                FlowLayoutPanel chatPanel = chatControl as FlowLayoutPanel;
                if (chatPanel == null)
                {
                    return;
                }

                AddChatBubble(chatPanel, text, true);
                AddChatBubble(chatPanel, summaryFactory(), false);
            };
            panel.Controls.Add(button);
        }

        private static Control FindChatPanel(Control parent)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Control control in parent.Controls)
            {
                if (control is FlowLayoutPanel && control.Dock == DockStyle.Fill)
                {
                    return control;
                }
            }

            return null;
        }

        private static void AddChatBubble(FlowLayoutPanel chatPanel, string text, bool user)
        {
            Label bubble = new Label
            {
                AutoSize = false,
                Width = Math.Max(240, chatPanel.ClientSize.Width - 60),
                Height = 54,
                Text = text,
                Padding = new Padding(14, 8, 14, 8),
                Margin = new Padding(0, 0, 0, 10),
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextPrimary,
                BackColor = user ? UiTheme.SoftBlue : Color.White,
                TextAlign = user ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
            };

            chatPanel.Controls.Add(bubble);
            chatPanel.ScrollControlIntoView(bubble);
        }

        private void SetMessage(string message, Color color)
        {
            if (_messageLabel == null)
            {
                return;
            }

            _messageLabel.Text = message;
            _messageLabel.ForeColor = color;
        }

        private sealed class AiAvatar : Control
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush brush = new SolidBrush(UiTheme.PrimaryBlue))
                {
                    e.Graphics.FillEllipse(brush, new Rectangle(0, 0, Width - 1, Height - 1));
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    "AI",
                    UiTheme.Font(14F, FontStyle.Bold),
                    ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
