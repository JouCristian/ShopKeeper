using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class PurchaseEditForm : Form
    {
        private readonly ProductService _productService;
        private readonly PurchaseService _purchaseService;
        private readonly BindingList<PurchaseLineView> _lines;
        private readonly BindingSource _bindingSource;

        private DateTimePicker _purchaseDatePicker;
        private ComboBox _productComboBox;
        private Label _productInfoLabel;
        private NumericUpDown _quantityNumeric;
        private NumericUpDown _priceNumeric;
        private DateTimePicker _productionDatePicker;
        private DateTimePicker _expiryDatePicker;
        private TextBox _lineRemarkTextBox;
        private TextBox _recordRemarkTextBox;
        private Label _lineTotalLabel;
        private Label _totalAmountLabel;
        private DataGridView _grid;

        public PurchaseEditForm()
        {
            _productService = new ProductService();
            _purchaseService = new PurchaseService();
            _lines = new BindingList<PurchaseLineView>();
            _bindingSource = new BindingSource { DataSource = _lines };

            Text = "新增入库单";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(980, 680);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            BuildUi();
            LoadProducts();
            UpdateLineTotal();
            UpdateTotalAmount();
        }

        private void BuildUi()
        {
            Label titleLabel = new Label
            {
                Text = "新增入库单",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                Location = new Point(24, 20),
                Size = new Size(240, 42)
            };
            Controls.Add(titleLabel);

            Controls.Add(CreateLabel("入库日期", 78, 24, 80));
            _purchaseDatePicker = new DateTimePicker
            {
                Location = new Point(110, 76),
                Size = new Size(140, 30),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Value = DateTime.Today
            };
            Controls.Add(_purchaseDatePicker);

            Controls.Add(CreateLabel("整单备注", 78, 280, 80));
            _recordRemarkTextBox = new TextBox
            {
                Location = new Point(365, 76),
                Size = new Size(560, 30)
            };
            UiComponentHelper.CenterTextBoxContent(_recordRemarkTextBox);
            Controls.Add(_recordRemarkTextBox);

            GroupBox addGroup = new GroupBox
            {
                Text = "添加入库明细",
                Location = new Point(24, 120),
                Size = new Size(920, 180)
            };
            Controls.Add(addGroup);

            addGroup.Controls.Add(CreateLabel("商品", 32, 18, 60));
            _productComboBox = new ComboBox
            {
                Location = new Point(78, 30),
                Size = new Size(240, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _productComboBox.SelectedIndexChanged += ProductComboBox_SelectedIndexChanged;
            addGroup.Controls.Add(_productComboBox);

            _productInfoLabel = new Label
            {
                Location = new Point(335, 31),
                Size = new Size(560, 30),
                ForeColor = Color.FromArgb(73, 80, 87),
                TextAlign = ContentAlignment.MiddleLeft
            };
            addGroup.Controls.Add(_productInfoLabel);

            addGroup.Controls.Add(CreateLabel("数量", 76, 18, 60));
            _quantityNumeric = CreateNumeric(78, 92, 3);
            _quantityNumeric.ValueChanged += delegate { UpdateLineTotal(); };
            addGroup.Controls.Add(_quantityNumeric);

            addGroup.Controls.Add(CreateLabel("进货单价", 76, 235, 86));
            _priceNumeric = CreateNumeric(78, 326, 2);
            _priceNumeric.ValueChanged += delegate { UpdateLineTotal(); };
            addGroup.Controls.Add(_priceNumeric);

            _lineTotalLabel = new Label
            {
                Location = new Point(465, 78),
                Size = new Size(180, 30),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };
            addGroup.Controls.Add(_lineTotalLabel);

            addGroup.Controls.Add(CreateLabel("生产日期", 120, 18, 80));
            _productionDatePicker = CreateOptionalDatePicker(102, 118);
            addGroup.Controls.Add(_productionDatePicker);

            addGroup.Controls.Add(CreateLabel("到期日期", 120, 260, 80));
            _expiryDatePicker = CreateOptionalDatePicker(344, 118);
            addGroup.Controls.Add(_expiryDatePicker);

            addGroup.Controls.Add(CreateLabel("备注", 120, 500, 60));
            _lineRemarkTextBox = new TextBox
            {
                Location = new Point(560, 118),
                Size = new Size(180, 30)
            };
            UiComponentHelper.CenterTextBoxContent(_lineRemarkTextBox);
            addGroup.Controls.Add(_lineRemarkTextBox);

            Button addLineButton = new Button
            {
                Text = "加入明细",
                Location = new Point(770, 112),
                Size = new Size(120, 40),
                BackColor = UiTheme.SuccessGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
            addLineButton.FlatAppearance.BorderSize = 0;
            addLineButton.Click += AddLineButton_Click;
            addGroup.Controls.Add(addLineButton);

            _grid = new DataGridView
            {
                Location = new Point(24, 315),
                Size = new Size(920, 260),
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
            BuildGridColumns();
            ApplyPurchaseGridColumnLayout();
            Controls.Add(_grid);

            _totalAmountLabel = new Label
            {
                Location = new Point(24, 590),
                Size = new Size(380, 40),
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_totalAmountLabel);

            Button saveButton = new Button
            {
                Text = "保存入库单",
                Location = new Point(800, 600),
                Size = new Size(140, 44),
                BackColor = UiTheme.PrimaryBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductName", Width = 150 });
            AddNumberColumn("数量", "Quantity", 90, "N3");
            AddNumberColumn("进货单价", "PurchasePrice", 90, "N2");
            AddNumberColumn("小计", "LineTotal", 90, "N2");
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "生产日期", DataPropertyName = "ProductionDateText", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "到期日期", DataPropertyName = "ExpiryDateText", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 220 });
            DataGridViewButtonColumn deleteColumn = new DataGridViewButtonColumn
            {
                Name = "DeleteColumn",
                HeaderText = "操作",
                Text = "删除",
                Width = 70,
                UseColumnTextForButtonValue = true
            };
            _grid.Columns.Add(deleteColumn);
        }

        private void ApplyPurchaseGridColumnLayout()
        {
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            SetFillColumn(0, 150, 1.7F);
            SetFillColumn(1, 90, 0.9F);
            SetFillColumn(2, 100, 1F);
            SetFillColumn(3, 90, 0.9F);
            SetFillColumn(4, 108, 1.05F);
            SetFillColumn(5, 108, 1.05F);
            SetFillColumn(6, 130, 1.3F);
            SetFixedColumn("DeleteColumn", 78);
        }

        private void SetFillColumn(int columnIndex, int minimumWidth, float fillWeight)
        {
            if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
            {
                return;
            }

            DataGridViewColumn column = _grid.Columns[columnIndex];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.MinimumWidth = minimumWidth;
            column.FillWeight = fillWeight;
        }

        private void SetFixedColumn(string columnName, int width)
        {
            if (!_grid.Columns.Contains(columnName))
            {
                return;
            }

            DataGridViewColumn column = _grid.Columns[columnName];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = width;
            column.Width = width;
        }

        private void LoadProducts()
        {
            _productComboBox.Items.Clear();
            foreach (Product product in _productService.GetActiveProducts())
            {
                _productComboBox.Items.Add(new ProductComboItem(product));
            }

            if (_productComboBox.Items.Count > 0)
            {
                _productComboBox.SelectedIndex = 0;
            }
        }

        private void ProductComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProductComboItem item = _productComboBox.SelectedItem as ProductComboItem;
            if (item == null)
            {
                _productInfoLabel.Text = string.Empty;
                return;
            }

            Product product = item.Product;
            _productInfoLabel.Text = string.Format(
                "当前库存：{0:N3}    库存均价：{1:N2}    保质期：{2}",
                product.CurrentStock,
                product.AverageCost,
                product.RequiresExpiry ? "启用" : "不启用");

            _expiryDatePicker.Enabled = product.RequiresExpiry;
            if (!product.RequiresExpiry)
            {
                _expiryDatePicker.Checked = false;
            }
        }

        private void AddLineButton_Click(object sender, EventArgs e)
        {
            ProductComboItem item = _productComboBox.SelectedItem as ProductComboItem;
            if (item == null)
            {
                MessageBox.Show("请选择商品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_quantityNumeric.Value <= 0)
            {
                MessageBox.Show("入库数量必须大于 0。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lines.Add(new PurchaseLineView
            {
                ProductId = item.Product.Id,
                ProductName = item.Product.Name,
                RequiresExpiry = item.Product.RequiresExpiry,
                Quantity = _quantityNumeric.Value,
                PurchasePrice = _priceNumeric.Value,
                ProductionDate = _productionDatePicker.Checked ? (DateTime?)_productionDatePicker.Value.Date : null,
                ExpiryDate = _expiryDatePicker.Enabled && _expiryDatePicker.Checked ? (DateTime?)_expiryDatePicker.Value.Date : null,
                Remark = _lineRemarkTextBox.Text.Trim()
            });

            _quantityNumeric.Value = 0;
            _priceNumeric.Value = 0;
            _productionDatePicker.Checked = false;
            if (_expiryDatePicker.Enabled)
            {
                _expiryDatePicker.Checked = false;
            }
            _lineRemarkTextBox.Clear();
            UpdateTotalAmount();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            PurchaseRecord record = new PurchaseRecord
            {
                PurchaseDate = _purchaseDatePicker.Value.Date,
                Remark = _recordRemarkTextBox.Text
            };

            foreach (PurchaseLineView line in _lines)
            {
                record.Items.Add(new PurchaseItem
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    PurchasePrice = line.PurchasePrice,
                    ProductionDate = line.ProductionDate,
                    ExpiryDate = line.ExpiryDate,
                    Remark = line.Remark
                });
            }

            if (_purchaseService.HasExpiryWarning(record))
            {
                DialogResult result = MessageBox.Show(
                    "有启用保质期的商品未填写到期日期，仍然保存吗？",
                    "保质期提醒",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            string message;
            if (!_purchaseService.TrySave(record, out message))
            {
                MessageBox.Show(message, "校验提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "DeleteColumn")
            {
                return;
            }

            _lines.RemoveAt(e.RowIndex);
            UpdateTotalAmount();
        }

        private void UpdateLineTotal()
        {
            _lineTotalLabel.Text = "小计：" + (_quantityNumeric.Value * _priceNumeric.Value).ToString("N2");
        }

        private void UpdateTotalAmount()
        {
            decimal total = 0;
            foreach (PurchaseLineView line in _lines)
            {
                total += line.LineTotal;
            }

            _totalAmountLabel.Text = "入库总金额：" + total.ToString("N2");
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

        private static Label CreateLabel(string text, int top, int left, int width)
        {
            return new Label
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static NumericUpDown CreateNumeric(int top, int left, int decimalPlaces)
        {
            return new NumericUpDown
            {
                Location = new Point(left, top),
                Size = new Size(110, 30),
                DecimalPlaces = decimalPlaces,
                Minimum = 0,
                Maximum = 9999999,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
        }

        private static DateTimePicker CreateOptionalDatePicker(int left, int top)
        {
            return new DateTimePicker
            {
                Location = new Point(left, top),
                Size = new Size(140, 30),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Checked = false
            };
        }

        private sealed class ProductComboItem
        {
            public ProductComboItem(Product product)
            {
                Product = product;
            }

            public Product Product { get; private set; }

            public override string ToString()
            {
                return Product.Name;
            }
        }

        private sealed class PurchaseLineView
        {
            public long ProductId { get; set; }

            public string ProductName { get; set; }

            public bool RequiresExpiry { get; set; }

            public decimal Quantity { get; set; }

            public decimal PurchasePrice { get; set; }

            public decimal LineTotal
            {
                get { return Quantity * PurchasePrice; }
            }

            public DateTime? ProductionDate { get; set; }

            public DateTime? ExpiryDate { get; set; }

            public string Remark { get; set; }

            public string ProductionDateText
            {
                get { return ProductionDate.HasValue ? ProductionDate.Value.ToString("yyyy-MM-dd") : string.Empty; }
            }

            public string ExpiryDateText
            {
                get { return ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : string.Empty; }
            }
        }
    }
}
