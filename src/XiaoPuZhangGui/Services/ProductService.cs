using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class ProductService
    {
        private readonly ProductRepository _productRepository;

        public ProductService()
        {
            string connectionString = DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<Product> Search(string keyword, long? categoryId, string status)
        {
            return _productRepository.Search(
                string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim(),
                categoryId,
                string.IsNullOrWhiteSpace(status) ? "全部" : status);
        }

        public IList<Product> GetActiveProducts()
        {
            return _productRepository.Search(string.Empty, null, "在售");
        }

        public Product GetById(long id)
        {
            return _productRepository.GetById(id);
        }

        public bool TrySave(Product product, out string message)
        {
            Normalize(product);

            if (!Validate(product, out message))
            {
                return false;
            }

            if (product.Id > 0)
            {
                _productRepository.Update(product);
                message = "商品信息已保存。";
            }
            else
            {
                product.Status = "在售";
                product.Id = _productRepository.Insert(product);
                message = "商品已新增。";
            }

            return true;
        }

        public void SetStatus(long id, bool enabled)
        {
            _productRepository.SetStatus(id, enabled ? "在售" : "停用");
        }

        public bool TryDelete(long id, out string message)
        {
            try
            {
                _productRepository.Delete(id);
                message = "商品已删除，历史记录仍会保留。";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static void Normalize(Product product)
        {
            product.Name = (product.Name ?? string.Empty).Trim();
            product.Barcode = (product.Barcode ?? string.Empty).Trim();
            product.Specification = (product.Specification ?? string.Empty).Trim();
            product.Remark = (product.Remark ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(product.Status))
            {
                product.Status = "在售";
            }

            if (!product.RequiresExpiry)
            {
                product.ExpiryDate = null;
            }
        }

        private static bool Validate(Product product, out string message)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
            {
                message = "商品名称不能为空。";
                return false;
            }

            if (product.CategoryId <= 0)
            {
                message = "请选择商品分类。";
                return false;
            }

            if (product.DefaultPrice < 0)
            {
                message = "默认售价不能小于 0。";
                return false;
            }

            if (product.CurrentStock < 0)
            {
                message = "当前库存不能小于 0。";
                return false;
            }

            if (product.AverageCost < 0)
            {
                message = "库存均价不能小于 0。";
                return false;
            }

            if (product.MinStockAlert < 0)
            {
                message = "最低库存不能小于 0。";
                return false;
            }

            if (product.Status != "在售" && product.Status != "停用" && product.Status != "已删除")
            {
                message = "商品状态不正确。";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
