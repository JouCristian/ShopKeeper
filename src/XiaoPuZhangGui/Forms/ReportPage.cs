using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private ProfitTrendChart _profitTrendChart;
        private Button _trendUnitButton;
        private TrendUnitOption _trendUnitOption;

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
            BackColor = UiTheme.PageBackground;
            Font = UiTheme.Font(11F);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 86,
                Text = "经营报表",
                Font = UiTheme.Font(28F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
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
                BackColor = UiTheme.CardBackground,
                Padding = new Padding(14, 12, 14, 12)
            };

            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.CardBackground
            };

            FlowLayoutPanel exportButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = UiTheme.CardBackground
            };

            _modeComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = UiTheme.Font(11F),
                Margin = new Padding(0, 10, 14, 0)
            };
            _modeComboBox.Items.AddRange(new object[] { "今日", "昨日", "本月", "上月", "本年", "去年", "自定义日期范围" });
            _modeComboBox.SelectedIndexChanged += ModeComboBox_SelectedIndexChanged;

            _startDatePicker = CreateDatePicker(DateTime.Today);
            _endDatePicker = CreateDatePicker(DateTime.Today);

            Button queryButton = UiComponentHelper.CreatePrimaryButton("查询", 88);
            queryButton.Margin = new Padding(0, 8, 18, 0);
            UiAssetHelper.ApplyIcon(queryButton, "action_search", 18, Color.White);
            UiComponentHelper.CenterButtonIcon(queryButton);
            queryButton.Click += delegate { LoadSelectedReport(); };

            _rangeLabel = new Label
            {
                Width = 320,
                Height = 38,
                Margin = new Padding(0, 9, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };

            filters.Controls.Add(CreateFilterLabel("统计模式", 76));
            filters.Controls.Add(_modeComboBox);
            filters.Controls.Add(CreateFilterLabel("开始日期", 76));
            filters.Controls.Add(_startDatePicker);
            filters.Controls.Add(CreateFilterLabel("结束日期", 76));
            filters.Controls.Add(_endDatePicker);
            filters.Controls.Add(queryButton);
            filters.Controls.Add(_rangeLabel);
            UiComponentHelper.NormalizeFilterBar(filters);

            Button exportReportButton = CreateExportButton("导出当前报表", UiTheme.PrimaryBlue, 132);
            exportReportButton.Click += ExportReportButton_Click;

            Button exportInventoryButton = CreateExportButton("导出库存清单", UiTheme.SuccessGreen, 132);
            exportInventoryButton.Click += ExportInventoryButton_Click;

            Button exportCreditButton = CreateExportButton("导出赊账清单", UiTheme.WarningOrange, 132);
            exportCreditButton.Click += ExportCreditButton_Click;

            Button exportExpiringButton = CreateExportButton("导出临期清单", UiTheme.InfoCyan, 132);
            exportExpiringButton.Click += ExportExpiringButton_Click;

            Button openFolderButton = CreateExportButton("打开导出目录", UiTheme.MutedGray, 132);
            openFolderButton.Click += OpenFolderButton_Click;

            exportButtons.Controls.Add(exportReportButton);
            exportButtons.Controls.Add(exportInventoryButton);
            exportButtons.Controls.Add(exportCreditButton);
            exportButtons.Controls.Add(exportExpiringButton);
            exportButtons.Controls.Add(openFolderButton);

            Panel controlsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.CardBackground
            };
            controlsPanel.Controls.Add(exportButtons);
            controlsPanel.Controls.Add(filters);

            PictureBox reportPicture = new PictureBox
            {
                Dock = DockStyle.Right,
                Width = 320,
                Image = UiAssetHelper.GetIllustration("report/header", new Size(340, 150)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = UiTheme.CardBackground,
                Margin = new Padding(12, 0, 0, 0)
            };

            panel.Controls.Add(controlsPanel);
            panel.Controls.Add(reportPicture);
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
                BackColor = UiTheme.PageBackground,
                Padding = new Padding(8)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 318));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel cards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                BackColor = UiTheme.PageBackground
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

            TableLayoutPanel bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.PageBackground
            };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66.67F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            _topSalesLabel = CreateHighlightLabel("销量最高商品：暂无数据");
            _topProfitLabel = CreateHighlightLabel("毛利润最高商品：暂无数据");

            TableLayoutPanel highlightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.PageBackground
            };
            highlightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            highlightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            highlightStack.Controls.Add(_topSalesLabel, 0, 0);
            highlightStack.Controls.Add(_topProfitLabel, 0, 1);

            bottomLayout.Controls.Add(CreateProfitTrendCard(), 0, 0);
            bottomLayout.Controls.Add(highlightStack, 1, 0);

            layout.Controls.Add(cards, 0, 0);
            layout.Controls.Add(bottomLayout, 0, 1);
            tab.Controls.Add(layout);
            return tab;
        }

        private Panel CreateProfitTrendCard()
        {
            Panel card = UiComponentHelper.CreateCardPanel(new Padding(14));
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(6);

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = UiTheme.CardBackground
            };

            Label title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "利润统计折线图",
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _trendUnitButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 40,
                Height = 32,
                BackColor = Color.White,
                ForeColor = UiTheme.PrimaryBlue,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Text = string.Empty
            };
            _trendUnitButton.FlatAppearance.BorderColor = UiTheme.CardBorder;
            _trendUnitButton.FlatAppearance.BorderSize = 1;
            UiAssetHelper.ApplyIcon(_trendUnitButton, "nav_settings", 18, UiTheme.PrimaryBlue);
            UiComponentHelper.CenterButtonIcon(_trendUnitButton);
            _trendUnitButton.Click += TrendUnitButton_Click;
            new ToolTip().SetToolTip(_trendUnitButton, "设置横坐标单位");

            header.Controls.Add(title);
            header.Controls.Add(_trendUnitButton);

            _profitTrendChart = new ProfitTrendChart
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.CardBackground
            };

            card.Controls.Add(_profitTrendChart);
            card.Controls.Add(header);
            return card;
        }

        private void TrendUnitButton_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            AddTrendUnitMenuItem(menu, "自动", TrendUnitOption.Auto);
            AddTrendUnitMenuItem(menu, "30分钟", TrendUnitOption.Minute30);
            AddTrendUnitMenuItem(menu, "1小时", TrendUnitOption.Hour1);
            AddTrendUnitMenuItem(menu, "3小时", TrendUnitOption.Hour3);
            AddTrendUnitMenuItem(menu, "6小时", TrendUnitOption.Hour6);
            AddTrendUnitMenuItem(menu, "一天", TrendUnitOption.Day1);
            AddTrendUnitMenuItem(menu, "3天", TrendUnitOption.Day3);
            AddTrendUnitMenuItem(menu, "10天", TrendUnitOption.Day10);
            AddTrendUnitMenuItem(menu, "15天", TrendUnitOption.Day15);
            AddTrendUnitMenuItem(menu, "一个月", TrendUnitOption.Month1);
            AddTrendUnitMenuItem(menu, "3个月", TrendUnitOption.Month3);
            AddTrendUnitMenuItem(menu, "6个月", TrendUnitOption.Month6);
            AddTrendUnitMenuItem(menu, "一年", TrendUnitOption.Year1);
            menu.Show(_trendUnitButton, new Point(0, _trendUnitButton.Height));
        }

        private void AddTrendUnitMenuItem(ContextMenuStrip menu, string text, TrendUnitOption option)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text)
            {
                Checked = _trendUnitOption == option,
                Tag = option
            };
            item.Click += delegate
            {
                _trendUnitOption = (TrendUnitOption)item.Tag;
                LoadSelectedReport();
            };
            menu.Items.Add(item);
        }

        private TabPage BuildRankTab()
        {
            TabPage tab = new TabPage("商品排行");
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260,
                BackColor = UiTheme.PageBackground
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
                BackColor = UiTheme.PageBackground
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
                TrendBucket trendBucket = ResolveTrendBucket(startTime, endTime);
                IList<ProfitTrendPoint> trendPoints = _reportService.GetProfitTrend(startTime, endTime, trendBucket.Duration, trendBucket.Months);

                BindReport(summary, salesRank, profitRank, lowStockItems, expiringItems, scrapItems, trendPoints, trendBucket.Label);
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

            if (mode == "本年" || mode == "去年")
            {
                return "年报";
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
            else if (mode == "本年")
            {
                startTime = ReportService.GetYearStart(today);
                endTime = ReportService.GetNextYearStart(today);
            }
            else if (mode == "去年")
            {
                DateTime lastYear = today.AddYears(-1);
                startTime = ReportService.GetYearStart(lastYear);
                endTime = ReportService.GetNextYearStart(lastYear);
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

        private TrendBucket ResolveTrendBucket(DateTime startTime, DateTime endTime)
        {
            if (_trendUnitOption != TrendUnitOption.Auto)
            {
                return ResolveTrendBucketByOption(_trendUnitOption);
            }

            string mode = _modeComboBox.SelectedItem == null ? "今日" : _modeComboBox.SelectedItem.ToString();
            if (mode == "本月" || mode == "上月")
            {
                return new TrendBucket(TimeSpan.FromDays(1), 0, "自动：一天");
            }

            if (mode == "本年" || mode == "去年")
            {
                return new TrendBucket(TimeSpan.Zero, 1, "自动：一月");
            }

            if (mode == "自定义日期范围" && (endTime - startTime).TotalDays > 2D)
            {
                return new TrendBucket(TimeSpan.FromDays(1), 0, "自动：一天");
            }

            return new TrendBucket(TimeSpan.FromHours(1), 0, "自动：1小时");
        }

        private static TrendBucket ResolveTrendBucketByOption(TrendUnitOption option)
        {
            switch (option)
            {
                case TrendUnitOption.Minute30:
                    return new TrendBucket(TimeSpan.FromMinutes(30), 0, "30分钟");
                case TrendUnitOption.Hour3:
                    return new TrendBucket(TimeSpan.FromHours(3), 0, "3小时");
                case TrendUnitOption.Hour6:
                    return new TrendBucket(TimeSpan.FromHours(6), 0, "6小时");
                case TrendUnitOption.Day1:
                    return new TrendBucket(TimeSpan.FromDays(1), 0, "一天");
                case TrendUnitOption.Day3:
                    return new TrendBucket(TimeSpan.FromDays(3), 0, "3天");
                case TrendUnitOption.Day10:
                    return new TrendBucket(TimeSpan.FromDays(10), 0, "10天");
                case TrendUnitOption.Day15:
                    return new TrendBucket(TimeSpan.FromDays(15), 0, "15天");
                case TrendUnitOption.Month1:
                    return new TrendBucket(TimeSpan.Zero, 1, "一个月");
                case TrendUnitOption.Month3:
                    return new TrendBucket(TimeSpan.Zero, 3, "3个月");
                case TrendUnitOption.Month6:
                    return new TrendBucket(TimeSpan.Zero, 6, "6个月");
                case TrendUnitOption.Year1:
                    return new TrendBucket(TimeSpan.Zero, 12, "一年");
                default:
                    return new TrendBucket(TimeSpan.FromHours(1), 0, "1小时");
            }
        }

        private void BindReport(
            ReportSummary summary,
            IList<ProductSalesRankItem> salesRank,
            IList<ProductProfitRankItem> profitRank,
            IList<LowStockReportItem> lowStockItems,
            IList<ExpiringProductReportItem> expiringItems,
            IList<ScrapSummaryItem> scrapItems,
            IList<ProfitTrendPoint> trendPoints,
            string trendUnitLabel)
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
            _profitTrendChart.SetData(trendPoints, trendUnitLabel);

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
            Color metricColor = ResolveMetricColor(title);
            Color backgroundColor = ResolveMetricBackground(title);
            Panel card = UiComponentHelper.CreateCardPanel(
                new Padding(14, 10, 14, 10),
                backgroundColor,
                ResolveMetricBorder(title));
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(6);

            Label titleLabel = UiComponentHelper.CreateIconTextLabel(title, ResolveMetricIcon(title), 18, metricColor);
            titleLabel.Dock = DockStyle.Top;
            titleLabel.Height = 28;
            titleLabel.ForeColor = UiTheme.TextSecondary;
            titleLabel.Font = UiTheme.Font(10.5F, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.AutoEllipsis = true;
            titleLabel.BackColor = backgroundColor;

            valueLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "0.00",
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = backgroundColor,
                ForeColor = UiTheme.TextPrimary,
                Font = UiTheme.Font(18F, FontStyle.Bold)
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
                BackColor = UiTheme.CardBackground,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
        }

        private static Panel CreateGridSection(string title, DataGridView grid, Label emptyLabel)
        {
            Panel section = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8),
                BackColor = UiTheme.PageBackground
            };

            Label titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = title,
                Font = UiTheme.Font(12F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary,
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
            return UiComponentHelper.CreateEmptyStateLabel(text, "empty/report");
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
                Font = UiTheme.Font(10F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            UiComponentHelper.ApplyButtonChrome(button, color, Color.Empty);
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
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };
        }

        private static Color ResolveMetricColor(string title)
        {
            if (title.Contains("报废"))
            {
                return Color.FromArgb(198, 40, 40);
            }

            if (title.Contains("赊账") || title.Contains("欠款"))
            {
                return Color.FromArgb(191, 79, 0);
            }

            if (title.Contains("利润") || title.Contains("实收") || title.Contains("已收"))
            {
                return Color.FromArgb(13, 122, 66);
            }

            if (title.Contains("成本") || title.Contains("进货"))
            {
                return Color.FromArgb(8, 112, 145);
            }

            if (title.Contains("单数") || title.Contains("件数"))
            {
                return Color.FromArgb(37, 99, 235);
            }

            return Color.FromArgb(25, 103, 210);
        }

        private static Color ResolveMetricBackground(string title)
        {
            if (title.Contains("报废"))
            {
                return UiTheme.SoftRed;
            }

            if (title.Contains("赊账") || title.Contains("欠款"))
            {
                return UiTheme.SoftOrange;
            }

            if (title.Contains("利润") || title.Contains("实收") || title.Contains("已收"))
            {
                return UiTheme.SoftGreen;
            }

            if (title.Contains("成本") || title.Contains("进货"))
            {
                return UiTheme.SoftCyan;
            }

            return UiTheme.CardBackground;
        }

        private static Color ResolveMetricBorder(string title)
        {
            if (title.Contains("报废"))
            {
                return UiTheme.DangerBorder;
            }

            if (title.Contains("赊账") || title.Contains("欠款"))
            {
                return UiTheme.WarningBorder;
            }

            if (title.Contains("利润") || title.Contains("实收") || title.Contains("已收"))
            {
                return UiTheme.SuccessBorder;
            }

            if (title.Contains("成本") || title.Contains("进货"))
            {
                return UiTheme.InfoBorder;
            }

            return UiTheme.CardBorder;
        }

        private static string ResolveMetricIcon(string title)
        {
            if (title.Contains("销售") || title.Contains("实收") || title.Contains("应收"))
            {
                return "nav_sales";
            }

            if (title.Contains("赊账") || title.Contains("欠款"))
            {
                return "nav_credit";
            }

            if (title.Contains("入库"))
            {
                return "nav_purchase";
            }

            if (title.Contains("报废"))
            {
                return "warning_stock";
            }

            return "nav_report";
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

        private sealed class TrendBucket
        {
            public TrendBucket(TimeSpan duration, int months, string label)
            {
                Duration = duration;
                Months = months;
                Label = label;
            }

            public TimeSpan Duration { get; private set; }

            public int Months { get; private set; }

            public string Label { get; private set; }
        }

        private enum TrendUnitOption
        {
            Auto,
            Minute30,
            Hour1,
            Hour3,
            Hour6,
            Day1,
            Day3,
            Day10,
            Day15,
            Month1,
            Month3,
            Month6,
            Year1
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2");
        }
    }

    internal sealed class ProfitTrendChart : Control
    {
        private IList<ProfitTrendPoint> _points = new List<ProfitTrendPoint>();
        private string _unitLabel = string.Empty;

        public ProfitTrendChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Font = UiTheme.Font(9.5F);
        }

        public void SetData(IList<ProfitTrendPoint> points, string unitLabel)
        {
            _points = points ?? new List<ProfitTrendPoint>();
            _unitLabel = string.IsNullOrWhiteSpace(unitLabel) ? string.Empty : unitLabel;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Rectangle plot = new Rectangle(68, 22, Math.Max(10, Width - 92), Math.Max(10, Height - 74));
            DrawCaption(e.Graphics, plot);

            if (_points == null || _points.Count == 0)
            {
                DrawEmpty(e.Graphics, plot);
                return;
            }

            decimal min = 0M;
            decimal max = 0M;
            foreach (ProfitTrendPoint point in _points)
            {
                min = Math.Min(min, point.NetProfit);
                max = Math.Max(max, point.NetProfit);
            }

            if (min == max)
            {
                max += 1M;
                min -= 1M;
            }

            DrawGrid(e.Graphics, plot, min, max);
            DrawLine(e.Graphics, plot, min, max);
            DrawXAxisLabels(e.Graphics, plot);
        }

        private void DrawCaption(Graphics graphics, Rectangle plot)
        {
            string text = string.IsNullOrEmpty(_unitLabel) ? "净利润" : "净利润 · " + _unitLabel;
            Rectangle bounds = new Rectangle(plot.Left, 0, Math.Max(0, plot.Width), 20);
            TextRenderer.DrawText(graphics, text, Font, bounds, UiTheme.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawEmpty(Graphics graphics, Rectangle plot)
        {
            TextRenderer.DrawText(
                graphics,
                "暂无利润趋势数据",
                UiTheme.Font(11F, FontStyle.Bold),
                plot,
                UiTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawGrid(Graphics graphics, Rectangle plot, decimal min, decimal max)
        {
            using (Pen gridPen = new Pen(Color.FromArgb(229, 236, 246)))
            using (Pen axisPen = new Pen(UiTheme.CardBorder))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + (plot.Height * i / 4);
                    graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                    decimal value = max - ((max - min) * i / 4M);
                    Rectangle labelBounds = new Rectangle(0, y - 10, plot.Left - 8, 20);
                    TextRenderer.DrawText(graphics, FormatCompactMoney(value), Font, labelBounds, UiTheme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }

                graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            }
        }

        private void DrawLine(Graphics graphics, Rectangle plot, decimal min, decimal max)
        {
            if (_points.Count == 1)
            {
                DrawPoint(graphics, plot.Left, ResolveY(plot, _points[0].NetProfit, min, max), UiTheme.PrimaryBlue);
                return;
            }

            PointF[] points = new PointF[_points.Count];
            for (int i = 0; i < _points.Count; i++)
            {
                float x = plot.Left + (plot.Width * i / (float)Math.Max(1, _points.Count - 1));
                float y = ResolveY(plot, _points[i].NetProfit, min, max);
                points[i] = new PointF(x, y);
            }

            using (Pen linePen = new Pen(UiTheme.PrimaryBlue, 2.4F))
            {
                linePen.StartCap = LineCap.Round;
                linePen.EndCap = LineCap.Round;
                linePen.LineJoin = LineJoin.Round;
                graphics.DrawLines(linePen, points);
            }

            int pointStep = _points.Count <= 60 ? 1 : Math.Max(1, _points.Count / 24);
            for (int i = 0; i < points.Length; i += pointStep)
            {
                DrawPoint(graphics, points[i].X, points[i].Y, UiTheme.PrimaryBlue);
            }

            PointF last = points[points.Length - 1];
            DrawPoint(graphics, last.X, last.Y, UiTheme.SuccessGreen);
            Rectangle valueBounds = new Rectangle(Math.Max(plot.Left, (int)last.X - 94), Math.Max(0, (int)last.Y - 30), 92, 22);
            TextRenderer.DrawText(graphics, FormatCompactMoney(_points[_points.Count - 1].NetProfit), UiTheme.Font(9.5F, FontStyle.Bold), valueBounds, UiTheme.SuccessGreen, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        private void DrawXAxisLabels(Graphics graphics, Rectangle plot)
        {
            int labelCount = Math.Min(7, _points.Count);
            int step = Math.Max(1, (_points.Count - 1) / Math.Max(1, labelCount - 1));
            for (int i = 0; i < _points.Count; i += step)
            {
                float x = plot.Left + (plot.Width * i / (float)Math.Max(1, _points.Count - 1));
                Rectangle bounds = new Rectangle((int)x - 36, plot.Bottom + 8, 72, 20);
                TextRenderer.DrawText(graphics, _points[i].Label, Font, bounds, UiTheme.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static float ResolveY(Rectangle plot, decimal value, decimal min, decimal max)
        {
            decimal span = max - min;
            if (span == 0)
            {
                return plot.Top + plot.Height / 2F;
            }

            decimal ratio = (max - value) / span;
            return plot.Top + (float)(ratio * plot.Height);
        }

        private static void DrawPoint(Graphics graphics, float x, float y, Color color)
        {
            using (SolidBrush brush = new SolidBrush(color))
            using (Pen border = new Pen(Color.White, 1.4F))
            {
                RectangleF rect = new RectangleF(x - 4F, y - 4F, 8F, 8F);
                graphics.FillEllipse(brush, rect);
                graphics.DrawEllipse(border, rect);
            }
        }

        private static string FormatCompactMoney(decimal value)
        {
            decimal abs = Math.Abs(value);
            if (abs >= 10000M)
            {
                return (value / 10000M).ToString("0.#") + "万";
            }

            return value.ToString("0.##");
        }
    }
}
