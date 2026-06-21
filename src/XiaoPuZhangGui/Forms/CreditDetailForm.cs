using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class CreditDetailForm : Form
    {
        private readonly CreditService _creditService;

        public CreditDetailForm(long creditRecordId)
        {
            _creditService = new CreditService();

            Text = "赊账详情";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(860, 540);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            CreditRecord record = _creditService.GetById(creditRecordId);
            BuildUi(record);
        }

        private void BuildUi(CreditRecord record)
        {
            if (record == null)
            {
                Controls.Add(new Label
                {
                    Text = "未找到赊账记录。",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                return;
            }

            Controls.Add(new Label
            {
                Text = "赊账详情",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(220, 40)
            });

            Controls.Add(new Label
            {
                Text = string.Format(
                    "赊账单号：{0}    销售单号：{1}\r\n欠款人：{2}    状态：{3}    赊账日期：{4:yyyy-MM-dd}    结清时间：{5}\r\n原始欠款：{6:N2}    已还：{7:N2}    剩余：{8:N2}\r\n备注：{9}",
                    record.CreditNo,
                    record.SalesOrderNo,
                    record.DebtorName,
                    record.StatusText,
                    record.CreditDate,
                    record.SettledAt.HasValue ? record.SettledAt.Value.ToString("yyyy-MM-dd") : "",
                    record.OriginalAmount,
                    record.PaidAmount,
                    record.RemainingAmount,
                    record.Remark),
                Location = new Point(28, 66),
                Size = new Size(800, 118),
                ForeColor = Color.FromArgb(73, 80, 87)
            });

            DataGridView grid = new DataGridView
            {
                Location = new Point(28, 200),
                Size = new Size(800, 280),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                DataSource = record.Payments
            };
            GridStyleHelper.ApplyStandardStyle(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "还款日期", DataPropertyName = "PaymentDate", Width = 130, DefaultCellStyle = { Format = "yyyy-MM-dd" } });
            AddMoneyColumn(grid, "还款金额", "Amount", 110);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 500 });
            Controls.Add(grid);

            Button closeButton = new Button
            {
                Text = "关闭",
                Location = new Point(708, 492),
                Size = new Size(120, 38),
                DialogResult = DialogResult.OK
            };
            Controls.Add(closeButton);
            AcceptButton = closeButton;
        }

        private static void AddMoneyColumn(DataGridView grid, string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            column.DefaultCellStyle.Format = "N2";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(column);
        }
    }
}
