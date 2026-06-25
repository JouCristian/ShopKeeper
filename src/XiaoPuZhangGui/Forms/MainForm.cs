using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
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
        private const string SettingsTitle = "系统设置";

        private readonly Panel _navigationPanel;
        private readonly FlowLayoutPanel _navigationListPanel;
        private readonly Panel _contentPanel;
        private readonly Label _footerLabel;
        private readonly Dictionary<string, SidebarNavigationButton> _navigationButtons;
        private string _currentPageTitle;

        public MainForm()
        {
            Text = "小铺掌柜";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 680);
            Size = new Size(1240, 760);
            Font = UiTheme.Font(11F);
            BackColor = Color.White;
            ApplyApplicationIcon();

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

            _footerLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Text = "本地离线运行",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.Font(9.5F),
                ForeColor = Color.FromArgb(196, 211, 229),
                BackColor = UiTheme.SidebarDark
            };

            Controls.Add(_contentPanel);
            Controls.Add(_navigationPanel);
            _navigationPanel.Controls.Add(_navigationListPanel);
            _navigationPanel.Controls.Add(_footerLabel);

            BuildNavigation();
            ResizeNavigationButtons();
            ShowPage(HomeDashboardTitle);
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
            AddNavigationButton(SettingsTitle, "店铺信息、数据库路径、备份路径和恢复相关设置入口。", "settings");
        }

        private void AddBrand()
        {
            Label brand = new Label
            {
                Dock = DockStyle.Top,
                Height = 72,
                Text = "小铺掌柜",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = UiTheme.Font(18F, FontStyle.Bold),
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
