using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class GridStyleHelper
    {
        public static void ApplyStandardStyle(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 42;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(33, 37, 41);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 6, 4, 6);
            grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 10F);
            grid.RowTemplate.Height = 34;
        }
    }
}
