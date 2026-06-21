using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class InventoryCheckPage : UserControl
    {
        private readonly InventoryCheckService _inventoryCheckService;
        private readonly ScrapService _scrapService;
        private readonly CategoryService _categoryService;
        private readonly BindingSource _checkBindingSource;
        private readonly BindingSource _scrapBindingSource;
        private TextBox _keywordTextBox;
        private ComboBox _categoryComboBox;
        private DataGridView _checkGrid;
        private TextBox _scrapSearchTextBox;
        private ComboBox _scrapProductComboBox;
        private NumericUpDown _scrapQuantityNumeric;
        private ComboBox _scrapReasonComboBox;
        private TextBox _scrapRemarkTextBox;
        private Label _scrapStockLabel;
        private Label _scrapCostLabel;
        private Label _scrapLossLabel;
        private DataGridView _scrapGrid;

        public InventoryCheckPage()
        {
            _inventoryCheckService = new InventoryCheckService();
            _scrapService = new ScrapService();
            _categoryService = new CategoryService();
            _checkBindingSource = new BindingSource();
            _scrapBindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "库存盘点",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            TabPage checkTab = new TabPage("盘点单列表");
            TabPage scrapTab = new TabPage("报废登记");
            checkTab.BackColor = BackColor;
            scrapTab.BackColor = BackColor;
            tabs.TabPages.Add(checkTab);
            tabs.TabPages.Add(scrapTab);

            BuildCheckTab(checkTab);
            BuildScrapTab(scrapTab);

            Controls.Add(tabs);
            Controls.Add(titleLabel);

            LoadCategories();
            LoadChecks();
            LoadScrapProducts();
            LoadScrapRecords();
        }

        private void BuildCheckTab(TabPage tab)
        {
            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = BackColor };
            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };

            Button addButton = CreateButton("新建盘点单", Color.FromArgb(40, 167, 69), 120);
            addButton.Click += AddButton_Click;
            _keywordTextBox = new TextBox { Width = 180, Font = new Font("Microsoft YaHei UI", 12F), Margin = new Padding(0, 20, 12, 0) };
            _keywordTextBox.KeyDown += KeywordTextBox_KeyDown;
            _categoryComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F),
                Margin = new Padding(0, 20, 12, 0)
            };
            Button refreshButton = CreateButton("刷新", Color.FromArgb(0, 123, 255), 90);
            refreshButton.Click += delegate { LoadChecks(); };

            filters.Controls.Add(addButton);
            filters.Controls.Add(CreateFilterLabel("商品名称", 80));
            filters.Controls.Add(_keywordTextBox);
            filters.Controls.Add(CreateFilterLabel("分类", 52));
            filters.Controls.Add(_categoryComboBox);
            filters.Controls.Add(refreshButton);

            _checkGrid = CreateGrid();
            _checkGrid.DataSource = _checkBindingSource;
            _checkGrid.CellContentClick += CheckGrid_CellContentClick;
            BuildCheckColumns();

            content.Controls.Add(_checkGrid);
            content.Controls.Add(filters);
            tab.Controls.Add(content);
        }

        private void BuildScrapTab(TabPage tab)
        {
            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = BackColor };
            Panel input = new Panel { Dock = DockStyle.Top, Height = 132, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };

            _scrapSearchTextBox = new TextBox { Location = new Point(84, 18), Size = new Size(180, 30), Font = new Font("Microsoft YaHei UI", 11F) };
            _scrapSearchTextBox.KeyDown += ScrapSearchTextBox_KeyDown;
            Button searchButton = CreateButton("查找", Color.FromArgb(0, 123, 255), 80);
            searchButton.Location = new Point(274, 15);
            searchButton.Margin = Padding.Empty;
            searchButton.Click += delegate { LoadScrapProducts(); };

            _scrapProductComboBox = new ComboBox
            {
                Location = new Point(430, 18),
                Size = new Size(260, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _scrapProductComboBox.SelectedIndexChanged += ScrapProductComboBox_SelectedIndexChanged;

            _scrapQuantityNumeric = new NumericUpDown
            {
                Location = new Point(84, 76),
                Size = new Size(120, 30),
                Minimum = 0,
                Maximum = 999999,
                DecimalPlaces = 3,
                Increment = 1,
                TextAlign = HorizontalAlignment.Right,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _scrapQuantityNumeric.ValueChanged += delegate { RefreshScrapPreview(); };

            _scrapReasonComboBox = new ComboBox
            {
                Location = new Point(274, 76),
                Size = new Size(120, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            _scrapReasonComboBox.Items.AddRange(new object[] { "过期", "破损", "丢失", "自用", "赠送", "其他" });
            _scrapReasonComboBox.SelectedIndex = 0;

            _scrapRemarkTextBox = new TextBox { Location = new Point(454, 76), Size = new Size(180, 30), Font = new Font("Microsoft YaHei UI", 11F) };
            Button saveButton = CreateButton("保存报废", Color.FromArgb(220, 53, 69), 110);
            saveButton.Location = new Point(654, 72);
            saveButton.Margin = Padding.Empty;
            saveButton.Click += SaveScrapButton_Click;

            _scrapStockLabel = CreateInfoLabel(710, 18, 120);
            _scrapCostLabel = CreateInfoLabel(832, 18, 120);
            _scrapLossLabel = CreateInfoLabel(780, 76, 170);

            input.Controls.Add(CreateLabel("商品搜索", 14, 18, 70));
            input.Controls.Add(_scrapSearchTextBox);
            input.Controls.Add(searchButton);
            input.Controls.Add(CreateLabel("选择商品", 360, 18, 70));
            input.Controls.Add(_scrapProductComboBox);
            input.Controls.Add(CreateLabel("报废数量", 14, 76, 70));
            input.Controls.Add(_scrapQuantityNumeric);
            input.Controls.Add(CreateLabel("原因", 226, 76, 42));
            input.Controls.Add(_scrapReasonComboBox);
            input.Controls.Add(CreateLabel("备注", 410, 76, 42));
            input.Controls.Add(_scrapRemarkTextBox);
            input.Controls.Add(saveButton);
            input.Controls.Add(_scrapStockLabel);
            input.Controls.Add(_scrapCostLabel);
            input.Controls.Add(_scrapLossLabel);

            _scrapGrid = CreateGrid();
            _scrapGrid.DataSource = _scrapBindingSource;
            BuildScrapColumns();

            content.Controls.Add(_scrapGrid);
            content.Controls.Add(input);
            tab.Controls.Add(content);
        }

        private void BuildCheckColumns()
        {
            _checkGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "盘点单号", DataPropertyName = "CheckNo", Width = 170 });
            _checkGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "盘点日期", DataPropertyName = "CheckDate", Width = 110, DefaultCellStyle = { Format = "yyyy-MM-dd" } });
            _checkGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品种类数", DataPropertyName = "ProductKindCount", Width = 90 });
            AddNumberColumn(_checkGrid, "盘盈数量", "TotalProfitQuantity", 100, "N3");
            AddNumberColumn(_checkGrid, "盘亏数量", "TotalLossQuantity", 100, "N3");
            AddNumberColumn(_checkGrid, "盘盈金额", "TotalProfitAmount", 100, "N2");
            AddNumberColumn(_checkGrid, "盘亏金额", "TotalLossAmount", 100, "N2");
            _checkGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 180 });
            _checkGrid.Columns.Add(new DataGridViewButtonColumn { Name = "DetailColumn", HeaderText = "操作", Text = "查看", Width = 80, UseColumnTextForButtonValue = true });
        }

        private void BuildScrapColumns()
        {
            _scrapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "报废单号", DataPropertyName = "ScrapNo", Width = 170 });
            _scrapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "报废日期", DataPropertyName = "ScrapDate", Width = 110, DefaultCellStyle = { Format = "yyyy-MM-dd" } });
            _scrapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品名称", DataPropertyName = "ProductNameSnapshot", Width = 150 });
            AddNumberColumn(_scrapGrid, "数量", "Quantity", 90, "N3");
            AddNumberColumn(_scrapGrid, "成本价", "CostPriceSnapshot", 90, "N2");
            AddNumberColumn(_scrapGrid, "损失金额", "LossAmount", 100, "N2");
            _scrapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "原因", DataPropertyName = "Reason", Width = 90 });
            _scrapGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 180 });
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (InventoryCheckEditForm form = new InventoryCheckEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadChecks();
                    LoadScrapProducts();
                }
            }
        }

        private void CheckGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _checkGrid.Columns[e.ColumnIndex].Name != "DetailColumn")
            {
                return;
            }

            InventoryCheck record = _checkGrid.Rows[e.RowIndex].DataBoundItem as InventoryCheck;
            if (record == null)
            {
                return;
            }

            using (InventoryCheckDetailForm form = new InventoryCheckDetailForm(record.Id))
            {
                form.ShowDialog(this);
            }
        }

        private void SaveScrapButton_Click(object sender, EventArgs e)
        {
            ProductItem item = _scrapProductComboBox.SelectedItem as ProductItem;
            ScrapRecord record = new ScrapRecord
            {
                ScrapDate = DateTime.Today,
                ProductId = item == null ? 0 : item.Product.Id,
                Quantity = _scrapQuantityNumeric.Value,
                Reason = _scrapReasonComboBox.Text,
                Remark = _scrapRemarkTextBox.Text
            };

            string message;
            if (!_scrapService.TrySave(record, out message))
            {
                MessageBox.Show(message, "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _scrapQuantityNumeric.Value = 0;
            _scrapRemarkTextBox.Clear();
            LoadScrapProducts();
            LoadScrapRecords();
        }

        private void KeywordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadChecks();
                e.Handled = true;
            }
        }

        private void ScrapSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadScrapProducts();
                e.Handled = true;
            }
        }

        private void ScrapProductComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshScrapPreview();
        }

        private void LoadCategories()
        {
            _categoryComboBox.Items.Clear();
            _categoryComboBox.Items.Add(new ComboBoxItem("全部", null));
            foreach (Category category in _categoryService.GetAllCategories())
            {
                _categoryComboBox.Items.Add(new ComboBoxItem(category.IsActive ? category.Name : category.Name + "（停用）", category.Id));
            }

            _categoryComboBox.SelectedIndex = 0;
        }

        private void LoadChecks()
        {
            ComboBoxItem category = _categoryComboBox.SelectedItem as ComboBoxItem;
            _checkBindingSource.DataSource = _inventoryCheckService.Search(_keywordTextBox.Text, category == null ? null : category.Id);
        }

        private void LoadScrapProducts()
        {
            long selectedId = 0;
            ProductItem selected = _scrapProductComboBox.SelectedItem as ProductItem;
            if (selected != null)
            {
                selectedId = selected.Product.Id;
            }

            _scrapProductComboBox.Items.Clear();
            foreach (Product product in _scrapService.SearchActiveProducts(_scrapSearchTextBox.Text))
            {
                ProductItem item = new ProductItem(product);
                _scrapProductComboBox.Items.Add(item);
                if (product.Id == selectedId)
                {
                    _scrapProductComboBox.SelectedItem = item;
                }
            }

            if (_scrapProductComboBox.SelectedIndex < 0 && _scrapProductComboBox.Items.Count > 0)
            {
                _scrapProductComboBox.SelectedIndex = 0;
            }

            RefreshScrapPreview();
        }

        private void LoadScrapRecords()
        {
            _scrapBindingSource.DataSource = _scrapService.Search();
        }

        private void RefreshScrapPreview()
        {
            ProductItem item = _scrapProductComboBox.SelectedItem as ProductItem;
            if (item == null)
            {
                _scrapStockLabel.Text = "库存：-";
                _scrapCostLabel.Text = "成本：-";
                _scrapLossLabel.Text = "损失：-";
                return;
            }

            Product product = item.Product;
            _scrapStockLabel.Text = "库存：" + product.CurrentStock.ToString("N3");
            _scrapCostLabel.Text = "成本：" + product.AverageCost.ToString("N2");
            _scrapLossLabel.Text = "损失：" + (_scrapQuantityNumeric.Value * product.AverageCost).ToString("N2");
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

        private static Label CreateFilterLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                Width = width,
                Height = 32,
                Margin = new Padding(0, 22, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Button CreateButton(string text, Color color, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                Margin = new Padding(0, 18, 12, 0),
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
    }
}
