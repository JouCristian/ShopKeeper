using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class ProductEditForm : Form
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly long? _productId;
        private readonly bool _isNew;

        private TextBox _nameTextBox;
        private ComboBox _categoryComboBox;
        private TextBox _barcodeTextBox;
        private TextBox _specificationTextBox;
        private NumericUpDown _defaultPriceNumeric;
        private NumericUpDown _currentStockNumeric;
        private NumericUpDown _averageCostNumeric;
        private NumericUpDown _minStockNumeric;
        private CheckBox _requiresExpiryCheckBox;
        private DateTimePicker _expiryDatePicker;
        private TextBox _remarkTextBox;
        private bool _loading;

        public ProductEditForm(long? productId)
        {
            _productService = new ProductService();
            _categoryService = new CategoryService();
            _productId = productId;
            _isNew = !productId.HasValue;

            Text = _isNew ? "新增商品" : "编辑商品";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 650);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);
            BackColor = Color.White;

            BuildUi();
            LoadCategories();
            LoadProduct();
        }

        private void BuildUi()
        {
            Label titleLabel = new Label
            {
                Text = Text,
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                Location = new Point(30, 24),
                Size = new Size(300, 44)
            };
            Controls.Add(titleLabel);

            _nameTextBox = AddTextBox("商品名称 *", 88, 220);
            _categoryComboBox = AddComboBox("分类 *", 138, 220);
            _categoryComboBox.SelectedIndexChanged += CategoryComboBox_SelectedIndexChanged;
            _barcodeTextBox = AddTextBox("条码", 188, 220);
            _specificationTextBox = AddTextBox("规格", 238, 220);
            _defaultPriceNumeric = AddNumeric("默认售价 *", 288, 220, 2);
            _currentStockNumeric = AddNumeric("当前库存 *", 338, 220, 3);
            _averageCostNumeric = AddNumeric("库存均价/首次进货价 *", 388, 220, 2);
            _minStockNumeric = AddNumeric("最低库存提醒 *", 438, 220, 3);

            Label expiryLabel = CreateLabel("是否启用保质期 *", 488);
            _requiresExpiryCheckBox = new CheckBox
            {
                Text = "启用",
                Location = new Point(220, 491),
                Size = new Size(90, 28)
            };
            _requiresExpiryCheckBox.CheckedChanged += RequiresExpiryCheckBox_CheckedChanged;
            Controls.Add(expiryLabel);
            Controls.Add(_requiresExpiryCheckBox);

            Label expiryDateLabel = CreateLabel("到期日期", 528);
            _expiryDatePicker = new DateTimePicker
            {
                Location = new Point(220, 528),
                Size = new Size(160, 30),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true
            };
            Controls.Add(expiryDateLabel);
            Controls.Add(_expiryDatePicker);

            _remarkTextBox = AddTextBox("备注", 568, 220);
            _remarkTextBox.Width = 390;

            Label noteLabel = new Label
            {
                Text = "提示：正式使用后，库存变化建议通过入库、销售、盘点模块完成。",
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(32, 600),
                Size = new Size(440, 30)
            };
            Controls.Add(noteLabel);

            Button saveButton = new Button
            {
                Text = "保存",
                Location = new Point(500, 590),
                Size = new Size(120, 42),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            Controls.Add(saveButton);
            AcceptButton = saveButton;
        }

        private void LoadCategories()
        {
            _loading = true;
            _categoryComboBox.Items.Clear();
            IList<Category> categories = _isNew ? _categoryService.GetActiveCategories() : _categoryService.GetAllCategories();
            foreach (Category category in categories)
            {
                _categoryComboBox.Items.Add(new CategoryComboItem(category));
            }

            if (_categoryComboBox.Items.Count > 0)
            {
                _categoryComboBox.SelectedIndex = 0;
            }

            _loading = false;
            ApplyDefaultExpiryByCategory();
        }

        private void LoadProduct()
        {
            if (_isNew)
            {
                return;
            }

            Product product = _productService.GetById(_productId.Value);
            if (product == null)
            {
                MessageBox.Show("未找到商品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            _loading = true;
            _nameTextBox.Text = product.Name;
            SelectCategory(product.CategoryId);
            _barcodeTextBox.Text = product.Barcode;
            _specificationTextBox.Text = product.Specification;
            _defaultPriceNumeric.Value = product.DefaultPrice;
            _currentStockNumeric.Value = product.CurrentStock;
            _averageCostNumeric.Value = product.AverageCost;
            _minStockNumeric.Value = product.MinStockAlert;
            _requiresExpiryCheckBox.Checked = product.RequiresExpiry;
            _expiryDatePicker.Checked = product.ExpiryDate.HasValue;
            if (product.ExpiryDate.HasValue)
            {
                _expiryDatePicker.Value = product.ExpiryDate.Value;
            }
            _remarkTextBox.Text = product.Remark;
            _loading = false;
            UpdateExpiryDateState();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            CategoryComboItem categoryItem = _categoryComboBox.SelectedItem as CategoryComboItem;
            Product product = new Product
            {
                Id = _productId.HasValue ? _productId.Value : 0,
                Name = _nameTextBox.Text,
                CategoryId = categoryItem == null ? 0 : categoryItem.Category.Id,
                Barcode = _barcodeTextBox.Text,
                Specification = _specificationTextBox.Text,
                DefaultPrice = _defaultPriceNumeric.Value,
                CurrentStock = _currentStockNumeric.Value,
                AverageCost = _averageCostNumeric.Value,
                MinStockAlert = _minStockNumeric.Value,
                RequiresExpiry = _requiresExpiryCheckBox.Checked,
                ExpiryDate = _requiresExpiryCheckBox.Checked && _expiryDatePicker.Checked ? (DateTime?)_expiryDatePicker.Value.Date : null,
                Status = "在售",
                Remark = _remarkTextBox.Text
            };

            if (!_isNew)
            {
                Product existing = _productService.GetById(_productId.Value);
                product.Status = existing == null ? "在售" : existing.Status;
            }

            string message;
            if (!_productService.TrySave(product, out message))
            {
                MessageBox.Show(message, "校验提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CategoryComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading)
            {
                return;
            }

            ApplyDefaultExpiryByCategory();
        }

        private void RequiresExpiryCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateExpiryDateState();
        }

        private void ApplyDefaultExpiryByCategory()
        {
            if (!_isNew)
            {
                return;
            }

            CategoryComboItem item = _categoryComboBox.SelectedItem as CategoryComboItem;
            _requiresExpiryCheckBox.Checked = item == null || item.Category.Name != "烟酒";
            UpdateExpiryDateState();
        }

        private void UpdateExpiryDateState()
        {
            _expiryDatePicker.Enabled = _requiresExpiryCheckBox.Checked;
            if (!_requiresExpiryCheckBox.Checked)
            {
                _expiryDatePicker.Checked = false;
            }
        }

        private void SelectCategory(long categoryId)
        {
            for (int i = 0; i < _categoryComboBox.Items.Count; i++)
            {
                CategoryComboItem item = _categoryComboBox.Items[i] as CategoryComboItem;
                if (item != null && item.Category.Id == categoryId)
                {
                    _categoryComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private TextBox AddTextBox(string label, int top, int left)
        {
            Controls.Add(CreateLabel(label, top));
            TextBox textBox = new TextBox
            {
                Location = new Point(left, top),
                Size = new Size(280, 30),
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            Controls.Add(textBox);
            return textBox;
        }

        private ComboBox AddComboBox(string label, int top, int left)
        {
            Controls.Add(CreateLabel(label, top));
            ComboBox comboBox = new ComboBox
            {
                Location = new Point(left, top),
                Size = new Size(280, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            Controls.Add(comboBox);
            return comboBox;
        }

        private NumericUpDown AddNumeric(string label, int top, int left, int decimalPlaces)
        {
            Controls.Add(CreateLabel(label, top));
            NumericUpDown numeric = new NumericUpDown
            {
                Location = new Point(left, top),
                Size = new Size(160, 30),
                DecimalPlaces = decimalPlaces,
                Maximum = 9999999,
                Minimum = 0,
                Font = new Font("Microsoft YaHei UI", 11F)
            };
            Controls.Add(numeric);
            return numeric;
        }

        private static Label CreateLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                Location = new Point(32, top),
                Size = new Size(180, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private sealed class CategoryComboItem
        {
            public CategoryComboItem(Category category)
            {
                Category = category;
            }

            public Category Category { get; private set; }

            public override string ToString()
            {
                return Category.IsActive ? Category.Name : Category.Name + "（停用）";
            }
        }
    }
}
