using System;
using System.Drawing;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal static class UiStatusStyleHelper
    {
        public static void ApplyStatusCellStyle(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0 || e.Value == null)
            {
                return;
            }

            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            if (!IsStatusColumn(column))
            {
                return;
            }

            string text = Convert.ToString(e.Value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            StatusPalette palette = ResolvePalette(text);
            e.CellStyle.BackColor = palette.BackColor;
            e.CellStyle.ForeColor = palette.ForeColor;
            e.CellStyle.SelectionBackColor = palette.BackColor;
            e.CellStyle.SelectionForeColor = palette.ForeColor;
        }

        private static bool IsStatusColumn(DataGridViewColumn column)
        {
            string propertyName = column.DataPropertyName ?? string.Empty;
            string headerText = column.HeaderText ?? string.Empty;
            return propertyName.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0
                || headerText.Contains("状态")
                || headerText.Contains("保质期");
        }

        private static StatusPalette ResolvePalette(string text)
        {
            if (ContainsAny(text, "已过期", "未结清", "亏", "报废", "危险"))
            {
                return new StatusPalette(Color.FromArgb(248, 215, 218), Color.FromArgb(132, 32, 41));
            }

            if (ContainsAny(text, "临期", "部分", "低库存", "预警", "停用"))
            {
                return new StatusPalette(Color.FromArgb(255, 243, 205), Color.FromArgb(102, 77, 3));
            }

            if (ContainsAny(text, "已结清", "在售", "正常", "盘盈", "完成"))
            {
                return new StatusPalette(Color.FromArgb(212, 237, 218), Color.FromArgb(21, 87, 36));
            }

            return new StatusPalette(Color.FromArgb(232, 244, 255), Color.FromArgb(24, 70, 120));
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            foreach (string value in values)
            {
                if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class StatusPalette
        {
            public StatusPalette(Color backColor, Color foreColor)
            {
                BackColor = backColor;
                ForeColor = foreColor;
            }

            public Color BackColor { get; private set; }

            public Color ForeColor { get; private set; }
        }
    }
}
