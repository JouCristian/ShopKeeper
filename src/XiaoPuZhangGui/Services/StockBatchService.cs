using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class StockBatchService
    {
        private readonly StockBatchRepository _stockBatchRepository;

        public StockBatchService()
        {
            string connectionString = DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath);
            _stockBatchRepository = new StockBatchRepository(connectionString);
        }

        public IList<StockBatch> GetByProductId(long productId)
        {
            return _stockBatchRepository.GetByProductId(productId);
        }
    }
}
