using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class CreditManagementPage : UserControl, IResponsivePage
    {
        private readonly CreditService _creditService;
        private readonly BindingSource _bindingSource;
        private readonly Panel _contentPanel;
        private readonly FlowLayoutPanel _filters;
        private ComboBox _statusComboBox;
        private TextBox _debtorTextBox;
        private DateTimePicker _startDatePicker;
        private DateTimePicker _endDatePicker;
        private DataGridView _grid;
        private Label _emptyLabel;

        public CreditManagementPage()
        {
            _creditService = new CreditService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Panel headerPanel = UiComponentHelper.CreatePageHeader(
                "赊账管理",
                "跟踪欠款、还款和未结清账款",
                "headers/credit");

            _contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), BackColor = BackColor };
            _filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 116,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
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

            Button refreshButton = CreateButton("刷新", UiTheme.PrimaryBlue, 90);
            refreshButton.Click += delegate { LoadRecords(); };

            _filters.Controls.Add(CreateFilterLabel("状态", 52));
            _filters.Controls.Add(_statusComboBox);
            _filters.Controls.Add(CreateFilterLabel("欠款人", 64));
            _filters.Controls.Add(_debtorTextBox);
            _filters.Controls.Add(CreateFilterLabel("开始日期", 78));
            _filters.Controls.Add(_startDatePicker);
            _filters.Controls.Add(CreateFilterLabel("结束日期", 78));
            _filters.Controls.Add(_endDatePicker);
            _filters.Controls.Add(refreshButton);
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
            GridStyleHelper.ApplyStandardStyle(_grid);
            BuildColumns();
            _emptyLabel = UiComponentHelper.CreateEmptyStateLabel("暂无赊账记录。", "empty/credit");

            _contentPanel.Controls.Add(_grid);
            _contentPanel.Controls.Add(_emptyLabel);
            _contentPanel.Controls.Add(_filters);
            Controls.Add(_contentPanel);
            Controls.Add(headerPanel);

            LoadRecords();
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
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "DeleteColumn", HeaderText = "删除", Text = "删除", Width = 70, UseColumnTextForButtonValue = true });
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
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
            else if (columnName == "DeleteColumn")
            {
                DeleteRecord(record);
            }
        }

        private void DeleteRecord(CreditRecord record)
        {
            DialogResult result = MessageBox.Show(
                "确认删除赊账记录「" + record.CreditNo + "」吗？\r\n\r\n删除后会同时删除还款明细，并将关联销售单标记为无赊账余额。此操作不可撤销。",
                "删除赊账记录",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            string message;
            if (!_creditService.TryDelete(record.Id, out message))
            {
                MessageBox.Show(message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadRecords();
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
            var records = _creditService.Search(
                _startDatePicker.Value.Date,
                _endDatePicker.Value.Date,
                _debtorTextBox.Text,
                _statusComboBox.Text);
            _bindingSource.DataSource = records;
            _emptyLabel.Visible = records.Count == 0;
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
                Font = UiTheme.Font(10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
