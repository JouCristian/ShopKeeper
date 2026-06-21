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

            SalesOrder order = _salesService.GetById(salesOrderId);
            BuildUi(order);
        }

        private void BuildUi(SalesOrder order)
        {
            if (order == null)
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

            Label infoLabel = new Label
            {
                Text = string.Format(
                    "单号：{0}    时间：{1:yyyy-MM-dd HH:mm:ss}\r\n应收：{2:N2}    成本：{3:N2}    毛利润：{4:N2}    实收：{5:N2}\r\n备注：{6}",
                    order.OrderNo,
                    order.SaleTime,
                    order.TotalAmount,
                    order.TotalCost,
                    order.GrossProfit,
                    order.PaidAmount,
                    order.Remark),
                Location = new Point(28, 66),
                Size = new Size(810, 92),
                ForeColor = Color.FromArgb(73, 80, 87)
            };
            Controls.Add(infoLabel);

            DataGridView grid = new DataGridView
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
                DataSource = order.Items
            };
            GridStyleHelper.ApplyStandardStyle(grid);

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductNameSnapshot", Width = 190 });
            AddNumberColumn(grid, "数量", "Quantity", 90, "N3");
            AddNumberColumn(grid, "售价", "SalePriceSnapshot", 90, "N2");
            AddNumberColumn(grid, "成本价", "CostPriceSnapshot", 90, "N2");
            AddNumberColumn(grid, "金额", "LineAmount", 100, "N2");
            AddNumberColumn(grid, "成本", "LineCost", 100, "N2");
            AddNumberColumn(grid, "毛利", "LineProfit", 100, "N2");
            Controls.Add(grid);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(718, 492),
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
    }
}
