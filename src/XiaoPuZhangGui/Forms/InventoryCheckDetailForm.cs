using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class InventoryCheckDetailForm : Form
    {
        private readonly InventoryCheckService _inventoryCheckService;

        public InventoryCheckDetailForm(long inventoryCheckId)
        {
            _inventoryCheckService = new InventoryCheckService();

            Text = "盘点单详情";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(1080, 560);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            InventoryCheck record = _inventoryCheckService.GetById(inventoryCheckId);
            BuildUi(record);
        }

        private void BuildUi(InventoryCheck record)
        {
            if (record == null)
            {
                Controls.Add(new Label
                {
                    Text = "未找到盘点单。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                return;
            }

            Controls.Add(new Label
            {
                Text = "盘点单详情",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(240, 40)
            });

            Controls.Add(new Label
            {
                Text = string.Format(
                    "单号：{0}    日期：{1:yyyy-MM-dd}\r\n盘盈数量：{2:N3}    盘亏数量：{3:N3}    盘盈金额：{4:N2}    盘亏金额：{5:N2}\r\n备注：{6}",
                    record.CheckNo,
                    record.CheckDate,
                    record.TotalProfitQuantity,
                    record.TotalLossQuantity,
                    record.TotalProfitAmount,
                    record.TotalLossAmount,
                    record.Remark),
                Location = new Point(28, 66),
                Size = new Size(1010, 92),
                ForeColor = Color.FromArgb(73, 80, 87)
            });

            DataGridView grid = new DataGridView
            {
                Location = new Point(28, 170),
                Size = new Size(1010, 320),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };
            GridStyleHelper.ApplyStandardStyle(grid);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ScrollBars = ScrollBars.Vertical;

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductNameSnapshot", Width = 180, MinimumWidth = 160 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类", DataPropertyName = "CategoryName", Width = 80, MinimumWidth = 80 });
            AddNumberColumn(grid, "系统库存", "SystemStock", 90, "N3");
            AddNumberColumn(grid, "实际库存", "ActualStock", 90, "N3");
            AddNumberColumn(grid, "差异数量", "DifferenceQuantity", 90, "N3");
            AddNumberColumn(grid, "成本价", "CostPriceSnapshot", 90, "N2");
            AddNumberColumn(grid, "差异金额", "DifferenceAmount", 100, "N2");
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "原因", DataPropertyName = "Reason", Width = 100, MinimumWidth = 100 });
            DataGridViewTextBoxColumn remarkColumn = new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", MinimumWidth = 160 };
            remarkColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(remarkColumn);
            ApplyColumnWidths(grid);
            grid.DataSource = record.Items;
            ApplyColumnWidths(grid);
            Controls.Add(grid);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(918, 506),
                Size = new Size(120, 38),
                DialogResult = DialogResult.OK
            };
            Controls.Add(closeButton);
            AcceptButton = closeButton;
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

        private static void ApplyColumnWidths(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count < 9)
            {
                return;
            }

            SetFixedColumn(grid.Columns[0], 180);
            SetFixedColumn(grid.Columns[1], 80);
            SetFixedColumn(grid.Columns[2], 90);
            SetFixedColumn(grid.Columns[3], 90);
            SetFixedColumn(grid.Columns[4], 90);
            SetFixedColumn(grid.Columns[5], 90);
            SetFixedColumn(grid.Columns[6], 100);
            SetFixedColumn(grid.Columns[7], 100);
            DataGridViewColumn remarkColumn = grid.Columns[8];
            remarkColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            remarkColumn.MinimumWidth = 160;
        }

        private static void SetFixedColumn(DataGridViewColumn column, int width)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
            column.MinimumWidth = width;
            column.Resizable = DataGridViewTriState.False;
        }
    }
}
