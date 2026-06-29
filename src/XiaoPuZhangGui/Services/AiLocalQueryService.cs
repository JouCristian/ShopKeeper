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
        private readonly CategoryService _categoryService;

        public AiLocalQueryService()
        {
            _productService = new ProductService();
            _reportService = new ReportService();
            _scrapService = new ScrapService();
            _categoryService = new CategoryService();
        }

        public string Answer(string userText, AiIntentResult intent)
        {
            string text = userText ?? string.Empty;
            string queryKind = intent == null ? string.Empty : intent.QueryKind;

            if (queryKind == "restock_advice")
            {
                return QueryRestockAdvice(ExtractOptionalCategoryKeyword(text));
            }

            if (queryKind == "new_product_advice")
            {
                return QueryNewProductAdvice();
            }

            if (queryKind == "category_low_stock")
            {
                return QueryLowStockProducts(ExtractCategoryKeyword(text));
            }

            if (queryKind == "category_stock" || queryKind == "category_query" || LooksLikeCategoryInventoryQuestion(text))
            {
                string categoryName = ExtractCategoryKeyword(text);
                if (ContainsAny(text, "快没货", "快没了", "低库存", "库存低", "缺货", "该补", "补货", "需要补", "不够卖"))
                {
                    return QueryLowStockProducts(categoryName);
                }

                return QueryProductsByCategory(categoryName);
            }

            if (ContainsAny(text, "有哪些商品", "现在有什么商品", "商品列表", "全部商品", "所有商品", "店里有什么")
                || queryKind == "all_inventory")
            {
                return QueryAllInventory();
            }

            if (ContainsAny(text, "谁还欠", "谁欠账", "谁欠钱", "谁赊账", "有谁赊账", "有没有人赊账", "有哪些赊账客户", "赊账客户", "欠多少钱", "还欠账", "欠款多少", "欠账客户", "未结清赊账", "没结清", "未还清", "未收回", "谁没给钱", "谁还没结账", "谁还欠钱", "还没还钱")
                || queryKind == "credit_customers")
            {
                return QueryCreditCustomers(ExtractCustomerKeyword(text), text);
            }

            if (ContainsAny(text, "快过期", "临期", "要过期", "哪些商品过期") || queryKind == "expiring_products")
            {
                return QueryExpiringProducts();
            }

            if (ContainsAny(text, "库存低", "低库存", "快没货", "快没了", "没货了", "快卖完", "缺货", "缺货商品", "哪些商品少", "库存不够", "不够卖", "哪些货该补", "该补哪些", "需要补货") || queryKind == "low_stock")
            {
                return QueryLowStockProducts(ExtractCategoryKeyword(text));
            }

            if (ContainsAny(text, "报废记录", "报废损失", "有没有报废", "最近有没有报废", "报废多少", "报废损失多少") || queryKind == "scrap_loss" || queryKind == "scrap_records")
            {
                return QueryScrapRecords(ResolveScrapScope(text, queryKind));
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

        public IList<Product> FindProductCandidatesForText(string userText)
        {
            return FindProducts(ExtractProductKeyword(userText));
        }

        public string AnswerForProducts(IList<Product> products, string queryKind)
        {
            if (products == null || products.Count == 0)
            {
                return "没有找到可查询的候选商品，可以换个名称试试。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("我把这些候选商品都查了一遍：");
            for (int index = 0; index < products.Count; index++)
            {
                Product product = products[index];
                builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                    + "，库存 " + FormatNumber(product.CurrentStock) + " 件"
                    + "，售价 " + FormatMoney(product.DefaultPrice)
                    + "，库存均价 " + FormatMoney(product.AverageCost));
            }

            return builder.ToString().TrimEnd();
        }

        public string AnswerForProduct(Product product, string queryKind)
        {
            if (product == null)
            {
                return "没有找到这个候选商品，可以换个名称试试。";
            }

            if (queryKind == "product_price")
            {
                return FormatProductName(product) + " 当前售价是 " + FormatMoney(product.DefaultPrice) + "，当前库存 " + FormatNumber(product.CurrentStock) + " 件。";
            }

            if (queryKind == "product_stock")
            {
                return FormatProductName(product) + " 当前库存是 " + FormatNumber(product.CurrentStock) + " 件，当前售价 " + FormatMoney(product.DefaultPrice) + "。";
            }

            return FormatProductName(product) + " 当前库存 " + FormatNumber(product.CurrentStock) + " 件，售价 " + FormatMoney(product.DefaultPrice) + "，库存均价 " + FormatMoney(product.AverageCost) + "。";
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

        public string QueryProductsByCategory(string categoryName)
        {
            Category category = ResolveCategory(categoryName);
            if (category == null)
            {
                string name = string.IsNullOrWhiteSpace(categoryName) ? "这个分类" : "“" + categoryName.Trim() + "”";
                return "系统里暂时没有找到" + name + "分类。当前已有分类：" + BuildCategoryListText() + "。";
            }

            IList<Product> products = _productService.GetActiveProducts();
            List<Product> matches = new List<Product>();
            decimal totalStock = 0m;
            foreach (Product product in products)
            {
                if (product != null && product.CategoryId == category.Id)
                {
                    matches.Add(product);
                    totalStock += product.CurrentStock;
                }
            }

            if (matches.Count == 0)
            {
                return "已找到“" + category.Name + "”分类，但这个分类下当前没有在售商品。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("“" + category.Name + "”分类下当前有 " + matches.Count + " 个在售商品，总库存 " + FormatNumber(totalStock) + " 件：");
            for (int index = 0; index < matches.Count; index++)
            {
                Product product = matches[index];
                builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                    + "，库存 " + FormatNumber(product.CurrentStock)
                    + "，最低库存 " + FormatNumber(product.MinStockAlert)
                    + "，售价 " + FormatMoney(product.DefaultPrice)
                    + "，成本价 " + FormatMoney(product.AverageCost)
                    + "，状态 " + ResolveInventoryStatus(product));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryCreditCustomers()
        {
            return QueryCreditCustomers(string.Empty);
        }

        public string QueryCreditCustomers(string customerKeyword)
        {
            return QueryCreditCustomers(customerKeyword, string.Empty);
        }

        public string QueryCreditCustomers(string customerKeyword, string scopeText)
        {
            IList<CreditRecord> records = _reportService.GetOutstandingCreditRecordsForExport();
            List<CreditRecord> matches = new List<CreditRecord>();
            string keyword = Normalize(customerKeyword);
            bool todayOnly = ContainsAny(scopeText, "今天", "今日");
            foreach (CreditRecord record in records)
            {
                if (todayOnly && record.CreditDate.Date != DateTime.Today)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(keyword) || Normalize(record.DebtorName).Contains(keyword))
                {
                    matches.Add(record);
                }
            }

            if (matches.Count == 0)
            {
                if (todayOnly)
                {
                    return BuildNoTodayCreditMessage(records);
                }

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
            builder.AppendLine((todayOnly ? "今天未结清赊账 " : "当前未结清赊账 ") + matches.Count + " 条，合计 " + FormatMoney(total) + "：");
            for (int index = 0; index < matches.Count; index++)
            {
                CreditRecord record = matches[index];
                builder.AppendLine((index + 1) + ". " + SafeCustomerName(record.DebtorName)
                    + "，剩余欠款 " + FormatMoney(record.RemainingAmount)
                    + "，赊账日期 " + record.CreditDate.ToString("yyyy-MM-dd"));
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildNoTodayCreditMessage(IList<CreditRecord> allOutstanding)
        {
            if (allOutstanding == null || allOutstanding.Count == 0)
            {
                return "今天没有看到新增未结清赊账记录，当前也没有未结清赊账。";
            }

            decimal total = 0m;
            foreach (CreditRecord record in allOutstanding)
            {
                total += record.RemainingAmount;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("今天没有看到新增未结清赊账记录。");
            builder.AppendLine("不过当前还有历史未结清赊账 " + allOutstanding.Count + " 条，合计 " + FormatMoney(total) + "：");
            for (int index = 0; index < allOutstanding.Count; index++)
            {
                CreditRecord record = allOutstanding[index];
                builder.AppendLine((index + 1) + ". " + SafeCustomerName(record.DebtorName)
                    + "，剩余欠款 " + FormatMoney(record.RemainingAmount)
                    + "，赊账日期 " + record.CreditDate.ToString("yyyy-MM-dd"));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryLowStockProducts()
        {
            return QueryLowStockProducts(string.Empty);
        }

        public string QueryLowStockProducts(string categoryName)
        {
            Category category = ResolveCategory(categoryName);
            if (!string.IsNullOrWhiteSpace(categoryName) && category == null)
            {
                return "系统里暂时没有找到“" + categoryName.Trim() + "”分类。当前已有分类：" + BuildCategoryListText() + "。";
            }

            IList<Product> products = _productService.GetActiveProducts();
            List<Product> matches = new List<Product>();
            foreach (Product product in products)
            {
                if (category != null && product.CategoryId != category.Id)
                {
                    continue;
                }

                if (IsLowStock(product))
                {
                    matches.Add(product);
                }
            }

            if (matches.Count == 0)
            {
                return category == null
                    ? "当前没有低库存商品。"
                    : "当前“" + category.Name + "”分类下没有低库存商品。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine((category == null ? "当前" : "当前“" + category.Name + "”分类下") + "低库存商品 " + matches.Count + " 个：");
            for (int index = 0; index < matches.Count; index++)
            {
                Product product = matches[index];
                builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                    + "，当前库存 " + FormatNumber(product.CurrentStock)
                    + "，预警线 " + FormatNumber(product.MinStockAlert)
                    + "，售价 " + FormatMoney(product.DefaultPrice));
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryRestockAdvice(string categoryName)
        {
            Category category = ResolveCategory(categoryName);
            if (!string.IsNullOrWhiteSpace(categoryName) && category == null)
            {
                return "系统里暂时没有找到“" + categoryName.Trim() + "”分类，暂时不能按这个范围给补货建议。当前已有分类：" + BuildCategoryListText() + "。";
            }

            IList<Product> products = _productService.GetActiveProducts();
            List<Product> scopedProducts = new List<Product>();
            foreach (Product product in products)
            {
                if (category != null && product.CategoryId != category.Id)
                {
                    continue;
                }

                scopedProducts.Add(product);
            }

            if (scopedProducts.Count == 0)
            {
                return category == null
                    ? "当前没有读取到在售商品，暂时无法给补货建议。"
                    : "当前“" + category.Name + "”分类下没有在售商品，暂时无法给补货建议。";
            }

            Dictionary<long, ProductSalesRankItem> salesByProductId = LoadRecentSalesRank();
            bool hasSalesData = salesByProductId.Count > 0;
            List<Product> mustRestock = new List<Product>();
            List<Product> watchList = new List<Product>();
            List<Product> avoidOverstock = new List<Product>();

            foreach (Product product in scopedProducts)
            {
                if (product.CurrentStock <= 0m || (product.MinStockAlert > 0m && product.CurrentStock <= product.MinStockAlert))
                {
                    mustRestock.Add(product);
                    continue;
                }

                if ((product.MinStockAlert > 0m && product.CurrentStock <= product.MinStockAlert * 1.5m)
                    || (product.MinStockAlert <= 0m && product.CurrentStock <= 3m))
                {
                    watchList.Add(product);
                    continue;
                }

                ProductSalesRankItem sales;
                bool hasProductSales = salesByProductId.TryGetValue(product.Id, out sales) && sales.SalesQuantity > 0m;
                if (!hasProductSales && product.CurrentStock >= Math.Max(product.MinStockAlert * 3m, 10m))
                {
                    avoidOverstock.Add(product);
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(category == null ? "根据当前库存，我给你的补货建议如下：" : "只看“" + category.Name + "”分类，补货建议如下：");
            if (!hasSalesData)
            {
                builder.AppendLine("目前近期销售数据不足，本次主要根据当前库存和最低库存提醒来判断。");
            }
            else
            {
                builder.AppendLine("已参考最近 30 天销售记录，同时结合当前库存和最低库存提醒。");
            }

            AppendRestockSection(builder, "必须优先补货", mustRestock, salesByProductId);
            AppendRestockSection(builder, "建议关注", watchList, salesByProductId);
            AppendRestockSection(builder, "暂不建议多进", avoidOverstock, salesByProductId);

            if (mustRestock.Count == 0 && watchList.Count == 0)
            {
                builder.AppendLine("目前没有明显必须补货的商品，可以先观察实际销售速度，避免一次性压太多库存。");
            }

            return builder.ToString().TrimEnd();
        }

        public string QueryNewProductAdvice()
        {
            IList<Product> products = _productService.GetActiveProducts();
            IList<Category> categories = _categoryService.GetActiveCategories();
            Dictionary<string, int> categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Category category in categories)
            {
                if (category != null && !string.IsNullOrWhiteSpace(category.Name) && !categoryCounts.ContainsKey(category.Name.Trim()))
                {
                    categoryCounts.Add(category.Name.Trim(), 0);
                }
            }

            foreach (Product product in products)
            {
                if (product == null)
                {
                    continue;
                }

                string categoryName = SafeText(product.CategoryName);
                if (!categoryCounts.ContainsKey(categoryName))
                {
                    categoryCounts.Add(categoryName, 0);
                }

                categoryCounts[categoryName]++;
                if (!string.IsNullOrWhiteSpace(product.Name))
                {
                    existingNames.Add(product.Name.Trim());
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("这是新品拓展建议，不是补已有库存。");
            builder.AppendLine("我会参考现有分类和库存结构，给你一些适合小卖铺少量试卖的方向。");
            builder.AppendLine();

            if (categoryCounts.Count == 0)
            {
                builder.AppendLine("当前商品分类还比较少，可以先从饮料、零食、烟酒、日用品这几类小卖铺常用品类里少量试卖。");
            }
            else
            {
                builder.AppendLine("当前已有分类：" + BuildCategoryCountText(categoryCounts));
            }

            AppendNewProductGroup(builder, "饮料/水类", new string[] { "矿泉水小瓶", "无糖茶", "电解质水", "冰红茶", "苏打水" }, existingNames);
            AppendNewProductGroup(builder, "零食小吃", new string[] { "瓜子", "花生", "辣条", "蛋黄派", "小面包", "口香糖" }, existingNames);
            AppendNewProductGroup(builder, "方便食品", new string[] { "泡面", "火腿肠", "卤蛋", "自热米饭", "八宝粥" }, existingNames);
            AppendNewProductGroup(builder, "日用品应急", new string[] { "纸巾", "打火机", "电池", "创可贴", "一次性雨衣" }, existingNames);

            builder.AppendLine();
            builder.AppendLine("建议做法：每个新品先少量进 3 到 10 件试卖，卖得动再补，不要一次性压太多货。");
            builder.AppendLine("如果店附近学生多，可以优先试零食和饮料；如果老住户多，可以优先试纸巾、电池、打火机这类应急品。");
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
            return QueryScrapRecords(ResolveScrapScope(userText, "scrap_loss"));
        }

        public string QueryScrapRecords(string scope)
        {
            IList<ScrapRecord> records = _scrapService.Search();
            List<ScrapRecord> matches = new List<ScrapRecord>();
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MaxValue;
            string scopeName = "全部";
            string normalizedScope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.Trim().ToLowerInvariant();

            if (normalizedScope == "today")
            {
                startTime = DateTime.Today;
                endTime = DateTime.Today.AddDays(1);
                scopeName = "今天 " + DateTime.Today.ToString("yyyy-MM-dd");
            }
            else if (normalizedScope == "recent")
            {
                startTime = DateTime.Today.AddDays(-7);
                endTime = DateTime.Today.AddDays(1);
                scopeName = "最近 7 天";
            }
            else if (normalizedScope == "month")
            {
                startTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                endTime = startTime.AddMonths(1);
                scopeName = "本月 " + startTime.ToString("yyyy-MM");
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
                return "本次查询范围：" + scopeName + "，没有看到报废记录。";
            }

            decimal totalLoss = 0m;
            decimal totalQuantity = 0m;
            foreach (ScrapRecord record in matches)
            {
                totalLoss += record.LossAmount;
                totalQuantity += record.Quantity;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("本次查询范围：" + scopeName + "。");
            builder.AppendLine("报废记录 " + matches.Count + " 条，报废数量 " + FormatNumber(totalQuantity) + " 件，损失合计 " + FormatMoney(totalLoss) + "：");
            for (int index = 0; index < matches.Count; index++)
            {
                ScrapRecord record = matches[index];
                builder.AppendLine((index + 1) + ". " + SafeText(record.ProductNameSnapshot)
                    + "，数量 " + FormatNumber(record.Quantity)
                    + "，成本价 " + FormatMoney(record.CostPriceSnapshot)
                    + "，损失 " + FormatMoney(record.LossAmount)
                    + "，原因 " + SafeText(record.Reason)
                    + "，日期 " + record.ScrapDate.ToString("yyyy-MM-dd HH:mm:ss"));
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

        public string ResolveCategoryNameFromText(string text)
        {
            Category category = ResolveCategory(ExtractCategoryKeyword(text));
            return category == null ? string.Empty : category.Name;
        }

        private string ExtractOptionalCategoryKeyword(string text)
        {
            string value = text ?? string.Empty;
            string category = ExtractCategoryKeyword(value);
            if (string.IsNullOrWhiteSpace(category))
            {
                return string.Empty;
            }

            if (ResolveCategory(category) != null)
            {
                return category;
            }

            return string.Empty;
        }

        private Category ResolveCategory(string categoryName)
        {
            string keyword = Normalize(categoryName);
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return null;
            }

            foreach (Category category in _categoryService.GetActiveCategories())
            {
                string categoryText = Normalize(category.Name);
                if (categoryText == keyword || categoryText.Contains(keyword) || keyword.Contains(categoryText))
                {
                    return category;
                }
            }

            string aliasTarget = ResolveCategoryAlias(keyword);
            if (!string.IsNullOrWhiteSpace(aliasTarget))
            {
                string normalizedAliasTarget = Normalize(aliasTarget);
                foreach (Category category in _categoryService.GetActiveCategories())
                {
                    string categoryText = Normalize(category.Name);
                    if (categoryText == normalizedAliasTarget || categoryText.Contains(normalizedAliasTarget) || normalizedAliasTarget.Contains(categoryText))
                    {
                        return category;
                    }
                }
            }

            return null;
        }

        private string ExtractCategoryKeyword(string text)
        {
            string value = text ?? string.Empty;
            foreach (Category category in _categoryService.GetActiveCategories())
            {
                if (!string.IsNullOrWhiteSpace(category.Name)
                    && value.IndexOf(category.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return category.Name;
                }
            }

            string alias = ResolveCategoryAlias(value);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                return alias;
            }

            value = Regex.Replace(value, @"(我说的是|对就是|对，就是|就是|目前|当前|现在|库存里|店里|有哪些|有什么|商品|分类|类|的|嘛|吗|呢|啊|？|\?)", string.Empty);
            value = Regex.Replace(value, @"(查一下|查询|看看|帮我|你不知道|所有|全部|一类|这一类|都)", string.Empty);
            return value.Trim();
        }

        private static string ResolveCategoryAlias(string text)
        {
            string value = Normalize(text);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (ContainsAny(value, "喝的", "饮品", "饮料类", "饮料", "酒水"))
            {
                return "饮料";
            }

            if (ContainsAny(value, "水") && ContainsAny(value, "有哪些", "有什么", "哪些", "喝的", "饮品", "饮料", "分类", "类", "库存里", "店里"))
            {
                return "饮料";
            }

            if (ContainsAny(value, "烟酒", "香烟", "烟草", "抽的"))
            {
                return "烟酒";
            }

            if (ContainsAny(value, "烟") && ContainsAny(value, "有哪些", "有什么", "哪些", "分类", "类", "库存", "目前", "当前", "现在", "店里"))
            {
                return "烟酒";
            }

            if (ContainsAny(value, "零食", "吃的", "小吃", "薯片", "辣条"))
            {
                return "零食";
            }

            if (ContainsAny(value, "日用品", "生活用品", "用的"))
            {
                return "日用品";
            }

            return string.Empty;
        }

        private string ResolveScrapScope(string userText, string queryKind)
        {
            string text = userText ?? string.Empty;
            if (ContainsAny(text, "所有", "全部", "全部报废", "所有报废"))
            {
                return "all";
            }

            if (ContainsAny(text, "今天", "今日"))
            {
                return "today";
            }

            if (ContainsAny(text, "最近", "近几天", "这一周", "近一周"))
            {
                return "recent";
            }

            if (ContainsAny(text, "这个月", "本月", "月报废"))
            {
                return "month";
            }

            return queryKind == "scrap_loss" ? "today" : "all";
        }

        private string ResolveScrapScope(string userText)
        {
            return ResolveScrapScope(userText, string.Empty);
        }

        private string ResolveScrapScopeFromKind(string queryKind)
        {
            return ResolveScrapScope(string.Empty, queryKind);
        }

        private bool LooksLikeCategoryInventoryQuestion(string text)
        {
            if (!ContainsAny(text, "哪些", "有哪些", "有什么", "商品", "库存", "分类", "类"))
            {
                return false;
            }

            return ResolveCategory(ExtractCategoryKeyword(text)) != null;
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
            value = Regex.Replace(value, @"(今天|今日|现在|当前|之前|以前|历史|全部|所有|那|有谁赊账了嘛|有谁赊账|谁赊账了|谁赊账|有没有人赊账|有哪些赊账客户|赊账客户|谁还欠账|谁还欠|谁欠账|谁欠钱|谁没给钱|谁还没结账|谁还欠钱|还没还钱|欠多少钱|还欠账|欠款多少|欠账客户|未结清赊账|没结清|未还清|未收回|多少钱|多少|嘛|吗|呢|？|\?)", string.Empty);
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

        private string BuildCategoryListText()
        {
            IList<Category> categories = _categoryService.GetActiveCategories();
            if (categories.Count == 0)
            {
                return "暂无分类";
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < categories.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("、");
                }

                builder.Append(SafeText(categories[index].Name));
            }

            return builder.ToString();
        }

        private static string BuildCategoryCountText(IDictionary<string, int> categoryCounts)
        {
            StringBuilder builder = new StringBuilder();
            int index = 0;
            foreach (KeyValuePair<string, int> item in categoryCounts)
            {
                if (index > 0)
                {
                    builder.Append("、");
                }

                builder.Append(item.Key).Append(" ").Append(item.Value).Append(" 个");
                index++;
            }

            return builder.ToString();
        }

        private static void AppendNewProductGroup(StringBuilder builder, string title, IEnumerable<string> candidates, ISet<string> existingNames)
        {
            List<string> missing = new List<string>();
            foreach (string candidate in candidates)
            {
                if (!ContainsSimilarProduct(existingNames, candidate))
                {
                    missing.Add(candidate);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine(title + "可以试：");
            int count = Math.Min(missing.Count, 5);
            for (int index = 0; index < count; index++)
            {
                builder.AppendLine((index + 1) + ". " + missing[index]);
            }
        }

        private static bool ContainsSimilarProduct(ISet<string> existingNames, string candidate)
        {
            if (existingNames == null || string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string normalizedCandidate = Normalize(candidate);
            foreach (string existingName in existingNames)
            {
                string normalizedExisting = Normalize(existingName);
                if (normalizedExisting.Contains(normalizedCandidate) || normalizedCandidate.Contains(normalizedExisting))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLowStock(Product product)
        {
            if (product == null)
            {
                return false;
            }

            return product.CurrentStock <= 0m
                || (product.MinStockAlert > 0m && product.CurrentStock <= product.MinStockAlert);
        }

        private static string ResolveInventoryStatus(Product product)
        {
            if (product == null)
            {
                return "未知";
            }

            if (IsLowStock(product))
            {
                return "库存偏低";
            }

            if (product.RequiresExpiry && product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date <= DateTime.Today.AddDays(15))
            {
                return "临期需关注";
            }

            return string.IsNullOrWhiteSpace(product.Status) ? "库存正常" : product.Status.Trim();
        }

        private Dictionary<long, ProductSalesRankItem> LoadRecentSalesRank()
        {
            Dictionary<long, ProductSalesRankItem> result = new Dictionary<long, ProductSalesRankItem>();
            try
            {
                DateTime start = DateTime.Today.AddDays(-30);
                DateTime end = DateTime.Today.AddDays(1);
                IList<ProductSalesRankItem> rankItems = _reportService.GetProductSalesRank(start, end, 10000);
                foreach (ProductSalesRankItem item in rankItems)
                {
                    if (item != null && item.ProductId > 0 && !result.ContainsKey(item.ProductId))
                    {
                        result.Add(item.ProductId, item);
                    }
                }
            }
            catch
            {
                return new Dictionary<long, ProductSalesRankItem>();
            }

            return result;
        }

        private static void AppendRestockSection(StringBuilder builder, string title, IList<Product> products, IDictionary<long, ProductSalesRankItem> salesByProductId)
        {
            if (products == null || products.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine(title + "：");
            int count = Math.Min(products.Count, 8);
            for (int index = 0; index < count; index++)
            {
                Product product = products[index];
                ProductSalesRankItem sales;
                decimal recentSales = salesByProductId != null && salesByProductId.TryGetValue(product.Id, out sales) ? sales.SalesQuantity : 0m;
                builder.AppendLine((index + 1) + ". " + FormatProductName(product)
                    + "，库存 " + FormatNumber(product.CurrentStock)
                    + "，预警线 " + FormatNumber(product.MinStockAlert)
                    + "，近 30 天销量 " + FormatNumber(recentSales)
                    + "，售价 " + FormatMoney(product.DefaultPrice));
            }
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
