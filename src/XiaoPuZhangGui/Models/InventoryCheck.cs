using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class InventoryCheck
    {
        public InventoryCheck()
        {
            Items = new List<InventoryCheckItem>();
        }

        public long Id { get; set; }

        public string CheckNo { get; set; }

        public DateTime CheckDate { get; set; }

        public decimal TotalProfitQuantity { get; set; }

        public decimal TotalLossQuantity { get; set; }

        public decimal TotalProfitAmount { get; set; }

        public decimal TotalLossAmount { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int ProductKindCount { get; set; }

        public IList<InventoryCheckItem> Items { get; private set; }
    }
}
