using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class CreditManagementPage : UserControl
    {
        private readonly CreditService _creditService;
        private readonly BindingSource _bindingSource;
        private ComboBox _statusComboBox;
        private TextBox _debtorTextBox;
        private DateTimePicker _startDatePicker;
        private DateTimePicker _endDatePicker;
        private DataGridView _grid;

        public CreditManagementPage()
        {
            _creditService = new CreditService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "赊账管理",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), BackColor = BackColor };
            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 72,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };

            _statusComboBox = new ComboBox
            {
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 11F),
                Margin = new Padding(0, 22, 12, 0)
            };
            _statusComboBox.Items.AddRange(new object[] { "全部", "未结清", "部分还款", "已结清" });
            _statusComboBox.SelectedIndex = 0;

            _debtorTextBox = new TextBox { Width = 170, Font = new Font("Microsoft YaHei UI", 12F), Margin = new Padding(0, 22, 12, 0) };
            _debtorTextBox.KeyDown += DebtorTextBox_KeyDown;
            _startDatePicker = CreateDatePicker(DateTime.Today.AddDays(-90));
            _endDatePicker = CreateDatePicker(DateTime.Today);

            Button refreshButton = CreateButton("刷新", Color.FromArgb(0, 123, 255), 90);
            refreshButton.Click += delegate { LoadRecords(); };

            filters.Controls.Add(CreateFilterLabel("状态", 52));
            filters.Controls.Add(_statusComboBox);
            filters.Controls.Add(CreateFilterLabel("欠款人", 64));
            filters.Controls.Add(_debtorTextBox);
            filters.Controls.Add(CreateFilterLabel("开始日期", 78));
            filters.Controls.Add(_startDatePicker);
            filters.Controls.Add(CreateFilterLabel("结束日期", 78));
            filters.Controls.Add(_endDatePicker);
            filters.Controls.Add(refreshButton);

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
            GridStyleHelper.ApplyStandardStyle(_grid);
            BuildColumns();

            content.Controls.Add(_grid);
            content.Controls.Add(filters);
            Controls.Add(content);
            Controls.Add(titleLabel);

            LoadRecords();
        }

        private void BuildColumns()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "赊账单号", DataPropertyName = "CreditNo", Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "赊账日期", DataPropertyName = "CreditDate", Width = 110, DefaultCellStyle = { Format = "yyyy-MM-dd" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "欠款人备注", DataPropertyName = "DebtorName", Width = 140 });
            AddMoneyColumn("原始欠款", "OriginalAmount", 100);
            AddMoneyColumn("已还金额", "PaidAmount", 100);
            AddMoneyColumn("剩余欠款", "RemainingAmount", 100);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", DataPropertyName = "StatusText", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "备注", DataPropertyName = "Remark", Width = 160 });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "PaymentColumn", HeaderText = "还款", Text = "还款", Width = 70, UseColumnTextForButtonValue = true });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "DetailColumn", HeaderText = "操作", Text = "查看", Width = 70, UseColumnTextForButtonValue = true });
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            CreditRecord record = _grid.Rows[e.RowIndex].DataBoundItem as CreditRecord;
            if (record == null)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == "DetailColumn")
            {
                using (CreditDetailForm form = new CreditDetailForm(record.Id))
                {
                    form.ShowDialog(this);
                }
            }
            else if (columnName == "PaymentColumn")
            {
                if (record.Status == "Settled")
                {
                    MessageBox.Show("该赊账已结清。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (CreditPaymentForm form = new CreditPaymentForm(record.Id))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadRecords();
                    }
                }
            }
        }

        private void DebtorTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadRecords();
                e.Handled = true;
            }
        }

        private void LoadRecords()
        {
            _bindingSource.DataSource = _creditService.Search(
                _startDatePicker.Value.Date,
                _endDatePicker.Value.Date,
                _debtorTextBox.Text,
                _statusComboBox.Text);
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

        private static DateTimePicker CreateDatePicker(DateTime value)
        {
            return new DateTimePicker
            {
                Width = 130,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                Value = value,
                Margin = new Padding(0, 20, 12, 0)
            };
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
    }
}
