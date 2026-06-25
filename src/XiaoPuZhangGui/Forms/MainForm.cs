using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class MainForm : Form
    {
        private const string HomeDashboardTitle = "首页看板";
        private const string SalesManagementTitle = "销售记账";
        private const string ProductManagementTitle = "商品管理";
        private const string PurchaseManagementTitle = "进货入库";
        private const string InventoryCheckTitle = "库存盘点";
        private const string CreditManagementTitle = "赊账管理";
        private const string ReportTitle = "经营报表";
        private const string AiAssistantTitle = "AI 助手";
        private const string SettingsTitle = "系统设置";

        private readonly NetworkStatusService _networkStatusService;
        private readonly AiSettingsService _aiSettingsService;
        private readonly Panel _navigationPanel;
        private readonly FlowLayoutPanel _navigationListPanel;
        private readonly Panel _contentPanel;
        private readonly Panel _statusPanel;
        private readonly Panel _aiNetworkDot;
        private readonly Panel _localSystemDot;
        private readonly Label _aiNetworkLabel;
        private readonly Label _localSystemLabel;
        private readonly Label _modeLabel;
        private readonly Dictionary<string, SidebarNavigationButton> _navigationButtons;
        private NetworkStatusResult _networkStatus;
        private string _currentPageTitle;

        public MainForm()
        {
            Text = "小铺掌柜 AI智能版";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 680);
            Size = new Size(1240, 760);
            Font = UiTheme.Font(11F);
            BackColor = Color.White;
            ApplyApplicationIcon();

            _networkStatusService = new NetworkStatusService();
            _aiSettingsService = new AiSettingsService();
            _networkStatus = NetworkStatusResult.Unknown();
            _navigationButtons = new Dictionary<string, SidebarNavigationButton>();

            _navigationPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = UiTheme.SidebarDark,
                Padding = new Padding(0)
            };

            _navigationListPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = _navigationPanel.BackColor,
                Padding = new Padding(0, 8, 0, 0)
            };
            _navigationListPanel.HorizontalScroll.Enabled = false;
            _navigationListPanel.HorizontalScroll.Visible = false;
            _navigationListPanel.Resize += delegate { ResizeNavigationButtons(); };

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.PageBackground
            };

            _statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = UiTheme.SidebarDark
            };
            _aiNetworkDot = CreateStatusDot();
            _localSystemDot = CreateStatusDot();
            _aiNetworkLabel = CreateStatusLabel();
            _localSystemLabel = CreateStatusLabel();
            _modeLabel = CreateModeLabel();
            BuildSidebarStatusPanel();

            Controls.Add(_contentPanel);
            Controls.Add(_navigationPanel);
            _navigationPanel.Controls.Add(_navigationListPanel);
            _navigationPanel.Controls.Add(_statusPanel);

            BuildNavigation();
            ResizeNavigationButtons();
            UpdateSidebarStatus();
            ShowPage(HomeDashboardTitle);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await RefreshNetworkStatusAsync();
        }


        private void ApplyApplicationIcon()
        {
            try
            {
                Icon associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associatedIcon != null)
                {
                    Icon = associatedIcon;
                }
            }
            catch
            {
                // 图标加载失败不影响程序启动。
            }
        }

        private void BuildNavigation()
        {
            AddBrand();

            AddNavigationButton(HomeDashboardTitle, "今日销售、利润、低库存和临期提醒。", "home");
            AddNavigationButton(SalesManagementTitle, "多商品开单、应收、成本、毛利润和库存扣减。", "sales");
            AddNavigationButton(ProductManagementTitle, "商品档案、分类、售价、库存预警和保质期设置入口。", "product");
            AddNavigationButton(PurchaseManagementTitle, "采购入库、批次、进价和到期日期登记入口。", "purchase");
            AddNavigationButton(InventoryCheckTitle, "库存修正、盈亏原因和报废处理入口。", "inventory");
            AddNavigationButton(CreditManagementTitle, "欠款查询、还款登记和备注管理入口。", "credit");
            AddNavigationButton(ReportTitle, "日报、月报、商品排行、库存提醒和报废摘要。", "report");
            AddNavigationButton(AiAssistantTitle, "联网 AI 经营助手、API 接入和智能分析入口。", "report");
            AddNavigationButton(SettingsTitle, "店铺信息、数据库路径、备份路径和恢复相关设置入口。", "settings");
        }

        private void AddBrand()
        {
            Label brand = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                Text = "小铺掌柜 AI智能版",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.Font(15F, FontStyle.Bold),
                ForeColor = Color.White
            };

            _navigationPanel.Controls.Add(brand);
        }

        private void AddNavigationButton(string title, string description, string iconName)
        {
            SidebarNavigationButton button = new SidebarNavigationButton
            {
                Height = 60,
                Width = _navigationListPanel.ClientSize.Width,
                Text = title,
                Tag = description,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0)
            };

            UiAssetHelper.ApplyIcon(button, "nav_" + ResolveNavigationIconKey(iconName), 22, Color.FromArgb(221, 235, 255));
            button.MouseDown += delegate { SelectNavigationButton(title, true); };
            button.Click += delegate { ShowPage(title); };

            _navigationButtons.Add(title, button);
            _navigationListPanel.Controls.Add(button);
        }

        private void ResizeNavigationButtons()
        {
            int width = _navigationListPanel.ClientSize.Width;
            if (width <= 0)
            {
                width = _navigationPanel.Width;
            }

            foreach (SidebarNavigationButton button in _navigationButtons.Values)
            {
                button.Width = width;
            }

            _navigationListPanel.HorizontalScroll.Value = 0;
            _navigationListPanel.HorizontalScroll.Visible = false;
        }

        private void ShowPage(string title)
        {
            if (!ConfirmLeaveCurrentPage(title))
            {
                SelectNavigationButton(_currentPageTitle, true);
                return;
            }

            SelectNavigationButton(title, true);
            _currentPageTitle = title;

            _contentPanel.Controls.Clear();

            if (title == HomeDashboardTitle)
            {
                ShowContentPage(new DashboardPage(ShowPage));
                return;
            }

            if (title == SalesManagementTitle)
            {
                ShowContentPage(new SalesManagementPage());
                return;
            }

            if (title == ProductManagementTitle)
            {
                ShowContentPage(new ProductManagementPage());
                return;
            }

            if (title == PurchaseManagementTitle)
            {
                ShowContentPage(new PurchaseManagementPage());
                return;
            }

            if (title == InventoryCheckTitle)
            {
                ShowContentPage(new InventoryCheckPage());
                return;
            }

            if (title == CreditManagementTitle)
            {
                ShowContentPage(new CreditManagementPage());
                return;
            }

            if (title == ReportTitle)
            {
                ShowContentPage(new ReportPage());
                return;
            }

            if (title == AiAssistantTitle)
            {
                ShowContentPage(new AiAssistantPage(
                    _networkStatusService,
                    _aiSettingsService,
                    _networkStatus,
                    OnAiPageNetworkStatusChanged,
                    UpdateSidebarStatus));
                return;
            }

            if (title == SettingsTitle)
            {
                ShowContentPage(new SettingsPage());
                return;
            }

            string description = _navigationButtons.ContainsKey(title)
                ? _navigationButtons[title].Tag.ToString()
                : string.Empty;

            ShowContentPage(new PlaceholderPage(title, description));
        }

        private bool ConfirmLeaveCurrentPage(string targetTitle)
        {
            if (string.Equals(_currentPageTitle, targetTitle, StringComparison.Ordinal))
            {
                return true;
            }

            IUnsavedChangesAware unsavedPage = GetCurrentUnsavedPage();
            if (unsavedPage == null || !unsavedPage.HasUnsavedChanges)
            {
                return true;
            }

            string description = string.IsNullOrWhiteSpace(unsavedPage.UnsavedChangesDescription)
                ? "当前页面还有未保存内容。"
                : unsavedPage.UnsavedChangesDescription;
            DialogResult result = MessageBox.Show(
                description + "\r\n\r\n如果现在切换页面，这些未保存内容会被清空。\r\n确定继续切换吗？",
                "内容尚未保存",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return result == DialogResult.Yes;
        }

        private IUnsavedChangesAware GetCurrentUnsavedPage()
        {
            if (_contentPanel == null || _contentPanel.Controls.Count == 0)
            {
                return null;
            }

            return _contentPanel.Controls[0] as IUnsavedChangesAware;
        }

        private async System.Threading.Tasks.Task RefreshNetworkStatusAsync()
        {
            _networkStatus = await _networkStatusService.CheckAsync();
            UpdateSidebarStatus();
        }

        private void OnAiPageNetworkStatusChanged(NetworkStatusResult result)
        {
            _networkStatus = result ?? NetworkStatusResult.Unknown();
            UpdateSidebarStatus();
        }

        private void BuildSidebarStatusPanel()
        {
            Label title = new Label
            {
                Text = "运行状态",
                Location = new Point(22, 8),
                Size = new Size(170, 20),
                Font = UiTheme.Font(9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(221, 235, 255),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _aiNetworkDot.Location = new Point(24, 36);
            _aiNetworkLabel.Location = new Point(42, 29);
            _aiNetworkLabel.Size = new Size(160, 26);

            _localSystemDot.Location = new Point(24, 62);
            _localSystemLabel.Location = new Point(42, 55);
            _localSystemLabel.Size = new Size(160, 26);

            _modeLabel.Location = new Point(22, 78);
            _modeLabel.Size = new Size(176, 20);

            _statusPanel.Controls.Add(title);
            _statusPanel.Controls.Add(_aiNetworkDot);
            _statusPanel.Controls.Add(_aiNetworkLabel);
            _statusPanel.Controls.Add(_localSystemDot);
            _statusPanel.Controls.Add(_localSystemLabel);
            _statusPanel.Controls.Add(_modeLabel);
        }

        private void UpdateSidebarStatus()
        {
            bool networkAvailable = _networkStatus != null && _networkStatus.IsNetworkAvailable;
            bool localReady = IsLocalSystemReady();
            AiSettings settings = _aiSettingsService.Load();
            bool apiAvailable = networkAvailable &&
                settings.AiEnabled &&
                settings.HasApiKey &&
                !string.IsNullOrWhiteSpace(settings.LastConnectionTestTime);

            _aiNetworkDot.BackColor = networkAvailable ? UiTheme.SuccessGreen : UiTheme.DangerRed;
            _localSystemDot.BackColor = localReady ? UiTheme.SuccessGreen : UiTheme.DangerRed;
            _aiNetworkLabel.Text = "AI 联网能力：" + (networkAvailable ? "可用" : "不可用");
            _localSystemLabel.Text = "本地经营系统：" + (localReady ? "正常" : "异常");
            _modeLabel.Text = apiAvailable ? "当前模式：AI 智能模式" : "当前模式：本地离线模式";
        }

        private static bool IsLocalSystemReady()
        {
            try
            {
                AppConfig config = AppConfigService.LoadOrCreateDefault();
                return !string.IsNullOrWhiteSpace(config.DatabasePath) && File.Exists(config.DatabasePath);
            }
            catch
            {
                return false;
            }
        }

        private static Panel CreateStatusDot()
        {
            return new RoundStatusDot
            {
                Size = new Size(10, 10),
                BackColor = UiTheme.MutedGray
            };
        }

        private static Label CreateStatusLabel()
        {
            return new Label
            {
                Font = UiTheme.Font(8.5F),
                ForeColor = Color.FromArgb(196, 211, 229),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateModeLabel()
        {
            return new Label
            {
                Font = UiTheme.Font(8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void SelectNavigationButton(string title, bool repaintImmediately)
        {
            foreach (KeyValuePair<string, SidebarNavigationButton> item in _navigationButtons)
            {
                bool selected = item.Key == title;
                item.Value.SetSelected(selected);
                if (repaintImmediately)
                {
                    item.Value.Update();
                }
            }
        }

        private void ShowContentPage(Control page)
        {
            UiComponentHelper.NormalizeControlMetrics(page);
            _contentPanel.Controls.Add(page);
        }

        private static string ResolveNavigationIconKey(string iconName)
        {
            return iconName == "home" ? "dashboard" : iconName;
        }
    }

    internal sealed class RoundStatusDot : Panel
    {
        public RoundStatusDot()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillEllipse(brush, new Rectangle(0, 0, Width - 1, Height - 1));
            }
        }
    }

    internal interface IUnsavedChangesAware
    {
        bool HasUnsavedChanges { get; }

        string UnsavedChangesDescription { get; }
    }

    internal sealed class SidebarNavigationButton : Button
    {
        private bool _isHovered;
        private bool _isSelected;

        public SidebarNavigationButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            BackColor = UiTheme.SidebarDark;
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            Padding = Padding.Empty;
            UseVisualStyleBackColor = false;
            FlatAppearance.BorderSize = 0;
        }

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected)
            {
                return;
            }

            _isSelected = selected;
            Invalidate();
        }

        protected override void OnMouseEnter(System.EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics graphics = pevent.Graphics;
            using (SolidBrush backgroundBrush = new SolidBrush(_isHovered && !_isSelected ? UiTheme.SidebarDarkHover : UiTheme.SidebarDark))
            {
                graphics.FillRectangle(backgroundBrush, ClientRectangle);
            }

            if (_isSelected)
            {
                using (SolidBrush selectedBrush = new SolidBrush(UiTheme.SidebarSelected))
                {
                    graphics.FillRectangle(selectedBrush, ClientRectangle);
                }
            }

            DrawCenteredContent(graphics);

            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(graphics, new Rectangle(4, 4, Math.Max(0, Width - 8), Math.Max(0, Height - 8)));
            }
        }

        private void DrawCenteredContent(Graphics graphics)
        {
            Image icon = Image;
            int iconWidth = icon == null ? 0 : icon.Width;
            int iconHeight = icon == null ? 0 : icon.Height;
            int gap = icon == null ? 0 : 10;
            Size textSize = TextRenderer.MeasureText(graphics, Text, Font, new Size(Width, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int totalWidth = iconWidth + gap + textSize.Width;
            int left = Math.Max(0, ((Width - totalWidth) / 2) - 10);

            if (icon != null)
            {
                int iconTop = Math.Max(0, (Height - iconHeight) / 2);
                graphics.DrawImage(icon, new Rectangle(left, iconTop, iconWidth, iconHeight));
                left += iconWidth + gap;
            }

            Rectangle textBounds = new Rectangle(left, 0, Math.Max(0, Width - left - 8), Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }
}
