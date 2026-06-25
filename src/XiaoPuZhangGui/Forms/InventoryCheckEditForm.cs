using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class InventoryCheckEditForm : Form
    {
        private readonly InventoryCheckService _inventoryCheckService;
        private readonly BindingList<InventoryLineView> _lines;
        private readonly BindingSource _bindingSource;
        private TextBox _productSearchTextBox;
        private ComboBox _productComboBox;
        private NumericUpDown _actualStockNumeric;
        private ComboBox _reasonComboBox;
        private TextBox _lineRemarkTextBox;
        private TextBox _remarkTextBox;
        private Label _systemStockLabel;
        private Label _costLabel;
        private Label _differenceLabel;
        private Label _differenceAmountLabel;
        private Label _totalLabel;
        private DataGridView _grid;

        public InventoryCheckEditForm()
        {
            _inventoryCheckService = new InventoryCheckService();
            _lines = new BindingList<InventoryLineView>();
            _bindingSource = new BindingSource();

            Text = "新建盘点单";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 640);
            Size = new Size(1040, 700);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            BuildUi();
            _bindingSource.DataSource = _lines;
            _lines.ListChanged += delegate { RefreshTotals(); };
            LoadProducts();
            RefreshPreview();
            RefreshTotals();
        }

        private void BuildUi()
        {
            Controls.Add(new Label
            {
                Text = "新建盘点单",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(18, 12),
                Size = new Size(220, 40)
            });

            Panel inputPanel = new Panel
            {
                Location = new Point(18, 62),
                Size = new Size(985, 150),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            BuildInputPanel(inputPanel);
            Controls.Add(inputPanel);

            _grid = new DataGridView
            {
                Location = new Point(18, 228),
                Size = new Size(985, 330),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                DataSource = _bindingSource
            };
            _grid.CellContentClick += Grid_CellContentClick;
            GridStyleHelper.ApplyStandardStyle(_grid);
            BuildColumns();
            ApplyInventoryGridColumnLayout();
            Controls.Add(_grid);

            Controls.Add(CreateLabel("整单备注", 18, 586, 80));
            _remarkTextBox = new TextBox
            {
                Location = new Point(98, 584),
                Size = new Size(420, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            Controls.Add(_remarkTextBox);

            _totalLabel = new Label
            {
                Location = new Point(540, 584),
                Size = new Size(280, 32),
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_totalLabel);

            Button saveButton = CreateButton("保存盘点单", UiTheme.PrimaryBlue, 130);
            saveButton.Location = new Point(854, 582);
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);
        }

        private void BuildInputPanel(Panel panel)
        {
            _productSearchTextBox = new TextBox { Location = new Point(84, 18), Size = new Size(190, 30), Font = new Font("Microsoft YaHei UI", 11F) };
            _productSearchTextBox.KeyDown += ProductSearchTextBox_KeyDown;
            Button searchButton = CreateButton("查找", UiTheme.PrimaryBlue, 80);
            searchButton.Location = new Point(286, 16);
            searchButton.Click += delegate { LoadProducts(); };

            _productComboBox = new ComboBox
            {
                Location = new Point(450, 18),
                Size = new Size(260, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _productComboBox.SelectedIndexChanged += ProductComboBox_SelectedIndexChanged;

            _actualStockNumeric = CreateNumeric(84, 74);
            _actualStockNumeric.ValueChanged += delegate { RefreshPreview(); };

            _reasonComboBox = new ComboBox
            {
                Location = new Point(280, 74),
                Size = new Size(130, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _reasonComboBox.Items.AddRange(new object[] { "", "盘盈", "盘亏", "账面错误", "漏记入库", "漏记销售", "计量误差", "其他" });
            _reasonComboBox.SelectedIndex = 0;

            _lineRemarkTextBox = new TextBox { Location = new Point(470, 74), Size = new Size(170, 30), Font = new Font("Microsoft YaHei UI", 11F) };

            Button addButton = CreateButton("加入明细", UiTheme.SuccessGreen, 110);
            addButton.Location = new Point(858, 108);
            addButton.Click += AddButton_Click;

            _systemStockLabel = CreateInfoLabel(728, 16, 120);
            _costLabel = CreateInfoLabel(850, 16, 120);
            _differenceLabel = CreateInfoLabel(660, 74, 105);
            _differenceAmountLabel = CreateInfoLabel(770, 74, 110);

            panel.Controls.Add(CreateLabel("商品搜索", 14, 19, 70));
            panel.Controls.Add(_productSearchTextBox);
            panel.Controls.Add(searchButton);
            panel.Controls.Add(CreateLabel("选择商品", 380, 19, 70));
            panel.Controls.Add(_productComboBox);
            panel.Controls.Add(CreateLabel("实际库存", 14, 75, 70));
            panel.Controls.Add(_actualStockNumeric);
            panel.Controls.Add(CreateLabel("原因", 232, 75, 42));
            panel.Controls.Add(_reasonComboBox);
            panel.Controls.Add(CreateLabel("备注", 424, 75, 42));
            panel.Controls.Add(_lineRemarkTextBox);
            panel.Controls.Add(addButton);
            panel.Controls.Add(_systemStockLabel);
            panel.Controls.Add(_costLabel);
            panel.Controls.Add(_differenceLabel);
            panel.Controls.Add(_differenceAmountLabel);
        }

        private void BuildColumns()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductName", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "CategoryName", Width = 80 });
            AddNumberColumn("系统库存", "SystemStock", 90, "N3");
            AddNumberColumn("实际库存", "ActualStock", 90, "N3");
            AddNumberColumn("差异数量", "DifferenceQuantity", 90, "N3");
            AddNumberColumn("成本价", "CostPrice", 90, "N2");
            AddNumberColumn("差异金额", "DifferenceAmount", 100, "N2");
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "原因", DataPropertyName = "Reason", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 120 });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteColumn",
                HeaderText = "操作",
                Text = "删除",
                Width = 70,
                UseColumnTextForButtonValue = true
            });
        }

        private void ApplyInventoryGridColumnLayout()
        {
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            SetColumnWidth(0, 150);
            SetColumnWidth(1, 74);
            SetColumnWidth(2, 92);
            SetColumnWidth(3, 92);
            SetColumnWidth(4, 92);
            SetColumnWidth(5, 82);
            SetColumnWidth(6, 96);
            SetColumnWidth(7, 88);
            SetColumnWidth(8, 116);
            SetColumnWidth(9, 70);
        }

        private void SetColumnWidth(int columnIndex, int width)
        {
            if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
            {
                return;
            }

            DataGridViewColumn column = _grid.Columns[columnIndex];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = width;
            column.Width = width;
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
            if (item != null)
            {
                _actualStockNumeric.Value = Clamp(item.Product.CurrentStock, _actualStockNumeric.Minimum, _actualStockNumeric.Maximum);
            }

            RefreshPreview();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            ProductItem item = _productComboBox.SelectedItem as ProductItem;
            if (item == null)
            {
                MessageBox.Show("请先选择商品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Product product = item.Product;
            _lines.Add(new InventoryLineView
            {
                ProductId = product.Id,
                ProductName = product.Name,
                CategoryName = product.CategoryName,
                SystemStock = product.CurrentStock,
                ActualStock = _actualStockNumeric.Value,
                CostPrice = product.AverageCost,
                Reason = _reasonComboBox.Text,
                Remark = _lineRemarkTextBox.Text.Trim()
            });

            _lineRemarkTextBox.Clear();
            RefreshTotals();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "DeleteColumn")
            {
                _lines.RemoveAt(e.RowIndex);
                RefreshTotals();
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            InventoryCheck record = BuildRecord();

            if (HasDifferenceWithoutReason())
            {
                DialogResult result = MessageBox.Show(
                    "存在盘盈或盘亏明细未填写原因，是否继续保存？",
                    "原因未填写",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            string message;
            if (!_inventoryCheckService.TrySave(record, out message))
            {
                MessageBox.Show(message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private InventoryCheck BuildRecord()
        {
            InventoryCheck record = new InventoryCheck
            {
                CheckDate = DateTime.Today,
                Remark = _remarkTextBox.Text
            };

            foreach (InventoryLineView line in _lines)
            {
                record.Items.Add(new InventoryCheckItem
                {
                    ProductId = line.ProductId,
                    ProductNameSnapshot = line.ProductName,
                    CategoryName = line.CategoryName,
                    SystemStock = line.SystemStock,
                    ActualStock = line.ActualStock,
                    DifferenceQuantity = line.DifferenceQuantity,
                    CostPriceSnapshot = line.CostPrice,
                    DifferenceAmount = line.DifferenceAmount,
                    Reason = line.Reason,
                    Remark = line.Remark
                });
            }

            return record;
        }

        private bool HasDifferenceWithoutReason()
        {
            foreach (InventoryLineView line in _lines)
            {
                if (line.DifferenceQuantity != 0 && string.IsNullOrWhiteSpace(line.Reason))
                {
                    return true;
                }
            }

            return false;
        }

        private void LoadProducts()
        {
            long selectedId = 0;
            ProductItem selected = _productComboBox.SelectedItem as ProductItem;
            if (selected != null)
            {
                selectedId = selected.Product.Id;
            }

            _productComboBox.Items.Clear();
            foreach (Product product in _inventoryCheckService.SearchActiveProducts(_productSearchTextBox.Text))
            {
                ProductItem item = new ProductItem(product);
                _productComboBox.Items.Add(item);
                if (product.Id == selectedId)
                {
                    _productComboBox.SelectedItem = item;
                }
            }

            if (_productComboBox.SelectedIndex < 0 && _productComboBox.Items.Count > 0)
            {
                _productComboBox.SelectedIndex = 0;
            }

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            ProductItem item = _productComboBox.SelectedItem as ProductItem;
            if (item == null)
            {
                _systemStockLabel.Text = "系统库存：-";
                _costLabel.Text = "成本：-";
                _differenceLabel.Text = "差异：-";
                _differenceAmountLabel.Text = "金额：-";
                return;
            }

            Product product = item.Product;
            decimal diff = _actualStockNumeric.Value - product.CurrentStock;
            decimal diffAmount = diff * product.AverageCost;
            _systemStockLabel.Text = "系统库存：" + product.CurrentStock.ToString("N3");
            _costLabel.Text = "成本：" + product.AverageCost.ToString("N2");
            _differenceLabel.Text = "差异：" + diff.ToString("N3");
            _differenceAmountLabel.Text = "金额：" + diffAmount.ToString("N2");
        }

        private void RefreshTotals()
        {
            decimal profitQty = 0;
            decimal lossQty = 0;
            decimal profitAmount = 0;
            decimal lossAmount = 0;

            foreach (InventoryLineView line in _lines)
            {
                if (line.DifferenceQuantity > 0)
                {
                    profitQty += line.DifferenceQuantity;
                    profitAmount += line.DifferenceAmount;
                }
                else if (line.DifferenceQuantity < 0)
                {
                    lossQty += Math.Abs(line.DifferenceQuantity);
                    lossAmount += Math.Abs(line.DifferenceAmount);
                }
            }

            _totalLabel.Text = string.Format("盘盈 {0:N3} / {1:N2}    盘亏 {2:N3} / {3:N2}", profitQty, profitAmount, lossQty, lossAmount);
            _bindingSource.ResetBindings(false);
        }

        private void AddNumberColumn(string headerText, string propertyName, int width, string format)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(column);
        }

        private static NumericUpDown CreateNumeric(int left, int top)
        {
            return new NumericUpDown
            {
                Location = new Point(left, top),
                Size = new Size(120, 30),
                Minimum = 0,
                Maximum = 999999,
                DecimalPlaces = 3,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
        }

        private static Button CreateButton(string text, Color color, int width)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, 38),
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

        private sealed class InventoryLineView
        {
            public long ProductId { get; set; }

            public string ProductName { get; set; }

            public string CategoryName { get; set; }

            public decimal SystemStock { get; set; }

            public decimal ActualStock { get; set; }

            public decimal CostPrice { get; set; }

            public string Reason { get; set; }

            public string Remark { get; set; }

            public decimal DifferenceQuantity
            {
                get { return ActualStock - SystemStock; }
            }

            public decimal DifferenceAmount
            {
                get { return DifferenceQuantity * CostPrice; }
            }
        }
    }
}
