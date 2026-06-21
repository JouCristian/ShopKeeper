using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class SalesManagementPage : UserControl
    {
        private readonly SalesService _salesService;
        private readonly BindingList<SalesLineView> _lines;
        private readonly BindingSource _lineBindingSource;
        private readonly BindingSource _orderBindingSource;
        private TextBox _productSearchTextBox;
        private ComboBox _productComboBox;
        private NumericUpDown _quantityNumeric;
        private NumericUpDown _priceNumeric;
        private TextBox _remarkTextBox;
        private Label _stockLabel;
        private Label _priceLabel;
        private Label _costLabel;
        private Label _expiryLabel;
        private Label _totalAmountLabel;
        private Label _totalCostLabel;
        private Label _profitLabel;
        private DataGridView _lineGrid;
        private DataGridView _orderGrid;

        public SalesManagementPage()
        {
            _salesService = new SalesService();
            _lines = new BindingList<SalesLineView>();
            _lineBindingSource = new BindingSource();
            _orderBindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "销售记账",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = BackColor
            };

            Panel inputPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = Color.White,
                Padding = new Padding(14)
            };
            BuildInputPanel(inputPanel);

            Panel summaryPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 96,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };
            BuildSummaryPanel(summaryPanel);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 180,
                Panel1MinSize = 120,
                Panel2MinSize = 120,
                BackColor = BackColor
            };
            split.SizeChanged += delegate { AdjustSalesSplit(split); };

            _lineGrid = CreateGrid();
            _lineGrid.DataSource = _lineBindingSource;
            _lineGrid.CellContentClick += LineGrid_CellContentClick;
            BuildLineColumns();

            _orderGrid = CreateGrid();
            _orderGrid.DataSource = _orderBindingSource;
            _orderGrid.CellContentClick += OrderGrid_CellContentClick;
            BuildOrderColumns();

            split.Panel1.Controls.Add(_lineGrid);
            split.Panel2.Controls.Add(_orderGrid);

            Label todayLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = "今日销售单",
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = BackColor
            };
            split.Panel2.Controls.Add(todayLabel);

            contentPanel.Controls.Add(split);
            contentPanel.Controls.Add(summaryPanel);
            contentPanel.Controls.Add(inputPanel);
            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            _lineBindingSource.DataSource = _lines;
            _lines.ListChanged += delegate { RefreshTotals(); };
            LoadProducts();
            LoadTodayOrders();
            RefreshTotals();
        }

        private void BuildInputPanel(Panel inputPanel)
        {
            _productSearchTextBox = new TextBox
            {
                Location = new Point(88, 16),
                Size = new Size(190, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _productSearchTextBox.KeyDown += ProductSearchTextBox_KeyDown;

            _productComboBox = new ComboBox
            {
                Location = new Point(454, 16),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _productComboBox.SelectedIndexChanged += ProductComboBox_SelectedIndexChanged;

            _quantityNumeric = CreateNumeric(88, 68, 0.001M, 999999M, 3);
            _priceNumeric = CreateNumeric(260, 68, 0M, 999999M, 2);

            Button searchButton = CreateButton("查找", Color.FromArgb(0, 123, 255), 84, 16);
            searchButton.Location = new Point(286, 14);
            searchButton.Click += delegate { LoadProducts(); };

            Button addButton = CreateButton("加入销售单", Color.FromArgb(40, 167, 69), 130, 68);
            addButton.Location = new Point(432, 64);
            addButton.Click += AddButton_Click;

            _stockLabel = CreateInfoLabel(724, 14, 130);
            _priceLabel = CreateInfoLabel(858, 14, 130);
            _costLabel = CreateInfoLabel(635, 52, 150);
            _expiryLabel = CreateInfoLabel(790, 52, 150);

            inputPanel.Controls.Add(CreateLabel("商品搜索", 14, 16, 72));
            inputPanel.Controls.Add(_productSearchTextBox);
            inputPanel.Controls.Add(searchButton);
            inputPanel.Controls.Add(CreateLabel("选择商品", 384, 16, 64));
            inputPanel.Controls.Add(_productComboBox);
            inputPanel.Controls.Add(CreateLabel("数量", 14, 69, 64));
            inputPanel.Controls.Add(_quantityNumeric);
            inputPanel.Controls.Add(CreateLabel("售价", 210, 69, 44));
            inputPanel.Controls.Add(_priceNumeric);
            inputPanel.Controls.Add(addButton);
            inputPanel.Controls.Add(_stockLabel);
            inputPanel.Controls.Add(_priceLabel);
            inputPanel.Controls.Add(_costLabel);
            inputPanel.Controls.Add(_expiryLabel);
        }

        private static void AdjustSalesSplit(SplitContainer split)
        {
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
                Location = new Point(72, 21),
                Size = new Size(310, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };

            _totalAmountLabel = CreateSummaryLabel(405, "应收金额：0.00");
            _totalCostLabel = CreateSummaryLabel(555, "商品成本：0.00");
            _profitLabel = CreateSummaryLabel(705, "本单毛利：0.00");

            Button saveButton = CreateButton("保存销售单", Color.FromArgb(0, 123, 255), 120, 50);
            saveButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saveButton.Location = new Point(summaryPanel.Width - 135, 49);
            saveButton.Click += SaveButton_Click;
            summaryPanel.Resize += delegate { saveButton.Location = new Point(summaryPanel.Width - 135, 49); };

            summaryPanel.Controls.Add(CreateLabel("备注", 14, 23, 52));
            summaryPanel.Controls.Add(_remarkTextBox);
            summaryPanel.Controls.Add(_totalAmountLabel);
            summaryPanel.Controls.Add(_totalCostLabel);
            summaryPanel.Controls.Add(_profitLabel);
            summaryPanel.Controls.Add(saveButton);
        }

        private void BuildLineColumns()
        {
            _lineGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductName", Width = 180 });
            AddNumberColumn(_lineGrid, "数量", "Quantity", 90, "N3");
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
        }

        private void BuildOrderColumns()
        {
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "销售单号", DataPropertyName = "OrderNo", Width = 170 });
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "销售时间", DataPropertyName = "SaleTime", Width = 150, DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" } });
            _orderGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品种类数", DataPropertyName = "ProductKindCount", Width = 90 });
            AddNumberColumn(_orderGrid, "销售总数量", "TotalQuantity", 110, "N3");
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
            if (e.RowIndex < 0 || _lineGrid.Columns[e.ColumnIndex].Name != "DeleteColumn")
            {
                return;
            }

            _lines.RemoveAt(e.RowIndex);
            RefreshTotals();
        }

        private void OrderGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _orderGrid.Columns[e.ColumnIndex].Name != "DetailColumn")
            {
                return;
            }

            SalesOrder order = _orderGrid.Rows[e.RowIndex].DataBoundItem as SalesOrder;
            if (order == null)
            {
                return;
            }

            using (SalesDetailForm form = new SalesDetailForm(order.Id))
            {
                form.ShowDialog(this);
            }
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
            LoadProducts();
            LoadTodayOrders();
            RefreshTotals();
        }

        private SalesOrder BuildOrder()
        {
            SalesOrder order = new SalesOrder
            {
                SaleTime = DateTime.Now,
                Remark = _remarkTextBox.Text
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

        private void LoadProducts()
        {
            long selectedProductId = 0;
            ProductItem selected = _productComboBox.SelectedItem as ProductItem;
            if (selected != null)
            {
                selectedProductId = selected.Product.Id;
            }

            _productComboBox.Items.Clear();
            foreach (Product product in _salesService.SearchActiveProducts(_productSearchTextBox.Text))
            {
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
            _orderBindingSource.DataSource = _salesService.GetTodayOrders();
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
            _lineBindingSource.ResetBindings(false);
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
                BorderStyle = BorderStyle.FixedSingle
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
                Increment = decimalPlaces == 3 ? 1 : 0.1M,
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
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
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

        private static Label CreateSummaryLabel(int left, string text)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, 21),
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

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
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
