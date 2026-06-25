using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class ReportService
    {
        private const int DefaultRankLimit = 10;
        private const int DefaultLowStockLimit = 20;
        private const int DefaultExpiringLimit = 20;
        private const int DefaultScrapLimit = 20;
        private const int DefaultExpiringDays = 15;

        private readonly ReportRepository _reportRepository;

        public ReportService()
            : this(DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath))
        {
        }

        internal ReportService(string connectionString)
        {
            _reportRepository = new ReportRepository(connectionString);
        }

        public ReportSummary GetSummary(DateTime startTime, DateTime endTime)
        {
            NormalizeRange(ref startTime, ref endTime);
            return _reportRepository.GetSummary(startTime, endTime);
        }

        public IList<ProductSalesRankItem> GetProductSalesRank(DateTime startTime, DateTime endTime)
        {
            NormalizeRange(ref startTime, ref endTime);
            return _reportRepository.GetProductSalesRank(startTime, endTime, DefaultRankLimit);
        }

        public IList<ProductProfitRankItem> GetProductProfitRank(DateTime startTime, DateTime endTime)
        {
            NormalizeRange(ref startTime, ref endTime);
            return _reportRepository.GetProductProfitRank(startTime, endTime, DefaultRankLimit);
        }

        public IList<LowStockReportItem> GetLowStockItems()
        {
            return _reportRepository.GetLowStockItems(DefaultLowStockLimit);
        }

        public IList<ExpiringProductReportItem> GetExpiringProducts()
        {
            return _reportRepository.GetExpiringProducts(DateTime.Today, DefaultExpiringDays, DefaultExpiringLimit);
        }

        public IList<ExpiringProductReportItem> GetExpiringProductsForExport()
        {
            return _reportRepository.GetExpiringProducts(DateTime.Today, DefaultExpiringDays, 10000);
        }

        public IList<Product> GetInventoryItemsForExport()
        {
            return _reportRepository.GetInventoryItems();
        }

        public IList<CreditRecord> GetOutstandingCreditRecordsForExport()
        {
            return _reportRepository.GetOutstandingCreditRecords();
        }

        public IList<ScrapSummaryItem> GetScrapSummary(DateTime startTime, DateTime endTime)
        {
            NormalizeRange(ref startTime, ref endTime);
            return _reportRepository.GetScrapSummary(startTime, endTime, DefaultScrapLimit);
        }

        public IList<ProfitTrendPoint> GetProfitTrend(DateTime startTime, DateTime endTime, TimeSpan bucketDuration, int bucketMonths)
        {
            NormalizeRange(ref startTime, ref endTime);
            return _reportRepository.GetProfitTrend(startTime, endTime, bucketDuration, bucketMonths);
        }

        public static DateTime GetDayStart(DateTime day)
        {
            return day.Date;
        }

        public static DateTime GetNextDayStart(DateTime day)
        {
            return day.Date.AddDays(1);
        }

        public static DateTime GetMonthStart(DateTime day)
        {
            return new DateTime(day.Year, day.Month, 1);
        }

        public static DateTime GetNextMonthStart(DateTime day)
        {
            return GetMonthStart(day).AddMonths(1);
        }

        private static void NormalizeRange(ref DateTime startTime, ref DateTime endTime)
        {
            if (endTime <= startTime)
            {
                endTime = startTime.Date.AddDays(1);
            }
        }
    }
}
