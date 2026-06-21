namespace XiaoPuZhangGui.Models
{
    internal sealed class ProductProfitRankItem
    {
        public long ProductId { get; set; }

        public string ProductName { get; set; }

        public decimal SalesQuantity { get; set; }

        public decimal SalesAmount { get; set; }

        public decimal ProductCost { get; set; }

        public decimal GrossProfit { get; set; }
    }
}
