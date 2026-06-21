using System;
using System.Collections.Generic;

namespace XiaoPuZhangGui.Models
{
    internal sealed class CreditRecord
    {
        public CreditRecord()
        {
            Payments = new List<CreditPayment>();
        }

        public long Id { get; set; }

        public string CreditNo { get; set; }

        public long SalesOrderId { get; set; }

        public string SalesOrderNo { get; set; }

        public string DebtorName { get; set; }

        public decimal OriginalAmount { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal RemainingAmount { get; set; }

        public string Status { get; set; }

        public DateTime CreditDate { get; set; }

        public DateTime? SettledAt { get; set; }

        public string Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IList<CreditPayment> Payments { get; private set; }

        public string StatusText
        {
            get
            {
                if (Status == "Settled")
                {
                    return "已结清";
                }

                if (Status == "PartiallyPaid")
                {
                    return "部分还款";
                }

                return "未结清";
            }
        }
    }
}
