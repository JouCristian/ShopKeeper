using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class SalesDetailForm : Form
    {
        private readonly SalesService _salesService;
        private readonly SalesOrder _order;
        private BindingList<SalesItem> _items;
        private Label _infoLabel;
        private DataGridView _grid;
        private Button _editButton;
        private Button _saveButton;
        private Button _closeButton;
        private bool _editMode;

        public SalesDetailForm(long salesOrderId)
        {
            _salesService = new SalesService();

            Text = "销售单详情";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(880, 540);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            _order = _salesService.GetById(salesOrderId);
            BuildUi();
        }

        private void BuildUi()
        {
            if (_order == null)
            {
                Controls.Add(new Label
                {
                    Text = "未找到销售单。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                return;
            }

            Label titleLabel = new Label
            {
                Text = "销售单详情",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(240, 40)
            };
            Controls.Add(titleLabel);

            _infoLabel = new Label
            {
                Location = new Point(28, 66),
                Size = new Size(810, 92),
                ForeColor = Color.FromArgb(73, 80, 87)
            };
            Controls.Add(_infoLabel);

            _items = new BindingList<SalesItem>();
            foreach (SalesItem item in _order.Items)
            {
                _items.Add(CloneItem(item));
            }

            _grid = new DataGridView
            {
                Location = new Point(28, 170),
                Size = new Size(810, 310),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                DataSource = _items
            };
            GridStyleHelper.ApplyStandardStyle(_grid);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.CellValueChanged += delegate { RecalculateGridRows(); };
            _grid.CellEndEdit += delegate { RecalculateGridRows(); };
            _grid.SizeChanged += delegate { ApplyDetailGridColumns(); };
            _grid.DataBindingComplete += delegate { ApplyDetailGridColumns(); };
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                MessageBox.Show("请输入正确的数字。", "校验提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.ThrowException = false;
            };

            BuildColumns();
            ApplyDetailGridColumns();
            Controls.Add(_grid);

            _editButton = new Button
            {
                Text = "修改编辑",
                Location = new Point(586, 492),
                Size = new Size(120, 38),
                BackColor = Color.FromArgb(255, 235, 238),
                ForeColor = Color.FromArgb(211, 47, 47),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            _editButton.FlatAppearance.BorderColor = Color.FromArgb(255, 174, 185);
            _editButton.Click += delegate { SetEditMode(true); };
            Controls.Add(_editButton);

            _saveButton = new Button
            {
                Text = "保存修改",
                Location = new Point(718, 492),
                Size = new Size(120, 38),
                BackColor = UiTheme.PrimaryBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Visible = false,
                UseVisualStyleBackColor = false
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            _closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(718, 492),
                Size = new Size(120, 38),
                DialogResult = DialogResult.OK
            };
            Controls.Add(_closeButton);
            AcceptButton = _closeButton;

            RecalculateGridRows();
            SetEditMode(false);
        }

        private void BuildColumns()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductColumn",
                HeaderText = "商品",
                DataPropertyName = "ProductNameSnapshot",
                Width = 180
            });
            AddNumberColumn("数量", "Quantity", 90, "N0", false);
            AddNumberColumn("售价", "SalePriceSnapshot", 90, "N2", false);
            AddNumberColumn("成本价", "CostPriceSnapshot", 88, "N2", true);
            AddNumberColumn("金额", "LineAmount", 88, "N2", true);
            AddNumberColumn("成本", "LineCost", 88, "N2", true);
            AddNumberColumn("毛利", "LineProfit", 88, "N2", true);
        }

        private void ApplyDetailGridColumns()
        {
            if (_grid == null || _grid.Columns.Count == 0)
            {
                return;
            }

            ApplyDetailFillColumn("ProductColumn", 170, 150);
            ApplyDetailFillColumn("Quantity", 70, 62);
            ApplyDetailFillColumn("SalePriceSnapshot", 76, 70);
            ApplyDetailFillColumn("CostPriceSnapshot", 76, 70);
            ApplyDetailFillColumn("LineAmount", 76, 70);
            ApplyDetailFillColumn("LineCost", 76, 70);
            ApplyDetailFillColumn("LineProfit", 76, 70);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void ApplyDetailFillColumn(string columnName, int minimumWidth, float fillWeight)
        {
            if (!_grid.Columns.Contains(columnName))
            {
                return;
            }

            DataGridViewColumn column = _grid.Columns[columnName];
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.MinimumWidth = minimumWidth;
            column.FillWeight = fillWeight;
            column.Resizable = DataGridViewTriState.False;
        }

        private void AddNumberColumn(string headerText, string propertyName, int width, string format, bool readOnly)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                Name = propertyName,
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width,
                ReadOnly = readOnly
            };
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(column);
        }

        private void SetEditMode(bool editMode)
        {
            _editMode = editMode;
            _grid.ReadOnly = !editMode;
            _grid.EditMode = editMode ? DataGridViewEditMode.EditOnKeystrokeOrF2 : DataGridViewEditMode.EditProgrammatically;
            _grid.SelectionMode = editMode ? DataGridViewSelectionMode.CellSelect : DataGridViewSelectionMode.FullRowSelect;
            _editButton.Visible = !editMode;
            _saveButton.Visible = editMode;
            _closeButton.Location = editMode ? new Point(586, 492) : new Point(718, 492);
            AcceptButton = editMode ? _saveButton : _closeButton;

            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.ReadOnly = !editMode;
            }

            _grid.Columns["CostPriceSnapshot"].ReadOnly = true;
            _grid.Columns["LineAmount"].ReadOnly = true;
            _grid.Columns["LineCost"].ReadOnly = true;
            _grid.Columns["LineProfit"].ReadOnly = true;

            if (editMode && _grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells["ProductColumn"];
                _grid.BeginEdit(true);
            }

            RefreshInfo();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            _grid.EndEdit();
            RecalculateGridRows();

            for (int i = 0; i < _items.Count; i++)
            {
                SalesItem item = _items[i];
                Product product = _salesService.FindActiveProductByName(item.ProductNameSnapshot);
                if (product == null)
                {
                    MessageBox.Show("第 " + (i + 1) + " 行商品“" + item.ProductNameSnapshot + "”不在库存商品中，不能保存修改。", "校验提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                item.ProductId = product.Id;
                item.ProductNameSnapshot = product.Name;
                item.CostPriceSnapshot = product.AverageCost;
                RecalculateItem(item);
            }

            _order.Items.Clear();
            foreach (SalesItem item in _items)
            {
                _order.Items.Add(item);
            }

            _order.PaidAmount = CalculateCurrentTotalAmount();
            _order.PaidAmountSpecified = true;

            string message;
            if (!_salesService.TryUpdate(_order, out message))
            {
                MessageBox.Show(message, "校验提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private decimal CalculateCurrentTotalAmount()
        {
            decimal totalAmount = 0;
            foreach (SalesItem item in _items)
            {
                totalAmount += item.LineAmount;
            }

            return totalAmount;
        }

        private void RecalculateGridRows()
        {
            foreach (SalesItem item in _items)
            {
                RecalculateItem(item);
            }

            _items.ResetBindings();
            RefreshInfo();
        }

        private static void RecalculateItem(SalesItem item)
        {
            if (item == null)
            {
                return;
            }

            item.ProductNameSnapshot = (item.ProductNameSnapshot ?? string.Empty).Trim();
            item.LineAmount = item.Quantity * item.SalePriceSnapshot;
            item.LineCost = item.Quantity * item.CostPriceSnapshot;
            item.LineProfit = item.LineAmount - item.LineCost;
        }

        private void RefreshInfo()
        {
            decimal totalAmount = 0;
            decimal totalCost = 0;
            foreach (SalesItem item in _items)
            {
                totalAmount += item.LineAmount;
                totalCost += item.LineCost;
            }

            decimal grossProfit = totalAmount - totalCost;
            _infoLabel.Text = string.Format(
                "单号：{0}    时间：{1:yyyy-MM-dd HH:mm:ss}\r\n应收：{2:N2}    成本：{3:N2}    毛利润：{4:N2}    实收：{5:N2}\r\n备注：{6}",
                _order.OrderNo,
                _order.SaleTime,
                totalAmount,
                totalCost,
                grossProfit,
                _editMode ? totalAmount : _order.PaidAmount,
                _order.Remark);
        }

        private static SalesItem CloneItem(SalesItem source)
        {
            return new SalesItem
            {
                Id = source.Id,
                SalesOrderId = source.SalesOrderId,
                ProductId = source.ProductId,
                ProductNameSnapshot = source.ProductNameSnapshot,
                Quantity = source.Quantity,
                SalePriceSnapshot = source.SalePriceSnapshot,
                CostPriceSnapshot = source.CostPriceSnapshot,
                LineAmount = source.LineAmount,
                LineCost = source.LineCost,
                LineProfit = source.LineProfit,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt
            };
        }
    }
}
