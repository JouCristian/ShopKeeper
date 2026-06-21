using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class SalesOrder
    {
        public SalesOrder()
        {
            Items = new List<SalesItem>();
        }

        public long Id { get; set; }

        public string OrderNo { get; set; }

        public DateTime SaleTime { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal TotalCost { get; set; }

        public decimal GrossProfit { get; set; }

        public decimal PaidAmount { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public int ProductKindCount { get; set; }

        public decimal TotalQuantity { get; set; }

        public IList<SalesItem> Items { get; private set; }
    }
}
