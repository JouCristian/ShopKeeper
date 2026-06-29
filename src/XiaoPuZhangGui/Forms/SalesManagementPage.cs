using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class SalesManagementPage : UserControl, IUnsavedChangesAware, IResponsivePage
    {
        private readonly SalesService _salesService;
        private readonly CategoryService _categoryService;
        private readonly BindingList<SalesLineView> _lines;
        private readonly BindingSource _lineBindingSource;
        private readonly BindingSource _orderBindingSource;
        private TextBox _productSearchTextBox;
        private ComboBox _productComboBox;
        private ComboBox _productCategoryComboBox;
        private NumericUpDown _quantityNumeric;
        private NumericUpDown _priceNumeric;
        private NumericUpDown _paidAmountNumeric;
        private TextBox _remarkTextBox;
        private TextBox _debtorNameTextBox;
        private Label _stockLabel;
        private Label _priceLabel;
        private Label _costLabel;
        private Label _expiryLabel;
        private Label _totalAmountLabel;
        private Label _totalCostLabel;
        private Label _profitLabel;
        private Label _creditAmountLabel;
        private Label _productSearchLabel;
        private Label _productSelectLabel;
        private Label _productCategoryLabel;
        private Label _quantityLabel;
        private Label _salePriceLabel;
        private Label _remarkLabel;
        private Label _paidLabel;
        private Label _debtorLabel;
        private Button _searchButton;
        private Button _addButton;
        private Button _saveButton;
        private DataGridView _lineGrid;
        private DataGridView _orderGrid;
        private Panel _headerPanel;
        private Panel _contentPanel;
        private Panel _inputPanel;
        private Panel _summaryPanel;
        private SplitContainer _split;
        private Label _lineTitleLabel;
        private Label _orderTitleLabel;
        private Label _lineEmptyLabel;
        private Label _orderEmptyLabel;
        private bool _syncingPaidAmount;
        private bool _paidAmountTouched;

        public SalesManagementPage()
        {
            _salesService = new SalesService();
            _categoryService = new CategoryService();
            _lines = new BindingList<SalesLineView>();
            _lineBindingSource = new BindingSource();
            _orderBindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            _headerPanel = UiComponentHelper.CreatePageHeader(
                "销售记账",
                "快速录入销售单，自动计算成本、毛利和赊账",
                "headers/sales");

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = BackColor
            };

            _inputPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 138,
                BackColor = Color.White,
                Padding = new Padding(14),
                AutoScroll = false
            };
            BuildInputPanel(_inputPanel);
            _inputPanel.Resize += delegate { ArrangeInputPanel(); };

            _summaryPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 138,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12),
                AutoScroll = false
            };
            BuildSummaryPanel(_summaryPanel);
            _summaryPanel.Resize += delegate { ArrangeSummaryPanel(); };

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 180,
                Panel1MinSize = 120,
                Panel2MinSize = 120,
                BackColor = BackColor
            };
            _split.SizeChanged += delegate { AdjustSalesSplit(_split); };

            _lineGrid = CreateGrid();
            _lineGrid.DataSource = _lineBindingSource;
            _lineGrid.CellContentClick += LineGrid_CellContentClick;
            _lineGrid.DataBindingComplete += delegate { ApplySalesGridColumns(); };
            _lineGrid.SizeChanged += delegate { ApplySalesGridColumns(); };
            BuildLineColumns();
            _lineEmptyLabel = UiComponentHelper.CreateEmptyStateLabel("当前销售单暂无商品，请先选择商品并加入销售单。", "empty/sales_cart");

            _orderGrid = CreateGrid();
            _orderGrid.DataSource = _orderBindingSource;
            _orderGrid.CellContentClick += OrderGrid_CellContentClick;
            BuildOrderColumns();
            _orderEmptyLabel = UiComponentHelper.CreateEmptyStateLabel("今日暂无销售单。", "empty/sales_orders");

            _split.Panel1.Controls.Add(_lineGrid);
            _split.Panel1.Controls.Add(_lineEmptyLabel);
            _split.Panel2.Controls.Add(_orderGrid);
            _split.Panel2.Controls.Add(_orderEmptyLabel);

            _lineTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "当前销售单",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = BackColor
            };
            _split.Panel1.Controls.Add(_lineTitleLabel);

            _orderTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "今日销售单",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = BackColor
            };
            _split.Panel2.Controls.Add(_orderTitleLabel);

            _contentPanel.Controls.Add(_split);
            _contentPanel.Controls.Add(_summaryPanel);
            _contentPanel.Controls.Add(_inputPanel);
            Controls.Add(_contentPanel);
            Controls.Add(_headerPanel);

            _lineBindingSource.DataSource = _lines;
            _lines.ListChanged += delegate { RefreshTotals(); };
            LoadProductCategories();
            LoadProducts();
            LoadTodayOrders();
            RefreshTotals();
            ArrangeInputPanel();
            ArrangeSummaryPanel();
        }

        public void ApplyLayout(UiLayoutMode mode)
        {
            bool compact = ResponsiveLayoutManager.IsCompact(mode);
            bool veryCompact = ResponsiveLayoutManager.IsVeryCompact(mode);
            if (_headerPanel != null)
            {
                _headerPanel.Height = veryCompact ? 104 : (compact ? 112 : 176);
            }

            if (_contentPanel != null)
            {
                _contentPanel.Padding = compact ? new Padding(8) : new Padding(16);
            }

            if (_inputPanel != null)
            {
                _inputPanel.Height = veryCompact ? 118 : (compact ? 112 : 138);
                _inputPanel.AutoScrollMinSize = Size.Empty;
                ArrangeInputPanel();
            }

            if (_summaryPanel != null)
            {
                _summaryPanel.Height = veryCompact ? 122 : (compact ? 116 : 138);
                _summaryPanel.AutoScrollMinSize = Size.Empty;
                ArrangeSummaryPanel();
            }

            if (_split != null)
            {
                _split.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
                _split.Panel1MinSize = compact ? 0 : 120;
                _split.Panel2MinSize = compact ? 0 : 120;
                AdjustSalesSplit(_split);
            }

            ResponsiveLayoutManager.ApplyGridMetrics(_lineGrid, mode);
            ResponsiveLayoutManager.ApplyGridMetrics(_orderGrid, mode);
            ApplySalesGridColumns();
        }

        public bool HasUnsavedChanges
        {
            get
            {
                return _lines.Count > 0
                    || (_remarkTextBox != null && !string.IsNullOrWhiteSpace(_remarkTextBox.Text))
                    || (_debtorNameTextBox != null && !string.IsNullOrWhiteSpace(_debtorNameTextBox.Text))
                    || (_lines.Count > 0 && _paidAmountTouched);
            }
        }

        public string UnsavedChangesDescription
        {
            get { return "当前销售单尚未保存。"; }
        }

        private void BuildInputPanel(Panel inputPanel)
        {
            _productSearchTextBox = new TextBox
            {
                Location = new Point(88, 16),
                Size = new Size(190, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            UiComponentHelper.CenterTextBoxContent(_productSearchTextBox);
            _productSearchTextBox.KeyDown += ProductSearchTextBox_KeyDown;

            _productComboBox = new ComboBox
            {
                Location = new Point(468, 16),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _productComboBox.SelectedIndexChanged += ProductComboBox_SelectedIndexChanged;

            _productCategoryComboBox = new ComboBox
            {
                Location = new Point(430, 16),
                Size = new Size(120, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _productCategoryComboBox.SelectedIndexChanged += delegate { LoadProducts(); };

            _quantityNumeric = CreateNumeric(88, 68, 1M, 999999M, 0);
            _priceNumeric = CreateNumeric(260, 68, 0M, 999999M, 2);

            _searchButton = CreateButton("查找", UiTheme.PrimaryBlue, 84, 16);
            _searchButton.Location = new Point(286, 14);
            _searchButton.Click += delegate { LoadProducts(); };

            _addButton = CreateButton("加入销售单", UiTheme.SuccessGreen, 130, 68);
            _addButton.Location = new Point(432, 64);
            _addButton.Click += AddButton_Click;

            _stockLabel = CreateInfoLabel(724, 14, 130);
            _priceLabel = CreateInfoLabel(858, 14, 130);
            _costLabel = CreateInfoLabel(635, 52, 150);
            _expiryLabel = CreateInfoLabel(790, 52, 150);

            _productSearchLabel = CreateLabel("商品搜索", 14, 16, 72);
            _productCategoryLabel = CreateLabel("分类", 384, 16, 42);
            _productSelectLabel = CreateLabel("选择商品", 562, 16, 78);
            _quantityLabel = CreateLabel("数量", 14, 69, 64);
            _salePriceLabel = CreateLabel("售价", 210, 69, 44);

            inputPanel.Controls.Add(_productSearchLabel);
            inputPanel.Controls.Add(_productSearchTextBox);
            inputPanel.Controls.Add(_searchButton);
            inputPanel.Controls.Add(_productCategoryLabel);
            inputPanel.Controls.Add(_productCategoryComboBox);
            inputPanel.Controls.Add(_productSelectLabel);
            inputPanel.Controls.Add(_productComboBox);
            inputPanel.Controls.Add(_quantityLabel);
            inputPanel.Controls.Add(_quantityNumeric);
            inputPanel.Controls.Add(_salePriceLabel);
            inputPanel.Controls.Add(_priceNumeric);
            inputPanel.Controls.Add(_addButton);
            inputPanel.Controls.Add(_stockLabel);
            inputPanel.Controls.Add(_priceLabel);
            inputPanel.Controls.Add(_costLabel);
            inputPanel.Controls.Add(_expiryLabel);
        }

        private void ArrangeInputPanel()
        {
            if (_inputPanel == null || _productSearchTextBox == null || _productComboBox == null || _productCategoryComboBox == null)
            {
                return;
            }

            int width = Math.Max(0, _inputPanel.ClientSize.Width);
            int left = 14;
            int gap = 12;
            int labelWidth = 72;
            int row1 = 14;
            int row2 = 58;
            int row3 = 102;
            bool narrow = width < 980;
            bool cashier = width < 1250;

            _productSearchLabel.Location = new Point(left, row1 + 2);
            _productSearchLabel.Size = new Size(labelWidth, 30);
            _productSearchTextBox.Location = new Point(left + labelWidth + gap, row1);
            _productSearchTextBox.Size = new Size(narrow ? Math.Max(150, Math.Min(220, width - 420)) : (cashier ? 210 : 240), 32);
            _searchButton.Location = new Point(_productSearchTextBox.Right + gap, row1 - 2);
            _searchButton.Size = new Size(cashier ? 82 : 90, 40);

            int categoryX = _searchButton.Right + (cashier ? 18 : 24);
            _productCategoryLabel.Location = new Point(categoryX, row1 + 2);
            _productCategoryLabel.Size = new Size(42, 30);
            _productCategoryComboBox.Location = new Point(_productCategoryLabel.Right + 6, row1);
            _productCategoryComboBox.Size = new Size(cashier ? 104 : 120, 32);

            int productX = narrow ? left + labelWidth + gap : _productCategoryComboBox.Right + labelWidth + 18;
            int productY = narrow ? row2 : row1 + 2;
            _productSelectLabel.Location = new Point(productX - labelWidth - gap, productY);
            _productSelectLabel.Size = new Size(labelWidth, 30);
            _productComboBox.Location = new Point(productX, productY - 2);
            int comboRightLimit = narrow ? width - 24 : width - (cashier ? 300 : 420);
            _productComboBox.Size = new Size(Math.Max(cashier ? 210 : 220, comboRightLimit - productX), 32);

            int quantityY = narrow ? row3 : row2;
            _quantityLabel.Location = new Point(left, quantityY + 2);
            _quantityLabel.Size = new Size(52, 30);
            _quantityNumeric.Location = new Point(left + 64, quantityY);
            _quantityNumeric.Size = new Size(126, 32);

            _salePriceLabel.Location = new Point(_quantityNumeric.Right + gap + 8, quantityY + 2);
            _salePriceLabel.Size = new Size(44, 30);
            _priceNumeric.Location = new Point(_salePriceLabel.Right + 6, quantityY);
            _priceNumeric.Size = new Size(126, 32);

            _addButton.Location = new Point(_priceNumeric.Right + (cashier ? 24 : 36), quantityY - 4);
            _addButton.Size = new Size(cashier ? 136 : 152, 44);

            int infoY = narrow ? row3 : row1;
            int infoX = narrow ? Math.Min(width - 500, _addButton.Right + 24) : Math.Max(_productComboBox.Right + 18, width - (cashier ? 300 : 430));
            if (infoX < _addButton.Right + 18)
            {
                infoY = narrow ? row1 : row2;
                infoX = Math.Max(left, width - 430);
            }

            _stockLabel.Location = new Point(infoX, infoY);
            _stockLabel.Size = new Size(cashier ? 105 : 118, 30);
            _priceLabel.Location = new Point(_stockLabel.Right + gap, infoY);
            _priceLabel.Size = new Size(cashier ? 105 : 118, 30);
            _costLabel.Location = new Point(infoX, infoY + 42);
            _costLabel.Size = new Size(cashier ? 115 : 136, 30);
            _expiryLabel.Location = new Point(_costLabel.Right + gap, infoY + 42);
            _expiryLabel.Size = new Size(cashier ? 132 : 150, 30);

            if (narrow)
            {
                _inputPanel.Height = 136;
            }
        }

        private static void AdjustSalesSplit(SplitContainer split)
        {
            if (split.Orientation == Orientation.Vertical)
            {
                if (split.Width <= 720)
                {
                    return;
                }

                int verticalDistance = Math.Max(360, Math.Min(split.Width - 420, (int)(split.Width * 0.48)));
                if (split.SplitterDistance != verticalDistance)
                {
                    split.SplitterDistance = verticalDistance;
                }

                return;
            }

            const int minimumTopHeight = 180;
            const int minimumBottomHeight = 280;
            const int desiredBottomHeight = 300;

            if (split.Height <= minimumTopHeight + minimumBottomHeight)
            {
                return;
            }

            int desiredDistance = split.Height - desiredBottomHeight;
            int maxDistance = split.Height - minimumBottomHeight;
            int distance = Math.Max(minimumTopHeight, Math.Min(desiredDistance, maxDistance));

            if (split.SplitterDistance != distance)
            {
                split.SplitterDistance = distance;
            }
        }

        private void BuildSummaryPanel(Panel summaryPanel)
        {
            _remarkTextBox = new TextBox
            {
                Location = new Point(72, 14),
                Size = new Size(300, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            UiComponentHelper.CenterTextBoxContent(_remarkTextBox);

            _paidAmountNumeric = new NumericUpDown
            {
                Location = new Point(460, 14),
                Size = new Size(120, 30),
                Minimum = 0,
                Maximum = 9999999,
                DecimalPlaces = 2,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _paidAmountNumeric.ValueChanged += PaidAmountNumeric_ValueChanged;

            _debtorNameTextBox = new TextBox
            {
                Location = new Point(770, 14),
                Size = new Size(170, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            UiComponentHelper.CenterTextBoxContent(_debtorNameTextBox);

            _creditAmountLabel = new Label
            {
                Text = "新增赊账：0.00",
                Location = new Point(590, 14),
                Size = new Size(140, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = UiTheme.DangerRed
            };

            _totalAmountLabel = CreateSummaryLabel(405, 58, "应收金额：0.00");
            _totalCostLabel = CreateSummaryLabel(555, 58, "商品成本：0.00");
            _profitLabel = CreateSummaryLabel(705, 58, "本单毛利：0.00");

            _saveButton = CreateButton("保存销售单", UiTheme.PrimaryBlue, 120, 50);
            _saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _saveButton.Location = new Point(summaryPanel.Width - 135, 86);
            _saveButton.Click += SaveButton_Click;

            _remarkLabel = CreateLabel("备注", 14, 16, 52);
            _paidLabel = CreateLabel("实收", 405, 16, 48);
            _debtorLabel = CreateLabel("欠款人", 710, 16, 58);

            summaryPanel.Controls.Add(_remarkLabel);
            summaryPanel.Controls.Add(_remarkTextBox);
            summaryPanel.Controls.Add(_paidLabel);
            summaryPanel.Controls.Add(_paidAmountNumeric);
            summaryPanel.Controls.Add(_creditAmountLabel);
            summaryPanel.Controls.Add(_debtorLabel);
            summaryPanel.Controls.Add(_debtorNameTextBox);
            summaryPanel.Controls.Add(_totalAmountLabel);
            summaryPanel.Controls.Add(_totalCostLabel);
            summaryPanel.Controls.Add(_profitLabel);
            summaryPanel.Controls.Add(_saveButton);
            ArrangeSummaryPanel();
        }

        private void ArrangeSummaryPanel()
        {
            if (_summaryPanel == null || _remarkTextBox == null || _saveButton == null)
            {
                return;
            }

            int width = Math.Max(0, _summaryPanel.ClientSize.Width);
            bool narrow = width < 980;
            bool cashier = width < 1250;
            int left = 14;
            int gap = 12;
            int top = cashier ? 10 : 18;
            int secondRow = narrow ? 52 : (cashier ? 48 : 62);
            int saveWidth = cashier ? 118 : 150;
            int saveHeight = cashier ? 36 : 46;

            _saveButton.Size = new Size(saveWidth, saveHeight);
            _saveButton.Location = new Point(Math.Max(left, width - saveWidth - 14), cashier ? 64 : 76);

            int usableRight = Math.Max(360, _saveButton.Left - 24);
            _remarkLabel.Location = new Point(left, top + 2);
            _remarkLabel.Size = new Size(52, 30);
            _remarkTextBox.Location = new Point(left + 64, top);
            int remarkWidth = narrow ? Math.Max(260, usableRight - _remarkTextBox.Left) : 380;
            _remarkTextBox.Size = new Size(remarkWidth, 32);

            int paidX = narrow ? left : _remarkTextBox.Right + (cashier ? 24 : 34);
            int paidY = narrow ? secondRow : top;
            _paidLabel.Location = new Point(paidX, paidY + 2);
            _paidLabel.Size = new Size(48, 30);
            _paidAmountNumeric.Location = new Point(_paidLabel.Right + 8, paidY);
            _paidAmountNumeric.Size = new Size(128, 32);

            _creditAmountLabel.Location = new Point(_paidAmountNumeric.Right + gap, paidY + 2);
            _creditAmountLabel.Size = new Size(152, 30);

            int debtorX = narrow ? _creditAmountLabel.Right + gap : _creditAmountLabel.Right + 24;
            if (debtorX + 236 > usableRight && narrow)
            {
                debtorX = left;
                paidY = 104;
            }

            _debtorLabel.Location = new Point(debtorX, paidY + 2);
            _debtorLabel.Size = new Size(58, 30);
            _debtorNameTextBox.Location = new Point(_debtorLabel.Right + 8, paidY);
            _debtorNameTextBox.Size = new Size(Math.Max(120, Math.Min(180, usableRight - _debtorNameTextBox.Left)), 32);

            int totalY = narrow ? 84 : (cashier ? 58 : 62);
            int totalWidth = cashier ? 132 : 180;
            int totalGap = cashier ? 12 : 22;
            int totalGroupWidth = totalWidth * 3 + totalGap * 2;
            int maxTotalX = Math.Max(left, _saveButton.Left - 20 - totalGroupWidth);
            int totalX = narrow ? left : Math.Min(Math.Max(left, width - (cashier ? 610 : 720)), maxTotalX);
            _totalAmountLabel.Location = new Point(totalX, totalY);
            _totalAmountLabel.Size = new Size(totalWidth, 32);
            _totalCostLabel.Location = new Point(_totalAmountLabel.Right + totalGap, totalY);
            _totalCostLabel.Size = new Size(totalWidth, 32);
            _profitLabel.Location = new Point(_totalCostLabel.Right + totalGap, totalY);
            _profitLabel.Size = new Size(totalWidth, 32);

            if (narrow)
            {
                _summaryPanel.Height = 122;
            }
        }

        private void PaidAmountNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (!_syncingPaidAmount)
            {
                _paidAmountTouched = true;
            }

            RefreshCreditAmount();
        }

        private void BuildLineColumns()
        {
            _lineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductColumn", HeaderText = "商品", DataPropertyName = "ProductName", Width = 190 });
            AddNumberColumn(_lineGrid, "数量", "Quantity", 90, "N0");
            AddNumberColumn(_lineGrid, "售价", "SalePrice", 90, "N2");
            AddNumberColumn(_lineGrid, "成本价", "CostPrice", 90, "N2");
            AddNumberColumn(_lineGrid, "金额", "LineAmount", 100, "N2");
            AddNumberColumn(_lineGrid, "成本", "LineCost", 100, "N2");
            AddNumberColumn(_lineGrid, "毛利", "LineProfit", 100, "N2");
            _lineGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteColumn",
                HeaderText = "操作",
                Text = "删除",
                Width = 80,
                UseColumnTextForButtonValue = true
            });
            ApplySalesGridColumns();
        }

        private void ApplySalesGridColumns()
        {
            if (_lineGrid == null || _lineGrid.Columns.Count == 0)
            {
                return;
            }

            ApplyFillColumn(_lineGrid, "ProductColumn", 170, 180);
            ApplyFillColumn(_lineGrid, "Quantity", 64, 72);
            ApplyFillColumn(_lineGrid, "SalePrice", 70, 74);
            ApplyFillColumn(_lineGrid, "CostPrice", 70, 74);
            ApplyFillColumn(_lineGrid, "LineAmount", 72, 76);
            ApplyFillColumn(_lineGrid, "LineCost", 72, 76);
            ApplyFillColumn(_lineGrid, "LineProfit", 72, 76);
            ApplyFillColumn(_lineGrid, "DeleteColumn", 62, 64);
            _lineGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void BuildOrderColumns()
        {
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "销售单号", DataPropertyName = "OrderNo", Width = 170 });
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "销售时间", DataPropertyName = "SaleTime", Width = 150, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" } });
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品种类数", DataPropertyName = "ProductKindCount", Width = 90 });
            AddNumberColumn(_orderGrid, "销售总数量", "TotalQuantity", 110, "N0");
            AddNumberColumn(_orderGrid, "应收金额", "TotalAmount", 100, "N2");
            AddNumberColumn(_orderGrid, "商品成本", "TotalCost", 100, "N2");
            AddNumberColumn(_orderGrid, "毛利润", "GrossProfit", 100, "N2");
            _orderGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DetailColumn",
                HeaderText = "操作",
                Text = "查看",
                Width = 80,
                UseColumnTextForButtonValue = true
            });
            _orderGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteOrderColumn",
                HeaderText = "删除",
                Text = "删除",
                Width = 70,
                UseColumnTextForButtonValue = true
            });
        }

        private void ProductSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadProducts();
                e.Handled = true;
            }
        }

        private void ProductComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProductItem item = _productComboBox.SelectedItem as ProductItem;
            if (item == null)
            {
                ClearProductInfo();
                return;
            }

            Product product = item.Product;
            _stockLabel.Text = "库存：" + product.CurrentStock.ToString("N3");
            _priceLabel.Text = "售价：" + product.DefaultPrice.ToString("N2");
            _costLabel.Text = "成本：" + product.AverageCost.ToString("N2");
            _expiryLabel.Text = "保质期：" + (product.RequiresExpiry ? "启用" : "不启用");
            _priceNumeric.Value = Clamp(product.DefaultPrice, _priceNumeric.Minimum, _priceNumeric.Maximum);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            ProductItem item = _productComboBox.SelectedItem as ProductItem;
            if (item == null)
            {
                MessageBox.Show("请先选择商品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_quantityNumeric.Value <= 0)
            {
                MessageBox.Show("销售数量必须大于 0。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Product product = item.Product;
            decimal existingQuantity = 0;
            foreach (SalesLineView line in _lines)
            {
                if (line.ProductId == product.Id)
                {
                    existingQuantity += line.Quantity;
                }
            }

            decimal requestedQuantity = existingQuantity + _quantityNumeric.Value;
            if (requestedQuantity > product.CurrentStock)
            {
                MessageBox.Show(
                    "销售数量不能超过当前库存。\r\n当前库存：" + product.CurrentStock.ToString("N0") + "\r\n本次加入后数量：" + requestedQuantity.ToString("N0"),
                    "无法加入销售单",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _lines.Add(new SalesLineView
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = _quantityNumeric.Value,
                SalePrice = _priceNumeric.Value,
                CostPrice = product.AverageCost
            });

            _quantityNumeric.Value = 1;
            RefreshTotals();
        }

        private void LineGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _lineGrid.Columns[e.ColumnIndex].Name != "DeleteColumn")
            {
                return;
            }

            _lines.RemoveAt(e.RowIndex);
            RefreshTotals();
        }

        private void OrderGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            SalesOrder order = _orderGrid.Rows[e.RowIndex].DataBoundItem as SalesOrder;
            if (order == null)
            {
                return;
            }

            string columnName = _orderGrid.Columns[e.ColumnIndex].Name;
            if (columnName == "DetailColumn")
            {
                using (SalesDetailForm form = new SalesDetailForm(order.Id))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadProducts();
                        LoadTodayOrders();
                    }
                }
            }
            else if (columnName == "DeleteOrderColumn")
            {
                DeleteOrder(order);
            }
        }

        private void DeleteOrder(SalesOrder order)
        {
            DialogResult result = MessageBox.Show(
                "确认删除销售单「" + order.OrderNo + "」吗？\r\n\r\n删除后会恢复对应商品库存，并删除关联赊账记录。此操作不可撤销。",
                "删除销售单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            string message;
            if (!_salesService.TryDelete(order.Id, out message))
            {
                MessageBox.Show(message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadProducts();
            LoadTodayOrders();
            RefreshTotals();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            SalesOrder order = BuildOrder();

            if (_salesService.HasStockShortage(order))
            {
                DialogResult stockResult = MessageBox.Show(
                    "当前系统库存不足，可能是库存未及时修正，是否仍然继续销售？",
                    "库存不足",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (stockResult != DialogResult.Yes)
                {
                    return;
                }
            }

            if (_salesService.HasPriceBelowCost(order))
            {
                DialogResult priceResult = MessageBox.Show(
                    "当前售价低于成本价，确认继续销售吗？",
                    "售价低于成本",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (priceResult != DialogResult.Yes)
                {
                    return;
                }
            }

            string message;
            if (!_salesService.TrySave(order, out message))
            {
                MessageBox.Show(message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _lines.Clear();
            _remarkTextBox.Clear();
            _debtorNameTextBox.Clear();
            _paidAmountTouched = false;
            LoadProducts();
            LoadTodayOrders();
            RefreshTotals();
        }

        private SalesOrder BuildOrder()
        {
            SalesOrder order = new SalesOrder
            {
                SaleTime = DateTime.Now,
                Remark = _remarkTextBox.Text,
                PaidAmount = _paidAmountNumeric.Value,
                PaidAmountSpecified = true,
                DebtorName = _debtorNameTextBox.Text
            };

            foreach (SalesLineView line in _lines)
            {
                order.Items.Add(new SalesItem
                {
                    ProductId = line.ProductId,
                    ProductNameSnapshot = line.ProductName,
                    Quantity = line.Quantity,
                    SalePriceSnapshot = line.SalePrice,
                    CostPriceSnapshot = line.CostPrice,
                    LineAmount = line.LineAmount,
                    LineCost = line.LineCost,
                    LineProfit = line.LineProfit
                });
            }

            return order;
        }

        private void LoadProductCategories()
        {
            if (_productCategoryComboBox == null)
            {
                return;
            }

            _productCategoryComboBox.Items.Clear();
            _productCategoryComboBox.Items.Add(new ComboBoxItem("全部", null));
            foreach (Category category in _categoryService.GetActiveCategories())
            {
                _productCategoryComboBox.Items.Add(new ComboBoxItem(category.Name, category.Id));
            }

            _productCategoryComboBox.SelectedIndex = 0;
        }

        private void LoadProducts()
        {
            long selectedProductId = 0;
            ProductItem selected = _productComboBox.SelectedItem as ProductItem;
            if (selected != null)
            {
                selectedProductId = selected.Product.Id;
            }

            _productComboBox.Items.Clear();
            ComboBoxItem categoryItem = _productCategoryComboBox == null ? null : _productCategoryComboBox.SelectedItem as ComboBoxItem;
            long? categoryId = categoryItem == null ? null : categoryItem.Id;
            foreach (Product product in _salesService.SearchActiveProducts(_productSearchTextBox.Text))
            {
                if (categoryId.HasValue && product.CategoryId != categoryId.Value)
                {
                    continue;
                }

                ProductItem item = new ProductItem(product);
                _productComboBox.Items.Add(item);
                if (product.Id == selectedProductId)
                {
                    _productComboBox.SelectedItem = item;
                }
            }

            if (_productComboBox.SelectedIndex < 0 && _productComboBox.Items.Count > 0)
            {
                _productComboBox.SelectedIndex = 0;
            }

            if (_productComboBox.Items.Count == 0)
            {
                ClearProductInfo();
            }
        }

        private void LoadTodayOrders()
        {
            var orders = _salesService.GetTodayOrders();
            _orderBindingSource.DataSource = orders;
            _orderEmptyLabel.Visible = orders.Count == 0;
        }

        private void RefreshTotals()
        {
            decimal totalAmount = 0;
            decimal totalCost = 0;

            foreach (SalesLineView line in _lines)
            {
                totalAmount += line.LineAmount;
                totalCost += line.LineCost;
            }

            decimal profit = totalAmount - totalCost;
            _totalAmountLabel.Text = "应收金额：" + totalAmount.ToString("N2");
            _totalCostLabel.Text = "商品成本：" + totalCost.ToString("N2");
            _profitLabel.Text = "本单毛利：" + profit.ToString("N2");
            if (!_paidAmountTouched)
            {
                SetPaidAmount(totalAmount);
            }

            RefreshCreditAmount();
            _lineBindingSource.ResetBindings(false);
            ApplySalesGridColumns();
            _lineEmptyLabel.Visible = _lines.Count == 0;
        }

        private void RefreshCreditAmount()
        {
            decimal totalAmount = 0;
            foreach (SalesLineView line in _lines)
            {
                totalAmount += line.LineAmount;
            }

            decimal creditAmount = _paidAmountNumeric.Value < totalAmount ? totalAmount - _paidAmountNumeric.Value : 0;
            _creditAmountLabel.Text = "新增赊账：" + creditAmount.ToString("N2");
            _debtorNameTextBox.Enabled = creditAmount > 0;
            _debtorNameTextBox.BackColor = creditAmount > 0 ? Color.FromArgb(255, 243, 205) : Color.FromArgb(248, 249, 250);
            if (creditAmount == 0)
            {
                _debtorNameTextBox.Clear();
            }
        }

        private void SetPaidAmount(decimal value)
        {
            decimal safeValue = Clamp(value, _paidAmountNumeric.Minimum, _paidAmountNumeric.Maximum);
            _syncingPaidAmount = true;
            _paidAmountNumeric.Value = safeValue;
            _syncingPaidAmount = false;
        }

        private void ClearProductInfo()
        {
            _stockLabel.Text = "库存：-";
            _priceLabel.Text = "售价：-";
            _costLabel.Text = "成本：-";
            _expiryLabel.Text = "保质期：-";
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

        private static NumericUpDown CreateNumeric(int left, int top, decimal minimum, decimal maximum, int decimalPlaces)
        {
            return new NumericUpDown
            {
                Location = new Point(left, top),
                Size = new Size(110, 30),
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimalPlaces,
                Increment = decimalPlaces == 0 || decimalPlaces == 3 ? 1 : 0.1M,
                Value = minimum <= 1 && maximum >= 1 ? 1 : minimum,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
        }

        private static Button CreateButton(string text, Color color, int width, int top)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                Top = top,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static Label CreateLabel(string text, int left, int top, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateInfoLabel(int left, int top, int width)
        {
            return new Label
            {
                Location = new Point(left, top),
                Size = new Size(width, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(73, 80, 87)
            };
        }

        private static Label CreateSummaryLabel(int left, int top, string text)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(150, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
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

        private static void ApplyFillColumn(DataGridView grid, string key, int minimumWidth, float fillWeight)
        {
            DataGridViewColumn column = null;
            foreach (DataGridViewColumn candidate in grid.Columns)
            {
                if (string.Equals(candidate.Name, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.DataPropertyName, key, StringComparison.OrdinalIgnoreCase))
                {
                    column = candidate;
                    break;
                }
            }

            if (column == null)
            {
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.MinimumWidth = minimumWidth;
            column.FillWeight = fillWeight;
            column.Resizable = DataGridViewTriState.False;
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private sealed class ComboBoxItem
        {
            public ComboBoxItem(string text, long? id)
            {
                Text = text;
                Id = id;
            }

            public string Text { get; private set; }

            public long? Id { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class ProductItem
        {
            public ProductItem(Product product)
            {
                Product = product;
            }

            public Product Product { get; private set; }

            public override string ToString()
            {
                return Product.Name + "  库存 " + Product.CurrentStock.ToString("N3");
            }
        }

        private sealed class SalesLineView
        {
            public long ProductId { get; set; }

            public string ProductName { get; set; }

            public decimal Quantity { get; set; }

            public decimal SalePrice { get; set; }

            public decimal CostPrice { get; set; }

            public decimal LineAmount
            {
                get { return Quantity * SalePrice; }
            }

            public decimal LineCost
            {
                get { return Quantity * CostPrice; }
            }

            public decimal LineProfit
            {
                get { return LineAmount - LineCost; }
            }
        }
    }
}
