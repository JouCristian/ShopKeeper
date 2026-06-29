using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class AiAssistantPage : UserControl, IResponsivePage
    {
        private const string EmptyChatImageTag = "empty-chat-image";
        private const int DefaultMessageRenderLimit = 100;
        private const string SystemPrompt =
            "你是“小铺掌柜 AI经营助手”，服务对象是小卖铺老板。你只能基于用户提供的本地经营摘要进行分析，不能编造不存在的数据。请用简洁、清楚、接地气的中文回答。重点关注收入、利润、库存、赊账、热销、滞销和补货建议。你的建议要谨慎、可执行，不要使用复杂金融术语。你不能要求直接访问数据库，也不能要求用户上传完整数据库。遇到数据不足时，要说明“当前数据不足，建议先补充或继续记录一段时间”。默认控制在 5 到 10 条要点内。金额单位用“元”。风险提醒要温和，明确区分“已经从数据看出”和“建议关注”。必须使用纯文本输出，不要使用 Markdown 语法，不要使用 #、##、**、```、表格、引用块或代码块。可以使用普通编号，比如“1.”、“2.”、“3.”，也可以用短横线列出要点。";

        private readonly NetworkStatusService _networkStatusService;
        private readonly AiSettingsService _settingsService;
        private readonly BusinessSummaryService _businessSummaryService;
        private readonly AiConversationService _conversationService;
        private readonly AiPurchaseDraftService _purchaseDraftService;
        private readonly AiActionDraftService _actionDraftService;
        private readonly AiIntentRouter _intentRouter;
        private readonly AiSemanticIntentService _semanticIntentService;
        private readonly AiLocalQueryService _localQueryService;
        private readonly AiQuickQuestionService _quickQuestionService;
        private readonly ProductService _productService;
        private readonly AiStoreProfileService _storeProfileService;
        private readonly Action<NetworkStatusResult> _networkStatusChanged;
        private readonly Action _aiSettingsChanged;
        private NetworkStatusResult _networkStatus;
        private AiConversation _currentConversation;
        private Panel _pagePanel;
        private Panel _pageHeaderPanel;
        private Panel _contentPanel;
        private Panel _chatSidebar;
        private FlowLayoutPanel _suggestionsPanel;
        private Label _messageLabel;
        private Label _conversationTitleLabel;
        private Label _monthAiUsageValueLabel;
        private Label _todayAiUsageValueLabel;
        private UiLayoutMode _currentLayoutMode = UiLayoutMode.Normal;
        private ComboBox _conversationComboBox;
        private ComboBox _modelComboBox;
        private FlowLayoutPanel _chatPanel;
        private Panel _chatFrame;
        private TextBox _inputTextBox;
        private Button _sendButton;
        private Button _copyAnswerButton;
        private string _lastAssistantAnswerText;
        private AiPurchaseDraft _pendingPurchaseDraft;
        private Panel _pendingPurchaseConfirmCard;
        private AiActionDraft _pendingActionDraft;
        private long _pendingActionDraftMessageId;
        private int _pendingActionIndex;
        private Panel _pendingActionCard;
        private TextBox _actionProductTextBox;
        private TextBox _actionSpecTextBox;
        private TextBox _actionCategoryTextBox;
        private TextBox _actionQuantityTextBox;
        private TextBox _actionUnitTextBox;
        private TextBox _actionPurchasePriceTextBox;
        private TextBox _actionSalePriceTextBox;
        private TextBox _actionProductionDateTextBox;
        private TextBox _actionExpiryDateTextBox;
        private CheckBox _actionShelfLifeCheckBox;
        private TextBox _actionCustomerTextBox;
        private TextBox _actionCreditAmountTextBox;
        private TextBox _actionReceivedAmountTextBox;
        private TextBox _actionInventoryAdjustTextBox;
        private TextBox _actionOldPriceTextBox;
        private TextBox _actionNewPriceTextBox;
        private ComboBox _actionProductCandidateComboBox;
        private Label _actionHintLabel;
        private bool _refreshingActionHint;
        private TextBox _purchaseProductTextBox;
        private TextBox _purchaseCategoryTextBox;
        private TextBox _purchaseQuantityTextBox;
        private TextBox _purchaseCostTextBox;
        private TextBox _purchaseSalePriceTextBox;
        private TextBox _purchaseProductionDateTextBox;
        private TextBox _purchaseExpiryDateTextBox;
        private CheckBox _purchaseRequiresExpiryCheckBox;
        private string _lastSuccessfulConnectionTestTime;
        private CancellationTokenSource _activeRequestCancellation;
        private bool _isSending;
        private bool _loadingConversations;
        private bool _loadingMessages;
        private bool _actionReceivedAmountManualEdit;
        private bool _profilePromptShown;
        private bool _profilePromptPending;
        private string _lastIntentType = string.Empty;
        private string _lastQueryType = string.Empty;
        private string _lastSubject = string.Empty;
        private string _lastCategoryName = string.Empty;
        private string _lastActionIntent = string.Empty;
        private decimal _lastBatchPriceDelta;
        private string _lastCandidateQueryKind = string.Empty;
        private readonly List<Product> _lastCandidateProducts = new List<Product>();
        private readonly List<string> _lastCancelledDrafts = new List<string>();

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
            _purchaseDraftService = new AiPurchaseDraftService();
            _actionDraftService = new AiActionDraftService();
            _intentRouter = new AiIntentRouter();
            _semanticIntentService = new AiSemanticIntentService();
            _localQueryService = new AiLocalQueryService();
            _quickQuestionService = new AiQuickQuestionService();
            _productService = new ProductService();
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
            RequestScrollChatToBottom();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                RequestScrollChatToBottom();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _activeRequestCancellation != null)
            {
                _activeRequestCancellation.Cancel();
                _activeRequestCancellation.Dispose();
                _activeRequestCancellation = null;
            }

            base.Dispose(disposing);
        }

        private void Render()
        {
            Controls.Clear();

            _pagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = UiTheme.PagePaddingValue,
                BackColor = BackColor
            };

            _pageHeaderPanel = UiComponentHelper.CreatePageHeader(
                "小铺掌柜 AI智能版 · AI 助手",
                "联网后可接入 DeepSeek API，用本地经营摘要生成建议、简报和报告。",
                null,
                "headers/ai_assistant");

            _contentPanel = UiComponentHelper.CreateCardPanel(new Padding(24));
            _contentPanel.Dock = DockStyle.Fill;

            _pagePanel.Controls.Add(_contentPanel);
            _pagePanel.Controls.Add(_pageHeaderPanel);
            Controls.Add(_pagePanel);

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
            _chatSidebar = sidebar;
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
                Height = 56,
                Padding = new Padding(0, 4, 0, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.CardBackground
            };
            _suggestionsPanel = suggestions;
            foreach (string question in _quickQuestionService.CreateBuiltInList())
            {
                AddSuggestionButton(suggestions, question, question, GetBuiltInAnalysisKey(question));
            }

            AddQuickQuestionMenuButton(suggestions);

            _chatPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(246, 249, 253),
                Padding = new Padding(18, 14, 18, 14)
            };
            _chatPanel.Resize += delegate { ResizeChatBubbles(); };

            Panel inputPanel = BuildInputPanel();
            _chatFrame = UiComponentHelper.CreateCardPanel(new Padding(0), Color.FromArgb(246, 249, 253), Color.FromArgb(196, 207, 222));
            _chatFrame.Dock = DockStyle.Fill;
            _chatFrame.Controls.Add(_chatPanel);

            main.Controls.Add(_chatFrame);
            main.Controls.Add(inputPanel);
            main.Controls.Add(suggestions);
            main.Controls.Add(mainHeader);

            shell.Controls.Add(main);
            shell.Controls.Add(divider);
            shell.Controls.Add(sidebar);
            _contentPanel.Controls.Add(shell);
            ApplyCurrentResponsiveLayout();

            ShowConversationLoadingState();
            BeginLoadConversationMessages();
            QueueStoreProfilePrompt();
        }

        public void ApplyLayout(UiLayoutMode mode)
        {
            _currentLayoutMode = mode;
            bool compact = ResponsiveLayoutManager.IsCompact(mode);
            bool veryCompact = ResponsiveLayoutManager.IsVeryCompact(mode);
            if (_pagePanel != null)
            {
                _pagePanel.Padding = veryCompact ? new Padding(8) : (compact ? new Padding(10) : UiTheme.PagePaddingValue);
            }

            if (_pageHeaderPanel != null)
            {
                _pageHeaderPanel.Height = veryCompact ? 108 : (compact ? 118 : 156);
            }

            if (_contentPanel != null)
            {
                _contentPanel.Padding = veryCompact ? new Padding(10) : (compact ? new Padding(12) : new Padding(24));
            }

            if (_chatSidebar != null)
            {
                _chatSidebar.Width = veryCompact ? 196 : (compact ? 218 : 286);
                _chatSidebar.Padding = veryCompact ? new Padding(0, 0, 6, 0) : (compact ? new Padding(0, 0, 8, 0) : new Padding(0, 0, 18, 0));
            }

            if (_chatPanel != null)
            {
                _chatPanel.Padding = veryCompact ? new Padding(8) : (compact ? new Padding(10) : new Padding(18, 14, 18, 14));
                ResizeChatBubbles();
            }

            if (_suggestionsPanel != null)
            {
                _suggestionsPanel.Height = compact ? 40 : 56;
                foreach (Control control in _suggestionsPanel.Controls)
                {
                    Button button = control as Button;
                    if (button == null)
                    {
                        continue;
                    }

                    button.Height = compact ? 32 : 44;
                    button.Font = UiTheme.Font(compact ? 8.4F : 10F, FontStyle.Bold);
                    button.Margin = compact ? new Padding(0, 0, 7, 3) : new Padding(0, 0, UiTheme.Gap, UiTheme.Gap);
                    if (button.Text.Length == 0)
                    {
                        button.Width = compact ? 32 : 44;
                    }
                    else
                    {
                        button.Width = compact ? 104 : 156;
                    }
                }
            }
        }

        private void ApplyCurrentResponsiveLayout()
        {
            ApplyLayout(_currentLayoutMode);
            ResponsiveLayoutManager.ApplyControlTree(this, _currentLayoutMode);
            ApplyLayout(_currentLayoutMode);
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
                Height = 58,
                BackColor = UiTheme.CardBackground
            };
            AiAvatar avatar = new AiAvatar { Location = new Point(0, 7), Size = new Size(40, 40) };
            Label nameLabel = new Label
            {
                Text = "小铺经营助手",
                Location = new Point(50, 2),
                Size = new Size(168, 24),
                Font = UiTheme.Font(12.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            Label statusLabel = new Label
            {
                Text = _networkStatus.IsNetworkAvailable ? "AI 联网能力：可用" : "AI 联网能力：不可用",
                Location = new Point(50, 28),
                Size = new Size(168, 22),
                Font = UiTheme.Font(9.5F),
                ForeColor = UiTheme.TextSecondary
            };
            identity.Controls.Add(avatar);
            identity.Controls.Add(nameLabel);
            identity.Controls.Add(statusLabel);

            Label conversationLabel = CreateSidebarTitle("会话管理（最多保留10条）");
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

            TableLayoutPanel conversationTools = CreateSidebarButtonGrid(newButton, renameButton, deleteButton, 30F);

            Label modelLabel = CreateSidebarTitle("模型与能力");
            modelLabel.Dock = DockStyle.Top;
            _modelComboBox = CreateModelComboBox(settings.AiModel);
            _modelComboBox.Dock = DockStyle.Top;
            _modelComboBox.Height = 30;
            _modelComboBox.SelectedIndexChanged += ModelComboBox_SelectedIndexChanged;

            Button apiButton = CreateSoftSidebarButton("API 设置", UiTheme.SoftBlue, UiTheme.PrimaryBlue);
            Button profileButton = CreateSoftSidebarButton("店铺记忆", UiTheme.SoftGreen, UiTheme.SuccessGreen);
            Button refreshButton = CreateSoftSidebarButton("检测网络", UiTheme.SoftCyan, UiTheme.InfoCyan);
            apiButton.Click += delegate { RenderConfigurationState(_settingsService.Load(), true); };
            profileButton.Click += delegate { ShowStoreProfileEditor(false); };
            refreshButton.Click += async delegate { await RefreshNetworkAsync(); };

            TableLayoutPanel settingsTools = CreateSidebarButtonGrid(apiButton, profileButton, refreshButton, 32F);
            Panel usageCard = BuildAiUsageCard();
            Panel usageSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 6,
                BackColor = UiTheme.CardBackground
            };

            sidebar.Controls.Add(usageCard);
            sidebar.Controls.Add(usageSpacer);
            sidebar.Controls.Add(settingsTools);
            sidebar.Controls.Add(_modelComboBox);
            sidebar.Controls.Add(modelLabel);
            sidebar.Controls.Add(conversationTools);
            sidebar.Controls.Add(_conversationComboBox);
            sidebar.Controls.Add(conversationLabel);
            sidebar.Controls.Add(identity);
        }

        private Panel BuildAiUsageCard()
        {
            Panel card = UiComponentHelper.CreateCardPanel(new Padding(8), UiTheme.SoftSlate, UiTheme.CardBorder);
            card.Dock = DockStyle.Top;
            card.Height = 148;
            card.Margin = new Padding(0, 0, 0, 10);

            Label title = new Label
            {
                Text = "AI 使用概览",
                Dock = DockStyle.Top,
                Height = 22,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            TableLayoutPanel metrics = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SoftSlate,
                Padding = new Padding(0)
            };
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Panel monthMetric = CreateAiUsageMetric("本月使用", UiTheme.SoftBlue, UiTheme.PrimaryBlue, out _monthAiUsageValueLabel);
            Panel todayMetric = CreateAiUsageMetric("今日请求", UiTheme.SoftGreen, UiTheme.SuccessGreen, out _todayAiUsageValueLabel);
            monthMetric.Margin = new Padding(0, 0, 6, 0);
            todayMetric.Margin = new Padding(6, 0, 0, 0);
            metrics.Controls.Add(monthMetric, 0, 0);
            metrics.Controls.Add(todayMetric, 1, 0);

            Panel advice = UiComponentHelper.CreateCardPanel(new Padding(4, 4, 4, 4), UiTheme.SoftOrange, UiTheme.WarningBorder);
            advice.Dock = DockStyle.Top;
            advice.Height = 52;

            TableLayoutPanel adviceLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.SoftOrange,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            adviceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            adviceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            Label adviceFirstLine = new Label
            {
                Text = "使用建议：高级分析才使用",
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(7.25F, FontStyle.Bold),
                ForeColor = UiTheme.WarningOrange,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            };

            Label adviceSecondLine = new Label
            {
                Text = "deepseek-v4-pro",
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(8F, FontStyle.Bold),
                ForeColor = UiTheme.WarningOrange,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            };

            adviceLayout.Controls.Add(adviceFirstLine, 0, 0);
            adviceLayout.Controls.Add(adviceSecondLine, 0, 1);
            advice.Controls.Add(adviceLayout);

            Panel adviceSpacer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 5,
                BackColor = UiTheme.SoftSlate
            };

            card.Controls.Add(advice);
            card.Controls.Add(adviceSpacer);
            card.Controls.Add(metrics);
            card.Controls.Add(title);
            UpdateAiUsageLabels();
            return card;
        }

        private Panel CreateAiUsageMetric(string label, Color backColor, Color accentColor, out Label valueLabel)
        {
            Panel panel = UiComponentHelper.CreateCardPanel(new Padding(5, 2, 5, 2), backColor, UiTheme.CardBorder);
            panel.Dock = DockStyle.Fill;

            valueLabel = new Label
            {
                Text = "0 次",
                Dock = DockStyle.Top,
                Height = 20,
                Font = UiTheme.Font(10.2F, FontStyle.Bold),
                ForeColor = accentColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label titleLabel = new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 16,
                Font = UiTheme.Font(8F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private void UpdateAiUsageLabels()
        {
            DateTime now = DateTime.Now;
            DateTime todayStart = now.Date;
            DateTime tomorrowStart = todayStart.AddDays(1);
            DateTime monthStart = new DateTime(now.Year, now.Month, 1);
            DateTime nextMonthStart = monthStart.AddMonths(1);

            int todayCount = _conversationService.CountUserRequests(todayStart, tomorrowStart);
            int monthCount = _conversationService.CountUserRequests(monthStart, nextMonthStart);

            if (_todayAiUsageValueLabel != null)
            {
                _todayAiUsageValueLabel.Text = todayCount + " 次";
            }

            if (_monthAiUsageValueLabel != null)
            {
                _monthAiUsageValueLabel.Text = monthCount + " 次";
            }
        }

        private Panel BuildMainConversationHeader(AiSettings settings)
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
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
            _sendButton.Click += async delegate
            {
                if (_isSending)
                {
                    CancelActiveRequest();
                    return;
                }

                await SendManualMessageAsync();
            };

            _copyAnswerButton = UiComponentHelper.CreateSecondaryButton("复制回答", 92);
            _copyAnswerButton.Location = new Point(532, 14);
            _copyAnswerButton.Click += delegate { CopyLatestAssistantAnswer(); };

            inputPanel.Resize += delegate
            {
                UpdateInputPanelLayout(inputPanel);
            };

            inputPanel.Controls.Add(_inputTextBox);
            inputPanel.Controls.Add(_copyAnswerButton);
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
                AddEmptyStateImage();
                return;
            }

            IList<AiStoredMessage> messages = _currentConversation.Messages;
            int startIndex = Math.Max(0, messages.Count - DefaultMessageRenderLimit);
            if (startIndex > 0)
            {
                AddChatBubble("已先显示最近 " + DefaultMessageRenderLimit + " 条消息。更早内容仍保存在本地会话记录中。", false, false);
            }

            for (int index = startIndex; index < messages.Count; index++)
            {
                AiStoredMessage message = messages[index];
                if (string.Equals(message.MessageType, "action_draft", StringComparison.OrdinalIgnoreCase))
                {
                    AiActionDraft draft = _actionDraftService.DeserializeDraft(message.DataContextType);
                    if (draft != null && HasPendingActionItems(draft))
                    {
                        AddActionDraftQueueCard(draft, message.Id, false);
                    }
                    else
                    {
                        AddChatBubble(message.Content, false, false);
                    }

                    continue;
                }

                if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    _lastAssistantAnswerText = message.Content;
                    if (string.Equals(message.MessageType, "chat", StringComparison.OrdinalIgnoreCase))
                    {
                        AddChatBubble(BuildStoredAssistantDisplayText(message), false, false);
                    }
                    else
                    {
                        AddChatBubble(message.Content, false, false);
                    }

                    continue;
                }

                AddChatBubble(message.Content, message.Role == "user", false);
            }

            RequestScrollChatToBottom();
        }

        private void ShowConversationLoadingState()
        {
            if (_chatPanel == null)
            {
                return;
            }

            _loadingMessages = true;
            _chatPanel.Controls.Clear();
            AddChatBubble("正在加载 AI 对话，请稍候……", false, false);
            SetSendingState(false);
            if (_sendButton != null)
            {
                _sendButton.Enabled = false;
            }
        }

        private void BeginLoadConversationMessages()
        {
            if (!IsHandleCreated || IsDisposed || Disposing)
            {
                LoadConversationMessagesSafely();
                return;
            }

            BeginInvoke(new MethodInvoker(LoadConversationMessagesSafely));
        }

        private void LoadConversationMessagesSafely()
        {
            try
            {
                LoadConversationMessages();
            }
            catch (Exception ex)
            {
                if (_chatPanel != null)
                {
                    _chatPanel.Controls.Clear();
                    AddChatBubble("AI 对话加载失败：" + ex.Message, false, false);
                }
            }
            finally
            {
                _loadingMessages = false;
                SetSendingState(false);
                RequestScrollChatToBottom();
            }
        }

        private async Task SendManualMessageAsync()
        {
            if (_loadingMessages)
            {
                return;
            }

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
            RemoveEmptyStateImage();
            AddChatBubble(userText, true, true);
            SetSendingState(true);
            RichTextBox waitingBubble = AddChatBubble("正在理解你的问题……", false, false);

            AiSemanticIntentResult semanticIntent = await TryClassifySemanticIntentAsync(userText);
            if (semanticIntent.Success)
            {
                SetBubbleContent(waitingBubble, "正在判断需要读取哪些本地数据……", false);
                ResizeBubble(waitingBubble);

                if (semanticIntent.IntentType == "unsafe")
                {
                    string answer = "这个操作风险太高，我不能直接执行。涉及清空、删除、撤销或绕过确认的请求，都需要你到对应页面手动核对处理。";
                    SetExistingBubbleAsLocalAnswer(waitingBubble, answer, "system_local", "已读取：语义识别结果\r\n未读取：本地经营数据\r\n数据缺失：危险动作已拦截");
                    SetSendingState(false);
                    return;
                }

                if (await TryHandleSemanticIntentAsync(userText, semanticIntent, waitingBubble))
                {
                    return;
                }

                if (semanticIntent.RouteType == AiIntentResult.RouteChat)
                {
                    BusinessSummaryResult chatContext = BusinessSummaryResult.Fail("普通对话", "NO_BUSINESS_CONTEXT");
                    string chatPrompt = BuildAiUserPrompt(chatContext, userText);
                    await SendToAiAsync(chatPrompt, chatContext, waitingBubble);
                    return;
                }

                SetBubbleContent(waitingBubble, string.IsNullOrWhiteSpace(semanticIntent.ClarificationQuestion)
                    ? "我还需要你再说具体一点，比如要查库存、看利润、问补货，还是登记销售/入库。"
                    : semanticIntent.ClarificationQuestion, false);
                ResizeBubble(waitingBubble);
                string question = string.IsNullOrWhiteSpace(semanticIntent.ClarificationQuestion)
                    ? "我还需要你再说具体一点，比如要查库存、看利润、问补货，还是登记销售/入库。"
                    : semanticIntent.ClarificationQuestion;
                SaveLocalMessage(question, "system_local", "已读取：语义识别结果\r\n未读取：本地经营数据\r\n数据缺失：意图需要确认");
                SetSendingState(false);
                return;
            }

            SetBubbleContent(waitingBubble, "AI 理解失败，正在使用本地规则兜底……", false);
            ResizeBubble(waitingBubble);

            if (TryHandleContextualFollowUp(userText, waitingBubble))
            {
                return;
            }

            if (TryHandleBatchPriceUpdatePreview(userText, waitingBubble))
            {
                return;
            }

            string routedText = ResolveContextualUserText(userText);
            AiIntentResult intent = _intentRouter.Route(routedText);
            if (intent.RouteType == AiIntentResult.RouteQuery)
            {
                string answer = _localQueryService.Answer(routedText, intent);
                _lastAssistantAnswerText = answer;
                SetExistingBubbleAsLocalAnswer(waitingBubble, answer, "system_local", BuildLocalQueryStorageContext(intent));
                UpdateConversationContext(intent, routedText);
                SetSendingState(false);
                return;
            }

            if (intent.RouteType == AiIntentResult.RouteAnalysis)
            {
                _lastIntentType = AiIntentResult.RouteAnalysis;
                _lastQueryType = intent.AnalysisKey;
                _lastSubject = routedText;
                ClearCandidateContext();
                await SendAnalysisAsync(userText, intent.AnalysisKey, waitingBubble);
                return;
            }

            if (intent.RouteType == AiIntentResult.RouteUnknown)
            {
                string question = string.IsNullOrWhiteSpace(intent.FollowUpQuestion)
                    ? "你是想查询信息，还是要执行入库、销售、改价等操作？"
                    : intent.FollowUpQuestion;
                _lastIntentType = AiIntentResult.RouteUnknown;
                _lastQueryType = string.Empty;
                _lastAssistantAnswerText = question;
                SetExistingBubbleAsLocalAnswer(waitingBubble, question, "system_local", "已读取：未使用本地经营数据\r\n未读取：今日销售摘要、库存摘要、赊账记录、进货记录\r\n数据缺失：意图不明确");
                SetSendingState(false);
                return;
            }

            if (intent.IsAction && await TryHandleActionDraftAsync(routedText, intent, waitingBubble))
            {
                _lastIntentType = AiIntentResult.RouteAction;
                _lastQueryType = string.Empty;
                ClearCandidateContext();
                return;
            }

            BusinessSummaryResult liveContext = BusinessSummaryResult.Fail("普通对话", "NO_BUSINESS_CONTEXT");
            string prompt = BuildAiUserPrompt(liveContext, userText);
            await SendToAiAsync(prompt, liveContext, waitingBubble);
        }

        private async Task<AiSemanticIntentResult> TryClassifySemanticIntentAsync(string userText)
        {
            AiSettings settings = _settingsService.Load();
            settings.AiApiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                return new AiSemanticIntentResult { Success = false, ErrorMessage = "API Key is empty." };
            }

            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)))
                {
                    return await _semanticIntentService.ClassifyAsync(
                        userText,
                        _currentConversation == null ? null : _currentConversation.Messages,
                        settings,
                        _storeProfileService.Load(),
                        cts.Token);
                }
            }
            catch
            {
                return new AiSemanticIntentResult { Success = false, ErrorMessage = "Semantic classifier failed." };
            }
        }

        private async Task<bool> TryHandleSemanticIntentAsync(string userText, AiSemanticIntentResult semantic, RichTextBox waitingBubble)
        {
            if (semantic == null || !semantic.Success || semantic.Confidence < 0.55m)
            {
                return false;
            }

            AiIntentResult intent = semantic.ToIntentResult();
            string routedText = semantic.BuildSubjectText(userText);

            if (intent.RouteType == AiIntentResult.RouteQuery)
            {
                SetBubbleContent(waitingBubble, "正在读取本地经营数据……", false);
                ResizeBubble(waitingBubble);
                string answer = _localQueryService.Answer(routedText, intent);
                _lastAssistantAnswerText = answer;
                await SendSemanticQueryAnswerAsync(userText, routedText, answer, intent, waitingBubble);
                UpdateConversationContext(intent, routedText);
                return true;
            }

            if (intent.RouteType == AiIntentResult.RouteAnalysis)
            {
                _lastIntentType = AiIntentResult.RouteAnalysis;
                _lastQueryType = intent.AnalysisKey;
                _lastSubject = routedText;
                ClearCandidateContext();
                await SendAnalysisAsync(userText, intent.AnalysisKey, waitingBubble);
                return true;
            }

            if (intent.RouteType == AiIntentResult.RouteUnknown && semantic.NeedsClarification)
            {
                string question = string.IsNullOrWhiteSpace(semantic.ClarificationQuestion)
                    ? "我还需要你再说具体一点，比如要查库存、看利润、问补货，还是登记销售/入库。"
                    : semantic.ClarificationQuestion;
                _lastIntentType = AiIntentResult.RouteUnknown;
                _lastQueryType = string.Empty;
                _lastAssistantAnswerText = question;
                SetExistingBubbleAsLocalAnswer(waitingBubble, question, "system_local", "已读取：语义识别结果\r\n未读取：本地经营数据\r\n数据缺失：用户意图需要确认");
                SetSendingState(false);
                return true;
            }

            if (intent.IsAction)
            {
                if (semantic.SemanticTask == "batch_price_update" || semantic.ActionType == "batch_price_update" || semantic.ActionType == "product_price_update")
                {
                    if (TryHandleSemanticBatchPriceUpdatePreview(semantic, waitingBubble))
                    {
                        return true;
                    }

                    if (TryHandleBatchPriceUpdatePreview(userText, waitingBubble))
                    {
                        return true;
                    }
                }

                if (await TryHandleActionDraftAsync(userText, intent, waitingBubble))
                {
                    _lastIntentType = AiIntentResult.RouteAction;
                    _lastQueryType = string.Empty;
                    ClearCandidateContext();
                    return true;
                }
            }

            return false;
        }

        private async Task SendSemanticQueryAnswerAsync(string userText, string routedText, string localAnswer, AiIntentResult intent, RichTextBox waitingBubble)
        {
            string contextInfo = BuildLocalQueryStorageContext(intent);
            AiSettings settings = _settingsService.Load();
            settings.AiApiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                SetExistingBubbleAsLocalAnswer(waitingBubble, localAnswer, "system_local", contextInfo);
                SetSendingState(false);
                return;
            }

            SetSendingState(true);
            SetBubbleContent(waitingBubble, "已读取本地数据，正在整理回答……", false);
            ResizeBubble(waitingBubble);
            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(65)))
                {
                    _activeRequestCancellation = cts;
                    DeepSeekClient client = new DeepSeekClient(settings.AiBaseUrl, settings.AiModel, settings.AiApiKey);
                    AiResponseResult result = await SendChatWithRetryAsync(
                        client,
                        settings,
                        BuildSemanticGroundedMessages(userText, routedText, localAnswer),
                        cts.Token,
                        waitingBubble,
                        contextInfo);

                    string finalAnswer = result.Success ? result.Content : localAnswer;
                    _lastAssistantAnswerText = finalAnswer;
                    SetBubbleContent(waitingBubble, BuildAssistantDisplayText(finalAnswer, contextInfo, DateTime.Now), false);
                    if (!result.Success)
                    {
                        SetBubbleTextColor(waitingBubble, UiTheme.DangerRed);
                    }

                    ResizeBubble(waitingBubble);
                    SaveLocalMessage(finalAnswer, result.Success ? "chat" : "system_local", contextInfo);
                    RequestScrollChatToBottom();
                }
            }
            catch
            {
                _lastAssistantAnswerText = localAnswer;
                SetBubbleContent(waitingBubble, BuildAssistantDisplayText(localAnswer, contextInfo, DateTime.Now), false);
                ResizeBubble(waitingBubble);
                SaveLocalMessage(localAnswer, "system_local", contextInfo);
                RequestScrollChatToBottom();
            }
            finally
            {
                _activeRequestCancellation = null;
                SetSendingState(false);
            }
        }

        private string ResolveContextualUserText(string userText)
        {
            if (_currentConversation == null || _currentConversation.Messages == null)
            {
                return userText;
            }

            IList<AiStoredMessage> messages = _currentConversation.Messages;
            int startIndex = messages.Count - 1;
            if (startIndex >= 0
                && string.Equals(messages[startIndex].Role, "user", StringComparison.OrdinalIgnoreCase)
                && string.Equals((messages[startIndex].Content ?? string.Empty).Trim(), (userText ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                startIndex--;
            }

            if (LooksLikeCorrectionSalesText(userText) && HasRecentCancelledDraftContext(messages, startIndex))
            {
                return NormalizeCorrectionToSaleText(userText);
            }

            if (LooksLikeCreditContinuation(userText) && HasRecentQueryKind(messages, startIndex, "credit_customers"))
            {
                return "查询全部未结清赊账";
            }

            if (!LooksLikeShortProductReply(userText))
            {
                return userText;
            }

            int assistantIndex = -1;
            for (int index = startIndex; index >= 0 && index >= startIndex - 6; index--)
            {
                AiStoredMessage message = messages[index];
                if (message != null && string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsAmbiguousProductPrompt(message.Content))
                    {
                        assistantIndex = index;
                    }

                    break;
                }
            }

            if (assistantIndex < 0)
            {
                return userText;
            }

            for (int index = assistantIndex - 1; index >= 0 && index >= assistantIndex - 8; index--)
            {
                AiStoredMessage message = messages[index];
                if (message == null || !string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AiIntentResult previousIntent = _intentRouter.Route(message.Content);
                if (previousIntent.RouteType != AiIntentResult.RouteQuery)
                {
                    continue;
                }

                if (previousIntent.QueryKind == "product_price")
                {
                    return userText.Trim() + " 多少钱";
                }

                if (previousIntent.QueryKind == "product_stock")
                {
                    return userText.Trim() + " 库存多少";
                }

                return "查 " + userText.Trim();
            }

            return userText;
        }

        private bool TryHandleContextualFollowUp(string userText)
        {
            return TryHandleContextualFollowUp(userText, null);
        }

        private bool TryHandleContextualFollowUp(string userText, RichTextBox waitingBubble)
        {
            string value = (userText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (_lastCandidateProducts.Count > 0)
            {
                if (LooksLikeAllCandidateRequest(value))
                {
                    string answer = _localQueryService.AnswerForProducts(_lastCandidateProducts, _lastCandidateQueryKind);
                    AddContextualLocalAnswer(answer, _lastCandidateQueryKind, value, false, waitingBubble);
                    return true;
                }

                int candidateIndex = ParseCandidateIndex(value);
                if (candidateIndex >= 0 && candidateIndex < _lastCandidateProducts.Count)
                {
                    Product product = _lastCandidateProducts[candidateIndex];
                    string answer = _localQueryService.AnswerForProduct(product, _lastCandidateQueryKind);
                    AddContextualLocalAnswer(answer, _lastCandidateQueryKind, FormatProductName(product), false, waitingBubble);
                    return true;
                }

                Product matchedCandidate = FindCandidateProduct(value);
                if (matchedCandidate != null)
                {
                    string answer = _localQueryService.AnswerForProduct(matchedCandidate, _lastCandidateQueryKind);
                    AddContextualLocalAnswer(answer, _lastCandidateQueryKind, FormatProductName(matchedCandidate), false, waitingBubble);
                    return true;
                }
            }

            string continuationText = ResolveSimpleQueryContinuation(value);
            if (!string.IsNullOrWhiteSpace(continuationText))
            {
                AiIntentResult intent = _intentRouter.Route(continuationText);
                if (intent.RouteType == AiIntentResult.RouteQuery)
                {
                    string answer = _localQueryService.Answer(continuationText, intent);
                    AddContextualLocalAnswer(answer, intent.QueryKind, continuationText, true, waitingBubble);
                    return true;
                }
            }

            return false;
        }

        private bool TryHandleBatchPriceUpdatePreview(string userText)
        {
            return TryHandleBatchPriceUpdatePreview(userText, null);
        }

        private bool TryHandleBatchPriceUpdatePreview(string userText, RichTextBox waitingBubble)
        {
            BatchPriceIntent intent = ParseBatchPriceIntent(userText);
            if (intent == null)
            {
                intent = ResolveBatchPriceFollowUp(userText);
            }

            if (intent == null)
            {
                return false;
            }

            string resolvedCategory = _localQueryService.ResolveCategoryNameFromText(intent.Category);
            if (!string.IsNullOrWhiteSpace(resolvedCategory))
            {
                intent.Category = resolvedCategory;
            }

            return ShowBatchPriceUpdatePreview(intent.Category, intent.Delta, userText, waitingBubble);
        }

        private bool TryHandleSemanticBatchPriceUpdatePreview(AiSemanticIntentResult semantic, RichTextBox waitingBubble)
        {
            if (semantic == null || semantic.SemanticTask != "batch_price_update")
            {
                return false;
            }

            string category = semantic.CategoryName;
            if (string.IsNullOrWhiteSpace(category))
            {
                category = _lastCategoryName;
            }

            if (string.IsNullOrWhiteSpace(category) || semantic.ActionPriceDelta == 0m)
            {
                return false;
            }

            return ShowBatchPriceUpdatePreview(category, semantic.ActionPriceDelta, semantic.NormalizedText, waitingBubble);
        }

        private bool ShowBatchPriceUpdatePreview(string category, decimal delta, string subjectText, RichTextBox waitingBubble)
        {
            string resolvedCategory = _localQueryService.ResolveCategoryNameFromText(category);
            if (!string.IsNullOrWhiteSpace(resolvedCategory))
            {
                category = resolvedCategory;
            }

            IList<Product> products = _productService.GetActiveProducts();
            List<Product> matches = new List<Product>();
            foreach (Product product in products)
            {
                if (product != null && IsSameCategory(product.CategoryName, category))
                {
                    matches.Add(product);
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("已识别为高风险批量改价。");
            builder.AppendLine("范围：分类 = " + category);
            builder.AppendLine("调整方式：当前售价 " + (delta >= 0 ? "+" : "-") + Math.Abs(delta).ToString("0.##") + " 元");
            builder.AppendLine();

            if (matches.Count == 0)
            {
                builder.AppendLine("当前没有找到分类为“" + category + "”的在售商品，所以没有生成可执行预览。");
            }
            else
            {
                builder.AppendLine("批量改价预览：");
                for (int index = 0; index < matches.Count; index++)
                {
                    Product product = matches[index];
                    decimal newPrice = Math.Max(0m, product.DefaultPrice + delta);
                    builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                        + "，当前售价 " + product.DefaultPrice.ToString("0.00")
                        + " 元，新售价 " + newPrice.ToString("0.00") + " 元");
                }
            }

            builder.AppendLine();
            builder.AppendLine("当前版本先只做识别和预览，不会直接批量写库。请到商品管理里逐个确认改价，避免误改整类商品。");
            AddContextualLocalAnswer(builder.ToString().TrimEnd(), "batch_price_update", subjectText, false, waitingBubble);
            _lastIntentType = AiIntentResult.RouteAction;
            _lastActionIntent = "batch_price_update";
            _lastCategoryName = category;
            _lastBatchPriceDelta = delta;
            return true;
        }

        private void AddContextualLocalAnswer(string answer, string queryKind, string subject, bool updateCandidates)
        {
            AddContextualLocalAnswer(answer, queryKind, subject, updateCandidates, null);
        }

        private void AddContextualLocalAnswer(string answer, string queryKind, string subject, bool updateCandidates, RichTextBox waitingBubble)
        {
            _lastAssistantAnswerText = answer;
            if (waitingBubble == null)
            {
                AddPersistentLocalBubble(answer, "system_local", BuildLocalQueryStorageContext(queryKind));
            }
            else
            {
                SetExistingBubbleAsLocalAnswer(waitingBubble, answer, "system_local", BuildLocalQueryStorageContext(queryKind));
                SetSendingState(false);
            }

            _lastIntentType = AiIntentResult.RouteQuery;
            _lastQueryType = queryKind ?? string.Empty;
            _lastSubject = subject ?? string.Empty;
            RememberCategoryContext(_lastQueryType, subject);

            if (updateCandidates)
            {
                StoreCandidateContext(subject, queryKind);
            }
        }

        private void SetExistingBubbleAsLocalAnswer(RichTextBox bubble, string answer, string messageType, string dataContext)
        {
            if (bubble == null)
            {
                AddPersistentLocalBubble(answer, messageType, dataContext);
                return;
            }

            _lastAssistantAnswerText = answer;
            SetBubbleContent(bubble, BuildAssistantDisplayText(answer, dataContext, DateTime.Now), false);
            ResizeBubble(bubble);
            SaveLocalMessage(answer, messageType, dataContext);
            RequestScrollChatToBottom();
        }

        private void UpdateConversationContext(AiIntentResult intent, string routedText)
        {
            _lastIntentType = intent == null ? string.Empty : intent.RouteType;
            _lastQueryType = intent == null ? string.Empty : intent.QueryKind;
            _lastSubject = routedText ?? string.Empty;
            RememberCategoryContext(_lastQueryType, routedText);

            if (IsProductQuery(_lastQueryType))
            {
                StoreCandidateContext(routedText, _lastQueryType);
            }
            else
            {
                ClearCandidateContext();
            }
        }

        private void StoreCandidateContext(string routedText, string queryKind)
        {
            ClearCandidateContext();
            IList<Product> candidates = _localQueryService.FindProductCandidatesForText(routedText);
            if (candidates == null || candidates.Count <= 1)
            {
                return;
            }

            _lastCandidateProducts.AddRange(candidates);
            _lastCandidateQueryKind = queryKind ?? string.Empty;
        }

        private void ClearCandidateContext()
        {
            _lastCandidateProducts.Clear();
            _lastCandidateQueryKind = string.Empty;
        }

        private void RememberCategoryContext(string queryKind, string text)
        {
            if (string.IsNullOrWhiteSpace(queryKind))
            {
                return;
            }

            if (queryKind != "category_stock"
                && queryKind != "category_query"
                && queryKind != "category_low_stock"
                && queryKind != "restock_advice"
                && queryKind != "new_product_advice"
                && queryKind != "batch_price_update"
                && queryKind != "low_stock")
            {
                return;
            }

            string category = _localQueryService.ResolveCategoryNameFromText(text);
            if (!string.IsNullOrWhiteSpace(category))
            {
                _lastCategoryName = category;
            }
        }

        private bool HasRecentQueryKind(IList<AiStoredMessage> messages, int startIndex, string queryKind)
        {
            for (int index = startIndex; index >= 0 && index >= startIndex - 12; index--)
            {
                AiStoredMessage message = messages[index];
                if (message == null || !string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AiIntentResult previousIntent = _intentRouter.Route(message.Content);
                if (previousIntent.RouteType == AiIntentResult.RouteQuery && previousIntent.QueryKind == queryKind)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeShortProductReply(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 24)
            {
                return false;
            }

            return !ContainsAny(value,
                "多少钱", "售价", "价格", "库存", "入库", "销售", "卖了", "进了", "登记", "分析", "报表",
                "赊账", "欠款", "改价", "删除", "报废", "确认", "取消", "保存", "不要", "不用");
        }

        private static bool LooksLikeCreditContinuation(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 12)
            {
                return false;
            }

            return ContainsAny(value, "那之前", "之前的", "以前的", "历史的", "那以前", "之前呢", "以前呢");
        }

        private static bool IsAmbiguousProductPrompt(string text)
        {
            return ContainsAny(text, "找到几个相近商品", "你想查哪一个", "你想查哪个", "有几种", "想查哪种");
        }

        private static bool LooksLikeAllCandidateRequest(string text)
        {
            string value = NormalizeContextText(text);
            return ContainsAny(value, "都告诉我", "都说一下", "全部", "全都", "两个都看", "都查一下", "都看看", "都要", "一起说");
        }

        private static int ParseCandidateIndex(string text)
        {
            string value = NormalizeContextText(text);
            if (ContainsAny(value, "第一个", "第1个", "第1", "一号", "第一个吧"))
            {
                return 0;
            }

            if (ContainsAny(value, "第二个", "第2个", "第2", "二号", "第二个吧"))
            {
                return 1;
            }

            Match match = Regex.Match(value, @"第?(?<index>\d+)个?");
            if (match.Success && int.TryParse(match.Groups["index"].Value, out int parsed))
            {
                return parsed - 1;
            }

            return -1;
        }

        private Product FindCandidateProduct(string text)
        {
            string value = NormalizeContextText(text);
            value = Regex.Replace(value, @"(都告诉我|都说一下|全部|全都|这个|那个|那|呢|吧|啊|嘛|吗)$", string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            foreach (Product product in _lastCandidateProducts)
            {
                string productName = NormalizeContextText(product.Name);
                string displayName = NormalizeContextText(FormatProductName(product));
                if (productName == value || displayName == value || productName.Contains(value) || displayName.Contains(value) || value.Contains(productName))
                {
                    return product;
                }
            }

            return null;
        }

        private string ResolveSimpleQueryContinuation(string text)
        {
            string value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string category = _localQueryService.ResolveCategoryNameFromText(value);
            if (!string.IsNullOrWhiteSpace(category))
            {
            if (_lastQueryType == "restock_advice" || ContainsAny(value, "补", "进货", "该进", "建议"))
                {
                    return category + " 补货建议";
                }

                if (ContainsAny(value, "快没", "低库存", "缺货", "该补", "补货"))
                {
                    return category + " 哪些快没了";
                }

                if (_lastQueryType == "category_stock"
                    || _lastQueryType == "category_low_stock"
                    || LooksLikeSubjectContinuation(value)
                    || ContainsAny(value, "有哪些", "有什么", "库存", "商品"))
                {
                    return category + " 库存有哪些";
                }
            }

            if (LooksLikeCreditContinuation(value) && _lastQueryType == "credit_customers")
            {
                return "查询全部未结清赊账";
            }

            if (!string.IsNullOrWhiteSpace(_lastCategoryName)
                && (_lastQueryType == "category_stock" || _lastQueryType == "category_low_stock")
                && ContainsAny(value, "哪些快没", "快没了", "快没货", "低库存", "缺货", "该补", "补货"))
            {
                return _lastCategoryName + " 哪些快没了";
            }

            if (!string.IsNullOrWhiteSpace(_lastCategoryName)
                && _lastQueryType == "restock_advice"
                && ContainsAny(value, "那", "只看", "这个分类", "这一类", "呢", "嘛"))
            {
                return _lastCategoryName + " 补货建议";
            }

            if ((_lastQueryType == "product_stock" || _lastQueryType == "product_price") && LooksLikeSubjectContinuation(value))
            {
                string subject = ExtractContinuationSubject(value);
                if (!string.IsNullOrWhiteSpace(subject))
                {
                    return subject + (_lastQueryType == "product_price" ? " 多少钱" : " 库存多少");
                }
            }

            return string.Empty;
        }

        private static bool LooksLikeSubjectContinuation(string text)
        {
            string value = NormalizeContextText(text);
            if (string.IsNullOrWhiteSpace(value) || value.Length > 20)
            {
                return false;
            }

            return value.EndsWith("呢", StringComparison.Ordinal)
                || value.StartsWith("那", StringComparison.Ordinal)
                || value.EndsWith("吗", StringComparison.Ordinal)
                || value.EndsWith("嘛", StringComparison.Ordinal);
        }

        private static string ExtractContinuationSubject(string text)
        {
            string value = (text ?? string.Empty).Trim();
            value = Regex.Replace(value, @"^(那|那么|再查|查一下|看看|这个|那个)", string.Empty);
            value = Regex.Replace(value, @"(呢|吗|嘛|啊|吧|？|\?)$", string.Empty);
            return value.Trim();
        }

        private static bool LooksLikeCorrectionSalesText(string text)
        {
            string value = text ?? string.Empty;
            return ContainsAny(value, "搞错了", "不对", "重新记", "刚才说错了", "说错了", "不是")
                && ContainsAny(value, "瓶", "包", "袋", "条", "件", "个", "盒", "罐", "支");
        }

        private static string NormalizeCorrectionToSaleText(string text)
        {
            string value = (text ?? string.Empty).Trim();
            int index = value.LastIndexOf('是');
            if (index >= 0 && index + 1 < value.Length)
            {
                value = value.Substring(index + 1);
            }

            value = Regex.Replace(value, @"(搞错了|不对|重新记|刚才说错了|说错了|应该是|不是|，|,|。)", " ");
            value = value.Replace("+", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(value) ? text : "刚才卖了 " + value;
        }

        private bool HasRecentCancelledDraftContext(IList<AiStoredMessage> messages, int startIndex)
        {
            if (_lastCancelledDrafts.Count > 0)
            {
                return true;
            }

            for (int index = startIndex; index >= 0 && index >= startIndex - 12; index--)
            {
                AiStoredMessage message = messages[index];
                if (message != null && ContainsAny(message.Content, "已取消当前草稿", "已取消全部 AI 动作草稿", "已取消当前"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsProductQuery(string queryKind)
        {
            return queryKind == "product_stock" || queryKind == "product_price";
        }

        private static string NormalizeContextText(string text)
        {
            return (text ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        private static bool ContainsNormalized(string source, string keyword)
        {
            string normalizedSource = NormalizeContextText(source);
            string normalizedKeyword = NormalizeContextText(keyword);
            return !string.IsNullOrWhiteSpace(normalizedKeyword) && normalizedSource.IndexOf(normalizedKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatProductName(Product product)
        {
            if (product == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(product.Specification)
                ? (product.Name ?? string.Empty).Trim()
                : ((product.Name ?? string.Empty).Trim() + " " + product.Specification.Trim()).Trim();
        }

        private BatchPriceIntent ResolveBatchPriceFollowUp(string text)
        {
            string category = _localQueryService.ResolveCategoryNameFromText(text);
            if (!string.IsNullOrWhiteSpace(category) && _lastActionIntent == "batch_price_update" && _lastBatchPriceDelta != 0m)
            {
                return new BatchPriceIntent
                {
                    Category = category,
                    Delta = _lastBatchPriceDelta
                };
            }

            decimal delta;
            if (!string.IsNullOrWhiteSpace(_lastCategoryName) && TryParseBatchPriceDeltaOnly(text, out delta))
            {
                return new BatchPriceIntent
                {
                    Category = _lastCategoryName,
                    Delta = delta
                };
            }

            return null;
        }

        private static bool TryParseBatchPriceDeltaOnly(string text, out decimal delta)
        {
            delta = 0m;
            string value = text ?? string.Empty;
            if (!ContainsAny(value, "涨价", "降价", "都涨", "都降", "涨", "降"))
            {
                return false;
            }

            Match match = Regex.Match(value, @"(?<direction>涨价|降价|涨|降)\s*(?<amount>\d+(?:\.\d+)?)\s*(?:块|元)?");
            if (!match.Success)
            {
                return false;
            }

            decimal amount;
            if (!decimal.TryParse(match.Groups["amount"].Value, out amount))
            {
                return false;
            }

            delta = match.Groups["direction"].Value.Contains("降") ? -amount : amount;
            return true;
        }

        private static bool IsSameCategory(string productCategory, string targetCategory)
        {
            string productValue = NormalizeContextText(productCategory);
            string targetValue = NormalizeContextText(targetCategory);
            return !string.IsNullOrWhiteSpace(productValue)
                && !string.IsNullOrWhiteSpace(targetValue)
                && (productValue == targetValue || productValue.Contains(targetValue) || targetValue.Contains(productValue));
        }

        private static BatchPriceIntent ParseBatchPriceIntent(string text)
        {
            string value = text ?? string.Empty;
            if (!ContainsAny(value, "涨价", "降价", "都涨", "都降") || !ContainsAny(value, "所有", "全部", "整类", "这一类"))
            {
                return null;
            }

            Match match = Regex.Match(value, @"(?:把|将)?(?:所有|全部)?(?<category>[\u4e00-\u9fa5A-Za-z0-9]{1,12})(?:都|全部|一起)?(?<direction>涨价|降价|涨|降)\s*(?<amount>\d+(?:\.\d+)?)\s*(?:块|元)?");
            if (!match.Success)
            {
                return null;
            }

            string category = match.Groups["category"].Value.Trim();
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            decimal amount;
            if (!decimal.TryParse(match.Groups["amount"].Value, out amount))
            {
                return null;
            }

            string direction = match.Groups["direction"].Value;
            return new BatchPriceIntent
            {
                Category = category,
                Delta = direction.Contains("降") ? -amount : amount
            };
        }

        private sealed class BatchPriceIntent
        {
            public string Category { get; set; }

            public decimal Delta { get; set; }
        }

        private async Task SendKnownAnalysisAsync(string prompt, string analysisKey)
        {
            RemoveEmptyStateImage();
            AddChatBubble(prompt, true, true);
            AiIntentResult intent = _intentRouter.RouteKnownAnalysis(analysisKey);
            await SendAnalysisAsync(prompt, intent.AnalysisKey);
        }

        private async Task SendAnalysisAsync(string userText, string analysisKey)
        {
            await SendAnalysisAsync(userText, analysisKey, null);
        }

        private async Task SendAnalysisAsync(string userText, string analysisKey, RichTextBox waitingBubble)
        {
            BusinessSummaryResult liveContext = BuildAnalysisContext(analysisKey, userText);
            string prompt = BuildAiUserPrompt(liveContext, userText);
            await SendToAiAsync(prompt, liveContext, waitingBubble);
        }

        private BusinessSummaryResult BuildAnalysisContext(string analysisKey, string userText)
        {
            if (analysisKey == "today")
            {
                return _businessSummaryService.BuildTodaySummary();
            }

            if (analysisKey == "yesterday")
            {
                return _businessSummaryService.BuildYesterdaySummary();
            }

            if (analysisKey == "week")
            {
                return _businessSummaryService.BuildWeekSummary();
            }

            if (analysisKey == "month")
            {
                return _businessSummaryService.BuildMonthSummary();
            }

            if (analysisKey == "inventoryRisk" || analysisKey == "inventory")
            {
                return _businessSummaryService.BuildInventoryRiskSummary();
            }

            if (analysisKey == "credit")
            {
                return _businessSummaryService.BuildCreditRiskSummary();
            }

            if (analysisKey == "hotSlow" || analysisKey == "hot_slow")
            {
                return _businessSummaryService.BuildHotAndSlowProductsSummary();
            }

            return _businessSummaryService.BuildLiveContextForUserQuestion(userText);
        }

        private static string BuildLocalQueryStorageContext(AiIntentResult intent)
        {
            return BuildLocalQueryStorageContext(intent == null ? string.Empty : intent.QueryKind);
        }

        private static string BuildLocalQueryStorageContext(string queryKind)
        {
            string readText = "库存摘要";
            if (queryKind == "credit_customers")
            {
                readText = "赊账记录";
            }
            else if (queryKind == "scrap_loss")
            {
                readText = "报废记录";
            }
            else if (queryKind == "scrap_records")
            {
                readText = "报废记录";
            }
            else if (queryKind == "batch_price_update")
            {
                readText = "商品清单";
            }
            else if (queryKind == "category_stock" || queryKind == "category_query" || queryKind == "category_low_stock")
            {
                readText = "商品清单、库存摘要";
            }
            else if (queryKind == "restock_advice")
            {
                readText = "商品清单、库存摘要、低库存商品、近期销售排行";
            }
            else if (queryKind == "new_product_advice")
            {
                readText = "商品清单、库存摘要、分类结构、店铺记忆";
            }
            else if (queryKind == "all_inventory" || queryKind == "product_price" || queryKind == "product_stock" || queryKind == "low_stock" || queryKind == "expiring_products")
            {
                readText = "库存摘要";
            }

            return "已读取：" + readText
                + "\r\n未读取：今日销售摘要、进货记录、店铺记忆"
                + "\r\n数据缺失：未发现明显缺失";
        }

        private async Task<bool> TryHandleActionDraftAsync(string userText, AiIntentResult intent)
        {
            return await TryHandleActionDraftAsync(userText, intent, null);
        }

        private async Task<bool> TryHandleActionDraftAsync(string userText, AiIntentResult intent, RichTextBox statusBubble)
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                return false;
            }

            if (intent == null || !intent.IsAction)
            {
                return false;
            }

            if (HasPendingActionItems(_pendingActionDraft) && IsCancelAllActionText(userText))
            {
                CancelAllPendingActions("已取消全部 AI 动作草稿，未修改任何经营数据。");
                return true;
            }

            BusinessSummaryResult liveContext = _businessSummaryService.BuildInventorySnapshotSummary();
            AiSettings settings = _settingsService.Load();
            settings.AiApiKey = _settingsService.GetApiKey();
            SetSendingState(true);
            if (statusBubble == null)
            {
                statusBubble = AddChatBubble("正在生成确认单……\r\n我会先生成草稿，不会直接修改数据库。", false, false);
            }
            else
            {
                SetBubbleContent(statusBubble, "正在生成确认单……\r\n我会先生成草稿，不会直接修改数据库。", false);
                ResizeBubble(statusBubble);
            }

            try
            {
                AiActionDraftParseResult parseResult = null;
                bool usedFallback = false;
                AiActionDraftParseResult localFirst = _actionDraftService.CreateLocalFallbackDraft(_currentConversation.Id, userText);
                if (ShouldUseLocalActionDraft(localFirst))
                {
                    parseResult = localFirst;
                    usedFallback = true;
                }
                else if (!string.IsNullOrWhiteSpace(settings.AiApiKey))
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(65)))
                    {
                        _activeRequestCancellation = cts;
                        DeepSeekClient client = new DeepSeekClient(settings.AiBaseUrl, settings.AiModel, settings.AiApiKey);
                        parseResult = await client.ParseActionDraftAsync(userText, _storeProfileService.Load(), liveContext, _currentConversation.Id, cts.Token);
                    }
                }

                if (parseResult == null || !parseResult.Success)
                {
                    usedFallback = true;
                    parseResult = localFirst;
                }

                if (parseResult == null || !parseResult.Success || parseResult.Draft == null)
                {
                    string error = parseResult == null ? "AI 没有识别成功，请换一种说法或手动登记。" : parseResult.ErrorMessage;
                    SetBubbleContent(statusBubble, error, false);
                    SetBubbleTextColor(statusBubble, UiTheme.DangerRed);
                    ResizeBubble(statusBubble);
                    SaveLocalMessage(error, "error", string.Empty);
                    return true;
                }

                AiActionDraft draft = parseResult.Draft;
                _actionDraftService.ValidateDraft(draft);
                bool onlyUnknown = draft.Items.Count == 0 || draft.Items.All(item => item.ActionType == AiActionTypes.Unknown);
                if (draft.NeedUserClarification && onlyUnknown)
                {
                    string question = string.IsNullOrWhiteSpace(draft.ClarificationQuestion)
                        ? "我还没识别清楚要做哪类经营操作，请补充商品、数量或金额。"
                        : draft.ClarificationQuestion;
                    SetBubbleContent(statusBubble, question, false);
                    ResizeBubble(statusBubble);
                    SaveLocalMessage(question, "system_local", string.Empty);
                    return true;
                }

                string statusText = (usedFallback ? "AI JSON 解析未完成，已先用本地规则生成草稿。" : "AI 已生成动作草稿。")
                    + "\r\n请核对确认单，确认前不会写入数据库。";
                SetBubbleContent(statusBubble, statusText, false);
                ResizeBubble(statusBubble);
                SaveLocalMessage(statusText, "system_local", string.Empty);

                _pendingActionDraft = draft;
                _pendingActionIndex = FindFirstPendingActionIndex(draft);
                _pendingActionDraftMessageId = SaveActionDraftMessage(draft);
                AddActionDraftQueueCard(draft, _pendingActionDraftMessageId, true);
                return true;
            }
            catch (OperationCanceledException)
            {
                string text = "AI 动作识别已取消，未修改任何经营数据。";
                SetBubbleContent(statusBubble, text, false);
                SetBubbleTextColor(statusBubble, UiTheme.DangerRed);
                ResizeBubble(statusBubble);
                SaveLocalMessage(text, "error", string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                AiActionDraftParseResult fallback = _actionDraftService.CreateLocalFallbackDraft(_currentConversation.Id, userText);
                string text = "AI 动作识别遇到问题，已启用本地草稿兜底。\r\n原因：" + ex.Message;
                SetBubbleContent(statusBubble, text, false);
                ResizeBubble(statusBubble);
                SaveLocalMessage(text, "system_local", string.Empty);
                _pendingActionDraft = fallback.Draft;
                _pendingActionIndex = FindFirstPendingActionIndex(_pendingActionDraft);
                _pendingActionDraftMessageId = SaveActionDraftMessage(_pendingActionDraft);
                AddActionDraftQueueCard(_pendingActionDraft, _pendingActionDraftMessageId, true);
                return true;
            }
            finally
            {
                if (_activeRequestCancellation != null)
                {
                    _activeRequestCancellation.Dispose();
                    _activeRequestCancellation = null;
                }

                SetSendingState(false);
            }
        }

        private bool HandlePurchaseAssistantMessage(string userText)
        {
            if (_pendingPurchaseDraft == null && !_purchaseDraftService.IsPurchaseIntent(userText))
            {
                return false;
            }

            if (IsCancelPurchaseText(userText))
            {
                _pendingPurchaseDraft = null;
                RemovePendingPurchaseConfirmCard();
                AddChatBubble("已取消本次 AI 入库登记，未修改任何经营数据。", false, false);
                return true;
            }

            _pendingPurchaseDraft = _purchaseDraftService.UpdateDraft(_pendingPurchaseDraft, userText);
            AiPurchaseDraftReview review = _purchaseDraftService.Review(_pendingPurchaseDraft);
            RemovePendingPurchaseConfirmCard();

            if (!review.IsReady)
            {
                AddChatBubble(_purchaseDraftService.BuildMissingFieldsMessage(review), false, false);
                return true;
            }

            AddChatBubble("我已经把入库信息整理成确认单了。请先核对，确认后我再写入商品和入库数据。", false, false);
            AddPurchaseConfirmationCard(review);
            return true;
        }

        private static bool ShouldUseLocalActionDraft(AiActionDraftParseResult parseResult)
        {
            if (parseResult == null || !parseResult.Success || parseResult.Draft == null || parseResult.Draft.Items == null)
            {
                return false;
            }

            foreach (AiActionDraftItem item in parseResult.Draft.Items)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.ActionType == AiActionTypes.CreditRegister
                    || item.ActionType == AiActionTypes.InventoryAdjust
                    || item.ActionType == AiActionTypes.ProductPriceUpdate
                    || item.ActionType == AiActionTypes.DeleteOrUndoRequest)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCancelPurchaseText(string text)
        {
            text = text ?? string.Empty;
            return text.Contains("取消")
                || text.Contains("不用登记")
                || text.Contains("先不入库")
                || text.Contains("不要入库")
                || text.Contains("不保存");
        }

        private void AddPurchaseConfirmationCard(AiPurchaseDraftReview review)
        {
            if (_chatPanel == null)
            {
                return;
            }

            int width = ResolveBubbleWidth(string.Empty, false);
            Panel card = UiComponentHelper.CreateCardPanel(new Padding(16), Color.White, Color.FromArgb(184, 199, 219));
            card.Width = width;
            card.Height = 346;
            card.Margin = ResolveBubbleMargin(width, false);
            card.Tag = "purchase-confirm-card";

            Label title = new Label
            {
                Text = "AI 入库确认单",
                Dock = DockStyle.Top,
                Height = 32,
                Font = UiTheme.Font(13.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Panel formPanel = BuildPurchaseConfirmForm(review);

            Panel buttonRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.White
            };

            Button confirmButton = UiComponentHelper.CreatePrimaryButton("确认执行", 112);
            Button reviseButton = UiComponentHelper.CreateSecondaryButton("继续修改", 112);
            Button cancelButton = UiComponentHelper.CreateButton("取消入库", 112, UiTheme.SoftRed, UiTheme.DangerRed, UiTheme.DangerBorder);
            confirmButton.Location = new Point(0, 10);
            reviseButton.Location = new Point(0, 10);
            cancelButton.Location = new Point(0, 10);

            confirmButton.Click += delegate
            {
                confirmButton.Enabled = false;
                reviseButton.Enabled = false;
                cancelButton.Enabled = false;
                if (!TryApplyPurchaseFormToDraft(review.Draft))
                {
                    confirmButton.Enabled = true;
                    reviseButton.Enabled = true;
                    cancelButton.Enabled = true;
                    return;
                }

                AiPurchaseDraftReview latestReview = _purchaseDraftService.Review(review.Draft);
                if (!latestReview.IsReady)
                {
                    confirmButton.Enabled = true;
                    reviseButton.Enabled = true;
                    cancelButton.Enabled = true;
                    AddChatBubble(_purchaseDraftService.BuildMissingFieldsMessage(latestReview), false, false);
                    RemovePendingPurchaseConfirmCard();
                    return;
                }

                AiPurchaseExecutionResult result = _purchaseDraftService.Execute(latestReview.Draft);
                if (result.Success)
                {
                    _pendingPurchaseDraft = null;
                    AddChatBubble(BuildPurchaseSuccessMessage(latestReview, result), false, false);
                    UpdateAiUsageLabels();
                }
                else
                {
                    AddChatBubble(result.Message, false, false);
                }

                RequestScrollChatToBottom();
            };

            reviseButton.Click += delegate
            {
                AddChatBubble("可以继续补充或修改信息，例如：“售价改成3.5元，分类饮料”。我会重新整理确认单。", false, false);
                RequestScrollChatToBottom();
            };

            cancelButton.Click += delegate
            {
                _pendingPurchaseDraft = null;
                RemovePendingPurchaseConfirmCard();
                AddChatBubble("已取消本次入库确认，未修改任何经营数据。", false, false);
            };

            buttonRow.Controls.Add(confirmButton);
            buttonRow.Controls.Add(reviseButton);
            buttonRow.Controls.Add(cancelButton);
            card.Controls.Add(formPanel);
            card.Controls.Add(buttonRow);
            card.Controls.Add(title);
            _pendingPurchaseConfirmCard = card;
            _chatPanel.Controls.Add(card);
            card.PerformLayout();
            LayoutPurchaseConfirmButtons(card);
            RequestScrollChatToBottom();
        }

        private string BuildPurchaseSuccessMessage(AiPurchaseDraftReview review, AiPurchaseExecutionResult result)
        {
            AiPurchaseDraft draft = review.Draft;
            string productName = review.MatchedProduct == null
                ? (draft.ProductName + " " + draft.Specification).Trim()
                : review.MatchedProduct.Name;

            return result.Message
                + "\r\n\r\n商品：" + productName
                + "\r\n入库数量：" + draft.Quantity.GetValueOrDefault().ToString("0.###") + " 件"
                + "\r\n进货单价：" + draft.PurchasePrice.GetValueOrDefault().ToString("0.00") + " 元"
                + "\r\n入库金额：" + (draft.Quantity.GetValueOrDefault() * draft.PurchasePrice.GetValueOrDefault()).ToString("0.00") + " 元"
                + "\r\n数据已写入：商品管理、进货入库、库存批次";
        }

        private Panel BuildPurchaseConfirmForm(AiPurchaseDraftReview review)
        {
            AiPurchaseDraft draft = review.Draft;
            Panel panel = UiComponentHelper.CreateCardPanel(new Padding(12), UiTheme.SoftSlate, UiTheme.CardBorder);
            panel.Dock = DockStyle.Fill;

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 190,
                ColumnCount = 4,
                RowCount = 4,
                BackColor = UiTheme.SoftSlate,
                Padding = new Padding(0)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int index = 0; index < 4; index++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            }

            string productName = review.MatchedProduct == null
                ? ((draft.ProductName + " " + draft.Specification).Trim())
                : review.MatchedProduct.Name;
            _purchaseProductTextBox = AddPurchaseFormTextBox(table, 0, 0, "商品", productName);
            _purchaseCategoryTextBox = AddPurchaseFormTextBox(table, 0, 1, "分类", review.MatchedCategory != null ? review.MatchedCategory.Name : draft.CategoryName);
            _purchaseQuantityTextBox = AddPurchaseFormTextBox(table, 1, 0, "入库数量", draft.Quantity.HasValue ? draft.Quantity.Value.ToString("0.###") : string.Empty);
            _purchaseCostTextBox = AddPurchaseFormTextBox(table, 1, 1, "进货单价", draft.PurchasePrice.HasValue ? draft.PurchasePrice.Value.ToString("0.00") : string.Empty);
            _purchaseSalePriceTextBox = AddPurchaseFormTextBox(table, 2, 0, "预计售价", draft.SalePrice.HasValue ? draft.SalePrice.Value.ToString("0.00") : string.Empty);
            _purchaseProductionDateTextBox = AddPurchaseFormTextBox(table, 2, 1, "生产日期", draft.ProductionDate.HasValue ? draft.ProductionDate.Value.ToString("yyyy-MM-dd") : string.Empty);
            _purchaseExpiryDateTextBox = AddPurchaseFormTextBox(table, 3, 0, "到期日期", draft.ExpiryDate.HasValue ? draft.ExpiryDate.Value.ToString("yyyy-MM-dd") : string.Empty);

            Label expiryLabel = CreatePurchaseFormLabel("保质期");
            table.Controls.Add(expiryLabel, 2, 3);
            _purchaseRequiresExpiryCheckBox = new CheckBox
            {
                Text = "启用",
                Checked = draft.RequiresExpiry.GetValueOrDefault(false),
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10F),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = UiTheme.SoftSlate
            };
            table.Controls.Add(_purchaseRequiresExpiryCheckBox, 3, 3);

            Label hintLabel = new Label
            {
                Text = "可以直接在这里修改字段。确认执行前不会写入数据库。",
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(9.5F),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(hintLabel);
            panel.Controls.Add(table);
            return panel;
        }

        private TextBox AddPurchaseFormTextBox(TableLayoutPanel table, int row, int pairIndex, string labelText, string value)
        {
            int labelColumn = pairIndex == 0 ? 0 : 2;
            int inputColumn = pairIndex == 0 ? 1 : 3;
            table.Controls.Add(CreatePurchaseFormLabel(labelText), labelColumn, row);

            TextBox textBox = new TextBox
            {
                Text = value ?? string.Empty,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10F),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 5, 12, 5)
            };
            UiComponentHelper.CenterTextBoxContent(textBox);
            table.Controls.Add(textBox, inputColumn, row);
            return textBox;
        }

        private static Label CreatePurchaseFormLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 8, 0)
            };
        }

        private bool TryApplyPurchaseFormToDraft(AiPurchaseDraft draft)
        {
            decimal quantity;
            decimal purchasePrice;
            decimal salePrice;
            DateTime dateValue;

            draft.ProductName = (_purchaseProductTextBox.Text ?? string.Empty).Trim();
            draft.CategoryName = (_purchaseCategoryTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(draft.ProductName))
            {
                AddChatBubble("商品名称不能为空，请在确认单里填写商品名称。", false, false);
                return false;
            }

            string specification = ExtractSpecificationFromText(draft.ProductName);
            if (!string.IsNullOrWhiteSpace(specification))
            {
                draft.Specification = specification;
            }

            if (!TryParseDecimalField(_purchaseQuantityTextBox.Text, "入库数量", out quantity))
            {
                return false;
            }

            if (!TryParseDecimalField(_purchaseCostTextBox.Text, "进货单价", out purchasePrice))
            {
                return false;
            }

            draft.Quantity = quantity;
            draft.PurchasePrice = purchasePrice;

            if (string.IsNullOrWhiteSpace(_purchaseSalePriceTextBox.Text))
            {
                draft.SalePrice = null;
            }
            else
            {
                if (!TryParseDecimalField(_purchaseSalePriceTextBox.Text, "预计售价", out salePrice))
                {
                    return false;
                }

                draft.SalePrice = salePrice;
            }

            draft.RequiresExpiry = _purchaseRequiresExpiryCheckBox.Checked;
            if (string.IsNullOrWhiteSpace(_purchaseProductionDateTextBox.Text))
            {
                draft.ProductionDate = null;
            }
            else
            {
                if (!TryParseDateField(_purchaseProductionDateTextBox.Text, "生产日期", out dateValue))
                {
                    return false;
                }

                draft.ProductionDate = dateValue;
                draft.RequiresExpiry = true;
            }

            if (string.IsNullOrWhiteSpace(_purchaseExpiryDateTextBox.Text))
            {
                draft.ExpiryDate = null;
            }
            else
            {
                if (!TryParseDateField(_purchaseExpiryDateTextBox.Text, "到期日期", out dateValue))
                {
                    return false;
                }

                draft.ExpiryDate = dateValue;
                draft.RequiresExpiry = true;
            }

            return true;
        }

        private static string ExtractSpecificationFromText(string text)
        {
            Match match = Regex.Match(text ?? string.Empty, @"(?<value>\d+(?:\.\d+)?\s*(?:ml|ML|毫升|l|L|升|g|G|克|kg|KG|千克))");
            return match.Success ? match.Groups["value"].Value.Replace(" ", string.Empty) : string.Empty;
        }

        private bool TryParseDecimalField(string text, string fieldName, out decimal value)
        {
            if (!decimal.TryParse((text ?? string.Empty).Trim(), out value) || value <= 0)
            {
                AddChatBubble(fieldName + "需要填写大于 0 的数字。", false, false);
                return false;
            }

            return true;
        }

        private bool TryParseDateField(string text, string fieldName, out DateTime value)
        {
            string normalized = (text ?? string.Empty).Trim()
                .Replace("年", "-")
                .Replace("月", "-")
                .Replace("日", string.Empty)
                .Replace("/", "-")
                .Replace(".", "-");
            string[] formats = { "yyyy-M-d", "yyyy-MM-dd", "yyyy-M-dd", "yyyy-MM-d" };
            if (!DateTime.TryParseExact(normalized, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value)
                && !DateTime.TryParse(normalized, out value))
            {
                AddChatBubble(fieldName + "格式不正确，请使用 2026-04-23 这样的日期格式。", false, false);
                return false;
            }

            value = value.Date;
            return true;
        }

        private void RemovePendingPurchaseConfirmCard()
        {
            if (_pendingPurchaseConfirmCard == null || _chatPanel == null)
            {
                return;
            }

            if (_chatPanel.Controls.Contains(_pendingPurchaseConfirmCard))
            {
                _chatPanel.Controls.Remove(_pendingPurchaseConfirmCard);
            }

            _pendingPurchaseConfirmCard.Dispose();
            _pendingPurchaseConfirmCard = null;
        }

        private void AddActionDraftQueueCard(AiActionDraft draft, long messageId, bool scrollToBottom)
        {
            if (_chatPanel == null || draft == null || !HasPendingActionItems(draft))
            {
                return;
            }

            RemovePendingActionCard();
            _pendingActionDraft = draft;
            _pendingActionDraftMessageId = messageId;
            EnsurePendingActionIndex();

            AiActionDraftItem item = _pendingActionDraft.Items[_pendingActionIndex];
            int width = ResolveBubbleWidth(string.Empty, false);
            Panel card = UiComponentHelper.CreateCardPanel(new Padding(16), Color.White, ResolveRiskBorder(item));
            card.Width = width;
            card.Height = 438;
            card.Margin = ResolveBubbleMargin(width, false);
            card.Tag = "action-draft-card";

            Panel header = BuildActionDraftHeader(item);
            Control form = BuildActionDraftForm(item);
            Panel buttonRow = BuildActionDraftButtonRow(item);

            card.Controls.Add(form);
            card.Controls.Add(buttonRow);
            card.Controls.Add(header);
            _pendingActionCard = card;
            _chatPanel.Controls.Add(card);
            card.PerformLayout();
            if (scrollToBottom)
            {
                RequestScrollChatToBottom();
            }
        }

        private Panel BuildActionDraftHeader(AiActionDraftItem item)
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White
            };

            int pendingCount = CountPendingActionItems(_pendingActionDraft);
            TableLayoutPanel nav = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 300,
                Height = 40,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White,
                Visible = pendingCount > 1
            };
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));

            Button prevButton = UiComponentHelper.CreateSecondaryButton("< 上一条", 88);
            Button nextButton = UiComponentHelper.CreateSecondaryButton("下一条 >", 88);
            prevButton.Dock = DockStyle.Fill;
            nextButton.Dock = DockStyle.Fill;
            prevButton.Margin = new Padding(0, 2, 4, 2);
            nextButton.Margin = new Padding(4, 2, 0, 2);
            prevButton.Click += delegate { MovePendingAction(-1); };
            nextButton.Click += delegate { MovePendingAction(1); };

            Label countLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = BuildActionCountText(),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary
            };
            nav.Controls.Add(prevButton, 0, 0);
            nav.Controls.Add(countLabel, 1, 0);
            nav.Controls.Add(nextButton, 2, 0);

            Label title = new Label
            {
                Dock = DockStyle.Fill,
                Text = _actionDraftService.BuildItemDisplayTitle(item),
                Font = UiTheme.Font(13.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            header.Controls.Add(title);
            header.Controls.Add(nav);
            return header;
        }

        private Control BuildActionDraftForm(AiActionDraftItem item)
        {
            Panel panel = UiComponentHelper.CreateCardPanel(new Padding(12), UiTheme.SoftSlate, UiTheme.CardBorder);
            panel.Dock = DockStyle.Fill;

            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 220,
                ColumnCount = 4,
                RowCount = 5,
                BackColor = UiTheme.SoftSlate,
                Padding = Padding.Empty
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int index = 0; index < 5; index++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            }

            ResetActionFormFields();
            if (item.ActionType == AiActionTypes.DeleteOrUndoRequest || item.ActionType == AiActionTypes.Unknown)
            {
                Label notice = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = item.ActionType == AiActionTypes.DeleteOrUndoRequest
                        ? "暂时没有找到可撤销的上一条 AI 操作。\r\n\r\n撤销和删除属于高风险动作，本轮不会自动修改数据库。"
                        : "我还没有识别出明确的经营动作。\r\n\r\n请换一种说法，或直接到对应页面手动处理。",
                    Font = UiTheme.Font(10.5F),
                    ForeColor = UiTheme.TextSecondary,
                    BackColor = UiTheme.SoftSlate,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14)
                };
                panel.Controls.Add(notice);
                return panel;
            }

            if (item.ActionType == AiActionTypes.CreditRegister)
            {
                _actionCustomerTextBox = AddActionFormTextBox(table, 0, 0, "客户", item.CustomerName);
                _actionCreditAmountTextBox = AddActionFormTextBox(table, 0, 1, "赊账金额", FormatNullable(item.CreditAmount));
                _actionProductTextBox = AddActionFormTextBox(table, 1, 0, "商品", item.ProductName);
                _actionQuantityTextBox = AddActionFormTextBox(table, 1, 1, "数量", FormatNullable(item.Quantity));
                _actionSalePriceTextBox = AddActionFormTextBox(table, 2, 0, "售价", FormatNullable(item.SalePrice));
                _actionReceivedAmountTextBox = AddActionFormTextBox(table, 2, 1, "实收金额", FormatNullable(item.ActualReceivedAmount));
            }
            else if (item.ActionType == AiActionTypes.ProductPriceUpdate)
            {
                _actionProductTextBox = AddActionFormTextBox(table, 0, 0, "商品", item.ProductName);
                _actionSpecTextBox = AddActionFormTextBox(table, 0, 1, "规格", item.ProductSpec);
                _actionOldPriceTextBox = AddActionFormTextBox(table, 1, 0, "原售价", FormatNullable(item.PriceChangeOldValue));
                _actionNewPriceTextBox = AddActionFormTextBox(table, 1, 1, "新售价", FormatNullable(item.PriceChangeNewValue));
            }
            else if (item.ActionType == AiActionTypes.InventoryAdjust)
            {
                _actionProductTextBox = AddActionFormTextBox(table, 0, 0, "商品", item.ProductName);
                _actionSpecTextBox = AddActionFormTextBox(table, 0, 1, "规格", item.ProductSpec);
                if (IsScrapActionItem(item))
                {
                    _actionQuantityTextBox = AddActionFormTextBox(table, 1, 0, "报废数量", FormatNullable(item.Quantity));
                }
                else
                {
                    _actionInventoryAdjustTextBox = AddActionFormTextBox(table, 1, 0, "实际库存", FormatNullable(item.InventoryAdjustQuantity));
                }

                _actionUnitTextBox = AddActionFormTextBox(table, 1, 1, "单位", item.Unit);
            }
            else if (item.ActionType == AiActionTypes.SaleRecord)
            {
                _actionProductTextBox = AddActionFormTextBox(table, 0, 0, "商品", item.ProductName);
                _actionSpecTextBox = AddActionFormTextBox(table, 0, 1, "规格", item.ProductSpec);
                _actionQuantityTextBox = AddActionFormTextBox(table, 1, 0, "销售数量", FormatNullable(item.Quantity));
                _actionUnitTextBox = AddActionFormTextBox(table, 1, 1, "单位", item.Unit);
                _actionSalePriceTextBox = AddActionFormTextBox(table, 2, 0, "售价", FormatNullable(item.SalePrice));
                _actionReceivedAmountTextBox = AddActionFormTextBox(table, 2, 1, "实收金额", FormatNullable(item.ActualReceivedAmount));
                AddActionProductCandidateComboBox(table, 3, item);
                AttachSaleAmountAutoCalculation(item);
            }
            else
            {
                _actionProductTextBox = AddActionFormTextBox(table, 0, 0, "商品", item.ProductName);
                _actionCategoryTextBox = AddActionFormTextBox(table, 0, 1, "分类", item.Category);
                _actionSpecTextBox = AddActionFormTextBox(table, 1, 0, "规格", item.ProductSpec);
                _actionUnitTextBox = AddActionFormTextBox(table, 1, 1, "单位", item.Unit);
                _actionQuantityTextBox = AddActionFormTextBox(table, 2, 0, "入库数量", FormatNullable(item.Quantity));
                _actionPurchasePriceTextBox = AddActionFormTextBox(table, 2, 1, "进货单价", FormatNullable(item.PurchasePrice));
                _actionSalePriceTextBox = AddActionFormTextBox(table, 3, 0, "预计售价", FormatNullable(item.SalePrice));

                Label shelfLifeLabel = CreatePurchaseFormLabel("保质期");
                table.Controls.Add(shelfLifeLabel, 2, 3);
                _actionShelfLifeCheckBox = new CheckBox
                {
                    Text = "启用",
                    Checked = item.ShelfLifeEnabled.GetValueOrDefault(false),
                    Dock = DockStyle.Fill,
                    Height = UiTheme.InputHeight,
                    Font = UiTheme.Font(10F),
                    ForeColor = UiTheme.TextPrimary,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = UiTheme.SoftSlate,
                    Margin = new Padding(0, 5, 12, 5)
                };
                table.Controls.Add(_actionShelfLifeCheckBox, 3, 3);

                _actionProductionDateTextBox = AddActionFormTextBox(table, 4, 0, "生产日期", FormatDate(item.ProductionDate));
                _actionExpiryDateTextBox = AddActionFormTextBox(table, 4, 1, "到期日期", FormatDate(item.ExpiryDate));
            }

            Label hintLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = BuildActionHintText(item),
                Font = UiTheme.Font(9.5F),
                ForeColor = ResolveRiskTextColor(item),
                BackColor = ResolveHintBackColor(item),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 4, 10, 4)
            };
            _actionHintLabel = hintLabel;

            panel.Controls.Add(hintLabel);
            panel.Controls.Add(table);
            AttachActionFormLiveReview(item);
            return panel;
        }

        private Panel BuildActionDraftButtonRow(AiActionDraftItem item)
        {
            Panel row = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Color.White
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 548,
                Height = 58,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 9, 0, 0),
                BackColor = Color.White
            };

            Button confirm = UiComponentHelper.CreatePrimaryButton("确认执行", 104);
            Button revise = UiComponentHelper.CreateSecondaryButton("继续修改", 104);
            Button cancelCurrent = UiComponentHelper.CreateButton("取消当前", 104, UiTheme.SoftRed, UiTheme.DangerRed, UiTheme.DangerBorder);
            Button cancelAll = UiComponentHelper.CreateButton("取消全部", 104, UiTheme.SoftOrange, UiTheme.WarningOrange, UiTheme.WarningBorder);
            confirm.Enabled = item != null
                && item.ActionType != AiActionTypes.DeleteOrUndoRequest
                && item.ActionType != AiActionTypes.Unknown;
            confirm.Click += delegate { ConfirmCurrentActionDraft(); };
            revise.Click += delegate
            {
                AddPersistentLocalBubble("可以直接在确认单里修改字段。修改完成后再点“确认执行”。", "system_local", string.Empty);
            };
            cancelCurrent.Click += delegate { CancelCurrentActionDraft(); };
            cancelAll.Click += delegate { CancelAllPendingActions("已取消全部 AI 动作草稿，未修改任何经营数据。"); };

            buttons.Controls.Add(confirm);
            buttons.Controls.Add(revise);
            buttons.Controls.Add(cancelCurrent);
            buttons.Controls.Add(cancelAll);
            row.Controls.Add(buttons);
            return row;
        }

        private TextBox AddActionFormTextBox(TableLayoutPanel table, int row, int pairIndex, string labelText, string value)
        {
            int labelColumn = pairIndex == 0 ? 0 : 2;
            int inputColumn = pairIndex == 0 ? 1 : 3;
            table.Controls.Add(CreatePurchaseFormLabel(labelText), labelColumn, row);

            TextBox textBox = new TextBox
            {
                Text = value ?? string.Empty,
                Dock = DockStyle.Fill,
                Multiline = false,
                Font = UiTheme.Font(10F),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 5, 12, 5),
                Height = UiTheme.InputHeight
            };
            UiComponentHelper.CenterTextBoxContent(textBox);
            table.Controls.Add(textBox, inputColumn, row);
            return textBox;
        }

        private void AddActionProductCandidateComboBox(TableLayoutPanel table, int row, AiActionDraftItem item)
        {
            if (item == null || item.CandidateProductNames == null || item.CandidateProductNames.Count <= 1)
            {
                return;
            }

            table.Controls.Add(CreatePurchaseFormLabel("匹配商品"), 0, row);
            _actionProductCandidateComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.Font(10F),
                Margin = new Padding(0, 5, 12, 5)
            };
            foreach (string candidate in item.CandidateProductNames)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && !_actionProductCandidateComboBox.Items.Contains(candidate))
                {
                    _actionProductCandidateComboBox.Items.Add(candidate);
                }
            }

            if (!string.IsNullOrWhiteSpace(item.MatchedProductName) && !_actionProductCandidateComboBox.Items.Contains(item.MatchedProductName))
            {
                _actionProductCandidateComboBox.Items.Insert(0, item.MatchedProductName);
            }

            _actionProductCandidateComboBox.SelectedItem = string.IsNullOrWhiteSpace(item.MatchedProductName) ? null : item.MatchedProductName;
            if (_actionProductCandidateComboBox.SelectedIndex < 0 && _actionProductCandidateComboBox.Items.Count > 0)
            {
                _actionProductCandidateComboBox.SelectedIndex = 0;
            }

            _actionProductCandidateComboBox.SelectedIndexChanged += delegate
            {
                if (_actionProductTextBox != null && _actionProductCandidateComboBox.SelectedItem != null)
                {
                    _actionProductTextBox.Text = _actionProductCandidateComboBox.SelectedItem.ToString();
                }
            };

            table.Controls.Add(_actionProductCandidateComboBox, 1, row);
            table.SetColumnSpan(_actionProductCandidateComboBox, 3);
        }

        private void AttachActionFormLiveReview(AiActionDraftItem item)
        {
            EventHandler handler = delegate { RefreshActionHintFromForm(item); };
            AttachTextChanged(_actionProductTextBox, handler);
            AttachTextChanged(_actionSpecTextBox, handler);
            AttachTextChanged(_actionCategoryTextBox, handler);
            AttachTextChanged(_actionQuantityTextBox, handler);
            AttachTextChanged(_actionUnitTextBox, handler);
            AttachTextChanged(_actionPurchasePriceTextBox, handler);
            AttachTextChanged(_actionSalePriceTextBox, handler);
            AttachTextChanged(_actionProductionDateTextBox, handler);
            AttachTextChanged(_actionExpiryDateTextBox, handler);
            AttachTextChanged(_actionCustomerTextBox, handler);
            AttachTextChanged(_actionCreditAmountTextBox, handler);
            AttachTextChanged(_actionReceivedAmountTextBox, handler);
            AttachTextChanged(_actionInventoryAdjustTextBox, handler);
            AttachTextChanged(_actionOldPriceTextBox, handler);
            AttachTextChanged(_actionNewPriceTextBox, handler);

            if (_actionShelfLifeCheckBox != null)
            {
                _actionShelfLifeCheckBox.CheckedChanged += handler;
            }
        }

        private static void AttachTextChanged(TextBox textBox, EventHandler handler)
        {
            if (textBox != null)
            {
                textBox.TextChanged += handler;
            }
        }

        private void RefreshActionHintFromForm(AiActionDraftItem item)
        {
            if (_refreshingActionHint || item == null || _pendingActionDraft == null || _actionHintLabel == null)
            {
                return;
            }

            try
            {
                _refreshingActionHint = true;
                if (!ApplyActionFormToItem(item, false))
                {
                    return;
                }

                _actionDraftService.ValidateDraft(_pendingActionDraft);
                _actionHintLabel.Text = BuildActionHintText(item);
                _actionHintLabel.ForeColor = ResolveRiskTextColor(item);
                _actionHintLabel.BackColor = ResolveHintBackColor(item);
                if (_pendingActionCard != null)
                {
                    _pendingActionCard.Invalidate();
                }
            }
            finally
            {
                _refreshingActionHint = false;
            }
        }

        private void AttachSaleAmountAutoCalculation(AiActionDraftItem item)
        {
            bool receivedAutoFilled = item != null
                && item.Warnings != null
                && item.Warnings.Any(warning => warning.Contains("未说明实收金额") || warning.Contains("已按优惠"));
            _actionReceivedAmountManualEdit = _actionReceivedAmountTextBox != null
                && !string.IsNullOrWhiteSpace(_actionReceivedAmountTextBox.Text)
                && !receivedAutoFilled;
            if (_actionReceivedAmountTextBox != null)
            {
                _actionReceivedAmountTextBox.TextChanged += delegate
                {
                    if (_actionReceivedAmountTextBox.Focused)
                    {
                        _actionReceivedAmountManualEdit = true;
                    }
                };
            }

            EventHandler recalc = delegate { UpdateSaleReceivedAmountFromForm(); };
            if (_actionQuantityTextBox != null)
            {
                _actionQuantityTextBox.TextChanged += recalc;
            }

            if (_actionSalePriceTextBox != null)
            {
                _actionSalePriceTextBox.TextChanged += recalc;
            }

            UpdateSaleReceivedAmountFromForm();
        }

        private void UpdateSaleReceivedAmountFromForm()
        {
            if (_actionReceivedAmountManualEdit || _actionReceivedAmountTextBox == null)
            {
                return;
            }

            decimal quantity;
            decimal salePrice;
            if (decimal.TryParse((_actionQuantityTextBox == null ? string.Empty : _actionQuantityTextBox.Text).Trim(), out quantity)
                && decimal.TryParse((_actionSalePriceTextBox == null ? string.Empty : _actionSalePriceTextBox.Text).Trim(), out salePrice)
                && quantity > 0
                && salePrice >= 0)
            {
                _actionReceivedAmountTextBox.Text = (quantity * salePrice).ToString("0.##");
            }
        }

        private void ConfirmCurrentActionDraft()
        {
            AiActionDraftItem item = GetCurrentPendingActionItem();
            if (item == null)
            {
                return;
            }

            if (!ApplyActionFormToItem(item))
            {
                return;
            }

            _actionDraftService.ValidateDraft(_pendingActionDraft);
            if (_actionHintLabel != null)
            {
                _actionHintLabel.Text = BuildActionHintText(item);
                _actionHintLabel.ForeColor = ResolveRiskTextColor(item);
                _actionHintLabel.BackColor = ResolveHintBackColor(item);
            }

            List<string> blockingErrors = ResolveActionBlockingMessages(item);
            if (blockingErrors.Count > 0)
            {
                string errorText = "这条草稿暂时不能执行：\r\n\r\n" + string.Join("\r\n", blockingErrors.ToArray());
                MessageBox.Show(this, errorText, "需要先补全信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AddPersistentLocalBubble(errorText, "error", _actionDraftService.SerializeDraft(_pendingActionDraft));
                UpdateActionDraftMessage();
                RefreshActionDraftCard();
                return;
            }

            List<string> warnings = ResolveActionWarningMessages(item);
            if (warnings.Count > 0)
            {
                string warningText = "执行前请确认这些提醒：\r\n\r\n"
                    + string.Join("\r\n", warnings.ToArray())
                    + "\r\n\r\n确认无误后，系统才会写入经营数据。是否继续？";
                DialogResult dialogResult = MessageBox.Show(this, warningText, "确认执行 AI 草稿", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dialogResult != DialogResult.Yes)
                {
                    AddPersistentLocalBubble("已暂停执行。可以继续在确认单里修改后再确认。", "system_local", _actionDraftService.SerializeDraft(_pendingActionDraft));
                    RefreshActionDraftCard();
                    return;
                }
            }

            if (!_actionDraftService.CanExecuteItem(item))
            {
                AddPersistentLocalBubble(_actionDraftService.BuildNotExecutableMessage(item), "error", _actionDraftService.SerializeDraft(_pendingActionDraft));
                UpdateActionDraftMessage();
                RefreshActionDraftCard();
                return;
            }

            AiActionExecutionResult result = _actionDraftService.Execute(_pendingActionDraft, item);
            string message = result.Success
                ? _actionDraftService.BuildExecutionSuccessMessage(item, result)
                : result.Message;
            AddPersistentLocalBubble(message, result.Success ? "action_result" : "error", _actionDraftService.SerializeDraft(_pendingActionDraft));
            UpdateActionDraftMessage();
            if (HasPendingActionItems(_pendingActionDraft))
            {
                _pendingActionIndex = FindFirstPendingActionIndex(_pendingActionDraft);
                RefreshActionDraftCard();
            }
            else
            {
                RemovePendingActionCard();
                _pendingActionDraft = null;
                _pendingActionDraftMessageId = 0;
            }
        }

        private void CancelCurrentActionDraft()
        {
            AiActionDraftItem item = GetCurrentPendingActionItem();
            if (item == null)
            {
                return;
            }

            RememberCancelledDraft(item);
            item.Status = AiActionDraftStatus.Cancelled;
            _actionDraftService.ValidateDraft(_pendingActionDraft);
            AddPersistentLocalBubble("已取消当前草稿：" + _actionDraftService.BuildItemDisplayTitle(item), "action_result", _actionDraftService.SerializeDraft(_pendingActionDraft));
            UpdateActionDraftMessage();
            if (HasPendingActionItems(_pendingActionDraft))
            {
                _pendingActionIndex = FindFirstPendingActionIndex(_pendingActionDraft);
                RefreshActionDraftCard();
            }
            else
            {
                RemovePendingActionCard();
            }
        }

        private void CancelAllPendingActions(string message)
        {
            if (_pendingActionDraft != null)
            {
                foreach (AiActionDraftItem item in _pendingActionDraft.Items)
                {
                    if (item.Status != AiActionDraftStatus.Executed)
                    {
                        RememberCancelledDraft(item);
                        item.Status = AiActionDraftStatus.Cancelled;
                    }
                }

                _actionDraftService.ValidateDraft(_pendingActionDraft);
                UpdateActionDraftMessage();
            }

            RemovePendingActionCard();
            _pendingActionDraft = null;
            _pendingActionDraftMessageId = 0;
            AddPersistentLocalBubble(message, "action_result", string.Empty);
        }

        private void RememberCancelledDraft(AiActionDraftItem item)
        {
            if (item == null)
            {
                return;
            }

            string title = _actionDraftService.BuildItemDisplayTitle(item);
            if (!string.IsNullOrWhiteSpace(title))
            {
                _lastCancelledDrafts.Insert(0, title);
            }

            while (_lastCancelledDrafts.Count > 6)
            {
                _lastCancelledDrafts.RemoveAt(_lastCancelledDrafts.Count - 1);
            }
        }

        private bool ApplyActionFormToItem(AiActionDraftItem item)
        {
            return ApplyActionFormToItem(item, true);
        }

        private bool ApplyActionFormToItem(AiActionDraftItem item, bool showErrors)
        {
            if (item == null)
            {
                return false;
            }

            if (_actionProductTextBox != null)
            {
                item.ProductName = ReadText(_actionProductTextBox);
            }

            if (_actionProductCandidateComboBox != null && _actionProductCandidateComboBox.SelectedItem != null)
            {
                item.ProductName = _actionProductCandidateComboBox.SelectedItem.ToString();
            }

            if (_actionSpecTextBox != null)
            {
                item.ProductSpec = ReadText(_actionSpecTextBox);
            }

            if (_actionCategoryTextBox != null)
            {
                item.Category = ReadText(_actionCategoryTextBox);
            }

            if (_actionUnitTextBox != null)
            {
                item.Unit = ReadText(_actionUnitTextBox);
            }

            if (_actionCustomerTextBox != null)
            {
                item.CustomerName = ReadText(_actionCustomerTextBox);
            }

            if (_actionQuantityTextBox != null)
            {
                item.Quantity = ReadOptionalDecimal(_actionQuantityTextBox, "数量", showErrors);
            }

            if (_actionPurchasePriceTextBox != null)
            {
                item.PurchasePrice = ReadOptionalDecimal(_actionPurchasePriceTextBox, "进货单价", showErrors);
            }

            if (_actionSalePriceTextBox != null)
            {
                item.SalePrice = ReadOptionalDecimal(_actionSalePriceTextBox, "售价", showErrors);
            }

            if (_actionCreditAmountTextBox != null)
            {
                item.CreditAmount = ReadOptionalDecimal(_actionCreditAmountTextBox, "赊账金额", showErrors);
            }

            if (_actionReceivedAmountTextBox != null)
            {
                item.ActualReceivedAmount = ReadOptionalDecimal(_actionReceivedAmountTextBox, "实收金额", showErrors);
            }

            if (_actionInventoryAdjustTextBox != null)
            {
                item.InventoryAdjustQuantity = ReadOptionalDecimal(_actionInventoryAdjustTextBox, "实际库存", showErrors);
            }

            if (_actionOldPriceTextBox != null)
            {
                item.PriceChangeOldValue = ReadOptionalDecimal(_actionOldPriceTextBox, "原售价", showErrors);
            }

            if (_actionNewPriceTextBox != null)
            {
                item.PriceChangeNewValue = ReadOptionalDecimal(_actionNewPriceTextBox, "新售价", showErrors);
            }

            if (_actionShelfLifeCheckBox != null)
            {
                item.ShelfLifeEnabled = _actionShelfLifeCheckBox.Checked;
            }

            DateTime? productionDate = ReadOptionalDate(_actionProductionDateTextBox, "生产日期", showErrors);
            DateTime? expiryDate = ReadOptionalDate(_actionExpiryDateTextBox, "到期日期", showErrors);
            if (_actionProductionDateTextBox != null)
            {
                item.ProductionDate = productionDate;
            }

            if (_actionExpiryDateTextBox != null)
            {
                item.ExpiryDate = expiryDate;
            }
            if (productionDate.HasValue || expiryDate.HasValue)
            {
                item.ShelfLifeEnabled = true;
            }

            _actionDraftService.NormalizeSpecAndUnit(item);
            return true;
        }

        private decimal? ReadOptionalDecimal(TextBox textBox, string fieldName)
        {
            return ReadOptionalDecimal(textBox, fieldName, true);
        }

        private decimal? ReadOptionalDecimal(TextBox textBox, string fieldName, bool showErrors)
        {
            if (textBox == null || string.IsNullOrWhiteSpace(textBox.Text))
            {
                return null;
            }

            decimal value;
            if (!decimal.TryParse(textBox.Text.Trim(), out value))
            {
                if (showErrors)
                {
                    AddPersistentLocalBubble(fieldName + "需要填写数字。", "error", string.Empty);
                }
                return null;
            }

            return value;
        }

        private DateTime? ReadOptionalDate(TextBox textBox, string fieldName)
        {
            return ReadOptionalDate(textBox, fieldName, true);
        }

        private DateTime? ReadOptionalDate(TextBox textBox, string fieldName, bool showErrors)
        {
            if (textBox == null || string.IsNullOrWhiteSpace(textBox.Text))
            {
                return null;
            }

            DateTime value;
            string normalized = textBox.Text.Trim()
                .Replace("年", "-")
                .Replace("月", "-")
                .Replace("日", string.Empty)
                .Replace("/", "-")
                .Replace(".", "-");
            string[] formats = { "yyyy-M-d", "yyyy-MM-dd", "yyyy-M-dd", "yyyy-MM-d" };
            if (!DateTime.TryParseExact(normalized, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value)
                && !DateTime.TryParse(normalized, out value))
            {
                if (showErrors)
                {
                    AddPersistentLocalBubble(fieldName + "格式不正确，请使用 2026-04-23 这样的日期格式。", "error", string.Empty);
                }
                return null;
            }

            return value.Date;
        }

        private void MovePendingAction(int delta)
        {
            if (_pendingActionDraft == null || CountPendingActionItems(_pendingActionDraft) <= 1)
            {
                return;
            }

            List<int> pendingIndexes = ResolvePendingIndexes(_pendingActionDraft);
            int currentOrdinal = pendingIndexes.IndexOf(_pendingActionIndex);
            if (currentOrdinal < 0)
            {
                currentOrdinal = 0;
            }

            int nextOrdinal = (currentOrdinal + delta + pendingIndexes.Count) % pendingIndexes.Count;
            _pendingActionIndex = pendingIndexes[nextOrdinal];
            RefreshActionDraftCard();
        }

        private void RefreshActionDraftCard()
        {
            if (_pendingActionDraft == null)
            {
                return;
            }

            AddActionDraftQueueCard(_pendingActionDraft, _pendingActionDraftMessageId, true);
        }

        private void RemovePendingActionCard()
        {
            if (_pendingActionCard == null || _chatPanel == null)
            {
                return;
            }

            if (_chatPanel.Controls.Contains(_pendingActionCard))
            {
                _chatPanel.Controls.Remove(_pendingActionCard);
            }

            _pendingActionCard.Dispose();
            _pendingActionCard = null;
        }

        private AiActionDraftItem GetCurrentPendingActionItem()
        {
            if (_pendingActionDraft == null || _pendingActionDraft.Items.Count == 0)
            {
                return null;
            }

            EnsurePendingActionIndex();
            return _pendingActionDraft.Items[_pendingActionIndex];
        }

        private void EnsurePendingActionIndex()
        {
            if (_pendingActionDraft == null || _pendingActionDraft.Items.Count == 0)
            {
                _pendingActionIndex = 0;
                return;
            }

            if (_pendingActionIndex < 0 || _pendingActionIndex >= _pendingActionDraft.Items.Count
                || IsFinishedAction(_pendingActionDraft.Items[_pendingActionIndex]))
            {
                _pendingActionIndex = FindFirstPendingActionIndex(_pendingActionDraft);
            }
        }

        private int FindFirstPendingActionIndex(AiActionDraft draft)
        {
            if (draft == null)
            {
                return 0;
            }

            for (int index = 0; index < draft.Items.Count; index++)
            {
                if (!IsFinishedAction(draft.Items[index]))
                {
                    return index;
                }
            }

            return 0;
        }

        private static bool HasPendingActionItems(AiActionDraft draft)
        {
            return draft != null && draft.Items.Any(item => !IsFinishedAction(item));
        }

        private static int CountPendingActionItems(AiActionDraft draft)
        {
            return draft == null ? 0 : draft.Items.Count(item => !IsFinishedAction(item));
        }

        private static List<int> ResolvePendingIndexes(AiActionDraft draft)
        {
            List<int> indexes = new List<int>();
            if (draft == null)
            {
                return indexes;
            }

            for (int index = 0; index < draft.Items.Count; index++)
            {
                if (!IsFinishedAction(draft.Items[index]))
                {
                    indexes.Add(index);
                }
            }

            return indexes;
        }

        private static bool IsFinishedAction(AiActionDraftItem item)
        {
            return item == null
                || item.Status == AiActionDraftStatus.Executed
                || item.Status == AiActionDraftStatus.Cancelled;
        }

        private string BuildActionCountText()
        {
            int pending = CountPendingActionItems(_pendingActionDraft);
            if (pending <= 1)
            {
                return "剩余 1 条";
            }

            List<int> indexes = ResolvePendingIndexes(_pendingActionDraft);
            int ordinal = indexes.IndexOf(_pendingActionIndex) + 1;
            return Math.Max(1, ordinal) + " / " + pending;
        }

        private string BuildActionHintText(AiActionDraftItem item)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("确认前不会写入数据库。");
            if (item.MissingFields.Count > 0)
            {
                builder.Append(" 需要处理：");
                builder.Append(string.Join("、", item.MissingFields.Select(field => _actionDraftService.ToFieldDisplayName(field)).ToArray()));
                builder.Append("。");
            }

            if (item.Warnings.Count > 0)
            {
                builder.Append(" 提醒：");
                builder.Append(string.Join("；", item.Warnings.ToArray()));
            }

            if (item.ActionType != AiActionTypes.PurchaseIn
                && item.ActionType != AiActionTypes.SaleRecord
                && item.ActionType != AiActionTypes.CreditRegister
                && item.ActionType != AiActionTypes.InventoryAdjust
                && item.ActionType != AiActionTypes.ProductPriceUpdate)
            {
                builder.Append(" 该动作暂未接入 AI 写库执行。");
            }

            return builder.ToString();
        }

        private static bool IsScrapActionItem(AiActionDraftItem item)
        {
            if (item == null)
            {
                return false;
            }

            string text = ((item.Notes ?? string.Empty) + " " + (item.ProductName ?? string.Empty)).Trim();
            return text.Contains("报废")
                || text.Contains("过期")
                || text.Contains("损坏")
                || text.Contains("破损")
                || text.Contains("扔掉")
                || text.Contains("丢掉");
        }

        private Color ResolveHintBackColor(AiActionDraftItem item)
        {
            if (item.RiskLevel == AiActionRiskLevels.High)
            {
                return UiTheme.SoftRed;
            }

            if (item.MissingFields.Count > 0 || item.Warnings.Count > 0)
            {
                return UiTheme.SoftOrange;
            }

            return UiTheme.SoftGreen;
        }

        private Color ResolveRiskBorder(AiActionDraftItem item)
        {
            if (item.RiskLevel == AiActionRiskLevels.High)
            {
                return UiTheme.DangerBorder;
            }

            if (item.RiskLevel == AiActionRiskLevels.Medium)
            {
                return UiTheme.WarningBorder;
            }

            return Color.FromArgb(184, 199, 219);
        }

        private Color ResolveRiskTextColor(AiActionDraftItem item)
        {
            if (item.RiskLevel == AiActionRiskLevels.High)
            {
                return UiTheme.DangerRed;
            }

            if (item.MissingFields.Count > 0 || item.Warnings.Count > 0)
            {
                return UiTheme.WarningOrange;
            }

            return UiTheme.TextSecondary;
        }

        private long SaveActionDraftMessage(AiActionDraft draft)
        {
            string content = _actionDraftService.BuildDraftSummary(draft);
            long messageId = _conversationService.AddMessage(_currentConversation.Id, "assistant", content, "action_draft", _actionDraftService.SerializeDraft(draft));
            _currentConversation = _conversationService.Load(_currentConversation.Id);
            ReloadConversationComboBox();
            return messageId;
        }

        private void UpdateActionDraftMessage()
        {
            if (_pendingActionDraft == null || _pendingActionDraftMessageId <= 0)
            {
                return;
            }

            _conversationService.UpdateMessage(
                _currentConversation.Id,
                _pendingActionDraftMessageId,
                _actionDraftService.BuildDraftSummary(_pendingActionDraft),
                "action_draft",
                _actionDraftService.SerializeDraft(_pendingActionDraft));
            _currentConversation = _conversationService.Load(_currentConversation.Id);
            ReloadConversationComboBox();
        }

        private void SaveLocalMessage(string text, string messageType, string dataContext)
        {
            _conversationService.AddMessage(_currentConversation.Id, "assistant", text, messageType, dataContext ?? string.Empty);
            _currentConversation = _conversationService.Load(_currentConversation.Id);
            ReloadConversationComboBox();
        }

        private void AddPersistentLocalBubble(string text, string messageType, string dataContext)
        {
            AddChatBubble(text, false, false);
            SaveLocalMessage(text, messageType, dataContext);
        }

        private static bool IsCancelAllActionText(string text)
        {
            text = text ?? string.Empty;
            return text.Contains("取消全部")
                || text.Contains("全部取消")
                || text.Contains("都不登记")
                || text.Contains("都不要保存");
        }

        private static string ReadText(TextBox textBox)
        {
            return textBox == null ? string.Empty : (textBox.Text ?? string.Empty).Trim();
        }

        private void ResetActionFormFields()
        {
            _actionProductTextBox = null;
            _actionSpecTextBox = null;
            _actionCategoryTextBox = null;
            _actionQuantityTextBox = null;
            _actionUnitTextBox = null;
            _actionPurchasePriceTextBox = null;
            _actionSalePriceTextBox = null;
            _actionProductionDateTextBox = null;
            _actionExpiryDateTextBox = null;
            _actionShelfLifeCheckBox = null;
            _actionCustomerTextBox = null;
            _actionCreditAmountTextBox = null;
            _actionReceivedAmountTextBox = null;
            _actionInventoryAdjustTextBox = null;
            _actionOldPriceTextBox = null;
            _actionNewPriceTextBox = null;
            _actionProductCandidateComboBox = null;
            _actionHintLabel = null;
            _actionReceivedAmountManualEdit = false;
        }

        private List<string> ResolveActionBlockingMessages(AiActionDraftItem item)
        {
            List<string> messages = new List<string>();
            if (item == null || item.MissingFields == null)
            {
                return messages;
            }

            foreach (string field in item.MissingFields)
            {
                if (_actionDraftService.IsBlockingMissingField(field))
                {
                    string text = _actionDraftService.ToFieldDisplayName(field);
                    if (!messages.Contains(text))
                    {
                        messages.Add(text);
                    }
                }
            }

            if (item.ActionType != AiActionTypes.PurchaseIn
                && item.ActionType != AiActionTypes.SaleRecord
                && item.ActionType != AiActionTypes.CreditRegister
                && item.ActionType != AiActionTypes.InventoryAdjust
                && item.ActionType != AiActionTypes.ProductPriceUpdate)
            {
                messages.Add("“" + _actionDraftService.ToActionText(item.ActionType) + "”暂未接入 AI 写库执行，请先用对应页面手动处理。");
            }

            return messages;
        }

        private List<string> ResolveActionWarningMessages(AiActionDraftItem item)
        {
            List<string> messages = new List<string>();
            if (item == null)
            {
                return messages;
            }

            if (item.Warnings != null)
            {
                foreach (string warning in item.Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning) && !messages.Contains(warning))
                    {
                        messages.Add(warning);
                    }
                }
            }

            if (item.ActionType == AiActionTypes.SaleRecord && string.IsNullOrWhiteSpace(item.ProductSpec))
            {
                messages.Add("规格为空，但不会阻止销售记账；请确认商品匹配正确。");
            }

            if (string.IsNullOrWhiteSpace(item.Notes))
            {
                messages.Add("没有备注。");
            }

            return messages;
        }

        private static string FormatNullable(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("0.###") : string.Empty;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd") : string.Empty;
        }

        private async Task SendToAiAsync(string userPrompt, BusinessSummaryResult liveContext)
        {
            await SendToAiAsync(userPrompt, liveContext, null);
        }

        private async Task SendToAiAsync(string userPrompt, BusinessSummaryResult liveContext, RichTextBox waitingBubble)
        {
            AiSettings settings = _settingsService.Load();
            settings.AiApiKey = _settingsService.GetApiKey();
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                if (waitingBubble == null)
                {
                    AddChatBubble("请先配置 DeepSeek API Key。", false, false);
                }
                else
                {
                    SetBubbleContent(waitingBubble, "请先配置 DeepSeek API Key。", false);
                    ResizeBubble(waitingBubble);
                }

                SetSendingState(false);
                return;
            }

            AiDataTrace dataTrace = BuildDataTrace(liveContext);
            string contextInfo = dataTrace.ReadText;
            SetSendingState(true);
            if (waitingBubble == null)
            {
                waitingBubble = AddChatBubble(BuildWaitingStatusText(dataTrace), false, false);
            }
            else
            {
                SetBubbleContent(waitingBubble, "正在整理回答……", false);
                ResizeBubble(waitingBubble);
            }

            try
            {
                List<AiChatMessage> messages = BuildRequestMessages(userPrompt);
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(65)))
                {
                    _activeRequestCancellation = cts;
                    DeepSeekClient client = new DeepSeekClient(settings.AiBaseUrl, settings.AiModel, settings.AiApiKey);
                    AiResponseResult result = await SendChatWithRetryAsync(client, settings, messages, cts.Token, waitingBubble, contextInfo);
                    if (result.Success)
                    {
                        _lastAssistantAnswerText = result.Content;
                        SetBubbleContent(waitingBubble, BuildAssistantDisplayText(result.Content, dataTrace, DateTime.Now), false);
                        ResizeBubble(waitingBubble);
                        RequestScrollChatToBottom();
                        SaveAssistantMessage(result.Content, liveContext);
                    }
                    else
                    {
                        SetBubbleContent(waitingBubble, BuildAiErrorDisplayText(result.ErrorMessage, contextInfo), false);
                        SetBubbleTextColor(waitingBubble, UiTheme.DangerRed);
                        ResizeBubble(waitingBubble);
                        RequestScrollChatToBottom();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SetBubbleContent(waitingBubble, BuildAiErrorDisplayText("AI 请求已取消。", contextInfo), false);
                SetBubbleTextColor(waitingBubble, UiTheme.DangerRed);
                ResizeBubble(waitingBubble);
                RequestScrollChatToBottom();
            }
            catch (Exception ex)
            {
                SetBubbleContent(waitingBubble, BuildAiErrorDisplayText("AI 请求处理失败：" + ex.Message, contextInfo), false);
                SetBubbleTextColor(waitingBubble, UiTheme.DangerRed);
                ResizeBubble(waitingBubble);
                RequestScrollChatToBottom();
            }
            finally
            {
                if (_activeRequestCancellation != null)
                {
                    _activeRequestCancellation.Dispose();
                    _activeRequestCancellation = null;
                }

                SetSendingState(false);
                RequestScrollChatToBottom();
            }
        }

        private async Task<AiResponseResult> SendChatWithRetryAsync(
            DeepSeekClient client,
            AiSettings settings,
            List<AiChatMessage> messages,
            CancellationToken cancellationToken,
            RichTextBox waitingBubble,
            string contextInfo)
        {
            AiResponseResult result = await client.SendChatAsync(settings, messages, cancellationToken);
            if (result.Success || !ShouldRetryAiFailure(result.ErrorMessage))
            {
                return result;
            }

            SetBubbleContent(waitingBubble, BuildRetryStatusText(contextInfo, result.ErrorMessage), false);
            ResizeBubble(waitingBubble);
            RequestScrollChatToBottom();
            await Task.Delay(800, cancellationToken);
            return await client.SendChatAsync(settings, messages, cancellationToken);
        }

        private static bool ShouldRetryAiFailure(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return true;
            }

            string text = errorMessage.ToLowerInvariant();
            if (text.Contains("key") || text.Contains("余额") || text.Contains("权限") || text.Contains("取消") || text.Contains("模型"))
            {
                return false;
            }

            return text.Contains("超时")
                || text.Contains("网络")
                || text.Contains("暂时")
                || text.Contains("频繁")
                || text.Contains("不可用")
                || text.Contains("timeout")
                || text.Contains("temporarily")
                || text.Contains("server");
        }

        private void CancelActiveRequest()
        {
            if (_activeRequestCancellation == null || _activeRequestCancellation.IsCancellationRequested)
            {
                return;
            }

            _activeRequestCancellation.Cancel();
            if (_sendButton != null)
            {
                _sendButton.Enabled = false;
                _sendButton.Text = "取消中";
            }
        }

        private string BuildLocalDataUsageLabel(BusinessSummaryResult liveContext)
        {
            List<string> sources = new List<string>();
            if (liveContext != null && liveContext.Success)
            {
                AddDataSourceLabels(sources, liveContext.JsonText);
                if (sources.Count == 0 && !string.IsNullOrWhiteSpace(liveContext.Title))
                {
                    AddDataSourceLabel(sources, liveContext.Title);
                }
            }

            AiStoreProfile profile = _storeProfileService.Load();
            if (profile != null && profile.IsInitialized)
            {
                AddUniqueLabel(sources, "店铺记忆");
            }

            return sources.Count == 0 ? "未使用本地经营数据" : string.Join("、", sources.ToArray());
        }

        private static void AddDataSourceLabels(List<string> labels, string labelText)
        {
            if (labels == null || string.IsNullOrWhiteSpace(labelText))
            {
                return;
            }

            string[] parts = labelText.Split(new[] { '、', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                AddDataSourceLabel(labels, part.Trim());
            }
        }

        private static void AddDataSourceLabel(List<string> labels, string title)
        {
            if (labels == null || string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            string label = title;
            if (title.Contains("今日") || title.Contains("收入"))
            {
                label = "今日销售摘要";
            }
            else if (title.Contains("库存"))
            {
                label = "库存摘要";
            }
            else if (title.Contains("赊账"))
            {
                label = "赊账记录";
            }
            else if (title.Contains("热销") || title.Contains("滞销"))
            {
                label = "商品销售排行";
            }
            else if (title.Contains("本周"))
            {
                label = "本周经营摘要";
            }
            else if (title.Contains("本月") || title.Contains("月报"))
            {
                label = "本月经营摘要";
            }

            AddUniqueLabel(labels, label);
        }

        private static void AddUniqueLabel(List<string> labels, string label)
        {
            if (labels == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            foreach (string existing in labels)
            {
                if (string.Equals(existing, label, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            labels.Add(label);
        }

        private AiDataTrace BuildDataTrace(BusinessSummaryResult liveContext)
        {
            List<string> readSources = BuildLocalDataSourceList(liveContext);
            List<string> expectedSources = new List<string>
            {
                "今日销售摘要",
                "库存摘要",
                "赊账记录",
                "进货记录"
            };

            AiStoreProfile profile = _storeProfileService.Load();
            if (profile != null && profile.IsInitialized)
            {
                AddUniqueLabel(readSources, "店铺记忆");
            }
            else
            {
                expectedSources.Add("店铺记忆");
            }

            List<string> unreadSources = new List<string>();
            foreach (string source in expectedSources)
            {
                if (!ContainsLabel(readSources, source))
                {
                    unreadSources.Add(source);
                }
            }

            List<string> gaps = BuildDataGapList(liveContext);
            return new AiDataTrace
            {
                ReadText = readSources.Count == 0 ? "未使用本地经营数据" : string.Join("、", readSources.ToArray()),
                UnreadText = unreadSources.Count == 0 ? "无" : string.Join("、", unreadSources.ToArray()),
                GapText = gaps.Count == 0 ? "未发现明显缺失" : string.Join("、", gaps.ToArray())
            };
        }

        private List<string> BuildLocalDataSourceList(BusinessSummaryResult liveContext)
        {
            List<string> sources = new List<string>();
            if (liveContext != null && liveContext.Success)
            {
                AddDataSourceLabels(sources, liveContext.JsonText);
                if (sources.Count == 0 && !string.IsNullOrWhiteSpace(liveContext.Title))
                {
                    AddDataSourceLabel(sources, liveContext.Title);
                }
            }

            return sources;
        }

        private static List<string> BuildDataGapList(BusinessSummaryResult liveContext)
        {
            List<string> gaps = new List<string>();
            if (liveContext == null || !liveContext.Success)
            {
                gaps.Add("本次未读取经营摘要");
                return gaps;
            }

            string text = liveContext.SummaryText ?? string.Empty;
            if (ContainsAny(text, "订单数量：0", "订单数量: 0", "销售单数量：0", "销售总额：0.00", "销售总额: 0.00"))
            {
                gaps.Add("今日暂无销售记录");
            }

            if (ContainsAny(text, "商品销售数量：0", "销售数量：0", "销售数量: 0"))
            {
                AddUniqueLabel(gaps, "暂无商品销售明细");
            }

            if (ContainsAny(text, "未结清赊账：0.00", "未结清赊账总额：0.00", "未结清赊账总额: 0.00"))
            {
                AddUniqueLabel(gaps, "暂无未结清赊账");
            }

            if (ContainsAny(text, "低库存商品：暂无数据", "低库存商品: 暂无数据"))
            {
                AddUniqueLabel(gaps, "暂无低库存商品");
            }

            if (ContainsAny(text, "临期商品：暂无数据", "临期商品: 暂无数据"))
            {
                AddUniqueLabel(gaps, "暂无临期商品");
            }

            return gaps;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrEmpty(text) || values == null)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrEmpty(value) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsLabel(List<string> labels, string label)
        {
            if (labels == null || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            foreach (string existing in labels)
            {
                if (string.Equals(existing, label, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildWaitingStatusText(AiDataTrace dataTrace)
        {
            return "正在读取本地数据：\r\n"
                + BuildDataSourceChecklist(dataTrace == null ? string.Empty : dataTrace.ReadText)
                + "\r\n\r\n未读取：" + (dataTrace == null ? "未知" : dataTrace.UnreadText)
                + "\r\n数据缺失：" + (dataTrace == null ? "未知" : dataTrace.GapText)
                + "\r\n正在请求 AI，请稍候……";
        }

        private static string BuildRetryStatusText(string contextInfo, string previousError)
        {
            return "第一次请求失败，正在自动重试一次。\r\n\r\n已读取：" + contextInfo + "\r\n失败原因：" + previousError;
        }

        private static string BuildAssistantDisplayText(string content, AiDataTrace dataTrace, DateTime timestamp)
        {
            AiDataTrace trace = dataTrace ?? AiDataTrace.Empty();
            return "本次数据状态"
                + "\r\n已读取：" + trace.ReadText
                + "\r\n未读取：" + trace.UnreadText
                + "\r\n数据缺失：" + trace.GapText
                + "\r\n统计时间：" + timestamp.ToString("yyyy-MM-dd HH:mm")
                + "\r\n\r\n"
                + (content ?? string.Empty).Trim()
                + "\r\n\r\n数据来源：" + trace.ReadText;
        }

        private static string BuildAssistantDisplayText(string content, string contextInfo, DateTime timestamp)
        {
            string normalizedContext = string.IsNullOrWhiteSpace(contextInfo) ? "历史消息未记录数据来源" : contextInfo.Trim();
            if (normalizedContext.Contains("已读取：") && normalizedContext.Contains("未读取："))
            {
                return "本次数据状态\r\n"
                    + normalizedContext
                    + "\r\n统计时间：" + timestamp.ToString("yyyy-MM-dd HH:mm")
                    + "\r\n\r\n"
                    + (content ?? string.Empty).Trim();
            }

            return "本次数据状态"
                + "\r\n已读取：" + normalizedContext
                + "\r\n未读取：历史消息未记录"
                + "\r\n数据缺失：历史消息未记录"
                + "\r\n统计时间：" + timestamp.ToString("yyyy-MM-dd HH:mm")
                + "\r\n\r\n"
                + (content ?? string.Empty).Trim()
                + "\r\n\r\n数据来源：" + normalizedContext;
        }

        private static string BuildStoredAssistantDisplayText(AiStoredMessage message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            string contextInfo = string.IsNullOrWhiteSpace(message.DataContextType)
                ? "历史消息未记录数据来源"
                : message.DataContextType;
            return BuildAssistantDisplayText(message.Content, contextInfo, message.CreatedAt);
        }

        private static string BuildAiErrorDisplayText(string errorMessage, string contextInfo)
        {
            return "AI 请求失败\r\n\r\n"
                + "已使用：" + contextInfo
                + "\r\n错误提示：" + (string.IsNullOrWhiteSpace(errorMessage) ? "AI 服务暂时不可用。" : errorMessage)
                + "\r\n\r\n本地销售、入库、库存、赊账、报表和备份功能不受影响。";
        }

        private static string BuildDataSourceChecklist(string contextInfo)
        {
            if (string.IsNullOrWhiteSpace(contextInfo) || contextInfo == "未使用本地经营数据")
            {
                return "• 未匹配到本地经营数据，本次按普通对话处理";
            }

            string[] parts = contextInfo.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder builder = new StringBuilder();
            foreach (string part in parts)
            {
                builder.AppendLine("✓ " + part.Trim());
            }

            return builder.ToString().TrimEnd();
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

                if (stored.MessageType == "action_draft" || stored.MessageType == "action_result" || stored.MessageType == "error")
                {
                    continue;
                }

                messages.Add(new AiChatMessage { Role = stored.Role, Content = stored.Content });
            }

            messages.Add(AiChatMessage.User(currentPrompt));
            return messages;
        }

        private static List<AiChatMessage> BuildSemanticGroundedMessages(string userText, string routedText, string localAnswer)
        {
            List<AiChatMessage> messages = new List<AiChatMessage>();
            messages.Add(AiChatMessage.System(
                "你是小铺掌柜的回答整理助手。本地程序已经完成数据库查询，你只能基于本地查询结果回答。"
                + "不要编造没有出现在本地查询结果里的销售、库存、利润、赊账或报废数据。"
                + "如果本地结果显示数据不足，就明确告诉老板缺哪些数据，并给出下一步怎么记录。"
                + "请用中文纯文本回答，不要 Markdown，不要表格，不要代码块。"));
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("用户原话：");
            builder.AppendLine(userText ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("本地程序识别后的查询主题：");
            builder.AppendLine(routedText ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("本地数据库查询结果：");
            builder.AppendLine(localAnswer ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("请把上面的本地查询结果整理成老板容易理解的话。");
            messages.Add(AiChatMessage.User(builder.ToString()));
            return messages;
        }

        private static string BuildAiUserPrompt(BusinessSummaryResult liveContext, string requestText)
        {
            string plainTextRule = "\r\n【输出格式要求】\r\n请只用纯文本回答，不要使用 Markdown 语法。不要输出 #、##、###、**加粗**、```代码块```、表格、引用块。可以使用普通编号 1. 2. 3.，每条建议分行写。";
            DateTime now = DateTime.Now;
            string dateRule = "\r\n【统一日期要求】\r\n本次统计时间：" + now.ToString("yyyy-MM-dd HH:mm")
                + "\r\n如果回答提到“今天”，必须以 " + now.ToString("yyyy-MM-dd")
                + " 为准，不要把历史销售记录、历史赊账日期或示例日期当成今天。";
            if (liveContext != null && liveContext.Success && !string.IsNullOrWhiteSpace(liveContext.SummaryText))
            {
                return liveContext.SummaryText + dateRule + "\r\n【用户当前问题】\r\n" + requestText + "\r\n请在回答里说明“根据当前本地记录”。" + plainTextRule;
            }

            return "用户当前问题：\r\n" + requestText + dateRule + "\r\n如果没有提供本地经营摘要，请不要假装知道销售、库存或赊账数据。" + plainTextRule;
        }

        private void AddSuggestionButton(FlowLayoutPanel panel, string text, string prompt)
        {
            AddSuggestionButton(panel, text, prompt, string.Empty);
        }

        private void AddSuggestionButton(FlowLayoutPanel panel, string text, string prompt, string analysisKey)
        {
            Color[] backgrounds = { UiTheme.SoftBlue, UiTheme.SoftGreen, UiTheme.SoftOrange, UiTheme.SoftCyan, UiTheme.SoftPurple, UiTheme.SoftSlate };
            Color[] foregrounds = { UiTheme.PrimaryBlue, UiTheme.SuccessGreen, UiTheme.WarningOrange, UiTheme.InfoCyan, Color.FromArgb(91, 33, 182), UiTheme.TextPrimary };
            int index = panel.Controls.Count % backgrounds.Length;
            Button button = UiComponentHelper.CreateButton(text, 104, backgrounds[index], foregrounds[index], UiTheme.CardBorder);
            button.Height = 32;
            button.Font = UiTheme.Font(8.4F, FontStyle.Bold);
            button.Margin = new Padding(0, 0, 7, 3);
            button.FlatAppearance.MouseOverBackColor = UiComponentHelper.Lighten(backgrounds[index], 6);
            button.FlatAppearance.MouseDownBackColor = UiComponentHelper.Darken(backgrounds[index], 6);
            button.Click += async delegate
            {
                if (string.IsNullOrWhiteSpace(analysisKey))
                {
                    await SendUserTextAsync(prompt);
                    return;
                }

                await SendKnownAnalysisAsync(prompt, analysisKey);
            };
            panel.Controls.Add(button);
        }

        private static string GetBuiltInAnalysisKey(string question)
        {
            if (string.Equals(question, "分析今日收入", StringComparison.OrdinalIgnoreCase))
            {
                return "today";
            }

            if (string.Equals(question, "生成本周经营小结", StringComparison.OrdinalIgnoreCase))
            {
                return "week";
            }

            if (string.Equals(question, "生成本月经营月报", StringComparison.OrdinalIgnoreCase))
            {
                return "month";
            }

            if (string.Equals(question, "库存补货建议", StringComparison.OrdinalIgnoreCase))
            {
                return "inventoryRisk";
            }

            if (string.Equals(question, "赊账客户提醒", StringComparison.OrdinalIgnoreCase))
            {
                return "credit";
            }

            if (string.Equals(question, "热销与滞销商品", StringComparison.OrdinalIgnoreCase))
            {
                return "hotSlow";
            }

            return string.Empty;
        }

        private void AddQuickQuestionMenuButton(FlowLayoutPanel panel)
        {
            Button button = UiComponentHelper.CreateButton(string.Empty, 36, Color.White, UiTheme.TextSecondary, UiTheme.CardBorder);
            button.Height = 36;
            button.Margin = new Padding(0, 0, 0, 4);
            button.Image = UiAssetHelper.GetIcon("settings", 18, UiTheme.TextSecondary);
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.Overlay;
            button.FlatAppearance.MouseOverBackColor = UiTheme.SoftSlate;
            button.Click += delegate { ShowQuickQuestionMenu(button); };
            panel.Controls.Add(button);
        }

        private void ShowQuickQuestionMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip
            {
                Font = UiTheme.Font(10F),
                BackColor = Color.White,
                ShowImageMargin = false
            };

            IList<string> questions = _quickQuestionService.Load();
            if (questions.Count == 0)
            {
                ToolStripMenuItem emptyItem = new ToolStripMenuItem("暂无自定义快捷问题");
                emptyItem.Enabled = false;
                menu.Items.Add(emptyItem);
            }
            else
            {
                foreach (string question in questions)
                {
                    ToolStripMenuItem questionItem = new ToolStripMenuItem(question);
                    questionItem.Click += async delegate { await SendUserTextAsync(question); };
                    menu.Items.Add(questionItem);
                }
            }

            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem addItem = new ToolStripMenuItem("点击新增快捷问题");
            addItem.ForeColor = UiTheme.PrimaryBlue;
            addItem.Font = UiTheme.Font(10F, FontStyle.Bold);
            addItem.Click += delegate
            {
                string question = PromptForQuickQuestion();
                if (string.IsNullOrWhiteSpace(question))
                {
                    return;
                }

                _quickQuestionService.Add(question);
            };
            menu.Items.Add(addItem);
            Size preferredSize = menu.GetPreferredSize(Size.Empty);
            int menuWidth = Math.Max(260, preferredSize.Width);
            menu.MinimumSize = new Size(menuWidth, 0);
            int left = Math.Min(0, anchor.Width - menuWidth);
            menu.Show(anchor, new Point(left, anchor.Height + 2));
        }

        private string PromptForQuickQuestion()
        {
            using (Form form = new Form())
            {
                form.Text = "新增快捷问题";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(520, 176);
                form.BackColor = Color.White;
                form.Font = UiTheme.Font(10.5F);

                Label label = new Label
                {
                    Text = "输入一个常用问题",
                    Location = new Point(24, 22),
                    Size = new Size(440, 28),
                    Font = UiTheme.Font(12F, FontStyle.Bold),
                    ForeColor = UiTheme.TextPrimary
                };
                form.Controls.Add(label);

                TextBox textBox = new TextBox
                {
                    Location = new Point(24, 62),
                    Size = new Size(472, 34),
                    Font = UiTheme.Font(10.5F)
                };
                UiComponentHelper.CenterTextBoxContent(textBox);
                form.Controls.Add(textBox);

                Button saveButton = UiComponentHelper.CreatePrimaryButton("保存", 96);
                saveButton.Location = new Point(296, 118);
                saveButton.Click += delegate
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Focus();
                        return;
                    }

                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };
                form.Controls.Add(saveButton);

                Button cancelButton = UiComponentHelper.CreateSecondaryButton("取消", 96);
                cancelButton.Location = new Point(400, 118);
                cancelButton.Click += delegate
                {
                    form.DialogResult = DialogResult.Cancel;
                    form.Close();
                };
                form.Controls.Add(cancelButton);

                form.AcceptButton = saveButton;
                form.CancelButton = cancelButton;
                return form.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : string.Empty;
            }
        }

        private void CopyLatestAssistantAnswer()
        {
            string answer = ResolveLatestAssistantAnswerText();
            if (string.IsNullOrWhiteSpace(answer))
            {
                SetCopyAnswerButtonText("暂无回答");
                return;
            }

            try
            {
                Clipboard.SetText(answer);
                SetCopyAnswerButtonText("已复制");
            }
            catch
            {
                SetCopyAnswerButtonText("复制失败");
            }
        }

        private string ResolveLatestAssistantAnswerText()
        {
            if (!string.IsNullOrWhiteSpace(_lastAssistantAnswerText))
            {
                return _lastAssistantAnswerText;
            }

            if (_currentConversation == null || _currentConversation.Messages == null)
            {
                return string.Empty;
            }

            for (int index = _currentConversation.Messages.Count - 1; index >= 0; index--)
            {
                AiStoredMessage message = _currentConversation.Messages[index];
                if (message != null && string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                {
                    return message.Content;
                }
            }

            return string.Empty;
        }

        private void SetCopyAnswerButtonText(string text)
        {
            if (_copyAnswerButton == null)
            {
                return;
            }

            _copyAnswerButton.Text = text;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1300 };
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                if (_copyAnswerButton != null && !_copyAnswerButton.IsDisposed)
                {
                    _copyAnswerButton.Text = "复制回答";
                }
            };
            timer.Start();
        }

        private void AddEmptyStateImage()
        {
            if (_chatPanel == null)
            {
                return;
            }

            int maxByWidth = Math.Max(260, _chatPanel.ClientSize.Width - 120);
            int maxByHeight = _chatPanel.ClientSize.Height > 320 ? _chatPanel.ClientSize.Height - 160 : 420;
            int size = Math.Max(340, Math.Min(520, Math.Min(maxByWidth, maxByHeight)));
            Panel imageCard = UiComponentHelper.CreateCardPanel(new Padding(14), Color.White, UiTheme.CardBorder);
            imageCard.Width = size;
            imageCard.Height = size;
            imageCard.Margin = new Padding(8, 18, 0, 12);
            imageCard.BackColor = Color.White;
            imageCard.Tag = EmptyChatImageTag;

            PictureBox picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = UiAssetHelper.GetIllustration("empty/ai_chat_empty", new Size(size - 34, size - 34))
            };
            imageCard.Controls.Add(picture);
            _chatPanel.Controls.Add(imageCard);
        }

        private void RemoveEmptyStateImage()
        {
            if (_chatPanel == null)
            {
                return;
            }

            for (int index = _chatPanel.Controls.Count - 1; index >= 0; index--)
            {
                Control control = _chatPanel.Controls[index];
                if (control != null && string.Equals(control.Tag as string, EmptyChatImageTag, StringComparison.Ordinal))
                {
                    _chatPanel.Controls.RemoveAt(index);
                    control.Dispose();
                }
            }
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
            _chatPanel.Controls.Add(bubble);
            ResizeBubble(bubble);
            RequestScrollChatToBottom();

            if (persist)
            {
                _conversationService.AddMessage(_currentConversation.Id, user ? "user" : "assistant", text, "chat", string.Empty);
                _currentConversation = _conversationService.Load(_currentConversation.Id);
                ReloadConversationComboBox();
                UpdateAiUsageLabels();
            }

            return bubble;
        }

        private void SaveAssistantMessage(string text, BusinessSummaryResult liveContext)
        {
            string dataContextType = BuildDataTrace(liveContext).ToStorageText();
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
            DeepSeekConnectionResult result;
            try
            {
                result = await client.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                result = new DeepSeekConnectionResult
                {
                    Success = false,
                    Message = "AI 连接测试失败：" + ex.Message
                };
            }

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

            NormalizeChatScrollRange();
            int currentY = -_chatPanel.AutoScrollPosition.Y;
            int nextY = Math.Min(ResolveChatMaxScroll(), Math.Max(0, currentY - delta));
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
                    if (string.Equals(control.Tag as string, "purchase-confirm-card", StringComparison.Ordinal)
                        || string.Equals(control.Tag as string, "action-draft-card", StringComparison.Ordinal))
                    {
                        int cardWidth = ResolveBubbleWidth(string.Empty, false);
                        control.Width = cardWidth;
                        control.Margin = ResolveBubbleMargin(cardWidth, false);
                        if (string.Equals(control.Tag as string, "purchase-confirm-card", StringComparison.Ordinal))
                        {
                            LayoutPurchaseConfirmButtons(control);
                        }
                    }

                    continue;
                }

                bool user = bubble.Tag is bool && (bool)bubble.Tag;
                bubble.Width = ResolveBubbleWidth(bubble.Text, user);
                bubble.Margin = ResolveBubbleMargin(bubble.Width, user);
                ResizeBubble(bubble);
            }

            NormalizeChatScrollRange();
        }

        private static void LayoutPurchaseConfirmButtons(Control card)
        {
            if (card == null || card.Controls.Count == 0)
            {
                return;
            }

            foreach (Control child in card.Controls)
            {
                Panel buttonRow = child as Panel;
                if (buttonRow == null || buttonRow.Dock != DockStyle.Bottom)
                {
                    continue;
                }

                Button confirmButton = null;
                Button reviseButton = null;
                Button cancelButton = null;
                foreach (Control buttonControl in buttonRow.Controls)
                {
                    Button button = buttonControl as Button;
                    if (button == null)
                    {
                        continue;
                    }

                    if (button.Text == "确认执行")
                    {
                        confirmButton = button;
                    }
                    else if (button.Text == "继续修改")
                    {
                        reviseButton = button;
                    }
                    else if (button.Text == "取消入库")
                    {
                        cancelButton = button;
                    }
                }

                if (confirmButton == null || reviseButton == null || cancelButton == null)
                {
                    return;
                }

                confirmButton.Left = buttonRow.ClientSize.Width - confirmButton.Width - 2;
                confirmButton.Top = 10;
                reviseButton.Left = confirmButton.Left - reviseButton.Width - 10;
                reviseButton.Top = 10;
                cancelButton.Left = reviseButton.Left - cancelButton.Width - 10;
                cancelButton.Top = 10;
                return;
            }
        }

        private static void ResizeBubble(RichTextBox bubble)
        {
            bool user = bubble.Tag is bool && (bool)bubble.Tag;
            int desiredHeight = MeasureRichTextHeight(bubble);
            int maxHeight = user ? 190 : Math.Max(800, desiredHeight);
            bubble.Height = Math.Min(maxHeight, desiredHeight);
            bubble.ScrollBars = desiredHeight > maxHeight ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
        }

        private static int MeasureRichTextHeight(RichTextBox bubble)
        {
            if (bubble == null || string.IsNullOrEmpty(bubble.Text))
            {
                return 48;
            }

            try
            {
                if (!bubble.IsHandleCreated)
                {
                    bubble.CreateControl();
                }

                int lastIndex = Math.Max(0, bubble.TextLength - 1);
                Point lastChar = bubble.GetPositionFromCharIndex(lastIndex);
                int lineHeight = Math.Max(20, TextRenderer.MeasureText("测", bubble.Font).Height);
                int richHeight = lastChar.Y + lineHeight + 34;
                if (richHeight > 48)
                {
                    return richHeight;
                }
            }
            catch
            {
            }

            Size textSize = TextRenderer.MeasureText(
                bubble.Text,
                bubble.Font,
                new Size(Math.Max(80, bubble.Width - 26), 20000),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            return Math.Max(48, textSize.Height + 34);
        }

        private void UpdateInputPanelLayout(Panel inputPanel)
        {
            if (inputPanel == null || _inputTextBox == null || _sendButton == null)
            {
                return;
            }

            const int inputMinHeight = 44;
            const int inputMaxHeight = 138;
            int copyWidth = _copyAnswerButton == null ? 0 : _copyAnswerButton.Width;
            int copyGap = _copyAnswerButton == null ? 0 : 10;
            int availableWidth = Math.Max(280, inputPanel.ClientSize.Width - _sendButton.Width - copyWidth - copyGap - 50);
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
            if (_copyAnswerButton != null)
            {
                _copyAnswerButton.Left = _sendButton.Left - _copyAnswerButton.Width - 10;
                _copyAnswerButton.Top = _sendButton.Top;
            }

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
            bool inCodeBlock = false;
            bool previousBlank = false;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].TrimEnd();
                string trimmed = line.Trim();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (!previousBlank && builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    previousBlank = true;
                    continue;
                }

                previousBlank = false;
                if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
                {
                    builder.AppendLine();
                }

                int lineStart = builder.Length;
                FontStyle lineStyle = FontStyle.Regular;
                float lineSize = 10.5F;

                if (inCodeBlock)
                {
                    AppendInlineMarkdown(builder, "    " + line, ranges, FontStyle.Regular, 10F);
                    ranges.Add(new MarkdownFormatRange(lineStart, builder.Length - lineStart, FontStyle.Regular, 10F));
                    continue;
                }

                if (IsMarkdownHorizontalRule(trimmed))
                {
                    builder.Append("────────────");
                    ranges.Add(new MarkdownFormatRange(lineStart, builder.Length - lineStart, FontStyle.Regular, 10F));
                    continue;
                }

                string content = line.TrimStart();
                Match headingMatch = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
                Match quoteMatch = Regex.Match(trimmed, @"^>\s?(.*)$");
                Match bulletMatch = Regex.Match(trimmed, @"^[-*+]\s+(.+)$");
                Match orderedMatch = Regex.Match(trimmed, @"^(\d+)[\.\)、)]\s+(.+)$");

                if (headingMatch.Success)
                {
                    int level = headingMatch.Groups[1].Value.Length;
                    content = headingMatch.Groups[2].Value.Trim();
                    lineStyle = FontStyle.Bold;
                    lineSize = level == 1 ? 14F : (level == 2 ? 13F : (level == 3 ? 12F : 11F));
                }
                else if (quoteMatch.Success)
                {
                    content = "｜ " + quoteMatch.Groups[1].Value.Trim();
                    lineStyle = FontStyle.Italic;
                    lineSize = 10F;
                }
                else if (bulletMatch.Success)
                {
                    int indentLength = Math.Min(line.Length - line.TrimStart().Length, 6);
                    content = new string(' ', indentLength) + "• " + bulletMatch.Groups[1].Value.Trim();
                }
                else if (orderedMatch.Success)
                {
                    int indentLength = Math.Min(line.Length - line.TrimStart().Length, 6);
                    content = new string(' ', indentLength) + orderedMatch.Groups[1].Value + ". " + orderedMatch.Groups[2].Value.Trim();
                }
                else if (Regex.IsMatch(trimmed, @"^[一二三四五六七八九十]+[、.．]\s*.+$"))
                {
                    content = trimmed;
                    lineStyle = FontStyle.Bold;
                    lineSize = 12.5F;
                }

                AppendInlineMarkdown(builder, content, ranges, lineStyle, lineSize);
                if (lineStyle != FontStyle.Regular || Math.Abs(lineSize - 10.5F) > 0.01F)
                {
                    ranges.Add(new MarkdownFormatRange(lineStart, builder.Length - lineStart, lineStyle, lineSize));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static bool IsMarkdownHorizontalRule(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string compact = text.Replace(" ", string.Empty);
            return compact.Length >= 3
                && (IsRepeated(compact, '-') || IsRepeated(compact, '_') || IsRepeated(compact, '*') || IsRepeated(compact, '─') || IsRepeated(compact, '—'));
        }

        private static bool IsRepeated(string text, char value)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] != value)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendInlineMarkdown(
            StringBuilder builder,
            string text,
            List<MarkdownFormatRange> ranges,
            FontStyle defaultStyle,
            float defaultSize)
        {
            builder.Append(CleanInlineMarkdown(text));
        }

        private static string CleanInlineMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string result = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)", "$1（$2）");
            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "$1");
            result = Regex.Replace(result, @"__(.+?)__", "$1");
            result = Regex.Replace(result, @"`([^`]+)`", "$1");
            result = Regex.Replace(result, @"~~(.+?)~~", "$1");
            return result;
        }

        private sealed class AiDataTrace
        {
            public string ReadText { get; set; }

            public string UnreadText { get; set; }

            public string GapText { get; set; }

            public static AiDataTrace Empty()
            {
                return new AiDataTrace
                {
                    ReadText = "未使用本地经营数据",
                    UnreadText = "未知",
                    GapText = "未知"
                };
            }

            public string ToStorageText()
            {
                return "已读取：" + (string.IsNullOrWhiteSpace(ReadText) ? "未使用本地经营数据" : ReadText)
                    + "\r\n未读取：" + (string.IsNullOrWhiteSpace(UnreadText) ? "未知" : UnreadText)
                    + "\r\n数据缺失：" + (string.IsNullOrWhiteSpace(GapText) ? "未知" : GapText);
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

            _chatPanel.PerformLayout();
            NormalizeChatScrollRange();
            int max = ResolveChatMaxScroll();
            _chatPanel.AutoScrollPosition = new Point(0, max);
        }

        private void NormalizeChatScrollRange()
        {
            if (_chatPanel == null)
            {
                return;
            }

            int contentHeight = ResolveChatContentHeight();
            if (contentHeight <= _chatPanel.ClientSize.Height)
            {
                _chatPanel.AutoScrollMinSize = Size.Empty;
                if (_chatPanel.AutoScrollPosition.Y != 0)
                {
                    _chatPanel.AutoScrollPosition = Point.Empty;
                }
                return;
            }

            _chatPanel.AutoScrollMinSize = new Size(0, contentHeight);
        }

        private int ResolveChatMaxScroll()
        {
            if (_chatPanel == null)
            {
                return 0;
            }

            return Math.Max(0, ResolveChatContentHeight() - _chatPanel.ClientSize.Height);
        }

        private int ResolveChatContentHeight()
        {
            if (_chatPanel == null)
            {
                return 0;
            }

            int contentHeight = _chatPanel.Padding.Top + _chatPanel.Padding.Bottom;
            foreach (Control control in _chatPanel.Controls)
            {
                if (!control.Visible)
                {
                    continue;
                }

                contentHeight += control.Margin.Top + control.Height + control.Margin.Bottom;
            }

            return contentHeight;
        }

        private void RequestScrollChatToBottom()
        {
            if (_chatPanel == null || _chatPanel.Controls.Count == 0 || IsDisposed || Disposing)
            {
                return;
            }

            ScrollChatToBottom();
            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke(new MethodInvoker(delegate
            {
                ScrollChatToBottom();
            }));
        }

        private void SetSendingState(bool sending)
        {
            _isSending = sending;
            if (_sendButton != null)
            {
                _sendButton.Enabled = !_loadingMessages;
                _sendButton.Text = sending ? "取消" : "发送";
            }

            if (_inputTextBox != null)
            {
                _inputTextBox.Enabled = !sending && !_loadingMessages;
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
                Height = 26,
                Font = UiTheme.Font(9.2F, FontStyle.Bold),
                ForeColor = UiTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 0)
            };
        }

        private static Button CreateSoftSidebarButton(string text, Color backColor, Color foreColor)
        {
            Button button = UiComponentHelper.CreateButton(text, 96, backColor, foreColor, UiTheme.CardBorder);
            button.Height = 32;
            button.Font = UiTheme.Font(9F, FontStyle.Bold);
            button.FlatAppearance.MouseOverBackColor = UiComponentHelper.Lighten(backColor, 8);
            button.FlatAppearance.MouseDownBackColor = UiComponentHelper.Darken(backColor, 8);
            return button;
        }

        private static Button CreateSoftDangerSidebarButton(string text)
        {
            Button button = UiComponentHelper.CreateButton(text, 96, UiTheme.SoftRed, UiTheme.DangerRed, UiTheme.DangerBorder);
            button.Height = 32;
            button.Font = UiTheme.Font(9F, FontStyle.Bold);
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
                Height = (int)(48F + secondRowHeight),
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0, 6, 0, 0),
                BackColor = UiTheme.CardBackground
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, secondRowHeight));

            PrepareSidebarGridButton(firstButton, new Padding(0, 0, 6, 6));
            PrepareSidebarGridButton(secondButton, new Padding(6, 0, 0, 6));
            PrepareSidebarGridButton(wideButton, new Padding(0, 3, 0, 0));

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
                Image avatar = UiAssetHelper.GetIcon("ai_avatar", Math.Max(1, Math.Min(Width, Height)));
                if (avatar != null)
                {
                    e.Graphics.DrawImage(avatar, new Rectangle(0, 0, Width, Height));
                    return;
                }

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
