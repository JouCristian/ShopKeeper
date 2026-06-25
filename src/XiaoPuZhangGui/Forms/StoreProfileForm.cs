using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class StoreProfileForm : Form
    {
        private readonly TextBox _storeNameTextBox;
        private readonly TextBox _locationTextBox;
        private readonly TextBox _businessTypeTextBox;
        private readonly TextBox _customersTextBox;
        private readonly TextBox _productsTextBox;
        private readonly TextBox _hoursTextBox;
        private readonly TextBox _preferenceTextBox;
        private readonly TextBox _pricingTextBox;
        private readonly TextBox _restockTextBox;
        private readonly TextBox _creditTextBox;
        private readonly TextBox _notesTextBox;

        public StoreProfileForm(AiStoreProfile profile, bool firstRun)
        {
            Text = firstRun ? "初始化店铺记忆" : "店铺记忆设置";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(720, 640);
            BackColor = Color.White;
            Font = UiTheme.Font(10.5F);

            Label titleLabel = new Label
            {
                Text = firstRun ? "先让 AI 了解一点店铺情况" : "店铺记忆",
                Location = new Point(24, 18),
                Size = new Size(420, 34),
                Font = UiTheme.Font(18F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            };
            Controls.Add(titleLabel);

            Label subtitleLabel = new Label
            {
                Text = "这些信息只保存在本地，用来让 AI 回答更贴近你家的店。可以跳过，也可以以后再改。",
                Location = new Point(26, 56),
                Size = new Size(640, 28),
                ForeColor = UiTheme.TextSecondary
            };
            Controls.Add(subtitleLabel);

            TableLayoutPanel table = new TableLayoutPanel
            {
                Location = new Point(26, 94),
                Size = new Size(660, 442),
                ColumnCount = 2,
                RowCount = 11
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(table);

            _storeNameTextBox = AddRow(table, 0, "店铺名称", profile.StoreName);
            _locationTextBox = AddRow(table, 1, "大概位置", profile.StoreLocation);
            _businessTypeTextBox = AddRow(table, 2, "经营类型", profile.BusinessType);
            _customersTextBox = AddRow(table, 3, "主要顾客", profile.MainCustomers);
            _productsTextBox = AddRow(table, 4, "主要商品", profile.MainProducts);
            _hoursTextBox = AddRow(table, 5, "营业时间", profile.OpeningHours);
            _preferenceTextBox = AddRow(table, 6, "店主关注点", profile.OwnerPreference);
            _pricingTextBox = AddRow(table, 7, "定价风格", profile.PricingStyle);
            _restockTextBox = AddRow(table, 8, "补货偏好", profile.RestockPreference);
            _creditTextBox = AddRow(table, 9, "赊账规则", profile.CreditPolicy);
            _notesTextBox = AddRow(table, 10, "其他记忆", profile.Notes);

            Button saveButton = UiComponentHelper.CreatePrimaryButton("保存记忆", 110);
            saveButton.Location = new Point(318, 566);
            saveButton.Click += delegate
            {
                Profile = BuildProfile(true);
                DialogResult = DialogResult.OK;
                Close();
            };

            Button skipButton = UiComponentHelper.CreateSecondaryButton(firstRun ? "先跳过" : "关闭", 96);
            skipButton.Location = new Point(438, 566);
            skipButton.Click += delegate
            {
                if (firstRun)
                {
                    Profile = BuildProfile(true);
                    DialogResult = DialogResult.OK;
                }
                else
                {
                    DialogResult = DialogResult.Cancel;
                }

                Close();
            };

            Button clearButton = UiComponentHelper.CreateDangerButton("清空记忆", 110);
            clearButton.Location = new Point(544, 566);
            clearButton.Click += delegate
            {
                ClearRequested = true;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(saveButton);
            Controls.Add(skipButton);
            Controls.Add(clearButton);
            Profile = profile;
        }

        public AiStoreProfile Profile { get; private set; }

        public bool ClearRequested { get; private set; }

        private static TextBox AddRow(TableLayoutPanel table, int row, string labelText, string value)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary
            };

            TextBox textBox = new TextBox
            {
                Text = value ?? string.Empty,
                Dock = DockStyle.Fill,
                Font = UiTheme.Font(10.5F)
            };
            UiComponentHelper.CenterTextBoxContent(textBox);

            table.Controls.Add(label, 0, row);
            table.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private AiStoreProfile BuildProfile(bool initialized)
        {
            return new AiStoreProfile
            {
                IsInitialized = initialized,
                StoreName = _storeNameTextBox.Text.Trim(),
                StoreLocation = _locationTextBox.Text.Trim(),
                BusinessType = _businessTypeTextBox.Text.Trim(),
                MainCustomers = _customersTextBox.Text.Trim(),
                MainProducts = _productsTextBox.Text.Trim(),
                OpeningHours = _hoursTextBox.Text.Trim(),
                OwnerPreference = _preferenceTextBox.Text.Trim(),
                PricingStyle = _pricingTextBox.Text.Trim(),
                RestockPreference = _restockTextBox.Text.Trim(),
                CreditPolicy = _creditTextBox.Text.Trim(),
                Notes = _notesTextBox.Text.Trim(),
                UpdatedAt = DateTime.Now
            };
        }
    }
}
