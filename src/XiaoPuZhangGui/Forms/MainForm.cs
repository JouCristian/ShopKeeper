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
        private readonly Dictionary<string, Button> _navigationButtons;

        public MainForm()
        {
            Text = "小铺掌柜";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 680);
            Size = new Size(1240, 760);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            _navigationButtons = new Dictionary<string, Button>();

            _navigationPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = Color.FromArgb(52, 58, 64),
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

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            Controls.Add(_contentPanel);
            Controls.Add(_navigationPanel);
            _navigationPanel.Controls.Add(_navigationListPanel);

            BuildNavigation();
            ShowPage(HomeDashboardTitle);
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
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.White
            };

            _navigationPanel.Controls.Add(brand);
        }

        private void AddNavigationButton(string title, string description, string iconName)
        {
            Button button = new Button
            {
                Height = 60,
                Width = _navigationPanel.Width,
                Text = title,
                Tag = description,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(52, 58, 64),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            UiAssetHelper.ApplyIcon(button, iconName, 22, Color.FromArgb(221, 235, 255));
            button.Click += delegate { ShowPage(title); };

            _navigationButtons.Add(title, button);
            _navigationListPanel.Controls.Add(button);
        }

        private void ShowPage(string title)
        {
            foreach (KeyValuePair<string, Button> item in _navigationButtons)
            {
                item.Value.BackColor = item.Key == title
                    ? Color.FromArgb(0, 123, 255)
                    : Color.FromArgb(52, 58, 64);
            }

            _contentPanel.Controls.Clear();

            if (title == HomeDashboardTitle)
            {
                _contentPanel.Controls.Add(new DashboardPage(ShowPage));
                return;
            }

            if (title == SalesManagementTitle)
            {
                _contentPanel.Controls.Add(new SalesManagementPage());
                return;
            }

            if (title == ProductManagementTitle)
            {
                _contentPanel.Controls.Add(new ProductManagementPage());
                return;
            }

            if (title == PurchaseManagementTitle)
            {
                _contentPanel.Controls.Add(new PurchaseManagementPage());
                return;
            }

            if (title == InventoryCheckTitle)
            {
                _contentPanel.Controls.Add(new InventoryCheckPage());
                return;
            }

            if (title == CreditManagementTitle)
            {
                _contentPanel.Controls.Add(new CreditManagementPage());
                return;
            }

            if (title == ReportTitle)
            {
                _contentPanel.Controls.Add(new ReportPage());
                return;
            }

            if (title == SettingsTitle)
            {
                _contentPanel.Controls.Add(new SettingsPage());
                return;
            }

            string description = _navigationButtons.ContainsKey(title)
                ? _navigationButtons[title].Tag.ToString()
                : string.Empty;

            _contentPanel.Controls.Add(new PlaceholderPage(title, description));
        }
    }
}
