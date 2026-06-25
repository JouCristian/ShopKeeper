using System;
using System.Drawing;
using System.Windows.Forms;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Services;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Forms
{
    internal sealed class CategoryManagementForm : Form
    {
        private readonly CategoryService _categoryService;
        private readonly DataGridView _grid;
        private readonly BindingSource _bindingSource;
        private readonly TextBox _nameTextBox;

        public CategoryManagementForm()
        {
            _categoryService = new CategoryService();
            _bindingSource = new BindingSource();

            Text = "分类管理";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 520);
            Font = new Font("Microsoft YaHei UI", 11F);
            BackColor = Color.White;

            Label titleLabel = new Label
            {
                Text = "分类管理",
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                Location = new Point(24, 20),
                Size = new Size(220, 40)
            };

            _nameTextBox = new TextBox
            {
                Location = new Point(28, 74),
                Size = new Size(220, 32),
                Font = new Font("Microsoft YaHei UI", 11F)
            };

            Button addButton = CreateButton("新增", 260, 72, UiTheme.SuccessGreen);
            addButton.Click += AddButton_Click;

            Button renameButton = CreateButton("改名", 350, 72, UiTheme.PrimaryBlue);
            renameButton.Click += RenameButton_Click;

            Button statusButton = CreateButton("停用/启用", 440, 72, UiTheme.MutedGray);
            statusButton.Click += StatusButton_Click;

            _grid = new DataGridView
            {
                Location = new Point(28, 126),
                Size = new Size(500, 350),
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                DataSource = _bindingSource
            };
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分类名称", DataPropertyName = "Name", Width = 260 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", DataPropertyName = "StatusText", Width = 120 });
            GridStyleHelper.ApplyStandardStyle(_grid);

            Label noteLabel = new Label
            {
                Text = "分类不会物理删除。停用后仍可在商品管理中筛选查看历史商品。",
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(28, 480),
                Size = new Size(500, 28)
            };

            Controls.Add(titleLabel);
            Controls.Add(_nameTextBox);
            Controls.Add(addButton);
            Controls.Add(renameButton);
            Controls.Add(statusButton);
            Controls.Add(_grid);
            Controls.Add(noteLabel);
            UiComponentHelper.NormalizeControlMetrics(this);

            LoadCategories();
        }

        private void LoadCategories()
        {
            _bindingSource.DataSource = _categoryService.GetAllCategories();
        }

        private Category SelectedCategory
        {
            get
            {
                if (_grid.CurrentRow == null)
                {
                    return null;
                }

                return _grid.CurrentRow.DataBoundItem as Category;
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            string message;
            if (!_categoryService.TryAdd(_nameTextBox.Text, out message))
            {
                MessageBox.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _nameTextBox.Clear();
            LoadCategories();
        }

        private void RenameButton_Click(object sender, EventArgs e)
        {
            Category category = SelectedCategory;
            if (category == null)
            {
                MessageBox.Show("请先选择分类。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message;
            if (!_categoryService.TryRename(category.Id, _nameTextBox.Text, out message))
            {
                MessageBox.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(message, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadCategories();
        }

        private void StatusButton_Click(object sender, EventArgs e)
        {
            Category category = SelectedCategory;
            if (category == null)
            {
                MessageBox.Show("请先选择分类。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool enable = !category.IsActive;
            string action = enable ? "启用" : "停用";
            string extra = _categoryService.HasProducts(category.Id)
                ? "\r\n该分类下已有商品，本操作不会删除商品。"
                : string.Empty;

            DialogResult result = MessageBox.Show(
                "确定要" + action + "分类“" + category.Name + "”吗？" + extra,
                "确认操作",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _categoryService.SetActive(category.Id, enable);
            LoadCategories();
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            Category category = SelectedCategory;
            if (category != null)
            {
                _nameTextBox.Text = category.Name;
            }
        }

        private static Button CreateButton(string text, int left, int top, Color color)
        {
            Button button = new Button
            {
                Text = text,
                Location = new Point(left, top),
                Size = new Size(80, 36),
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
