namespace XiaoPuZhangGui.Models
{
    internal sealed class ProductSalesRankItem
    {
        public long ProductId { get; set; }

        public string ProductName { get; set; }

        public decimal SalesQuantity { get; set; }

        public decimal SalesAmount { get; set; }
    }
}
