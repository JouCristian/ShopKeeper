using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class ReportPage : UserControl
    {
        private readonly ReportService _reportService;
        private readonly ExcelExportService _exportService;
        private readonly BindingSource _salesRankBindingSource;
        private readonly BindingSource _profitRankBindingSource;
        private readonly BindingSource _lowStockBindingSource;
        private readonly BindingSource _expiringBindingSource;
        private readonly BindingSource _scrapBindingSource;

        private ComboBox _modeComboBox;
        private DateTimePicker _startDatePicker;
        private DateTimePicker _endDatePicker;
        private Label _rangeLabel;
        private Label _topSalesLabel;
        private Label _topProfitLabel;

        private Label _salesReceivableValueLabel;
        private Label _salesPaidValueLabel;
        private Label _newCreditValueLabel;
        private Label _creditCollectedValueLabel;
        private Label _outstandingCreditValueLabel;
        private Label _productCostValueLabel;
        private Label _grossProfitValueLabel;
        private Label _scrapLossValueLabel;
        private Label _netProfitValueLabel;
        private Label _salesOrderCountValueLabel;
        private Label _soldQuantityValueLabel;
        private Label _purchaseTotalValueLabel;

        private DataGridView _salesRankGrid;
        private DataGridView _profitRankGrid;
        private DataGridView _lowStockGrid;
        private DataGridView _expiringGrid;
        private DataGridView _scrapGrid;
        private Label _salesRankEmptyLabel;
        private Label _profitRankEmptyLabel;
        private Label _lowStockEmptyLabel;
        private Label _expiringEmptyLabel;
        private Label _scrapEmptyLabel;

        public ReportPage()
        {
            _reportService = new ReportService();
            _exportService = new ExcelExportService(_reportService);
            _salesRankBindingSource = new BindingSource();
            _profitRankBindingSource = new BindingSource();
            _lowStockBindingSource = new BindingSource();
            _expiringBindingSource = new BindingSource();
            _scrapBindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "经营报表",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = BackColor
            };

            Panel filterPanel = BuildFilterPanel();
            TabControl tabs = BuildTabs();

            contentPanel.Controls.Add(tabs);
            contentPanel.Controls.Add(filterPanel);
            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            _modeComboBox.SelectedIndex = 0;
            LoadSelectedReport();
        }

        private Panel BuildFilterPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };

            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White
            };

            FlowLayoutPanel exportButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White
            };

            _modeComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F),
                Margin = new Padding(0, 10, 14, 0)
            };
            _modeComboBox.Items.AddRange(new object[] { "今日", "昨日", "本月", "上月", "自定义日期范围" });
            _modeComboBox.SelectedIndexChanged += ModeComboBox_SelectedIndexChanged;

            _startDatePicker = CreateDatePicker(DateTime.Today);
            _endDatePicker = CreateDatePicker(DateTime.Today);

            Button queryButton = new Button
            {
                Text = "查询",
                Width = 88,
                Height = 38,
                Margin = new Padding(0, 8, 18, 0),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            queryButton.FlatAppearance.BorderSize = 0;
            queryButton.Click += delegate { LoadSelectedReport(); };

            _rangeLabel = new Label
            {
                Width = 320,
                Height = 38,
                Margin = new Padding(0, 9, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(73, 80, 87)
            };

            filters.Controls.Add(CreateFilterLabel("统计模式", 76));
            filters.Controls.Add(_modeComboBox);
            filters.Controls.Add(CreateFilterLabel("开始日期", 76));
            filters.Controls.Add(_startDatePicker);
            filters.Controls.Add(CreateFilterLabel("结束日期", 76));
            filters.Controls.Add(_endDatePicker);
            filters.Controls.Add(queryButton);
            filters.Controls.Add(_rangeLabel);

            Button exportReportButton = CreateExportButton("导出当前报表", Color.FromArgb(0, 123, 255), 128);
            exportReportButton.Click += ExportReportButton_Click;

            Button exportInventoryButton = CreateExportButton("导出库存清单", Color.FromArgb(40, 167, 69), 128);
            exportInventoryButton.Click += ExportInventoryButton_Click;

            Button exportCreditButton = CreateExportButton("导出赊账清单", Color.FromArgb(255, 193, 7), 128);
            exportCreditButton.ForeColor = Color.FromArgb(33, 37, 41);
            exportCreditButton.Click += ExportCreditButton_Click;

            Button exportExpiringButton = CreateExportButton("导出临期清单", Color.FromArgb(23, 162, 184), 128);
            exportExpiringButton.Click += ExportExpiringButton_Click;

            Button openFolderButton = CreateExportButton("打开导出目录", Color.FromArgb(108, 117, 125), 128);
            openFolderButton.Click += OpenFolderButton_Click;

            exportButtons.Controls.Add(exportReportButton);
            exportButtons.Controls.Add(exportInventoryButton);
            exportButtons.Controls.Add(exportCreditButton);
            exportButtons.Controls.Add(exportExpiringButton);
            exportButtons.Controls.Add(openFolderButton);

            panel.Controls.Add(exportButtons);
            panel.Controls.Add(filters);
            return panel;
        }

        private TabControl BuildTabs()
        {
            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular)
            };

            tabs.TabPages.Add(BuildOverviewTab());
            tabs.TabPages.Add(BuildRankTab());
            tabs.TabPages.Add(BuildInventoryTab());
            tabs.TabPages.Add(BuildScrapTab());
            return tabs;
        }

        private TabPage BuildOverviewTab()
        {
            TabPage tab = new TabPage("经营总览");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 318));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));

            TableLayoutPanel cards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            for (int i = 0; i < 4; i++)
            {
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }

            for (int i = 0; i < 3; i++)
            {
                cards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            }

            cards.Controls.Add(CreateMetricCard("销售应收", out _salesReceivableValueLabel), 0, 0);
            cards.Controls.Add(CreateMetricCard("实收金额", out _salesPaidValueLabel), 1, 0);
            cards.Controls.Add(CreateMetricCard("新增赊账", out _newCreditValueLabel), 2, 0);
            cards.Controls.Add(CreateMetricCard("收回赊账", out _creditCollectedValueLabel), 3, 0);
            cards.Controls.Add(CreateMetricCard("当前未收赊账", out _outstandingCreditValueLabel), 0, 1);
            cards.Controls.Add(CreateMetricCard("商品成本", out _productCostValueLabel), 1, 1);
            cards.Controls.Add(CreateMetricCard("销售毛利润", out _grossProfitValueLabel), 2, 1);
            cards.Controls.Add(CreateMetricCard("报废损失", out _scrapLossValueLabel), 3, 1);
            cards.Controls.Add(CreateMetricCard("商品净利润", out _netProfitValueLabel), 0, 2);
            cards.Controls.Add(CreateMetricCard("销售单数", out _salesOrderCountValueLabel), 1, 2);
            cards.Controls.Add(CreateMetricCard("卖出件数", out _soldQuantityValueLabel), 2, 2);
            cards.Controls.Add(CreateMetricCard("进货总额", out _purchaseTotalValueLabel), 3, 2);

            TableLayoutPanel highlights = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(248, 249, 250)
            };
            highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            _topSalesLabel = CreateHighlightLabel("销量最高商品：暂无数据");
            _topProfitLabel = CreateHighlightLabel("毛利润最高商品：暂无数据");
            highlights.Controls.Add(_topSalesLabel, 0, 0);
            highlights.Controls.Add(_topProfitLabel, 1, 0);

            layout.Controls.Add(cards, 0, 0);
            layout.Controls.Add(highlights, 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private TabPage BuildRankTab()
        {
            TabPage tab = new TabPage("商品排行");
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _salesRankGrid = CreateGrid();
            _salesRankGrid.DataSource = _salesRankBindingSource;
            _salesRankEmptyLabel = CreateEmptyLabel("暂无商品销量数据");
            BuildSalesRankColumns();

            _profitRankGrid = CreateGrid();
            _profitRankGrid.DataSource = _profitRankBindingSource;
            _profitRankEmptyLabel = CreateEmptyLabel("暂无商品毛利润数据");
            BuildProfitRankColumns();

            split.Panel1.Controls.Add(CreateGridSection("商品销量排行", _salesRankGrid, _salesRankEmptyLabel));
            split.Panel2.Controls.Add(CreateGridSection("商品毛利润排行", _profitRankGrid, _profitRankEmptyLabel));
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildInventoryTab()
        {
            TabPage tab = new TabPage("库存提醒");
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _lowStockGrid = CreateGrid();
            _lowStockGrid.DataSource = _lowStockBindingSource;
            _lowStockEmptyLabel = CreateEmptyLabel("暂无低库存商品");
            BuildLowStockColumns();

            _expiringGrid = CreateGrid();
            _expiringGrid.DataSource = _expiringBindingSource;
            _expiringEmptyLabel = CreateEmptyLabel("暂无临期商品");
            BuildExpiringColumns();

            split.Panel1.Controls.Add(CreateGridSection("低库存商品", _lowStockGrid, _lowStockEmptyLabel));
            split.Panel2.Controls.Add(CreateGridSection("临期商品", _expiringGrid, _expiringEmptyLabel));
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildScrapTab()
        {
            TabPage tab = new TabPage("报废摘要");
            _scrapGrid = CreateGrid();
            _scrapGrid.DataSource = _scrapBindingSource;
            _scrapEmptyLabel = CreateEmptyLabel("当前日期范围内暂无报废记录");
            BuildScrapColumns();
            tab.Controls.Add(CreateGridSection("报废记录摘要", _scrapGrid, _scrapEmptyLabel));
            return tab;
        }

        private void ModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool custom = _modeComboBox.SelectedItem != null && _modeComboBox.SelectedItem.ToString() == "自定义日期范围";
            _startDatePicker.Enabled = custom;
            _endDatePicker.Enabled = custom;

            if (!custom)
            {
                LoadSelectedReport();
            }
        }

        private void LoadSelectedReport()
        {
            if (!ValidateCustomRange())
            {
                return;
            }

            DateTime startTime;
            DateTime endTime;
            ResolveRange(out startTime, out endTime);

            try
            {
                ReportSummary summary = _reportService.GetSummary(startTime, endTime);
                IList<ProductSalesRankItem> salesRank = _reportService.GetProductSalesRank(startTime, endTime);
                IList<ProductProfitRankItem> profitRank = _reportService.GetProductProfitRank(startTime, endTime);
                IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
                IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProducts();
                IList<ScrapSummaryItem> scrapItems = _reportService.GetScrapSummary(startTime, endTime);

                BindReport(summary, salesRank, profitRank, lowStockItems, expiringItems, scrapItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取经营报表失败：" + ex.Message, "经营报表", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportReportButton_Click(object sender, EventArgs e)
        {
            if (!ValidateCustomRange())
            {
                return;
            }

            DateTime startTime;
            DateTime endTime;
            ResolveRange(out startTime, out endTime);
            string reportName = ResolveReportExportName();
            ExportWithMessage(delegate { return _exportService.ExportReport(reportName, startTime, endTime); });
        }

        private void ExportInventoryButton_Click(object sender, EventArgs e)
        {
            ExportWithMessage(delegate { return _exportService.ExportInventoryList(); });
        }

        private void ExportCreditButton_Click(object sender, EventArgs e)
        {
            ExportWithMessage(delegate { return _exportService.ExportCreditList(); });
        }

        private void ExportExpiringButton_Click(object sender, EventArgs e)
        {
            ExportWithMessage(delegate { return _exportService.ExportExpiringList(); });
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            try
            {
                _exportService.OpenExportDirectory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开导出目录失败：" + ex.Message, "导出目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportWithMessage(Func<string> exportAction)
        {
            try
            {
                string path = exportAction();
                DialogResult result = MessageBox.Show(
                    "导出成功，文件已保存到：\r\n" + path + "\r\n\r\n是否打开导出目录？",
                    "导出成功",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    _exportService.OpenExportDirectory();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string ResolveReportExportName()
        {
            string mode = _modeComboBox.SelectedItem == null ? "今日" : _modeComboBox.SelectedItem.ToString();
            if (mode == "本月" || mode == "上月")
            {
                return "月报";
            }

            if (mode == "自定义日期范围")
            {
                return "经营报表";
            }

            return "日报";
        }

        private void ResolveRange(out DateTime startTime, out DateTime endTime)
        {
            string mode = _modeComboBox.SelectedItem == null ? "今日" : _modeComboBox.SelectedItem.ToString();
            DateTime today = DateTime.Today;

            if (mode == "昨日")
            {
                startTime = ReportService.GetDayStart(today.AddDays(-1));
                endTime = ReportService.GetDayStart(today);
            }
            else if (mode == "本月")
            {
                startTime = ReportService.GetMonthStart(today);
                endTime = ReportService.GetNextMonthStart(today);
            }
            else if (mode == "上月")
            {
                DateTime lastMonth = today.AddMonths(-1);
                startTime = ReportService.GetMonthStart(lastMonth);
                endTime = ReportService.GetNextMonthStart(lastMonth);
            }
            else if (mode == "自定义日期范围")
            {
                DateTime startDate = _startDatePicker.Value.Date;
                DateTime endDate = _endDatePicker.Value.Date;

                startTime = startDate;
                endTime = endDate.AddDays(1);
            }
            else
            {
                startTime = ReportService.GetDayStart(today);
                endTime = ReportService.GetNextDayStart(today);
            }

            _rangeLabel.Text = "统计范围：" + startTime.ToString("yyyy-MM-dd") + " 至 " + endTime.AddDays(-1).ToString("yyyy-MM-dd");
        }

        private bool ValidateCustomRange()
        {
            string mode = _modeComboBox.SelectedItem == null ? "今日" : _modeComboBox.SelectedItem.ToString();
            if (mode != "自定义日期范围")
            {
                return true;
            }

            if (_endDatePicker.Value.Date >= _startDatePicker.Value.Date)
            {
                return true;
            }

            MessageBox.Show("结束日期不能早于开始日期，请重新选择日期范围。", "日期范围", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void BindReport(
            ReportSummary summary,
            IList<ProductSalesRankItem> salesRank,
            IList<ProductProfitRankItem> profitRank,
            IList<LowStockReportItem> lowStockItems,
            IList<ExpiringProductReportItem> expiringItems,
            IList<ScrapSummaryItem> scrapItems)
        {
            _salesReceivableValueLabel.Text = FormatMoney(summary.SalesReceivable);
            _salesPaidValueLabel.Text = FormatMoney(summary.SalesPaid);
            _newCreditValueLabel.Text = FormatMoney(summary.NewCredit);
            _creditCollectedValueLabel.Text = FormatMoney(summary.CreditCollected);
            _outstandingCreditValueLabel.Text = FormatMoney(summary.OutstandingCredit);
            _productCostValueLabel.Text = FormatMoney(summary.ProductCost);
            _grossProfitValueLabel.Text = FormatMoney(summary.GrossProfit);
            _scrapLossValueLabel.Text = FormatMoney(summary.ScrapLoss);
            _netProfitValueLabel.Text = FormatMoney(summary.NetProfit);
            _salesOrderCountValueLabel.Text = summary.SalesOrderCount.ToString("N0");
            _soldQuantityValueLabel.Text = summary.SoldQuantity.ToString("N2");
            _purchaseTotalValueLabel.Text = FormatMoney(summary.PurchaseTotal);

            _topSalesLabel.Text = salesRank.Count == 0
                ? "销量最高商品：暂无数据"
                : "销量最高商品：" + salesRank[0].ProductName + "，" + salesRank[0].SalesQuantity.ToString("N2");
            _topProfitLabel.Text = profitRank.Count == 0
                ? "毛利润最高商品：暂无数据"
                : "毛利润最高商品：" + profitRank[0].ProductName + "，" + FormatMoney(profitRank[0].GrossProfit);

            _salesRankBindingSource.DataSource = salesRank;
            _profitRankBindingSource.DataSource = profitRank;
            _lowStockBindingSource.DataSource = lowStockItems;
            _expiringBindingSource.DataSource = expiringItems;
            _scrapBindingSource.DataSource = scrapItems;

            _salesRankEmptyLabel.Visible = salesRank.Count == 0;
            _profitRankEmptyLabel.Visible = profitRank.Count == 0;
            _lowStockEmptyLabel.Visible = lowStockItems.Count == 0;
            _expiringEmptyLabel.Visible = expiringItems.Count == 0;
            _scrapEmptyLabel.Visible = scrapItems.Count == 0;
        }

        private void BuildSalesRankColumns()
        {
            AddTextColumn(_salesRankGrid, "商品名称", "ProductName", 260);
            AddNumberColumn(_salesRankGrid, "销售数量", "SalesQuantity", 120, "N2");
            AddNumberColumn(_salesRankGrid, "销售金额", "SalesAmount", 130, "N2");
        }

        private void BuildProfitRankColumns()
        {
            AddTextColumn(_profitRankGrid, "商品名称", "ProductName", 240);
            AddNumberColumn(_profitRankGrid, "销售数量", "SalesQuantity", 100, "N2");
            AddNumberColumn(_profitRankGrid, "销售金额", "SalesAmount", 110, "N2");
            AddNumberColumn(_profitRankGrid, "商品成本", "ProductCost", 110, "N2");
            AddNumberColumn(_profitRankGrid, "毛利润", "GrossProfit", 110, "N2");
        }

        private void BuildLowStockColumns()
        {
            AddTextColumn(_lowStockGrid, "商品名称", "ProductName", 240);
            AddTextColumn(_lowStockGrid, "分类", "CategoryName", 140);
            AddNumberColumn(_lowStockGrid, "当前库存", "CurrentStock", 120, "N2");
            AddNumberColumn(_lowStockGrid, "最低库存", "MinStockAlert", 120, "N2");
        }

        private void BuildExpiringColumns()
        {
            AddTextColumn(_expiringGrid, "商品名称", "ProductName", 220);
            AddTextColumn(_expiringGrid, "批次", "BatchCode", 150);
            AddNumberColumn(_expiringGrid, "批次数量", "QuantityRemaining", 110, "N2");
            AddDateColumn(_expiringGrid, "到期日期", "ExpiryDate", 120);
            AddNumberColumn(_expiringGrid, "剩余天数", "DaysRemaining", 90, "N0");
            AddTextColumn(_expiringGrid, "状态", "StatusText", 90);
        }

        private void BuildScrapColumns()
        {
            AddTextColumn(_scrapGrid, "商品名称", "ProductName", 260);
            AddNumberColumn(_scrapGrid, "报废数量", "Quantity", 120, "N2");
            AddNumberColumn(_scrapGrid, "损失金额", "LossAmount", 130, "N2");
            AddTextColumn(_scrapGrid, "原因", "Reason", 260);
        }

        private static Panel CreateMetricCard(string title, out Label valueLabel)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(6),
                Padding = new Padding(14, 10, 14, 10)
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = title,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(73, 80, 87),
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular)
            };

            valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "0.00",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(33, 37, 41),
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold)
            };

            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private static Label CreateHighlightLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                Margin = new Padding(6),
                Padding = new Padding(14, 0, 14, 0),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41)
            };
        }

        private static Panel CreateGridSection(string title, DataGridView grid, Label emptyLabel)
        {
            Panel section = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            section.Controls.Add(grid);
            section.Controls.Add(emptyLabel);
            section.Controls.Add(titleLabel);
            return section;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
            };
            GridStyleHelper.ApplyStandardStyle(grid);
            return grid;
        }

        private static Label CreateEmptyLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Image = UiAssetHelper.GetIcon("empty_box", 22),
                ImageAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(108, 117, 125),
                BackColor = Color.White,
                Padding = new Padding(40, 0, 0, 0),
                Visible = false
            };
        }

        private static DateTimePicker CreateDatePicker(DateTime value)
        {
            return new DateTimePicker
            {
                Width = 130,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Value = value,
                Enabled = false,
                Margin = new Padding(0, 8, 14, 0)
            };
        }

        private static Button CreateExportButton(string text, Color color, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                Margin = new Padding(0, 7, 10, 0),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            if (text.Contains("导出"))
            {
                UiAssetHelper.ApplyIcon(button, "export_excel", 18, Color.White);
                button.Padding = new Padding(8, 0, 0, 0);
            }

            return button;
        }

        private static Label CreateFilterLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                Height = 38,
                Margin = new Padding(0, 9, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static void AddTextColumn(DataGridView grid, string headerText, string propertyName, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            });
        }

        private static void AddNumberColumn(DataGridView grid, string headerText, string propertyName, int width, string format)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(column);
        }

        private static void AddDateColumn(DataGridView grid, string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            column.DefaultCellStyle.Format = "yyyy-MM-dd";
            grid.Columns.Add(column);
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2");
        }
    }
}
