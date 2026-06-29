using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace XiaoPuZhangGui.Utils
{
    internal enum UiLayoutMode
    {
        Normal,
        CashierCompact,
        VeryCompact
    }

    internal interface IResponsivePage
    {
        void ApplyLayout(UiLayoutMode mode);
    }

    internal static class ResponsiveLayoutManager
    {
        private const int CashierCompactWidthThreshold = 1400;
        private const int CashierCompactHeightThreshold = 760;
        private const int VeryCompactWidthThreshold = 1200;
        private const int VeryCompactHeightThreshold = 700;
        private static readonly ConditionalWeakTable<Control, ControlSnapshot> Snapshots = new ConditionalWeakTable<Control, ControlSnapshot>();
        private static readonly ConditionalWeakTable<TableLayoutPanel, TableSnapshot> TableSnapshots = new ConditionalWeakTable<TableLayoutPanel, TableSnapshot>();

        public static UiLayoutMode DetectMode(Size size)
        {
            if (size.Width < VeryCompactWidthThreshold || size.Height < VeryCompactHeightThreshold)
            {
                return UiLayoutMode.VeryCompact;
            }

            if (size.Width < CashierCompactWidthThreshold || size.Height < CashierCompactHeightThreshold)
            {
                return UiLayoutMode.CashierCompact;
            }

            return UiLayoutMode.Normal;
        }

        public static bool IsCompact(UiLayoutMode mode)
        {
            return mode != UiLayoutMode.Normal;
        }

        public static bool IsVeryCompact(UiLayoutMode mode)
        {
            return mode == UiLayoutMode.VeryCompact;
        }

        public static bool IsCashierCompact(UiLayoutMode mode)
        {
            return mode == UiLayoutMode.CashierCompact;
        }

        public static int SidebarWidth(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? 172 : (IsCashierCompact(mode) ? 190 : 220);
        }

        public static int SidebarBrandHeight(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? 56 : (IsCashierCompact(mode) ? 64 : 72);
        }

        public static int SidebarButtonHeight(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? 48 : (IsCashierCompact(mode) ? 54 : 60);
        }

        public static int SidebarFooterHeight(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? 30 : (IsCashierCompact(mode) ? 34 : 40);
        }

        public static int SidebarAiStatusHeight(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? 76 : (IsCashierCompact(mode) ? 88 : 100);
        }

        public static Padding PagePadding(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? new Padding(10) : (IsCashierCompact(mode) ? new Padding(12) : UiTheme.PagePaddingValue);
        }

        public static Padding CardPadding(UiLayoutMode mode)
        {
            return IsVeryCompact(mode) ? new Padding(10) : (IsCashierCompact(mode) ? new Padding(12) : UiTheme.CardPaddingValue);
        }

        public static void ApplyToPage(Control page, UiLayoutMode mode)
        {
            if (page == null)
            {
                return;
            }

            IResponsivePage responsivePage = page as IResponsivePage;
            if (responsivePage != null)
            {
                responsivePage.ApplyLayout(mode);
            }

            ApplyControlTree(page, mode);
        }

        public static void ApplyControlTree(Control root, UiLayoutMode mode)
        {
            if (root == null)
            {
                return;
            }

            ApplyControl(root, mode);
            foreach (Control child in root.Controls)
            {
                ApplyControlTree(child, mode);
            }
        }

        public static void ApplyGridMetrics(DataGridView grid, UiLayoutMode mode)
        {
            if (grid == null)
            {
                return;
            }

            bool compact = IsCompact(mode);
            bool veryCompact = IsVeryCompact(mode);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = veryCompact ? 32 : (compact ? 34 : UiTheme.GridRowHeight);
            grid.ColumnHeadersHeight = veryCompact ? 36 : (compact ? 38 : UiTheme.GridHeaderHeight);
            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.Font(compact ? 9.5F : 10F, FontStyle.Bold);
            grid.DefaultCellStyle.Font = UiTheme.Font(compact ? 9.7F : 10F);
            grid.ColumnHeadersDefaultCellStyle.Padding = compact ? new Padding(4, 4, 4, 4) : new Padding(6, 6, 6, 6);
            grid.DefaultCellStyle.Padding = compact ? new Padding(4, 1, 4, 1) : new Padding(6, 2, 6, 2);
            grid.ScrollBars = ScrollBars.Both;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                ApplyColumnMetrics(column, compact);
            }

            if (veryCompact)
            {
                HideLowPriorityColumns(grid);
            }
            else
            {
                RestoreColumnVisibility(grid);
            }
        }

        public static void ApplyFixedColumns(DataGridView grid, IDictionary<string, int> widths, string fillColumnName)
        {
            if (grid == null || widths == null)
            {
                return;
            }

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                string key = !string.IsNullOrWhiteSpace(column.Name) ? column.Name : column.DataPropertyName;
                if (!string.IsNullOrWhiteSpace(key) && widths.ContainsKey(key))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = widths[key];
                    column.MinimumWidth = Math.Min(widths[key], 70);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fillColumnName) &&
                    string.Equals(key, fillColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.FillWeight = Math.Max(120, column.Width);
                }
            }
        }

        private static void ApplyControl(Control control, UiLayoutMode mode)
        {
            ControlSnapshot snapshot = GetSnapshot(control);
            bool compact = IsCompact(mode);
            bool veryCompact = IsVeryCompact(mode);

            if (control is DataGridView)
            {
                ApplyGridMetrics((DataGridView)control, mode);
                return;
            }

            if (control is TableLayoutPanel)
            {
                ApplyTableLayoutMetrics((TableLayoutPanel)control, mode);
            }

            if (control is PictureBox)
            {
                ((PictureBox)control).SizeMode = PictureBoxSizeMode.Zoom;
            }

            if (control is FlowLayoutPanel)
            {
                FlowLayoutPanel flow = (FlowLayoutPanel)control;
                flow.WrapContents = true;
                flow.Padding = compact ? CompactPadding(snapshot.Padding) : snapshot.Padding;
            }
            else if (control is Panel || control is TabPage || control is UserControl)
            {
                control.Padding = compact ? CompactPadding(snapshot.Padding) : snapshot.Padding;
            }

            if (control is Button)
            {
                Button button = (Button)control;
                button.Font = UiTheme.Font(compact ? (veryCompact ? 9.5F : 10F) : Math.Min(snapshot.FontSize, 11F), snapshot.FontStyle);
                if (button.Height >= 34 || snapshot.Height >= 34)
                {
                    button.Height = compact ? Math.Max(32, Math.Min(snapshot.Height, veryCompact ? 38 : 40)) : snapshot.Height;
                }
                return;
            }

            if (control is TextBox || control is ComboBox || control is NumericUpDown || control is DateTimePicker)
            {
                control.Font = UiTheme.Font(compact ? (veryCompact ? 9.8F : 10.2F) : Math.Min(snapshot.FontSize, 11F), snapshot.FontStyle);
                if (snapshot.Height >= 28)
                {
                    control.Height = compact ? Math.Max(28, Math.Min(snapshot.Height, veryCompact ? 34 : 36)) : snapshot.Height;
                }
                return;
            }

            Label label = control as Label;
            if (label != null)
            {
                float compactSize = ResolveCompactLabelSize(snapshot.FontSize, mode);
                label.Font = UiTheme.Font(compact ? compactSize : snapshot.FontSize, snapshot.FontStyle);
                if (snapshot.Height >= 30)
                {
                    label.Height = compact ? Math.Max(22, Math.Min(snapshot.Height, veryCompact ? 40 : 44)) : snapshot.Height;
                }
            }
        }

        private static void ApplyColumnMetrics(DataGridViewColumn column, bool compact)
        {
            if (column == null)
            {
                return;
            }

            if (column is DataGridViewButtonColumn)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                column.MinimumWidth = compact ? 58 : 70;
                return;
            }

            column.MinimumWidth = compact ? 64 : 80;
            if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.NotSet)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        private static void ApplyTableLayoutMetrics(TableLayoutPanel table, UiLayoutMode mode)
        {
            bool compact = IsCompact(mode);
            TableSnapshot snapshot = GetTableSnapshot(table);
            if (table.ColumnStyles.Count == 0)
            {
                return;
            }

            for (int i = 0; i < table.ColumnStyles.Count; i++)
            {
                ColumnStyle style = table.ColumnStyles[i];
                if (style.SizeType != SizeType.Absolute)
                {
                    continue;
                }

                float originalWidth = snapshot.GetColumnWidth(i, style.Width);
                if (!compact)
                {
                    style.Width = originalWidth;
                }
                else
                {
                    style.Width = originalWidth;
                }
            }
        }

        private static void HideLowPriorityColumns(DataGridView grid)
        {
            int visibleCount = 0;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (ShouldHideInCompact(column))
                {
                    column.Visible = false;
                }
                else
                {
                    column.Visible = true;
                    visibleCount++;
                }
            }

            if (visibleCount < 4)
            {
                RestoreColumnVisibility(grid);
            }
        }

        private static void RestoreColumnVisibility(DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.Visible = true;
            }
        }

        private static bool ShouldHideInCompact(DataGridViewColumn column)
        {
            if (column == null || column is DataGridViewButtonColumn)
            {
                return false;
            }

            string key = ((column.Name ?? string.Empty) + " " +
                (column.DataPropertyName ?? string.Empty) + " " +
                (column.HeaderText ?? string.Empty)).ToLowerInvariant();

            return key.Contains("barcode") ||
                key.Contains("spec") ||
                key.Contains("cost") ||
                key.Contains("profit") ||
                key.Contains("remark") ||
                key.Contains("shelflife") ||
                key.Contains("minstock") ||
                key.Contains("production") ||
                key.Contains("expiry") ||
                key.Contains("expire") ||
                key.Contains("order_no") ||
                key.Contains("orderno") ||
                key.Contains("creditno") ||
                key.Contains("checkno") ||
                key.Contains("scrapno");
        }

        private static Padding CompactPadding(Padding padding)
        {
            return new Padding(
                Math.Min(padding.Left, 10),
                Math.Min(padding.Top, 10),
                Math.Min(padding.Right, 10),
                Math.Min(padding.Bottom, 10));
        }

        private static float ResolveCompactLabelSize(float currentSize, UiLayoutMode mode)
        {
            bool veryCompact = IsVeryCompact(mode);

            if (currentSize >= 24F)
            {
                return veryCompact ? 21F : 23F;
            }

            if (currentSize >= 18F)
            {
                return veryCompact ? 16F : 17F;
            }

            if (currentSize >= 14F)
            {
                return 13.5F;
            }

            if (currentSize >= 11F)
            {
                return veryCompact ? 10F : 10.5F;
            }

            return Math.Max(9.5F, currentSize);
        }

        private static ControlSnapshot GetSnapshot(Control control)
        {
            ControlSnapshot snapshot;
            if (!Snapshots.TryGetValue(control, out snapshot))
            {
                snapshot = new ControlSnapshot(control);
                Snapshots.Add(control, snapshot);
            }

            return snapshot;
        }

        private static TableSnapshot GetTableSnapshot(TableLayoutPanel table)
        {
            TableSnapshot snapshot;
            if (!TableSnapshots.TryGetValue(table, out snapshot))
            {
                snapshot = new TableSnapshot(table);
                TableSnapshots.Add(table, snapshot);
            }

            return snapshot;
        }

        private sealed class ControlSnapshot
        {
            public ControlSnapshot(Control control)
            {
                FontSize = control.Font.Size;
                FontStyle = control.Font.Style;
                Padding = control.Padding;
                Height = control.Height;
            }

            public float FontSize { get; private set; }

            public FontStyle FontStyle { get; private set; }

            public Padding Padding { get; private set; }

            public int Height { get; private set; }
        }

        private sealed class TableSnapshot
        {
            private readonly float[] _columnWidths;

            public TableSnapshot(TableLayoutPanel table)
            {
                _columnWidths = new float[table.ColumnStyles.Count];
                for (int i = 0; i < table.ColumnStyles.Count; i++)
                {
                    _columnWidths[i] = table.ColumnStyles[i].Width;
                }
            }

            public float GetColumnWidth(int index, float fallback)
            {
                if (index < 0 || index >= _columnWidths.Length)
                {
                    return fallback;
                }

                return _columnWidths[index];
            }
        }
    }
}
