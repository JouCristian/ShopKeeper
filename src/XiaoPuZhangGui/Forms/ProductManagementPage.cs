using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class ProductManagementPage : UserControl
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly TextBox _searchTextBox;
        private readonly ComboBox _categoryComboBox;
        private readonly ComboBox _statusComboBox;
        private readonly DataGridView _grid;
        private readonly BindingSource _bindingSource;

        public ProductManagementPage()
        {
            _productService = new ProductService();
            _categoryService = new CategoryService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "商品管理",
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

            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 72,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };

            _searchTextBox = new TextBox
            {
                Width = 220,
                Font = new Font("Microsoft YaHei UI", 12F),
                Margin = new Padding(0, 22, 12, 0)
            };
            _searchTextBox.KeyDown += SearchTextBox_KeyDown;

            _categoryComboBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F),
                Margin = new Padding(0, 22, 12, 0)
            };

            _statusComboBox = new ComboBox
            {
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F),
                Margin = new Padding(0, 22, 12, 0)
            };
            _statusComboBox.Items.Add("全部");
            _statusComboBox.Items.Add("在售");
            _statusComboBox.Items.Add("停用");
            _statusComboBox.SelectedIndex = 0;

            Button searchButton = CreateButton("查询", Color.FromArgb(0, 123, 255));
            searchButton.Click += delegate { LoadProducts(); };

            Button addButton = CreateButton("新增商品", Color.FromArgb(40, 167, 69));
            addButton.Click += AddButton_Click;

            Button categoryButton = CreateButton("分类管理", Color.FromArgb(108, 117, 125));
            categoryButton.Click += CategoryButton_Click;

            filters.Controls.Add(CreateFilterLabel("名称/条码"));
            filters.Controls.Add(_searchTextBox);
            filters.Controls.Add(CreateFilterLabel("分类"));
            filters.Controls.Add(_categoryComboBox);
            filters.Controls.Add(CreateFilterLabel("状态"));
            filters.Controls.Add(_statusComboBox);
            filters.Controls.Add(searchButton);
            filters.Controls.Add(addButton);
            filters.Controls.Add(categoryButton);

            _grid = new DataGridView
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
                DataSource = _bindingSource
            };
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F);
            _grid.RowTemplate.Height = 34;

            BuildColumns();

            contentPanel.Controls.Add(_grid);
            contentPanel.Controls.Add(filters);
            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            LoadCategories();
            LoadProducts();
        }

        private void BuildColumns()
        {
            AddTextColumn("商品名称", "Name", 150);
            AddTextColumn("分类", "CategoryName", 90);
            AddTextColumn("条码", "Barcode", 110);
            AddTextColumn("规格", "Specification", 100);
            AddMoneyColumn("默认售价", "DefaultPrice", 90);
            AddMoneyColumn("当前库存", "CurrentStock", 90);
            AddMoneyColumn("库存均价", "AverageCost", 90);
            AddMoneyColumn("最低库存", "MinStockAlert", 90);
            AddTextColumn("保质期", "RequiresExpiryText", 70);
            AddTextColumn("到期日期", "ExpiryDateText", 100);
            AddTextColumn("状态", "Status", 70);

            DataGridViewButtonColumn editColumn = new DataGridViewButtonColumn
            {
                Name = "EditColumn",
                HeaderText = "编辑",
                Text = "编辑",
                Width = 70,
                UseColumnTextForButtonValue = true
            };
            _grid.Columns.Add(editColumn);

            DataGridViewButtonColumn statusColumn = new DataGridViewButtonColumn
            {
                Name = "StatusColumn",
                HeaderText = "操作",
                DataPropertyName = "StatusActionText",
                Width = 70
            };
            _grid.Columns.Add(statusColumn);
        }

        private void LoadCategories()
        {
            _categoryComboBox.Items.Clear();
            _categoryComboBox.Items.Add(new ComboBoxItem("全部", null, false));

            foreach (Category category in _categoryService.GetAllCategories())
            {
                _categoryComboBox.Items.Add(new ComboBoxItem(category.Name, category.Id, !category.IsActive));
            }

            _categoryComboBox.SelectedIndex = 0;
        }

        private void LoadProducts()
        {
            ComboBoxItem categoryItem = _categoryComboBox.SelectedItem as ComboBoxItem;
            long? categoryId = categoryItem == null ? null : categoryItem.Id;
            string status = _statusComboBox.SelectedItem == null ? "全部" : _statusComboBox.SelectedItem.ToString();
            IList<Product> products = _productService.Search(_searchTextBox.Text, categoryId, status);
            _bindingSource.DataSource = products;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (ProductEditForm form = new ProductEditForm(null))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadCategories();
                    LoadProducts();
                }
            }
        }

        private void CategoryButton_Click(object sender, EventArgs e)
        {
            using (CategoryManagementForm form = new CategoryManagementForm())
            {
                form.ShowDialog(this);
            }

            LoadCategories();
            LoadProducts();
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedProduct(e.RowIndex);
            }
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == "EditColumn")
            {
                EditSelectedProduct(e.RowIndex);
            }
            else if (columnName == "StatusColumn")
            {
                ToggleProductStatus(e.RowIndex);
            }
        }

        private void EditSelectedProduct(int rowIndex)
        {
            Product product = _grid.Rows[rowIndex].DataBoundItem as Product;
            if (product == null)
            {
                return;
            }

            using (ProductEditForm form = new ProductEditForm(product.Id))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadProducts();
                }
            }
        }

        private void ToggleProductStatus(int rowIndex)
        {
            Product product = _grid.Rows[rowIndex].DataBoundItem as Product;
            if (product == null)
            {
                return;
            }

            bool enable = product.Status == "停用";
            string action = enable ? "重新启用" : "停用";
            DialogResult result = MessageBox.Show(
                "确定要" + action + "商品“" + product.Name + "”吗？",
                "确认操作",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _productService.SetStatus(product.Id, enable);
            LoadProducts();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadProducts();
                e.Handled = true;
            }
        }

        private void AddTextColumn(string headerText, string propertyName, int width)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            });
        }

        private void AddMoneyColumn(string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            column.DefaultCellStyle.Format = "N2";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(column);
        }

        private static Label CreateFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                Width = 72,
                Height = 32,
                Margin = new Padding(0, 22, 6, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Button CreateButton(string text, Color color)
        {
            Button button = new Button
            {
                Text = text,
                Width = 100,
                Height = 38,
                Margin = new Padding(0, 18, 10, 0),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private sealed class ComboBoxItem
        {
            public ComboBoxItem(string text, long? id, bool inactive)
            {
                Text = inactive ? text + "（停用）" : text;
                Id = id;
            }

            public string Text { get; private set; }

            public long? Id { get; private set; }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
