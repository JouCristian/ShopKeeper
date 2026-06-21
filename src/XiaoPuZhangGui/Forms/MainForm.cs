using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class MainForm : Form
    {
        private const string ProductManagementTitle = "商品管理";
        private const string PurchaseManagementTitle = "进货入库";
        private const string SettingsTitle = "系统设置";

        private readonly Panel _navigationPanel;
        private readonly FlowLayoutPanel _navigationListPanel;
        private readonly Panel _contentPanel;
        private readonly Dictionary<string, Button> _navigationButtons;

        public MainForm()
        {
            Text = "小铺掌柜";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1024, 640);
            Size = new Size(1180, 720);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            _navigationButtons = new Dictionary<string, Button>();

            _navigationPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 210,
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
            ShowPage("首页看板");
        }

        private void BuildNavigation()
        {
            AddBrand();

            AddNavigationButton("首页看板", "今日销售、利润、低库存和临期提醒。");
            AddNavigationButton("销售记账", "收银单模式入口，后续支持多商品开单、实收和赊账。");
            AddNavigationButton(ProductManagementTitle, "商品档案、分类、售价、库存预警和保质期设置入口。");
            AddNavigationButton(PurchaseManagementTitle, "采购入库、批次、进价和到期日期登记入口。");
            AddNavigationButton("库存盘点", "库存修正、盈亏原因和报废处理入口。");
            AddNavigationButton("赊账管理", "欠款查询、还款登记和备注管理入口。");
            AddNavigationButton("报表导出", "日报、月报、排行和 Excel/WPS 导出入口。");
            AddNavigationButton(SettingsTitle, "店铺信息、数据库路径、备份路径和恢复相关设置入口。");
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

        private void AddNavigationButton(string title, string description)
        {
            Button button = new Button
            {
                Height = 58,
                Width = _navigationPanel.Width,
                Text = title,
                Tag = description,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(22, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(52, 58, 64),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
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
