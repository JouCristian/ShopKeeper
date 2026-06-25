using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class AiAssistantPage : UserControl
    {
        private const string SystemPrompt =
            "你是“小铺掌柜 AI经营助手”，服务对象是小卖铺老板。你只能基于用户提供的本地经营摘要进行分析，不能编造不存在的数据。请用简洁、清楚、接地气的中文回答。重点关注收入、利润、库存、赊账、热销、滞销和补货建议。你的建议要谨慎、可执行，不要使用复杂金融术语。你不能要求直接访问数据库，也不能要求用户上传完整数据库。遇到数据不足时，要说明“当前数据不足，建议先补充或继续记录一段时间”。默认控制在 5 到 10 条要点内。金额单位用“元”。风险提醒要温和，明确区分“已经从数据看出”和“建议关注”。";

        private readonly NetworkStatusService _networkStatusService;
        private readonly AiSettingsService _settingsService;
        private readonly BusinessSummaryService _businessSummaryService;
        private readonly AiConversationService _conversationService;
        private readonly AiStoreProfileService _storeProfileService;
        private readonly Action<NetworkStatusResult> _networkStatusChanged;
        private readonly Action _aiSettingsChanged;
        private NetworkStatusResult _networkStatus;
        private AiConversation _currentConversation;
        private Panel _contentPanel;
        private Label _messageLabel;
        private Label _conversationTitleLabel;
        private ComboBox _conversationComboBox;
        private ComboBox _modelComboBox;
        private FlowLayoutPanel _chatPanel;
        private TextBox _inputTextBox;
        private Button _sendButton;
        private string _lastSuccessfulConnectionTestTime;
        private bool _isSending;
        private bool _loadingConversations;
        private bool _profilePromptShown;
        private bool _profilePromptPending;

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
            _conversationService = new AiConversationService();
            _storeProfileService = new AiStoreProfileService();
            _networkStatus = networkStatus ?? NetworkStatusResult.Unknown();
            _networkStatusChanged = networkStatusChanged;
            _aiSettingsChanged = aiSettingsChanged;

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = UiTheme.Font(11F);

            Render();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            BeginStoreProfilePromptIfReady();
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

            _contentPanel = UiComponentHelper.CreateCardPanel(new Padding(24));
            _contentPanel.Dock = DockStyle.Fill;

            page.Controls.Add(_contentPanel);
            page.Controls.Add(header);
            Controls.Add(page);

            AiSettings settings = _settingsService.Load();
            if (!_networkStatus.IsNetworkAvailable)
            {
                _profilePromptPending = false;
                RenderOfflineState();
                return;
            }

            if (!settings.AiEnabled || !settings.HasApiKey)
            {
                _profilePromptPending = false;
                RenderConfigurationState(settings, false);
                return;
            }

            EnsureConversation(settings);
            RenderChatState(settings);
        }

        private void EnsureConversation(AiSettings settings)
        {
            if (_currentConversation == null)
            {
                _currentConversation = _conversationService.LoadLatestOrCreate(settings.AiModel);
            }
        }

        private void RenderOfflineState()
        {
            _contentPanel.Controls.Clear();
            Label title = CreateSectionTitle("当前未联网");
            Label description = new Label
            {
                Text = "AI 助手需要联网后才能使用。\r\n小铺掌柜的本地销售、入库、库存、赊账、报表和备份功能仍可正常使用。",
                Dock = DockStyle.Top,
                Height = 82,
                Font = UiTheme.Font(12F),
                ForeColor = UiTheme.TextSecondary
            };

            FlowLayoutPanel actions = CreateActionRow(56);
            Button retryButton = UiComponentHelper.CreatePrimaryButton("重新检测网络", 132);
            retryButton.Click += async delegate { await RefreshNetworkAsync(); };
            actions.Controls.Add(retryButton);

            _messageLabel = CreateMessageLabel(_networkStatus.Message);
            _contentPanel.Controls.Add(_messageLabel);
            _contentPanel.Controls.Add(actions);
            _contentPanel.Controls.Add(description);
            _contentPanel.Controls.Add(title);
        }

        private void RenderConfigurationState(AiSettings settings, bool allowBack)
        {
            _contentPanel.Controls.Clear();

            Label title = CreateSectionTitle("接入 DeepSeek API");
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

            ComboBox providerComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiTheme.Font(11F), Dock = DockStyle.Fill };
            providerComboBox.Items.Add("DeepSeek");
            providerComboBox.SelectedIndex = 0;
            TextBox baseUrlTextBox = CreateInputTextBox(settings.AiBaseUrl);
            TextBox apiKeyTextBox = new TextBox { Dock = DockStyle.Fill, Font = UiTheme.Font(11F), UseSystemPasswordChar = true };
            CheckBox showKeyCheckBox = new CheckBox { Text = "显示 API Key", Dock = DockStyle.Fill, ForeColor = UiTheme.TextSecondary };
            showKeyCheckBox.CheckedChanged += delegate { apiKeyTextBox.UseSystemPasswordChar = !showKeyCheckBox.Checked; };
            ComboBox modelComboBox = CreateModelComboBox(settings.AiModel);
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

            FlowLayoutPanel actions = CreateActionRow(58);
            Button testButton = UiComponentHelper.CreateSecondaryButton("测试连接", 112);
            Button saveButton = UiComponentHelper.CreatePrimaryButton("保存并启用", 124);
            Button clearButton = UiComponentHelper.CreateDangerButton("清除配置", 112);
            actions.Controls.Add(testButton);
            actions.Controls.Add(saveButton);
            actions.Controls.Add(clearButton);
            if (allowBack)
            {
                Button backButton = UiComponentHelper.CreateSecondaryButton("返回对话", 112);
                backButton.Click += delegate { Render(); };
                actions.Controls.Add(backButton);
            }

            _messageLabel = CreateMessageLabel("网络可用，请填写或调整 DeepSeek API 配置。");

            testButton.Click += async delegate { await TestConnectionAsync(baseUrlTextBox.Text, modelComboBox.Text, apiKeyTextBox.Text); };
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
                if (MessageBox.Show("确定清除 AI API 配置吗？本地经营数据不会受影响。", "清除 API 配置", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                _settingsService.Clear();
                _aiSettingsChanged();
                Render();
            };

            _contentPanel.Controls.Add(_messageLabel);
            _contentPanel.Controls.Add(actions);
            _contentPanel.Controls.Add(form);
            _contentPanel.Controls.Add(title);
        }

        private void RenderChatState(AiSettings settings)
        {
            _contentPanel.Controls.Clear();

            Panel shell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.CardBackground
            };

            Panel sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 286,
                Padding = new Padding(0, 0, 18, 0),
                BackColor = UiTheme.CardBackground
            };
            BuildChatHeader(sidebar, settings);

            Panel divider = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = UiTheme.CardBorder
            };

            Panel main = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 0, 0, 0),
                BackColor = UiTheme.CardBackground
            };

            Panel mainHeader = BuildMainConversationHeader(settings);

            FlowLayoutPanel suggestions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 108,
                Padding = new Padding(0, 10, 0, 6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.CardBackground
            };
            AddSuggestionButton(suggestions, "分析今日收入", "请分析今天收入怎么样");
            AddSuggestionButton(suggestions, "生成本周经营小结", "请生成本周经营小结");
            AddSuggestionButton(suggestions, "生成本月经营月报", "请生成本月经营月报");
            AddSuggestionButton(suggestions, "库存补货建议", "请根据当前库存状态给出补货建议");
            AddSuggestionButton(suggestions, "赊账客户提醒", "最近谁还欠账，请帮我分析赊账情况");
            AddSuggestionButton(suggestions, "热销与滞销商品", "请分析热销与滞销商品");

            _chatPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(246, 249, 253),
                Padding = new Padding(18)
            };
            _chatPanel.Resize += delegate { ResizeChatBubbles(); };
            LoadConversationMessages();

            Panel inputPanel = BuildInputPanel();
            main.Controls.Add(_chatPanel);
            main.Controls.Add(inputPanel);
            main.Controls.Add(suggestions);
            main.Controls.Add(mainHeader);

            shell.Controls.Add(main);
            shell.Controls.Add(divider);
            shell.Controls.Add(sidebar);
            _contentPanel.Controls.Add(shell);

            QueueStoreProfilePrompt();
        }

        private void QueueStoreProfilePrompt()
        {
            if (_profilePromptShown || _profilePromptPending || IsDisposed || Disposing)
            {
                return;
            }

            _profilePromptPending = true;
            BeginStoreProfilePromptIfReady();
        }

        private void BeginStoreProfilePromptIfReady()
        {
            if (!_profilePromptPending || !IsHandleCreated || IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke(new MethodInvoker(RunQueuedStoreProfilePrompt));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void RunQueuedStoreProfilePrompt()
        {
            if (IsDisposed || Disposing)
            {
                _profilePromptPending = false;
                return;
            }

            _profilePromptPending = false;
            ShowStoreProfileWizardIfNeeded();
        }

        private void BuildChatHeader(Panel sidebar, AiSettings settings)
        {
            sidebar.Controls.Clear();

            Panel identity = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = UiTheme.CardBackground
            };
            AiAvatar avatar = new AiAvatar { Location = new Point(0, 8), Size = new Size(52, 52) };
            Label nameLabel = new Label
            {
                Text = "小铺经营助手",
                Location = new Point(66, 4),
                Size = new Size(180, 28),
                Font = UiTheme.Font(15F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            Label statusLabel = new Label
            {
                Text = _networkStatus.IsNetworkAvailable ? "AI 联网能力：可用" : "AI 联网能力：不可用",
                Location = new Point(66, 34),
                Size = new Size(190, 24),
                ForeColor = UiTheme.TextSecondary
            };
            identity.Controls.Add(avatar);
            identity.Controls.Add(nameLabel);
            identity.Controls.Add(statusLabel);

            Label conversationLabel = CreateSidebarTitle("会话管理");
            conversationLabel.Dock = DockStyle.Top;

            _conversationComboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.Font(10.5F),
                Height = 34,
                Margin = new Padding(0)
            };
            _conversationComboBox.SelectedIndexChanged += ConversationComboBox_SelectedIndexChanged;
            ReloadConversationComboBox();

            Button newButton = UiComponentHelper.CreatePrimaryButton("新建对话", 104);
            Button renameButton = UiComponentHelper.CreateSecondaryButton("重命名", 92);
            Button deleteButton = CreateSoftDangerSidebarButton("删除此对话");

            newButton.Click += delegate { CreateNewConversation(); };
            renameButton.Click += delegate { RenameCurrentConversation(); };
            deleteButton.Click += delegate { DeleteCurrentConversation(); };

            TableLayoutPanel conversationTools = CreateSidebarButtonGrid(newButton, renameButton, deleteButton, 38F);

            Label modelLabel = CreateSidebarTitle("模型与能力");
            modelLabel.Dock = DockStyle.Top;
            _modelComboBox = CreateModelComboBox(settings.AiModel);
            _modelComboBox.Dock = DockStyle.Top;
            _modelComboBox.Height = 34;
            _modelComboBox.SelectedIndexChanged += ModelComboBox_SelectedIndexChanged;

            Button apiButton = CreateSoftSidebarButton("API 设置", UiTheme.SoftBlue, UiTheme.PrimaryBlue);
            Button profileButton = CreateSoftSidebarButton("店铺记忆", UiTheme.SoftGreen, UiTheme.SuccessGreen);
            Button refreshButton = CreateSoftSidebarButton("检测网络", UiTheme.SoftCyan, UiTheme.InfoCyan);
            apiButton.Click += delegate { RenderConfigurationState(_settingsService.Load(), true); };
            profileButton.Click += delegate { ShowStoreProfileEditor(false); };
            refreshButton.Click += async delegate { await RefreshNetworkAsync(); };

            TableLayoutPanel settingsTools = CreateSidebarButtonGrid(apiButton, profileButton, refreshButton, 46F);

            sidebar.Controls.Add(settingsTools);
            sidebar.Controls.Add(_modelComboBox);
            sidebar.Controls.Add(modelLabel);
            sidebar.Controls.Add(conversationTools);
            sidebar.Controls.Add(_conversationComboBox);
            sidebar.Controls.Add(conversationLabel);
            sidebar.Controls.Add(identity);
        }

        private Panel BuildMainConversationHeader(AiSettings settings)
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = UiTheme.SoftSlate,
                Padding = new Padding(12, 0, 12, 0)
            };

            _conversationTitleLabel = new Label
            {
                Text = _currentConversation.Title,
                Dock = DockStyle.Top,
                Height = 34,
                Font = UiTheme.Font(16F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label metaLabel = new Label
            {
                Text = "当前模型：" + settings.AiModel + "    当前状态：" + (_networkStatus.IsNetworkAvailable ? "联网可用" : "本地离线"),
                Dock = DockStyle.Top,
                Height = 24,
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            header.Controls.Add(metaLabel);
            header.Controls.Add(_conversationTitleLabel);
            return header;
        }

        private Panel BuildInputPanel()
        {
            Panel inputPanel = UiComponentHelper.CreateCardPanel(new Padding(12), UiTheme.SoftSlate, UiTheme.CardBorder);
            inputPanel.Dock = DockStyle.Bottom;
            inputPanel.Height = 78;

            _inputTextBox = new TextBox
            {
                Location = new Point(12, 12),
                Size = new Size(620, 44),
                Font = UiTheme.Font(11F),
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ScrollBars = ScrollBars.None
            };
            _inputTextBox.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendManualMessageAsync();
                }
            };
            _inputTextBox.TextChanged += delegate { UpdateInputPanelLayout(inputPanel); };

            _sendButton = UiComponentHelper.CreatePrimaryButton("发送", 96);
            _sendButton.Location = new Point(636, 14);
            _sendButton.Click += async delegate { await SendManualMessageAsync(); };
            inputPanel.Resize += delegate
            {
                UpdateInputPanelLayout(inputPanel);
            };

            inputPanel.Controls.Add(_inputTextBox);
            inputPanel.Controls.Add(_sendButton);
            UpdateInputPanelLayout(inputPanel);
            return inputPanel;
        }

        private void LoadConversationMessages()
        {
            _chatPanel.Controls.Clear();
            if (_currentConversation.Messages.Count == 0)
            {
                AiStoreProfile profile = _storeProfileService.Load();
                string welcome = profile.IsInitialized
                    ? "我已经了解这家店的基本情况，之后分析收入、库存和补货时会结合这些背景。"
                    : "你好，我是小铺经营助手。你可以先完善“店铺记忆”，也可以直接问我今天收入、库存、赊账或热销商品。";
                AddChatBubble(welcome, false, false);
                return;
            }

            foreach (AiStoredMessage message in _currentConversation.Messages)
            {
                AddChatBubble(message.Content, message.Role == "user", false);
            }
        }

        private async Task SendManualMessageAsync()
        {
            if (_isSending || _inputTextBox == null || string.IsNullOrWhiteSpace(_inputTextBox.Text))
            {
                return;
            }

            string userText = _inputTextBox.Text.Trim();
            _inputTextBox.Clear();
            await SendUserTextAsync(userText);
        }

        private async Task SendUserTextAsync(string userText)
        {
            AddChatBubble(userText, true, true);
            BusinessSummaryResult liveContext = _businessSummaryService.BuildLiveContextForUserQuestion(userText);
            string prompt = BuildAiUserPrompt(liveContext, userText);
            await SendToAiAsync(prompt, liveContext);
        }

        private async Task SendToAiAsync(string userPrompt, BusinessSummaryResult liveContext)
        {
            AiSettings settings = _settingsService.Load();
            settings.AiApiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                AddChatBubble("请先配置 DeepSeek API Key。", false, false);
                return;
            }

            SetSendingState(true);
            RichTextBox waitingBubble = AddChatBubble("正在分析，请稍候……", false, false);

            try
            {
                List<AiChatMessage> messages = BuildRequestMessages(userPrompt);
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(65)))
                {
                    DeepSeekClient client = new DeepSeekClient(settings.AiBaseUrl, settings.AiModel, settings.AiApiKey);
                    AiResponseResult result = await client.SendChatAsync(settings, messages, cts.Token);
                    if (result.Success)
                    {
                        SetBubbleContent(waitingBubble, result.Content, false);
                        ResizeBubble(waitingBubble);
                        SaveAssistantMessage(result.Content, liveContext);
                    }
                    else
                    {
                        SetBubbleContent(waitingBubble, result.ErrorMessage + "\r\n本地销售、入库、库存、赊账、报表和备份功能不受影响。", false);
                        SetBubbleTextColor(waitingBubble, UiTheme.DangerRed);
                        ResizeBubble(waitingBubble);
                    }
                }
            }
            finally
            {
                SetSendingState(false);
                ScrollChatToBottom();
            }
        }

        private List<AiChatMessage> BuildRequestMessages(string currentPrompt)
        {
            List<AiChatMessage> messages = new List<AiChatMessage>();
            messages.Add(AiChatMessage.System(SystemPrompt));
            messages.Add(AiChatMessage.System(_storeProfileService.Load().ToPromptText()));
            if (!string.IsNullOrWhiteSpace(_currentConversation.Summary))
            {
                messages.Add(AiChatMessage.System("【当前对话摘要】\r\n" + _currentConversation.Summary));
            }

            int startIndex = Math.Max(0, _currentConversation.Messages.Count - 12);
            for (int index = startIndex; index < _currentConversation.Messages.Count; index++)
            {
                AiStoredMessage stored = _currentConversation.Messages[index];
                if (stored.Role == "system")
                {
                    continue;
                }

                messages.Add(new AiChatMessage { Role = stored.Role, Content = stored.Content });
            }

            messages.Add(AiChatMessage.User(currentPrompt));
            return messages;
        }

        private static string BuildAiUserPrompt(BusinessSummaryResult liveContext, string requestText)
        {
            if (liveContext != null && liveContext.Success && !string.IsNullOrWhiteSpace(liveContext.SummaryText))
            {
                return liveContext.SummaryText + "\r\n【用户当前问题】\r\n" + requestText + "\r\n请在回答里说明“根据当前本地记录”。";
            }

            return "用户当前问题：\r\n" + requestText + "\r\n如果没有提供本地经营摘要，请不要假装知道销售、库存或赊账数据。";
        }

        private void AddSuggestionButton(FlowLayoutPanel panel, string text, string prompt)
        {
            Color[] backgrounds = { UiTheme.SoftBlue, UiTheme.SoftGreen, UiTheme.SoftOrange, UiTheme.SoftCyan, UiTheme.SoftPurple, UiTheme.SoftSlate };
            Color[] foregrounds = { UiTheme.PrimaryBlue, UiTheme.SuccessGreen, UiTheme.WarningOrange, UiTheme.InfoCyan, Color.FromArgb(91, 33, 182), UiTheme.TextPrimary };
            int index = panel.Controls.Count % backgrounds.Length;
            Button button = UiComponentHelper.CreateButton(text, 156, backgrounds[index], foregrounds[index], UiTheme.CardBorder);
            button.FlatAppearance.MouseOverBackColor = UiComponentHelper.Lighten(backgrounds[index], 6);
            button.FlatAppearance.MouseDownBackColor = UiComponentHelper.Darken(backgrounds[index], 6);
            button.Click += async delegate { await SendUserTextAsync(prompt); };
            panel.Controls.Add(button);
        }

        private RichTextBox AddChatBubble(string text, bool user, bool persist)
        {
            int width = ResolveBubbleWidth(text, user);
            ChatBubbleBox bubble = new ChatBubbleBox
            {
                Width = width,
                Margin = ResolveBubbleMargin(width, user),
                Font = UiTheme.Font(10.5F),
                ForeColor = UiTheme.TextPrimary,
                BackColor = user ? UiTheme.SoftBlue : UiTheme.SoftGreen,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                TabStop = false,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = true,
                WheelForwarded = ScrollChatPanelByWheel,
                Tag = user
            };
            SetBubbleContent(bubble, text, user);
            ResizeBubble(bubble);
            _chatPanel.Controls.Add(bubble);
            ScrollChatToBottom();

            if (persist)
            {
                _conversationService.AddMessage(_currentConversation.Id, user ? "user" : "assistant", text, "chat", string.Empty);
                _currentConversation = _conversationService.Load(_currentConversation.Id);
                ReloadConversationComboBox();
            }

            return bubble;
        }

        private void SaveAssistantMessage(string text, BusinessSummaryResult liveContext)
        {
            string dataContextType = liveContext != null && liveContext.Success ? liveContext.Title : string.Empty;
            _conversationService.AddMessage(_currentConversation.Id, "assistant", text, "chat", dataContextType);
            _currentConversation = _conversationService.Load(_currentConversation.Id);
            ReloadConversationComboBox();
        }

        private void CreateNewConversation()
        {
            AiSettings settings = _settingsService.Load();
            _currentConversation = _conversationService.Create("新对话", settings.AiModel);
            Render();
        }

        private void RenameCurrentConversation()
        {
            using (RenameConversationForm form = new RenameConversationForm(_currentConversation.Title))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _conversationService.Rename(_currentConversation.Id, form.NewTitle);
                _currentConversation = _conversationService.Load(_currentConversation.Id);
                ReloadConversationComboBox();
                _conversationTitleLabel.Text = _currentConversation.Title;
            }
        }

        private void DeleteCurrentConversation()
        {
            if (MessageBox.Show("确定删除当前 AI 对话吗？这只会删除聊天记录，不会影响经营数据。", "删除对话", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _conversationService.Archive(_currentConversation.Id);
            _currentConversation = _conversationService.LoadLatestOrCreate(_settingsService.Load().AiModel);
            Render();
        }

        private void ReloadConversationComboBox()
        {
            if (_conversationComboBox == null)
            {
                return;
            }

            _loadingConversations = true;
            _conversationComboBox.Items.Clear();
            foreach (AiConversation conversation in _conversationService.ListConversations())
            {
                _conversationComboBox.Items.Add(conversation);
                if (_currentConversation != null && conversation.Id == _currentConversation.Id)
                {
                    _conversationComboBox.SelectedItem = conversation;
                }
            }

            _loadingConversations = false;
        }

        private void ConversationComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConversations)
            {
                return;
            }

            AiConversation selected = _conversationComboBox.SelectedItem as AiConversation;
            if (selected == null || _currentConversation == null || selected.Id == _currentConversation.Id)
            {
                return;
            }

            _currentConversation = _conversationService.Load(selected.Id);
            Render();
        }

        private void ModelComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_modelComboBox == null || _modelComboBox.SelectedItem == null)
            {
                return;
            }

            string model = _modelComboBox.SelectedItem.ToString();
            AiSettings settings = _settingsService.Load();
            settings.AiEnabled = true;
            settings.AiModel = model;
            _settingsService.Save(settings, string.Empty);
            _conversationService.UpdateModel(_currentConversation.Id, model);
            _currentConversation = _conversationService.Load(_currentConversation.Id);
            AddChatBubble("已切换模型为 " + model, false, false);
            _aiSettingsChanged();
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

        private void ShowStoreProfileWizardIfNeeded()
        {
            if (_profilePromptShown)
            {
                return;
            }

            AiStoreProfile profile = _storeProfileService.Load();
            if (profile.IsInitialized)
            {
                return;
            }

            _profilePromptShown = true;
            ShowStoreProfileEditor(true);
        }

        private void ShowStoreProfileEditor(bool firstRun)
        {
            using (StoreProfileForm form = new StoreProfileForm(_storeProfileService.Load(), firstRun))
            {
                DialogResult result = form.ShowDialog(this);
                if (form.ClearRequested)
                {
                    if (MessageBox.Show("确定清空店铺记忆吗？这不会影响经营数据。", "清空店铺记忆", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _storeProfileService.Clear();
                        AddChatBubble("店铺记忆已清空。", false, false);
                    }

                    return;
                }

                if (result == DialogResult.OK)
                {
                    _storeProfileService.Save(form.Profile);
                    AddChatBubble(form.Profile.IsInitialized ? "店铺记忆已保存，后续分析会结合这些背景。" : "已跳过店铺记忆，之后也可以随时补充。", false, false);
                }
            }
        }

        private int ResolveBubbleWidth()
        {
            return ResolveBubbleWidth(string.Empty, false);
        }

        private int ResolveBubbleWidth(string text, bool user)
        {
            if (_chatPanel == null || _chatPanel.ClientSize.Width <= 80)
            {
                return 520;
            }

            int maxWidth = Math.Max(300, Math.Min(760, _chatPanel.ClientSize.Width - 64));
            if (!user)
            {
                return maxWidth;
            }

            string firstLine = string.IsNullOrEmpty(text) ? string.Empty : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
            int measuredWidth = TextRenderer.MeasureText(firstLine, UiTheme.Font(10.5F)).Width + 48;
            return Math.Max(180, Math.Min(maxWidth, measuredWidth));
        }

        private Padding ResolveBubbleMargin(int width, bool user)
        {
            if (_chatPanel == null || !user)
            {
                return new Padding(0, 0, 0, 12);
            }

            int left = Math.Max(0, _chatPanel.ClientSize.Width - width - 52);
            return new Padding(left, 0, 0, 12);
        }

        private void ScrollChatPanelByWheel(int delta)
        {
            if (_chatPanel == null)
            {
                return;
            }

            int currentY = -_chatPanel.AutoScrollPosition.Y;
            int nextY = Math.Max(0, currentY - delta);
            _chatPanel.AutoScrollPosition = new Point(0, nextY);
        }

        private void ResizeChatBubbles()
        {
            if (_chatPanel == null)
            {
                return;
            }

            foreach (Control control in _chatPanel.Controls)
            {
                RichTextBox bubble = control as RichTextBox;
                if (bubble == null)
                {
                    continue;
                }

                bool user = bubble.Tag is bool && (bool)bubble.Tag;
                bubble.Width = ResolveBubbleWidth(bubble.Text, user);
                bubble.Margin = ResolveBubbleMargin(bubble.Width, user);
                ResizeBubble(bubble);
            }
        }

        private static void ResizeBubble(RichTextBox bubble)
        {
            bool user = bubble.Tag is bool && (bool)bubble.Tag;
            Size textSize = TextRenderer.MeasureText(
                bubble.Text,
                bubble.Font,
                new Size(Math.Max(80, bubble.Width - 26), 6000),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int desiredHeight = Math.Max(48, textSize.Height + 30);
            int maxHeight = user ? 190 : 3600;
            bubble.Height = Math.Min(maxHeight, desiredHeight);
            bubble.ScrollBars = desiredHeight > maxHeight ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
        }

        private void UpdateInputPanelLayout(Panel inputPanel)
        {
            if (inputPanel == null || _inputTextBox == null || _sendButton == null)
            {
                return;
            }

            const int inputMinHeight = 44;
            const int inputMaxHeight = 138;
            int availableWidth = Math.Max(280, inputPanel.ClientSize.Width - _sendButton.Width - 38);
            Size textSize = TextRenderer.MeasureText(
                string.IsNullOrEmpty(_inputTextBox.Text) ? " " : _inputTextBox.Text,
                _inputTextBox.Font,
                new Size(Math.Max(80, availableWidth - 12), 4000),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int desiredInputHeight = Math.Max(inputMinHeight, Math.Min(inputMaxHeight, textSize.Height + 22));
            _inputTextBox.ScrollBars = textSize.Height + 22 > inputMaxHeight ? ScrollBars.Vertical : ScrollBars.None;
            _inputTextBox.Height = desiredInputHeight;
            _inputTextBox.Width = availableWidth;
            _inputTextBox.Left = 12;
            _inputTextBox.Top = 12;

            _sendButton.Left = inputPanel.ClientSize.Width - _sendButton.Width - 12;
            _sendButton.Top = _inputTextBox.Top + Math.Max(0, desiredInputHeight - _sendButton.Height);
            inputPanel.Height = desiredInputHeight + 24;
        }

        private static void SetBubbleContent(RichTextBox bubble, string text, bool user)
        {
            bubble.SuspendLayout();
            bubble.Clear();

            List<MarkdownFormatRange> ranges = new List<MarkdownFormatRange>();
            string displayText = user ? (text ?? string.Empty) : ConvertMarkdownToDisplayText(text, out ranges);
            bubble.Text = displayText;

            bubble.SelectAll();
            bubble.SelectionFont = UiTheme.Font(10.5F);
            bubble.SelectionColor = UiTheme.TextPrimary;
            bubble.SelectionAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            bubble.SelectionIndent = 10;
            bubble.SelectionRightIndent = 10;

            if (!user && ranges != null)
            {
                foreach (MarkdownFormatRange range in ranges)
                {
                    if (range.Length <= 0 || range.Start < 0 || range.Start >= bubble.TextLength)
                    {
                        continue;
                    }

                    int length = Math.Min(range.Length, bubble.TextLength - range.Start);
                    bubble.Select(range.Start, length);
                    bubble.SelectionFont = UiTheme.Font(range.Size, range.Style);
                    bubble.SelectionColor = UiTheme.TextPrimary;
                }
            }

            bubble.Select(0, 0);
            bubble.ResumeLayout();
        }

        private static void SetBubbleTextColor(RichTextBox bubble, Color color)
        {
            if (bubble == null)
            {
                return;
            }

            bubble.SelectAll();
            bubble.SelectionColor = color;
            bubble.ForeColor = color;
            bubble.Select(0, 0);
        }

        private static string ConvertMarkdownToDisplayText(string markdown, out List<MarkdownFormatRange> ranges)
        {
            ranges = new List<MarkdownFormatRange>();
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                string trimmed = line.Trim();
                int lineStart = builder.Length;
                FontStyle lineStyle = FontStyle.Regular;
                float lineSize = 10.5F;

                if (trimmed == "---" || trimmed == "___" || trimmed == "***")
                {
                    builder.Append("────────────────────────");
                }
                else
                {
                    string content = line;
                    if (trimmed.StartsWith("### "))
                    {
                        content = trimmed.Substring(4);
                        lineStyle = FontStyle.Bold;
                        lineSize = 12F;
                    }
                    else if (trimmed.StartsWith("## "))
                    {
                        content = trimmed.Substring(3);
                        lineStyle = FontStyle.Bold;
                        lineSize = 13F;
                    }
                    else if (trimmed.StartsWith("# "))
                    {
                        content = trimmed.Substring(2);
                        lineStyle = FontStyle.Bold;
                        lineSize = 14F;
                    }
                    else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    {
                        int indentLength = line.Length - line.TrimStart().Length;
                        content = new string(' ', Math.Min(indentLength, 6)) + "• " + trimmed.Substring(2);
                    }

                    AppendInlineMarkdown(builder, content, ranges, lineStyle, lineSize);
                    if (lineStyle != FontStyle.Regular || Math.Abs(lineSize - 10.5F) > 0.01F)
                    {
                        ranges.Add(new MarkdownFormatRange(lineStart, builder.Length - lineStart, lineStyle, lineSize));
                    }
                }

                if (index < lines.Length - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendInlineMarkdown(
            StringBuilder builder,
            string text,
            List<MarkdownFormatRange> ranges,
            FontStyle defaultStyle,
            float defaultSize)
        {
            int cursor = 0;
            while (cursor < text.Length)
            {
                int start = text.IndexOf("**", cursor, StringComparison.Ordinal);
                if (start < 0)
                {
                    builder.Append(text.Substring(cursor));
                    return;
                }

                int end = text.IndexOf("**", start + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    builder.Append(text.Substring(cursor));
                    return;
                }

                builder.Append(text.Substring(cursor, start - cursor));
                int rangeStart = builder.Length;
                string boldText = text.Substring(start + 2, end - start - 2);
                builder.Append(boldText);
                ranges.Add(new MarkdownFormatRange(rangeStart, boldText.Length, defaultStyle | FontStyle.Bold, defaultSize));
                cursor = end + 2;
            }
        }

        private sealed class MarkdownFormatRange
        {
            public MarkdownFormatRange(int start, int length, FontStyle style, float size)
            {
                Start = start;
                Length = length;
                Style = style;
                Size = size;
            }

            public int Start { get; private set; }

            public int Length { get; private set; }

            public FontStyle Style { get; private set; }

            public float Size { get; private set; }
        }

        private void ScrollChatToBottom()
        {
            if (_chatPanel == null || _chatPanel.Controls.Count == 0)
            {
                return;
            }

            _chatPanel.ScrollControlIntoView(_chatPanel.Controls[_chatPanel.Controls.Count - 1]);
        }

        private void SetSendingState(bool sending)
        {
            _isSending = sending;
            if (_sendButton != null)
            {
                _sendButton.Enabled = !sending;
                _sendButton.Text = sending ? "分析中" : "发送";
            }

            if (_inputTextBox != null)
            {
                _inputTextBox.Enabled = !sending;
            }
        }

        private static ComboBox CreateModelComboBox(string selectedModel)
        {
            ComboBox comboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiTheme.Font(10F), Dock = DockStyle.Fill };
            comboBox.Items.Add("deepseek-v4-flash");
            comboBox.Items.Add("deepseek-v4-pro");
            comboBox.SelectedItem = string.IsNullOrWhiteSpace(selectedModel) ? "deepseek-v4-flash" : selectedModel;
            if (comboBox.SelectedIndex < 0)
            {
                comboBox.SelectedIndex = 0;
            }

            return comboBox;
        }

        private static TextBox CreateInputTextBox(string text)
        {
            TextBox textBox = new TextBox { Text = text ?? string.Empty, Dock = DockStyle.Fill, Font = UiTheme.Font(11F) };
            UiComponentHelper.CenterTextBoxContent(textBox);
            return textBox;
        }

        private static Label CreateSectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 52,
                Font = UiTheme.Font(22F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
        }

        private static Label CreateSidebarTitle(string text)
        {
            return new Label
            {
                Text = text,
                Height = 34,
                Font = UiTheme.Font(10.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 10, 0, 0)
            };
        }

        private static Button CreateSoftSidebarButton(string text, Color backColor, Color foreColor)
        {
            Button button = UiComponentHelper.CreateButton(text, 104, backColor, foreColor, UiTheme.CardBorder);
            button.FlatAppearance.MouseOverBackColor = UiComponentHelper.Lighten(backColor, 8);
            button.FlatAppearance.MouseDownBackColor = UiComponentHelper.Darken(backColor, 8);
            return button;
        }

        private static Button CreateSoftDangerSidebarButton(string text)
        {
            Button button = UiComponentHelper.CreateButton(text, 104, UiTheme.SoftRed, UiTheme.DangerRed, UiTheme.DangerBorder);
            button.Font = UiTheme.Font(10F, FontStyle.Bold);
            button.FlatAppearance.MouseOverBackColor = UiComponentHelper.Lighten(UiTheme.SoftRed, 4);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(254, 226, 226);
            return button;
        }

        private static Label CreateMessageLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.TextSecondary };
        }

        private static FlowLayoutPanel CreateActionRow(int height)
        {
            return new FlowLayoutPanel { Dock = DockStyle.Top, Height = height, FlowDirection = FlowDirection.LeftToRight, BackColor = UiTheme.CardBackground };
        }

        private static TableLayoutPanel CreateSidebarButtonGrid(Button firstButton, Button secondButton, Button wideButton, float secondRowHeight)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(64F + secondRowHeight),
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = UiTheme.CardBackground
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, secondRowHeight));

            PrepareSidebarGridButton(firstButton, new Padding(0, 0, 8, 8));
            PrepareSidebarGridButton(secondButton, new Padding(8, 0, 0, 8));
            PrepareSidebarGridButton(wideButton, new Padding(0, 4, 0, 0));

            grid.Controls.Add(firstButton, 0, 0);
            grid.Controls.Add(secondButton, 1, 0);
            grid.Controls.Add(wideButton, 0, 1);
            grid.SetColumnSpan(wideButton, 2);
            return grid;
        }

        private static void PrepareSidebarGridButton(Button button, Padding margin)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = margin;
            button.Width = 0;
        }

        private static void AddFormRow(TableLayoutPanel form, int row, string labelText, Control valueControl)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Label label = new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.TextSecondary };
            form.Controls.Add(label, 0, row);
            form.Controls.Add(valueControl, 1, row);
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

                TextRenderer.DrawText(e.Graphics, "AI", UiTheme.Font(14F, FontStyle.Bold), ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private sealed class ChatBubbleBox : RichTextBox
        {
            public Action<int> WheelForwarded { get; set; }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                if (ScrollBars != RichTextBoxScrollBars.None)
                {
                    base.OnMouseWheel(e);
                    return;
                }

                if (WheelForwarded != null)
                {
                    WheelForwarded(e.Delta);
                    return;
                }

                base.OnMouseWheel(e);
            }
        }
    }
}
