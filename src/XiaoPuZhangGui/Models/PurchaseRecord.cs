using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class PurchaseRecord
    {
        public PurchaseRecord()
        {
            Items = new List<PurchaseItem>();
        }

        public long Id { get; set; }

        public string PurchaseNo { get; set; }

        public DateTime PurchaseDate { get; set; }

        public decimal TotalAmount { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IList<PurchaseItem> Items { get; private set; }

        public int ProductKindCount { get; set; }

        public decimal TotalQuantity { get; set; }
    }
}
