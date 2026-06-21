using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class PurchaseManagementPage : UserControl
    {
        private readonly PurchaseService _purchaseService;
        private readonly BindingSource _bindingSource;
        private readonly DateTimePicker _startDatePicker;
        private readonly DateTimePicker _endDatePicker;
        private readonly TextBox _keywordTextBox;
        private readonly DataGridView _grid;

        public PurchaseManagementPage()
        {
            _purchaseService = new PurchaseService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 250);
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Label titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 72,
                Text = "进货入库",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0)
            };

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = BackColor
            };

            FlowLayoutPanel filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 72,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = BackColor
            };

            Button addButton = CreateButton("新增入库", Color.FromArgb(40, 167, 69), 110);
            addButton.Click += AddButton_Click;

            _startDatePicker = CreateDatePicker(DateTime.Today.AddDays(-30));
            _endDatePicker = CreateDatePicker(DateTime.Today);
            _keywordTextBox = new TextBox
            {
                Width = 180,
                Font = new Font("Microsoft YaHei UI", 12F),
                Margin = new Padding(0, 22, 12, 0)
            };
            _keywordTextBox.KeyDown += KeywordTextBox_KeyDown;

            Button refreshButton = CreateButton("刷新", Color.FromArgb(0, 123, 255), 90);
            refreshButton.Click += delegate { LoadRecords(); };

            filters.Controls.Add(addButton);
            filters.Controls.Add(CreateFilterLabel("开始日期", 72));
            filters.Controls.Add(_startDatePicker);
            filters.Controls.Add(CreateFilterLabel("结束日期", 72));
            filters.Controls.Add(_endDatePicker);
            filters.Controls.Add(CreateFilterLabel("商品名称", 80));
            filters.Controls.Add(_keywordTextBox);
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

            AddTextColumn("入库单号", "PurchaseNo", 170);
            AddTextColumn("入库日期", "PurchaseDate", 110).DefaultCellStyle.Format = "yyyy-MM-dd";
            AddTextColumn("商品种类数", "ProductKindCount", 90);
            AddNumberColumn("入库总数量", "TotalQuantity", 110);
            AddMoneyColumn("入库总金额", "TotalAmount", 110);
            AddTextColumn("备注", "Remark", 220);
            DataGridViewButtonColumn detailColumn = new DataGridViewButtonColumn
            {
                Name = "DetailColumn",
                HeaderText = "操作",
                Text = "查看",
                Width = 80,
                UseColumnTextForButtonValue = true
            };
            _grid.Columns.Add(detailColumn);

            contentPanel.Controls.Add(_grid);
            contentPanel.Controls.Add(filters);
            Controls.Add(contentPanel);
            Controls.Add(titleLabel);

            LoadRecords();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (PurchaseEditForm form = new PurchaseEditForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRecords();
                }
            }
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "DetailColumn")
            {
                return;
            }

            PurchaseRecord record = _grid.Rows[e.RowIndex].DataBoundItem as PurchaseRecord;
            if (record == null)
            {
                return;
            }

            using (PurchaseDetailForm form = new PurchaseDetailForm(record.Id))
            {
                form.ShowDialog(this);
            }
        }

        private void KeywordTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadRecords();
                e.Handled = true;
            }
        }

        private void LoadRecords()
        {
            _bindingSource.DataSource = _purchaseService.Search(_startDatePicker.Value.Date, _endDatePicker.Value.Date, _keywordTextBox.Text);
        }

        private DataGridViewTextBoxColumn AddTextColumn(string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = headerText,
                DataPropertyName = propertyName,
                Width = width
            };
            _grid.Columns.Add(column);
            return column;
        }

        private void AddMoneyColumn(string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = AddTextColumn(headerText, propertyName, width);
            column.DefaultCellStyle.Format = "N2";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        private void AddNumberColumn(string headerText, string propertyName, int width)
        {
            DataGridViewTextBoxColumn column = AddTextColumn(headerText, propertyName, width);
            column.DefaultCellStyle.Format = "N3";
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
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
