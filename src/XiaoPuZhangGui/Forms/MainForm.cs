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
        private readonly Dictionary<string, Button> _navigationButtons;

        public MainForm()
        {
            Text = "小铺掌柜";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 680);
            Size = new Size(1240, 760);
            Font = UiTheme.Font(11F);
            BackColor = Color.White;
            ApplyApplicationIcon();

            _navigationButtons = new Dictionary<string, Button>();

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
            Button button = new Button
            {
                Height = 60,
                Width = _navigationListPanel.ClientSize.Width,
                Text = "     " + title,
                Tag = description,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.Font(11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = UiTheme.SidebarDark,
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = UiTheme.SidebarDarkHover;
            button.FlatAppearance.MouseDownBackColor = UiTheme.SidebarSelected;
            UiAssetHelper.ApplyIcon(button, "nav_" + ResolveNavigationIconKey(iconName), 22, Color.FromArgb(221, 235, 255));
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.Overlay;
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

            foreach (Button button in _navigationButtons.Values)
            {
                button.Width = width;
            }

            _navigationListPanel.HorizontalScroll.Value = 0;
            _navigationListPanel.HorizontalScroll.Visible = false;
        }

        private void ShowPage(string title)
        {
            foreach (KeyValuePair<string, Button> item in _navigationButtons)
            {
                item.Value.BackColor = item.Key == title
                    ? UiTheme.SidebarSelected
                    : UiTheme.SidebarDark;
            }

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
}
