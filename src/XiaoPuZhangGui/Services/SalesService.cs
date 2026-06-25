using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class SalesService
    {
        private readonly SalesRepository _salesRepository;
        private readonly ProductRepository _productRepository;

        public SalesService()
            : this(DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath))
        {
        }

        internal SalesService(string connectionString)
        {
            _salesRepository = new SalesRepository(connectionString);
            _productRepository = new ProductRepository(connectionString);
        }

        public IList<Product> GetActiveProducts()
        {
            return _productRepository.Search(string.Empty, null, "在售");
        }

        public IList<Product> SearchActiveProducts(string keyword)
        {
            return _productRepository.Search(
                string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim(),
                null,
                "在售");
        }

        public Product GetProduct(long productId)
        {
            return _productRepository.GetById(productId);
        }

        public IList<SalesOrder> GetTodayOrders()
        {
            DateTime start = DateTime.Today;
            DateTime end = DateTime.Today.AddDays(1).AddSeconds(-1);
            return _salesRepository.Search(start, end);
        }

        public SalesOrder GetById(long id)
        {
            return _salesRepository.GetById(id);
        }

        public bool TrySave(SalesOrder order, out string message)
        {
            Normalize(order);

            if (!Validate(order, out message))
            {
                return false;
            }

            order.Id = _salesRepository.Save(order);
            message = "销售单已保存。";
            return true;
        }

        public bool TryDelete(long id, out string message)
        {
            try
            {
                _salesRepository.Delete(id);
                message = "销售单已删除，库存已同步恢复。";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool HasStockShortage(SalesOrder order)
        {
            Dictionary<long, decimal> quantities = new Dictionary<long, decimal>();
            foreach (SalesItem item in order.Items)
            {
                if (!quantities.ContainsKey(item.ProductId))
                {
                    quantities[item.ProductId] = 0;
                }

                quantities[item.ProductId] += item.Quantity;
            }

            foreach (KeyValuePair<long, decimal> pair in quantities)
            {
                Product product = _productRepository.GetById(pair.Key);
                if (product != null && product.CurrentStock < pair.Value)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPriceBelowCost(SalesOrder order)
        {
            foreach (SalesItem item in order.Items)
            {
                Product product = _productRepository.GetById(item.ProductId);
                if (product != null && item.SalePriceSnapshot < product.AverageCost)
                {
                    return true;
                }
            }

            return false;
        }

        private void Normalize(SalesOrder order)
        {
            if (order.SaleTime == DateTime.MinValue)
            {
                order.SaleTime = DateTime.Now;
            }

            order.Remark = (order.Remark ?? string.Empty).Trim();
            order.TotalAmount = 0;
            order.TotalCost = 0;

            foreach (SalesItem item in order.Items)
            {
                Product product = _productRepository.GetById(item.ProductId);
                item.ProductNameSnapshot = product == null ? (item.ProductNameSnapshot ?? string.Empty) : product.Name;
                item.CostPriceSnapshot = product == null ? item.CostPriceSnapshot : product.AverageCost;
                item.LineAmount = item.Quantity * item.SalePriceSnapshot;
                item.LineCost = item.Quantity * item.CostPriceSnapshot;
                item.LineProfit = item.LineAmount - item.LineCost;

                order.TotalAmount += item.LineAmount;
                order.TotalCost += item.LineCost;
            }

            order.GrossProfit = order.TotalAmount - order.TotalCost;
            if (!order.PaidAmountSpecified)
            {
                order.PaidAmount = order.TotalAmount;
            }

            order.CreditAmount = order.PaidAmount < order.TotalAmount ? order.TotalAmount - order.PaidAmount : 0;
            order.DebtorName = (order.DebtorName ?? string.Empty).Trim();
        }

        private bool Validate(SalesOrder order, out string message)
        {
            if (order.Items == null || order.Items.Count == 0)
            {
                message = "请至少添加一条销售明细。";
                return false;
            }

            for (int i = 0; i < order.Items.Count; i++)
            {
                SalesItem item = order.Items[i];
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
                    message = "第 " + rowNumber + " 行销售数量必须大于 0。";
                    return false;
                }

                if (item.SalePriceSnapshot < 0)
                {
                    message = "第 " + rowNumber + " 行销售单价不能小于 0。";
                    return false;
                }
            }

            if (order.PaidAmount < 0)
            {
                message = "实收金额不能小于 0。";
                return false;
            }

            if (order.CreditAmount > 0 && string.IsNullOrWhiteSpace(order.DebtorName))
            {
                message = "存在赊账时，请填写欠款人备注。";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
