using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class ProfitTrendPoint
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string Label { get; set; }

        public decimal GrossProfit { get; set; }

        public decimal ScrapLoss { get; set; }

        public decimal NetProfit
        {
            get { return GrossProfit - ScrapLoss; }
        }
    }
}
