using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class ProductManagementPage : UserControl, IResponsivePage
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly Panel _contentPanel;
        private readonly FlowLayoutPanel _filters;
        private readonly TextBox _searchTextBox;
        private readonly ComboBox _categoryComboBox;
        private readonly ComboBox _statusComboBox;
        private readonly DataGridView _grid;
        private readonly BindingSource _bindingSource;
        private readonly Label _emptyLabel;

        public ProductManagementPage()
        {
            _productService = new ProductService();
            _categoryService = new CategoryService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Panel headerPanel = UiComponentHelper.CreatePageHeader(
                "商品管理",
                "维护商品档案、分类、库存预警和保质期信息",
                "headers/product");

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = BackColor
            };

            _filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 116,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
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

            Button searchButton = CreateButton("查询", UiTheme.PrimaryBlue);
            searchButton.Click += delegate { LoadProducts(); };

            Button addButton = CreateButton("新增商品", UiTheme.SuccessGreen);
            addButton.Click += AddButton_Click;

            Button categoryButton = CreateButton("分类管理", UiTheme.MutedGray);
            categoryButton.Click += CategoryButton_Click;

            _filters.Controls.Add(CreateFilterLabel("名称/条码"));
            _filters.Controls.Add(_searchTextBox);
            _filters.Controls.Add(CreateFilterLabel("分类"));
            _filters.Controls.Add(_categoryComboBox);
            _filters.Controls.Add(CreateFilterLabel("状态"));
            _filters.Controls.Add(_statusComboBox);
            _filters.Controls.Add(searchButton);
            _filters.Controls.Add(addButton);
            _filters.Controls.Add(categoryButton);
            UiComponentHelper.NormalizeFilterBar(_filters);

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
            GridStyleHelper.ApplyStandardStyle(_grid);
            _emptyLabel = UiComponentHelper.CreateEmptyStateLabel("暂无商品，请先新增商品。", "empty/product");

            BuildColumns();

            _contentPanel.Controls.Add(_grid);
            _contentPanel.Controls.Add(_emptyLabel);
            _contentPanel.Controls.Add(_filters);
            Controls.Add(_contentPanel);
            Controls.Add(headerPanel);

            LoadCategories();
            LoadProducts();
        }

        public void ApplyLayout(UiLayoutMode mode)
        {
            bool compact = ResponsiveLayoutManager.IsCompact(mode);
            bool veryCompact = ResponsiveLayoutManager.IsVeryCompact(mode);
            _contentPanel.Padding = veryCompact ? new Padding(10) : (compact ? new Padding(12) : new Padding(24));
            _filters.Height = veryCompact ? 106 : (compact ? 92 : 116);
            _filters.Padding = compact ? new Padding(0, 2, 0, 0) : Padding.Empty;
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

            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteColumn",
                HeaderText = "删除",
                Text = "删除",
                Width = 70,
                UseColumnTextForButtonValue = true
            });

            ApplyProductGridColumnLayout();
        }

        private void ApplyProductGridColumnLayout()
        {
            SetDataColumnLayout("Name", 1.35F, 150);
            SetDataColumnLayout("CategoryName", 0.8F, 86);
            SetDataColumnLayout("Barcode", 0.9F, 96);
            SetDataColumnLayout("Specification", 0.85F, 92);
            SetDataColumnLayout("DefaultPrice", 0.9F, 100);
            SetDataColumnLayout("CurrentStock", 1.05F, 118);
            SetDataColumnLayout("AverageCost", 1.05F, 118);
            SetDataColumnLayout("MinStockAlert", 1.05F, 118);
            SetDataColumnLayout("RequiresExpiryText", 0.78F, 88);
            SetDataColumnLayout("ExpiryDateText", 0.95F, 108);
            SetDataColumnLayout("Status", 0.78F, 88);
            SetButtonColumnLayout("EditColumn", 70);
            SetButtonColumnLayout("StatusColumn", 70);
            SetButtonColumnLayout("DeleteColumn", 70);
        }

        private void SetDataColumnLayout(string propertyName, float fillWeight, int minimumWidth)
        {
            DataGridViewColumn column = FindColumnByProperty(propertyName);
            if (column == null)
            {
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
        }

        private void SetButtonColumnLayout(string columnName, int width)
        {
            if (!_grid.Columns.Contains(columnName))
            {
                return;
            }

            DataGridViewColumn column = _grid.Columns[columnName];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
        }

        private DataGridViewColumn FindColumnByProperty(string propertyName)
        {
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                if (column.DataPropertyName == propertyName)
                {
                    return column;
                }
            }

            return null;
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
            _emptyLabel.Visible = products.Count == 0;
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
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
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
            else if (columnName == "DeleteColumn")
            {
                DeleteProduct(e.RowIndex);
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

        private void DeleteProduct(int rowIndex)
        {
            Product product = _grid.Rows[rowIndex].DataBoundItem as Product;
            if (product == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                "删除后该商品不会再出现在商品列表和选择下拉框中，历史记录仍会保留。\r\n\r\n是否继续删除「" + product.Name + "」？",
                "删除商品",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            string message;
            if (!_productService.TryDelete(product.Id, out message))
            {
                MessageBox.Show(message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Width = text == "名称/条码" ? 96 : 72,
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
                Font = UiTheme.Font(10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
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
