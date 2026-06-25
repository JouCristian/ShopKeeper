using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class PurchaseService
    {
        private readonly PurchaseRepository _purchaseRepository;
        private readonly ProductRepository _productRepository;

        public PurchaseService()
        {
            string connectionString = DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath);
            _purchaseRepository = new PurchaseRepository(connectionString);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<PurchaseRecord> Search(DateTime startDate, DateTime endDate, string productKeyword)
        {
            if (endDate < startDate)
            {
                DateTime temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            return _purchaseRepository.Search(
                startDate.Date,
                endDate.Date,
                string.IsNullOrWhiteSpace(productKeyword) ? string.Empty : productKeyword.Trim());
        }

        public PurchaseRecord GetById(long id)
        {
            return _purchaseRepository.GetById(id);
        }

        public bool TrySave(PurchaseRecord record, out string message)
        {
            Normalize(record);
            if (!Validate(record, out message))
            {
                return false;
            }

            record.Id = _purchaseRepository.Save(record);
            message = "入库单已保存。";
            return true;
        }

        public bool TryDelete(long id, out string message)
        {
            try
            {
                _purchaseRepository.Delete(id);
                message = "入库单已删除，库存已同步扣回。";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool HasExpiryWarning(PurchaseRecord record)
        {
            foreach (PurchaseItem item in record.Items)
            {
                Product product = _productRepository.GetById(item.ProductId);
                if (product != null && product.RequiresExpiry && !item.ExpiryDate.HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Normalize(PurchaseRecord record)
        {
            if (record.PurchaseDate == DateTime.MinValue)
            {
                record.PurchaseDate = DateTime.Today;
            }

            record.Remark = (record.Remark ?? string.Empty).Trim();
            foreach (PurchaseItem item in record.Items)
            {
                item.Remark = (item.Remark ?? string.Empty).Trim();
                item.LineTotal = item.Quantity * item.PurchasePrice;
            }
        }

        private bool Validate(PurchaseRecord record, out string message)
        {
            if (record.Items == null || record.Items.Count == 0)
            {
                message = "请至少添加一条入库明细。";
                return false;
            }

            for (int i = 0; i < record.Items.Count; i++)
            {
                PurchaseItem item = record.Items[i];
                int rowNumber = i + 1;

                if (item.ProductId <= 0)
                {
                    message = "第 " + rowNumber + " 行商品不能为空。";
                    return false;
                }

                Product product = _productRepository.GetById(item.ProductId);
                if (product == null || product.Status != "在售")
                {
                    message = "第 " + rowNumber + " 行商品不存在或已停用。";
                    return false;
                }

                if (item.Quantity <= 0)
                {
                    message = "第 " + rowNumber + " 行入库数量必须大于 0。";
                    return false;
                }

                if (item.PurchasePrice < 0)
                {
                    message = "第 " + rowNumber + " 行进货单价不能小于 0。";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }
    }
}
