using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class GridStyleHelper
    {
        public static void ApplyStandardStyle(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.AllowUserToResizeRows = false;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = UiTheme.CardBorder;
            grid.StandardTab = true;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = UiTheme.GridHeaderHeight;
            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.Font(10F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.SoftBlue;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 6, 6, 6);
            grid.DefaultCellStyle.Font = UiTheme.Font(10F);
            grid.DefaultCellStyle.ForeColor = UiTheme.TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = UiTheme.TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
            grid.RowTemplate.Height = UiTheme.GridRowHeight;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnAdded -= Grid_ColumnAdded;
            grid.ColumnAdded += Grid_ColumnAdded;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                ConfigureColumn(column);
            }

            grid.CellFormatting -= UiStatusStyleHelper.ApplyStatusCellStyle;
            grid.CellFormatting += UiStatusStyleHelper.ApplyStatusCellStyle;
        }

        private static void Grid_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            ConfigureColumn(e.Column);
        }

        private static void ConfigureColumn(DataGridViewColumn column)
        {
            if (column == null)
            {
                return;
            }

            if (column is DataGridViewButtonColumn)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                column.MinimumWidth = column.Width > 0 ? column.Width : 70;
                return;
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = column.Width > 0 ? column.Width : 100;
        }

        public static void FillLastColumn(DataGridView grid)
        {
            if (grid.Columns.Count == 0)
            {
                return;
            }

            grid.Columns[grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
    }
}
