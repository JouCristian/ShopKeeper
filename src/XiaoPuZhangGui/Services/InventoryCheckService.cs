using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class InventoryCheckService
    {
        private readonly InventoryCheckRepository _inventoryCheckRepository;
        private readonly ProductRepository _productRepository;

        public InventoryCheckService()
            : this(DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath))
        {
        }

        internal InventoryCheckService(string connectionString)
        {
            _inventoryCheckRepository = new InventoryCheckRepository(connectionString);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<InventoryCheck> Search(string keyword, long? categoryId)
        {
            return _inventoryCheckRepository.Search(
                string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim(),
                categoryId);
        }

        public InventoryCheck GetById(long id)
        {
            return _inventoryCheckRepository.GetById(id);
        }

        public IList<Product> SearchActiveProducts(string keyword)
        {
            return _productRepository.Search(
                string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim(),
                null,
                "在售");
        }

        public bool TrySave(InventoryCheck record, out string message)
        {
            Normalize(record);

            if (!Validate(record, out message))
            {
                return false;
            }

            record.Id = _inventoryCheckRepository.Save(record);
            message = "盘点单已保存。";
            return true;
        }

        public bool TryDelete(long id, out string message)
        {
            try
            {
                _inventoryCheckRepository.Delete(id);
                message = "盘点单已删除，库存已反向调整。";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static void Normalize(InventoryCheck record)
        {
            if (record.CheckDate == DateTime.MinValue)
            {
                record.CheckDate = DateTime.Today;
            }

            record.Remark = (record.Remark ?? string.Empty).Trim();

            foreach (InventoryCheckItem item in record.Items)
            {
                item.Reason = (item.Reason ?? string.Empty).Trim();
                item.Remark = (item.Remark ?? string.Empty).Trim();
            }
        }

        private bool Validate(InventoryCheck record, out string message)
        {
            if (record.Items == null || record.Items.Count == 0)
            {
                message = "请至少添加一条盘点明细。";
                return false;
            }

            for (int i = 0; i < record.Items.Count; i++)
            {
                InventoryCheckItem item = record.Items[i];
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

                if (item.ActualStock < 0)
                {
                    message = "第 " + rowNumber + " 行实际库存不能小于 0。";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }
    }
}
