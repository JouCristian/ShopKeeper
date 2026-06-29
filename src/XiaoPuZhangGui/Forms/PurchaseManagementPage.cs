using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class PurchaseManagementPage : UserControl, IResponsivePage
    {
        private readonly PurchaseService _purchaseService;
        private readonly BindingSource _bindingSource;
        private readonly Panel _contentPanel;
        private readonly FlowLayoutPanel _filters;
        private readonly DateTimePicker _startDatePicker;
        private readonly DateTimePicker _endDatePicker;
        private readonly TextBox _keywordTextBox;
        private readonly DataGridView _grid;
        private readonly Label _emptyLabel;

        public PurchaseManagementPage()
        {
            _purchaseService = new PurchaseService();
            _bindingSource = new BindingSource();

            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBackground;
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular);

            Panel headerPanel = UiComponentHelper.CreatePageHeader(
                "进货入库",
                "记录入库批次、数量和成本，保持库存准确",
                "headers/purchase");

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = BackColor
            };

            _filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 116,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = BackColor
            };

            Button addButton = CreateButton("新增入库", UiTheme.SuccessGreen, 110);
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

            Button refreshButton = CreateButton("刷新", UiTheme.PrimaryBlue, 90);
            refreshButton.Click += delegate { LoadRecords(); };

            _filters.Controls.Add(addButton);
            _filters.Controls.Add(CreateFilterLabel("开始日期", 72));
            _filters.Controls.Add(_startDatePicker);
            _filters.Controls.Add(CreateFilterLabel("结束日期", 72));
            _filters.Controls.Add(_endDatePicker);
            _filters.Controls.Add(CreateFilterLabel("商品名称", 80));
            _filters.Controls.Add(_keywordTextBox);
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
            _emptyLabel = UiComponentHelper.CreateEmptyStateLabel("暂无入库记录，请先新增入库单。", "empty/purchase");

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
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "DeleteColumn",
                HeaderText = "删除",
                Text = "删除",
                Width = 70,
                UseColumnTextForButtonValue = true
            });

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
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            PurchaseRecord record = _grid.Rows[e.RowIndex].DataBoundItem as PurchaseRecord;
            if (record == null)
            {
                return;
            }

            string columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == "DetailColumn")
            {
                using (PurchaseDetailForm form = new PurchaseDetailForm(record.Id))
                {
                    form.ShowDialog(this);
                }
            }
            else if (columnName == "DeleteColumn")
            {
                DeleteRecord(record);
            }
        }

        private void DeleteRecord(PurchaseRecord record)
        {
            DialogResult result = MessageBox.Show(
                "确认删除入库单「" + record.PurchaseNo + "」吗？\r\n\r\n删除后会扣回本次入库库存。若该批次已被后续销售或报废消耗，将无法删除。",
                "删除入库单",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            string message;
            if (!_purchaseService.TryDelete(record.Id, out message))
            {
                MessageBox.Show(message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "删除成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadRecords();
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
            var records = _purchaseService.Search(_startDatePicker.Value.Date, _endDatePicker.Value.Date, _keywordTextBox.Text);
            _bindingSource.DataSource = records;
            _emptyLabel.Visible = records.Count == 0;
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
                Font = UiTheme.Font(10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
    }
}
