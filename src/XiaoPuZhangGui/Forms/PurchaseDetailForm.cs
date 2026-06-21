using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class PurchaseDetailForm : Form
    {
        private readonly PurchaseService _purchaseService;

        public PurchaseDetailForm(long purchaseRecordId)
        {
            _purchaseService = new PurchaseService();

            Text = "入库单详情";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(820, 520);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            PurchaseRecord record = _purchaseService.GetById(purchaseRecordId);
            BuildUi(record);
        }

        private void BuildUi(PurchaseRecord record)
        {
            if (record == null)
            {
                Label missingLabel = new Label
                {
                    Text = "未找到入库单。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(missingLabel);
                return;
            }

            Label titleLabel = new Label
            {
                Text = "入库单详情",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(220, 40)
            };
            Controls.Add(titleLabel);

            Label infoLabel = new Label
            {
                Text = string.Format(
                    "单号：{0}    日期：{1:yyyy-MM-dd}    总金额：{2:N2}\r\n备注：{3}",
                    record.PurchaseNo,
                    record.PurchaseDate,
                    record.TotalAmount,
                    record.Remark),
                Location = new Point(28, 66),
                Size = new Size(760, 66),
                ForeColor = Color.FromArgb(73, 80, 87)
            };
            Controls.Add(infoLabel);

            DataGridView grid = new DataGridView
            {
                Location = new Point(28, 145),
                Size = new Size(760, 315),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                DataSource = record.Items
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "商品", DataPropertyName = "ProductNameSnapshot", Width = 170 });
            AddNumberColumn(grid, "数量", "Quantity", 90, "N3");
            AddNumberColumn(grid, "进货单价", "PurchasePrice", 100, "N2");
            AddNumberColumn(grid, "小计", "LineTotal", 100, "N2");
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "到期日期", DataPropertyName = "ExpiryDateText", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 180 });
            Controls.Add(grid);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(668, 470),
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
