using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiLocalQueryService
    {
        private readonly ProductService _productService;
        private readonly ReportService _reportService;
        private readonly ScrapService _scrapService;

        public AiLocalQueryService()
        {
            _productService = new ProductService();
            _reportService = new ReportService();
            _scrapService = new ScrapService();
        }

        public string Answer(string userText, AiIntentResult intent)
        {
            string text = userText ?? string.Empty;
            string queryKind = intent == null ? string.Empty : intent.QueryKind;

            if (ContainsAny(text, "有哪些商品", "现在有什么商品", "商品列表", "全部商品", "所有商品", "店里有什么")
                || queryKind == "all_inventory")
            {
                return QueryAllInventory();
            }

            if (ContainsAny(text, "谁还欠", "谁欠账", "谁欠钱", "欠多少钱", "还欠账", "欠款多少", "欠账客户", "未结清赊账", "没结清", "未还清", "未收回")
                || queryKind == "credit_customers")
            {
                return QueryCreditCustomers(ExtractCustomerKeyword(text));
            }

            if (ContainsAny(text, "快过期", "临期", "要过期", "哪些商品过期") || queryKind == "expiring_products")
            {
                return QueryExpiringProducts();
            }

            if (ContainsAny(text, "库存低", "低库存", "快没货", "快没了", "没货了", "快卖完", "缺货", "缺货商品", "哪些商品少", "库存不够", "不够卖") || queryKind == "low_stock")
            {
                return QueryLowStockProducts();
            }

            if (ContainsAny(text, "报废损失", "有没有报废", "最近有没有报废", "报废多少", "报废损失多少") || queryKind == "scrap_loss")
            {
                return QueryScrapLoss(text);
            }

            if (ContainsAny(text, "不赚钱", "毛利低", "利润低", "利润少", "没利润", "亏钱") || queryKind == "low_profit_products")
            {
                return QueryLowProfitProducts();
            }

            string productName = ExtractProductKeyword(text);
            if (ContainsAny(text, "多少钱", "售价", "卖多少", "价格", "多少元", "多贵") || queryKind == "product_price")
            {
                return QueryProductPrice(productName);
            }

            if (ContainsAny(text, "库存", "还剩", "剩多少", "有多少", "还有多少", "还有几", "还剩几", "剩几") || queryKind == "product_stock")
            {
                return QueryProductStock(productName);
            }

            return QueryProductInfo(productName);
        }

        public string QueryProductPrice(string productName)
        {
            IList<Product> matches = FindProducts(productName);
            if (matches.Count == 0)
            {
                return BuildNoProductFound(productName);
            }

            if (matches.Count > 1)
            {
                return BuildProductCandidates(matches);
            }

            Product product = matches[0];
            return FormatProductName(product) + " 当前售价是 " + FormatMoney(product.DefaultPrice) + "，当前库存 " + FormatNumber(product.CurrentStock) + " 件。";
        }

        public string QueryProductStock(string productName)
        {
            IList<Product> matches = FindProducts(productName);
            if (matches.Count == 0)
            {
                return BuildNoProductFound(productName);
            }

            if (matches.Count > 1)
            {
                return BuildProductCandidates(matches);
            }

            Product product = matches[0];
            return FormatProductName(product) + " 当前库存是 " + FormatNumber(product.CurrentStock) + " 件，当前售价 " + FormatMoney(product.DefaultPrice) + "。";
        }

        public string QueryProductInfo(string productName)
        {
            IList<Product> matches = FindProducts(productName);
            if (matches.Count == 0)
            {
                return BuildNoProductFound(productName);
            }

            if (matches.Count > 1)
            {
                return BuildProductCandidates(matches);
            }

            Product product = matches[0];
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(FormatProductName(product) + " 的本地记录如下：");
            builder.AppendLine("分类：" + SafeText(product.CategoryName));
            builder.AppendLine("规格：" + SafeText(product.Specification));
            builder.AppendLine("当前库存：" + FormatNumber(product.CurrentStock) + " 件");
            builder.AppendLine("默认售价：" + FormatMoney(product.DefaultPrice));
            builder.AppendLine("库存均价：" + FormatMoney(product.AverageCost));
            builder.AppendLine("最低库存：" + FormatNumber(product.MinStockAlert));
            builder.AppendLine("保质期：" + (product.RequiresExpiry ? "启用" : "未启用"));
            builder.AppendLine("到期日期：" + (product.ExpiryDate.HasValue ? product.ExpiryDate.Value.ToString("yyyy-MM-dd") : "未填写"));
            builder.AppendLine("状态：" + SafeText(product.Status));
            return builder.ToString().TrimEnd();
        }

        public string QueryAllInventory()
        {
            IList<Product> products = _productService.GetActiveProducts();
            if (products.Count == 0)
            {
                return "当前商品管理里暂无在售商品。";
            }

            decimal totalStock = 0m;
            foreach (Product product in products)
            {
                totalStock += product.CurrentStock;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("当前在售商品 " + products.Count + " 个，总库存 " + FormatNumber(totalStock) + " 件：");
            for (int index = 0; index < products.Count; index++)
            {
                Product product = products[index];
                builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                    + "，分类 " + SafeText(product.CategoryName)
                    + "，库存 " + FormatNumber(product.CurrentStock)
                    + "，售价 " + FormatMoney(product.DefaultPrice)
                    + "，库存均价 " + FormatMoney(product.AverageCost));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryCreditCustomers()
        {
            return QueryCreditCustomers(string.Empty);
        }

        public string QueryCreditCustomers(string customerKeyword)
        {
            IList<CreditRecord> records = _reportService.GetOutstandingCreditRecordsForExport();
            List<CreditRecord> matches = new List<CreditRecord>();
            string keyword = Normalize(customerKeyword);
            foreach (CreditRecord record in records)
            {
                if (string.IsNullOrWhiteSpace(keyword) || Normalize(record.DebtorName).Contains(keyword))
                {
                    matches.Add(record);
                }
            }

            if (matches.Count == 0)
            {
                return string.IsNullOrWhiteSpace(customerKeyword)
                    ? "当前没有未结清的赊账记录。"
                    : "没有查到“" + customerKeyword.Trim() + "”的未结清赊账记录。";
            }

            decimal total = 0m;
            foreach (CreditRecord record in matches)
            {
                total += record.RemainingAmount;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("当前未结清赊账 " + matches.Count + " 条，合计 " + FormatMoney(total) + "：");
            for (int index = 0; index < matches.Count; index++)
            {
                CreditRecord record = matches[index];
                builder.AppendLine((index + 1) + ". " + SafeCustomerName(record.DebtorName)
                    + "，剩余欠款 " + FormatMoney(record.RemainingAmount)
                    + "，赊账日期 " + record.CreditDate.ToString("yyyy-MM-dd"));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryLowStockProducts()
        {
            IList<LowStockReportItem> items = _reportService.GetLowStockItems();
            if (items.Count == 0)
            {
                return "当前没有低库存商品。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("当前低库存商品 " + items.Count + " 个：");
            for (int index = 0; index < items.Count; index++)
            {
                LowStockReportItem item = items[index];
                builder.AppendLine((index + 1) + ". " + SafeText(item.ProductName)
                    + "，当前库存 " + FormatNumber(item.CurrentStock)
                    + "，预警线 " + FormatNumber(item.MinStockAlert));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryExpiringProducts()
        {
            IList<ExpiringProductReportItem> items = _reportService.GetExpiringProductsForExport();
            if (items.Count == 0)
            {
                return "当前没有即将临期的商品。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("当前临期商品 " + items.Count + " 条：");
            for (int index = 0; index < items.Count; index++)
            {
                ExpiringProductReportItem item = items[index];
                builder.AppendLine((index + 1) + ". " + SafeText(item.ProductName)
                    + "，剩余 " + FormatNumber(item.QuantityRemaining)
                    + "，到期日 " + item.ExpiryDate.ToString("yyyy-MM-dd")
                    + "，剩余 " + item.DaysRemaining + " 天");
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryScrapLoss(string userText)
        {
            IList<ScrapRecord> records = _scrapService.Search();
            List<ScrapRecord> matches = new List<ScrapRecord>();
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MaxValue;
            string scope = "全部";

            if (ContainsAny(userText, "今天", "今日"))
            {
                startTime = DateTime.Today;
                endTime = DateTime.Today.AddDays(1);
                scope = "今天";
            }
            else if (ContainsAny(userText, "最近"))
            {
                startTime = DateTime.Today.AddDays(-7);
                endTime = DateTime.Today.AddDays(1);
                scope = "最近 7 天";
            }

            foreach (ScrapRecord record in records)
            {
                if (record.ScrapDate >= startTime && record.ScrapDate < endTime)
                {
                    matches.Add(record);
                }
            }

            if (matches.Count == 0)
            {
                return scope + "没有看到报废损失记录。";
            }

            decimal totalLoss = 0m;
            decimal totalQuantity = 0m;
            foreach (ScrapRecord record in matches)
            {
                totalLoss += record.LossAmount;
                totalQuantity += record.Quantity;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(scope + "报废记录 " + matches.Count + " 条，报废数量 " + FormatNumber(totalQuantity) + " 件，损失合计 " + FormatMoney(totalLoss) + "：");
            for (int index = 0; index < matches.Count; index++)
            {
                ScrapRecord record = matches[index];
                builder.AppendLine((index + 1) + ". " + SafeText(record.ProductNameSnapshot)
                    + "，数量 " + FormatNumber(record.Quantity)
                    + "，损失 " + FormatMoney(record.LossAmount)
                    + "，原因 " + SafeText(record.Reason)
                    + "，日期 " + record.ScrapDate.ToString("yyyy-MM-dd"));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryLowProfitProducts()
        {
            IList<Product> products = _productService.GetActiveProducts();
            List<ProductProfitView> candidates = new List<ProductProfitView>();
            foreach (Product product in products)
            {
                if (product.DefaultPrice <= 0m || product.AverageCost <= 0m)
                {
                    continue;
                }

                decimal profit = product.DefaultPrice - product.AverageCost;
                decimal profitRate = product.DefaultPrice == 0m ? 0m : profit / product.DefaultPrice;
                if (profit <= 0m || profitRate < 0.15m)
                {
                    candidates.Add(new ProductProfitView(product, profit, profitRate));
                }
            }

            candidates.Sort(delegate (ProductProfitView left, ProductProfitView right)
            {
                int rateCompare = left.ProfitRate.CompareTo(right.ProfitRate);
                return rateCompare != 0 ? rateCompare : left.Profit.CompareTo(right.Profit);
            });

            if (candidates.Count == 0)
            {
                return products.Count == 0
                    ? "当前没有读取到商品记录，暂时无法判断哪些商品不赚钱。"
                    : "当前没有发现明显低毛利或亏钱商品。判断依据是：售价和库存均价都已填写，且毛利率低于 15% 或单件毛利小于等于 0。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("我按当前售价和库存均价看了一下，以下商品毛利偏低，需要重点关注：");
            for (int index = 0; index < candidates.Count; index++)
            {
                ProductProfitView item = candidates[index];
                builder.AppendLine((index + 1) + ". " + FormatProductName(item.Product)
                    + "，售价 " + FormatMoney(item.Product.DefaultPrice)
                    + "，库存均价 " + FormatMoney(item.Product.AverageCost)
                    + "，单件毛利 " + FormatMoney(item.Profit)
                    + "，毛利率 " + (item.ProfitRate * 100m).ToString("0.#") + "%");
            }

            return builder.ToString().TrimEnd();
        }

        private IList<Product> FindProducts(string keyword)
        {
            List<Product> products = new List<Product>(_productService.GetActiveProducts());
            List<Product> exact = new List<Product>();
            List<Product> fuzzy = new List<Product>();
            string normalizedKeyword = Normalize(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return fuzzy;
            }

            foreach (Product product in products)
            {
                string productName = Normalize(product.Name);
                string displayName = Normalize(FormatProductName(product));
                if (productName == normalizedKeyword || displayName == normalizedKeyword)
                {
                    exact.Add(product);
                    continue;
                }

                if (productName.Contains(normalizedKeyword) || displayName.Contains(normalizedKeyword) || normalizedKeyword.Contains(productName))
                {
                    fuzzy.Add(product);
                }
            }

            return exact.Count > 0 ? exact : fuzzy;
        }

        private static string ExtractProductKeyword(string text)
        {
            string value = text ?? string.Empty;
            value = Regex.Replace(value, @"(我只是|只是|好奇|目前|当前|现在|请问|帮我|查一下|查询|看看|一下|啊|呢|吗|？|\?)", string.Empty);
            value = Regex.Replace(value, @"(多少钱|售价是多少|售价|卖多少|价格是多少|价格|多少元|多贵|库存多少|还剩多少|还剩几瓶|还剩几包|还剩几袋|还剩几件|还剩几盒|还剩几条|还剩几罐|还剩几支|还剩几|还剩|剩多少|剩几瓶|剩几包|剩几袋|剩几件|剩几盒|剩几条|剩几罐|剩几支|剩几|库存|有多少|还有多少|还有几瓶|还有几包|还有几袋|还有几件|还有几盒|还有几条|还有几罐|还有几支|还有几)", string.Empty);
            value = value.Replace("的", string.Empty);
            return value.Trim();
        }

        private static string ExtractCustomerKeyword(string text)
        {
            string value = text ?? string.Empty;
            value = Regex.Replace(value, @"(今天|今日|现在|当前|之前|以前|历史|全部|所有|那|谁还欠账|谁还欠|谁欠账|谁欠钱|欠多少钱|还欠账|欠款多少|欠账客户|未结清赊账|没结清|未还清|未收回|多少钱|多少|吗|呢|？|\?)", string.Empty);
            return value.Trim();
        }

        private static string BuildProductCandidates(IList<Product> products)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("我找到几个相近商品：");
            for (int index = 0; index < products.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("、");
                }

                builder.Append(FormatProductName(products[index]));
            }

            builder.Append("。你想查哪一个？");
            return builder.ToString();
        }

        private static string BuildNoProductFound(string keyword)
        {
            string name = string.IsNullOrWhiteSpace(keyword) ? "这个商品" : "“" + keyword.Trim() + "”";
            return "系统里暂时没有找到" + name + "，可以换个名称试试。";
        }

        private static string FormatProductName(Product product)
        {
            if (product == null)
            {
                return string.Empty;
            }

            string spec = product.Specification ?? string.Empty;
            if (string.IsNullOrWhiteSpace(spec))
            {
                return SafeText(product.Name);
            }

            return (SafeText(product.Name) + " " + spec.Trim()).Trim();
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && (text ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2") + " 元";
        }

        private static string FormatNumber(decimal value)
        {
            return value.ToString("0.###");
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();
        }

        private static string SafeCustomerName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未留姓名客户" : value.Trim();
        }

        private sealed class ProductProfitView
        {
            public ProductProfitView(Product product, decimal profit, decimal profitRate)
            {
                Product = product;
                Profit = profit;
                ProfitRate = profitRate;
            }

            public Product Product { get; private set; }

            public decimal Profit { get; private set; }

            public decimal ProfitRate { get; private set; }
        }
    }
}
