namespace XiaoPuZhangGui.Models
{
    internal sealed class LowStockReportItem
    {
        public string ProductName { get; set; }

        public string CategoryName { get; set; }

        public decimal CurrentStock { get; set; }

        public decimal MinStockAlert { get; set; }
    }
}
