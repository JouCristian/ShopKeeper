using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class CreditPayment
    {
        public long Id { get; set; }

        public long CreditRecordId { get; set; }

        public DateTime PaymentDate { get; set; }

        public decimal Amount { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
