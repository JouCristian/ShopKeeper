using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class DashboardPage : UserControl
    {
        private readonly ReportService _reportService;
        private readonly Action<string> _navigateAction;
        private readonly BindingSource _lowStockBindingSource;
        private readonly BindingSource _expiringBindingSource;
        private readonly BindingSource _creditBindingSource;
        private readonly BindingSource _salesRankBindingSource;
        private readonly BindingSource _profitRankBindingSource;

        private Label _subtitleLabel;
        private Label _titleLabel;
        private Label _errorLabel;
        private Label _creditTitleLabel;
        private Label _lowStockEmptyLabel;
        private Label _expiringEmptyLabel;
        private Label _creditEmptyLabel;
        private Label _salesRankEmptyLabel;
        private Label _profitRankEmptyLabel;

        private Label _salesReceivableLabel;
        private Label _salesPaidLabel;
        private Label _newCreditLabel;
        private Label _creditCollectedLabel;
        private Label _outstandingCreditLabel;
        private Label _productCostLabel;
        private Label _grossProfitLabel;
        private Label _scrapLossLabel;
        private Label _netProfitLabel;
        private Label _salesOrderCountLabel;
        private Label _soldQuantityLabel;

        public DashboardPage(Action<string> navigateAction)
            : this(new ReportService(), navigateAction)
        {
        }

        internal DashboardPage(ReportService reportService, Action<string> navigateAction)
        {
            _reportService = reportService;
            _navigateAction = navigateAction;
            _lowStockBindingSource = new BindingSource();
            _expiringBindingSource = new BindingSource();
            _creditBindingSource = new BindingSource();
            _salesRankBindingSource = new BindingSource();
            _profitRankBindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Controls.Add(BuildContentPanel());
            Controls.Add(BuildHeaderPanel());

            Load += delegate { LoadDashboard(); };
        }

        private Panel BuildHeaderPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 88,
                BackColor = Color.White,
                Padding = new Padding(28, 12, 24, 10)
            };

            Button refreshButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 108,
                Text = "刷新",
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            refreshButton.FlatAppearance.BorderSize = 0;
            refreshButton.Click += delegate { LoadDashboard(); };

            _titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                Text = GetStoreName() + " · 首页看板",
                Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            _subtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = string.Empty,
                Font = new Font("Microsoft YaHei UI", 10.5F),
                ForeColor = Color.FromArgb(73, 80, 87),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(_subtitleLabel);
            panel.Controls.Add(_titleLabel);
            panel.Controls.Add(refreshButton);
            return panel;
        }

        private Panel BuildContentPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = BackColor
            };

            _errorLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Visible = false,
                ForeColor = Color.FromArgb(176, 42, 55),
                BackColor = Color.FromArgb(248, 215, 218),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10.5F)
            };
            tabs.TabPages.Add(BuildOverviewTab());
            tabs.TabPages.Add(BuildReminderTab());
            tabs.TabPages.Add(BuildRankTab());

            panel.Controls.Add(tabs);
            panel.Controls.Add(_errorLabel);
            return panel;
        }

        private TabPage BuildOverviewTab()
        {
            TabPage tab = new TabPage("经营概览")
            {
                AutoScroll = true
            };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(6)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            layout.Controls.Add(BuildMetricGrid(), 0, 0);
            layout.Controls.Add(BuildQuickActionPanel(), 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private TableLayoutPanel BuildMetricGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(0, 4, 0, 4)
            };

            for (int index = 0; index < 4; index++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.4F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));

            _salesReceivableLabel = CreateMetricValueLabel();
            _salesPaidLabel = CreateMetricValueLabel();
            _grossProfitLabel = CreateMetricValueLabel();
            _netProfitLabel = CreateMetricValueLabel();
            _outstandingCreditLabel = CreateMetricValueLabel();
            _newCreditLabel = CreateMetricValueLabel();
            _creditCollectedLabel = CreateMetricValueLabel();
            _scrapLossLabel = CreateMetricValueLabel();
            _salesOrderCountLabel = CreateMetricValueLabel();
            _soldQuantityLabel = CreateMetricValueLabel();
            _productCostLabel = CreateMetricValueLabel();

            grid.Controls.Add(CreateMetricCard("今日销售应收", _salesReceivableLabel), 0, 0);
            grid.Controls.Add(CreateMetricCard("今日实收金额", _salesPaidLabel), 1, 0);
            grid.Controls.Add(CreateMetricCard("今日商品成本", _productCostLabel), 2, 0);
            grid.Controls.Add(CreateMetricCard("今日销售毛利润", _grossProfitLabel), 3, 0);
            grid.Controls.Add(CreateMetricCard("今日商品净利润", _netProfitLabel), 0, 1);
            grid.Controls.Add(CreateMetricCard("当前未收赊账", _outstandingCreditLabel), 1, 1);
            grid.Controls.Add(CreateMetricCard("今日新增赊账", _newCreditLabel), 2, 1);
            grid.Controls.Add(CreateMetricCard("今日收回赊账", _creditCollectedLabel), 3, 1);
            grid.Controls.Add(CreateMetricCard("今日报废损失", _scrapLossLabel), 0, 2);
            grid.Controls.Add(CreateMetricCard("今日销售单数", _salesOrderCountLabel), 1, 2);
            grid.Controls.Add(CreateMetricCard("今日卖出件数", _soldQuantityLabel), 2, 2);
            return grid;
        }

        private Panel BuildQuickActionPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16, 12, 16, 12)
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "快捷入口",
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White
            };

            buttons.Controls.Add(CreateQuickButton("销售记账"));
            buttons.Controls.Add(CreateQuickButton("商品管理"));
            buttons.Controls.Add(CreateQuickButton("进货入库"));
            buttons.Controls.Add(CreateQuickButton("库存盘点"));
            buttons.Controls.Add(CreateQuickButton("赊账管理"));
            buttons.Controls.Add(CreateQuickButton("经营报表"));

            panel.Controls.Add(buttons);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private TabPage BuildReminderTab()
        {
            TabPage tab = new TabPage("提醒事项");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));

            DataGridView lowStockGrid = CreateGrid();
            lowStockGrid.DataSource = _lowStockBindingSource;
            lowStockGrid.Columns.Add(CreateTextColumn("ProductName", "商品名称", 160));
            lowStockGrid.Columns.Add(CreateNumberColumn("CurrentStock", "当前库存", 90));
            lowStockGrid.Columns.Add(CreateNumberColumn("MinStockAlert", "最低库存", 90));
            _lowStockEmptyLabel = CreateEmptyLabel("暂无低库存商品");

            DataGridView expiringGrid = CreateGrid();
            expiringGrid.DataSource = _expiringBindingSource;
            expiringGrid.Columns.Add(CreateTextColumn("ProductName", "商品名称", 150));
            expiringGrid.Columns.Add(CreateNumberColumn("QuantityRemaining", "剩余数量", 90));
            expiringGrid.Columns.Add(CreateDateColumn("ExpiryDate", "到期日期", 100));
            expiringGrid.Columns.Add(CreateTextColumn("DaysRemainingText", "剩余天数", 86));
            expiringGrid.Columns.Add(CreateTextColumn("StatusText", "状态", 70));
            _expiringEmptyLabel = CreateEmptyLabel("暂无临期商品");

            DataGridView creditGrid = CreateGrid();
            creditGrid.DataSource = _creditBindingSource;
            creditGrid.Columns.Add(CreateTextColumn("DebtorName", "欠款人备注", 150));
            creditGrid.Columns.Add(CreateMoneyColumn("RemainingAmount", "剩余欠款", 92));
            creditGrid.Columns.Add(CreateDateColumn("CreditDate", "赊账日期", 100));
            creditGrid.Columns.Add(CreateTextColumn("StatusText", "状态", 84));
            _creditEmptyLabel = CreateEmptyLabel("暂无未结清赊账");
            _creditTitleLabel = CreateSectionTitle("未结清赊账提醒");

            layout.Controls.Add(CreateSection("低库存提醒", lowStockGrid, _lowStockEmptyLabel), 0, 0);
            layout.Controls.Add(CreateSection("临期 / 已过期提醒", expiringGrid, _expiringEmptyLabel), 1, 0);
            layout.Controls.Add(CreateSection(_creditTitleLabel, creditGrid, _creditEmptyLabel), 2, 0);
            tab.Controls.Add(layout);
            return tab;
        }

        private TabPage BuildRankTab()
        {
            TabPage tab = new TabPage("商品排行");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            DataGridView salesRankGrid = CreateGrid();
            salesRankGrid.DataSource = _salesRankBindingSource;
            salesRankGrid.Columns.Add(CreateNumberColumn("Rank", "排名", 60));
            salesRankGrid.Columns.Add(CreateTextColumn("ProductName", "商品名称", 190));
            salesRankGrid.Columns.Add(CreateNumberColumn("SalesQuantity", "销售数量", 92));
            salesRankGrid.Columns.Add(CreateMoneyColumn("SalesAmount", "销售金额", 92));
            _salesRankEmptyLabel = CreateEmptyLabel("暂无销售排行");

            DataGridView profitRankGrid = CreateGrid();
            profitRankGrid.DataSource = _profitRankBindingSource;
            profitRankGrid.Columns.Add(CreateNumberColumn("Rank", "排名", 60));
            profitRankGrid.Columns.Add(CreateTextColumn("ProductName", "商品名称", 190));
            profitRankGrid.Columns.Add(CreateNumberColumn("SalesQuantity", "销售数量", 92));
            profitRankGrid.Columns.Add(CreateMoneyColumn("GrossProfit", "毛利润", 92));
            _profitRankEmptyLabel = CreateEmptyLabel("暂无毛利排行");

            layout.Controls.Add(CreateSection("今日销量排行 Top 5", salesRankGrid, _salesRankEmptyLabel), 0, 0);
            layout.Controls.Add(CreateSection("今日毛利润排行 Top 5", profitRankGrid, _profitRankEmptyLabel), 1, 0);
            tab.Controls.Add(layout);
            return tab;
        }

        private Panel CreateMetricCard(string title, Label valueLabel)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                Padding = new Padding(14, 12, 14, 12),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(73, 80, 87),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panel.Controls.Add(valueLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private Label CreateMetricValueLabel()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = "0.00",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Button CreateQuickButton(string title)
        {
            Button button = new Button
            {
                Text = title,
                Width = 126,
                Height = 42,
                Margin = new Padding(0, 10, 12, 0),
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(33, 37, 41),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(206, 212, 218);
            button.Click += delegate
            {
                if (_navigateAction != null)
                {
                    _navigateAction(title);
                }
            };
            return button;
        }

        private Panel CreateSection(string title, DataGridView grid, Label emptyLabel)
        {
            return CreateSection(CreateSectionTitle(title), grid, emptyLabel);
        }

        private Panel CreateSection(Label titleLabel, DataGridView grid, Label emptyLabel)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                BackColor = Color.White,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.FixedSingle
            };

            panel.Controls.Add(grid);
            panel.Controls.Add(emptyLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private Label CreateSectionTitle(string title)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Label CreateEmptyLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Text = text,
                Visible = false,
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            GridStyleHelper.ApplyStandardStyle(grid);
            return grid;
        }

        private DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string headerText, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = headerText,
                FillWeight = width,
                MinimumWidth = Math.Min(width, 80)
            };
        }

        private DataGridViewTextBoxColumn CreateNumberColumn(string propertyName, string headerText, int width)
        {
            DataGridViewTextBoxColumn column = CreateTextColumn(propertyName, headerText, width);
            column.DefaultCellStyle.Format = "0.##";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return column;
        }

        private DataGridViewTextBoxColumn CreateMoneyColumn(string propertyName, string headerText, int width)
        {
            DataGridViewTextBoxColumn column = CreateTextColumn(propertyName, headerText, width);
            column.DefaultCellStyle.Format = "0.00";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return column;
        }

        private DataGridViewTextBoxColumn CreateDateColumn(string propertyName, string headerText, int width)
        {
            DataGridViewTextBoxColumn column = CreateTextColumn(propertyName, headerText, width);
            column.DefaultCellStyle.Format = "yyyy-MM-dd";
            return column;
        }

        private void LoadDashboard()
        {
            try
            {
                DateTime today = DateTime.Today;
                DateTime startTime = ReportService.GetDayStart(today);
                DateTime endTime = ReportService.GetNextDayStart(today);
                ReportSummary summary = _reportService.GetSummary(startTime, endTime);

                IList<ProductSalesRankItem> salesRank = _reportService.GetProductSalesRank(startTime, endTime);
                IList<ProductProfitRankItem> profitRank = _reportService.GetProductProfitRank(startTime, endTime);
                IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
                IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProducts();
                IList<CreditRecord> creditRecords = _reportService.GetOutstandingCreditRecordsForExport();

                UpdateSummary(summary, today);
                BindLowStock(TakeFirst(lowStockItems, 5));
                BindExpiring(TakeFirst(expiringItems, 5));
                BindCredits(TakeFirst(creditRecords, 5), creditRecords.Count, summary.OutstandingCredit);
                BindSalesRank(TakeFirst(salesRank, 5));
                BindProfitRank(TakeFirst(profitRank, 5));

                _errorLabel.Visible = false;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = "首页数据加载失败：" + ex.Message;
                _errorLabel.Visible = true;
            }
        }

        private void UpdateSummary(ReportSummary summary, DateTime today)
        {
            _subtitleLabel.Text = "今日经营概览 " + today.ToString("yyyy-MM-dd");
            _salesReceivableLabel.Text = FormatMoney(summary.SalesReceivable);
            _salesPaidLabel.Text = FormatMoney(summary.SalesPaid);
            _newCreditLabel.Text = FormatMoney(summary.NewCredit);
            _creditCollectedLabel.Text = FormatMoney(summary.CreditCollected);
            _outstandingCreditLabel.Text = FormatMoney(summary.OutstandingCredit);
            _productCostLabel.Text = FormatMoney(summary.ProductCost);
            _grossProfitLabel.Text = FormatMoney(summary.GrossProfit);
            _scrapLossLabel.Text = FormatMoney(summary.ScrapLoss);
            _netProfitLabel.Text = FormatMoney(summary.NetProfit);
            _salesOrderCountLabel.Text = summary.SalesOrderCount.ToString("0");
            _soldQuantityLabel.Text = FormatQuantity(summary.SoldQuantity);
        }

        private void BindLowStock(IList<LowStockReportItem> items)
        {
            List<LowStockRow> rows = new List<LowStockRow>();
            foreach (LowStockReportItem item in items)
            {
                rows.Add(new LowStockRow
                {
                    ProductName = item.ProductName,
                    CurrentStock = item.CurrentStock,
                    MinStockAlert = item.MinStockAlert
                });
            }

            _lowStockBindingSource.DataSource = rows;
            _lowStockEmptyLabel.Visible = rows.Count == 0;
        }

        private void BindExpiring(IList<ExpiringProductReportItem> items)
        {
            List<ExpiringRow> rows = new List<ExpiringRow>();
            foreach (ExpiringProductReportItem item in items)
            {
                rows.Add(new ExpiringRow
                {
                    ProductName = item.ProductName,
                    QuantityRemaining = item.QuantityRemaining,
                    ExpiryDate = item.ExpiryDate,
                    DaysRemainingText = item.DaysRemaining < 0 ? item.DaysRemaining.ToString("0") : item.DaysRemaining.ToString("0"),
                    StatusText = item.StatusText
                });
            }

            _expiringBindingSource.DataSource = rows;
            _expiringEmptyLabel.Visible = rows.Count == 0;
        }

        private void BindCredits(IList<CreditRecord> records, int totalCount, decimal totalOutstanding)
        {
            List<CreditRow> rows = new List<CreditRow>();
            foreach (CreditRecord record in records)
            {
                rows.Add(new CreditRow
                {
                    DebtorName = string.IsNullOrWhiteSpace(record.DebtorName) ? "未填写" : record.DebtorName,
                    RemainingAmount = record.RemainingAmount,
                    CreditDate = record.CreditDate,
                    StatusText = record.StatusText
                });
            }

            _creditBindingSource.DataSource = rows;
            _creditTitleLabel.Text = string.Format("未结清赊账提醒：{0} 笔 / {1}", totalCount, FormatMoney(totalOutstanding));
            _creditEmptyLabel.Visible = rows.Count == 0;
        }

        private void BindSalesRank(IList<ProductSalesRankItem> items)
        {
            List<SalesRankRow> rows = new List<SalesRankRow>();
            int rank = 1;
            foreach (ProductSalesRankItem item in items)
            {
                rows.Add(new SalesRankRow
                {
                    Rank = rank,
                    ProductName = item.ProductName,
                    SalesQuantity = item.SalesQuantity,
                    SalesAmount = item.SalesAmount
                });
                rank++;
            }

            _salesRankBindingSource.DataSource = rows;
            _salesRankEmptyLabel.Visible = rows.Count == 0;
        }

        private void BindProfitRank(IList<ProductProfitRankItem> items)
        {
            List<ProfitRankRow> rows = new List<ProfitRankRow>();
            int rank = 1;
            foreach (ProductProfitRankItem item in items)
            {
                rows.Add(new ProfitRankRow
                {
                    Rank = rank,
                    ProductName = item.ProductName,
                    SalesQuantity = item.SalesQuantity,
                    GrossProfit = item.GrossProfit
                });
                rank++;
            }

            _profitRankBindingSource.DataSource = rows;
            _profitRankEmptyLabel.Visible = rows.Count == 0;
        }

        private static IList<T> TakeFirst<T>(IList<T> source, int count)
        {
            List<T> result = new List<T>();
            for (int index = 0; index < source.Count && index < count; index++)
            {
                result.Add(source[index]);
            }

            return result;
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("0.00");
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.##");
        }

        private static string GetStoreName()
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigFilePath))
                {
                    return "小铺掌柜";
                }

                XDocument document = XDocument.Load(AppPaths.ConfigFilePath);
                XElement root = document.Root;
                XElement storeNameElement = root == null ? null : root.Element("StoreName");
                string storeName = storeNameElement == null ? string.Empty : storeNameElement.Value;
                return string.IsNullOrWhiteSpace(storeName) ? "小铺掌柜" : storeName.Trim();
            }
            catch
            {
                return "小铺掌柜";
            }
        }

        private sealed class LowStockRow
        {
            public string ProductName { get; set; }

            public decimal CurrentStock { get; set; }

            public decimal MinStockAlert { get; set; }
        }

        private sealed class ExpiringRow
        {
            public string ProductName { get; set; }

            public decimal QuantityRemaining { get; set; }

            public DateTime ExpiryDate { get; set; }

            public string DaysRemainingText { get; set; }

            public string StatusText { get; set; }
        }

        private sealed class CreditRow
        {
            public string DebtorName { get; set; }

            public decimal RemainingAmount { get; set; }

            public DateTime CreditDate { get; set; }

            public string StatusText { get; set; }
        }

        private sealed class SalesRankRow
        {
            public int Rank { get; set; }

            public string ProductName { get; set; }

            public decimal SalesQuantity { get; set; }

            public decimal SalesAmount { get; set; }
        }

        private sealed class ProfitRankRow
        {
            public int Rank { get; set; }

            public string ProductName { get; set; }

            public decimal SalesQuantity { get; set; }

            public decimal GrossProfit { get; set; }
        }
    }
}
