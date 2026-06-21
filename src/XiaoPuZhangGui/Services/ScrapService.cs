using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class ScrapService
    {
        private readonly ScrapRepository _scrapRepository;
        private readonly ProductRepository _productRepository;

        public ScrapService()
            : this(DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath))
        {
        }

        internal ScrapService(string connectionString)
        {
            _scrapRepository = new ScrapRepository(connectionString);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<ScrapRecord> Search()
        {
            return _scrapRepository.Search();
        }

        public IList<Product> SearchActiveProducts(string keyword)
        {
            return _productRepository.Search(
                string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim(),
                null,
                "在售");
        }

        public bool TrySave(ScrapRecord record, out string message)
        {
            Normalize(record);

            if (!Validate(record, out message))
            {
                return false;
            }

            record.Id = _scrapRepository.Save(record);
            message = "报废记录已保存。";
            return true;
        }

        private static void Normalize(ScrapRecord record)
        {
            if (record.ScrapDate == DateTime.MinValue)
            {
                record.ScrapDate = DateTime.Today;
            }

            record.Reason = (record.Reason ?? string.Empty).Trim();
            record.Remark = (record.Remark ?? string.Empty).Trim();
        }

        private bool Validate(ScrapRecord record, out string message)
        {
            if (record.ProductId <= 0)
            {
                message = "请选择报废商品。";
                return false;
            }

            Product product = _productRepository.GetById(record.ProductId);
            if (product == null || product.Status != "在售")
            {
                message = "商品不存在或已停用。";
                return false;
            }

            if (record.Quantity <= 0)
            {
                message = "报废数量必须大于 0。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.Reason))
            {
                message = "请选择报废原因。";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
