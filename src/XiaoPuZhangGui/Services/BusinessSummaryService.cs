using System;
using System.Collections.Generic;
using System.Text;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class BusinessSummaryService
    {
        private readonly ReportService _reportService;
        private readonly AiIntentRouter _intentRouter;

        public BusinessSummaryService()
        {
            _reportService = new ReportService();
            _intentRouter = new AiIntentRouter();
        }

        public BusinessSummaryResult BuildTodaySummary()
        {
            DateTime start = ReportService.GetDayStart(DateTime.Today);
            DateTime end = ReportService.GetNextDayStart(DateTime.Today);
            return BuildPeriodSummary("今日收入分析", start, end, "请分析今天的收入、实收、赊账、利润、异常点，并给出 3 到 5 条建议。", true);
        }

        public BusinessSummaryResult BuildWeekSummary()
        {
            DateTime today = DateTime.Today;
            int daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime start = today.AddDays(-daysFromMonday).Date;
            DateTime end = start.AddDays(7);
            return BuildPeriodSummary("本周经营小结", start, end, "请生成简洁周报，重点说明销售、利润、热销、库存、赊账和风险点。", true);
        }

        public BusinessSummaryResult BuildMonthSummary()
        {
            DateTime start = ReportService.GetMonthStart(DateTime.Today);
            DateTime end = ReportService.GetNextMonthStart(DateTime.Today);
            return BuildPeriodSummary("本月经营月报", start, end, "请生成小卖铺老板容易理解的月报，说明本月经营表现和下月关注点。", true);
        }

        public BusinessSummaryResult BuildInventoryRiskSummary()
        {
            try
            {
                DateTime today = DateTime.Today;
                IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
                IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProducts();
                IList<Product> inventoryItems = _reportService.GetInventoryItemsForExport();
                IList<ProductSalesRankItem> monthSales = _reportService.GetProductSalesRank(ReportService.GetMonthStart(today), ReportService.GetNextMonthStart(today));

                StringBuilder builder = CreateSummaryHeader("库存补货建议", "截至 " + today.ToString("yyyy-MM-dd"));
                AppendInventoryCoreData(builder, inventoryItems, lowStockItems, expiringItems);
                AppendInventoryDetails(builder, "当前全部商品库存明细", inventoryItems, 10000);
                AppendList(builder, "低库存商品", lowStockItems, 12, delegate(LowStockReportItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，当前库存 " + FormatNumber(item.CurrentStock) + "，预警线 " + FormatNumber(item.MinStockAlert);
                });
                AppendList(builder, "临期商品", expiringItems, 12, delegate(ExpiringProductReportItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，剩余 " + FormatNumber(item.QuantityRemaining) + "，到期日 " + item.ExpiryDate.ToString("yyyy-MM-dd") + "，剩余 " + item.DaysRemaining + " 天";
                });
                AppendList(builder, "库存为 0 或不足 0 的商品", FilterZeroStock(inventoryItems), 12, delegate(Product item, int index)
                {
                    return FormatIndex(index) + item.Name + "，当前库存 " + FormatNumber(item.CurrentStock);
                });
                AppendList(builder, "本月热销商品参考", monthSales, 8, delegate(ProductSalesRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，销量 " + FormatNumber(item.SalesQuantity) + "，销售额 " + FormatMoney(item.SalesAmount);
                });

                builder.AppendLine();
                builder.AppendLine("【请你完成】");
                builder.AppendLine("请根据上面的全部商品库存明细、低库存、临期和销售参考，给出补货、临期处理和需要关注的商品建议。不要编造数据。");
                AppendPlainTextOutputRule(builder);

                return Success("库存补货建议", "截至 " + today.ToString("yyyy-MM-dd"), builder.ToString());
            }
            catch (Exception ex)
            {
                return BusinessSummaryResult.Fail("库存补货建议", "生成库存摘要失败：" + ex.Message);
            }
        }

        public BusinessSummaryResult BuildCreditRiskSummary()
        {
            try
            {
                DateTime today = DateTime.Today;
                IList<CreditRecord> records = _reportService.GetOutstandingCreditRecordsForExport();
                decimal totalRemaining = 0;
                foreach (CreditRecord record in records)
                {
                    totalRemaining += record.RemainingAmount;
                }

                StringBuilder builder = CreateSummaryHeader("赊账客户提醒", "截至 " + today.ToString("yyyy-MM-dd"));
                builder.AppendLine("【核心数据】");
                builder.AppendLine("未结清赊账总额：" + FormatMoney(totalRemaining));
                builder.AppendLine("未结清客户/记录数量：" + records.Count + " 条");
                builder.AppendLine();
                AppendList(builder, "欠款金额较高记录", records, 12, delegate(CreditRecord item, int index)
                {
                    return FormatIndex(index) + SafeCustomerName(item.DebtorName) + "，剩余欠款 " + FormatMoney(item.RemainingAmount) + "，赊账日期 " + item.CreditDate.ToString("yyyy-MM-dd");
                });

                builder.AppendLine();
                builder.AppendLine("【请你完成】");
                builder.AppendLine("请温和地分析赊账风险，给出提醒收款、控制新增赊账和维护熟客关系的建议。不要输出吓人的措辞。");
                AppendPlainTextOutputRule(builder);

                return Success("赊账客户提醒", "截至 " + today.ToString("yyyy-MM-dd"), builder.ToString());
            }
            catch (Exception ex)
            {
                return BusinessSummaryResult.Fail("赊账客户提醒", "生成赊账摘要失败：" + ex.Message);
            }
        }

        public BusinessSummaryResult BuildHotAndSlowProductsSummary()
        {
            try
            {
                DateTime today = DateTime.Today;
                DateTime start = ReportService.GetMonthStart(today);
                DateTime end = ReportService.GetNextMonthStart(today);
                IList<ProductSalesRankItem> hotProducts = _reportService.GetProductSalesRank(start, end);
                IList<ProductProfitRankItem> profitProducts = _reportService.GetProductProfitRank(start, end);
                IList<Product> inventoryItems = _reportService.GetInventoryItemsForExport();

                StringBuilder builder = CreateSummaryHeader("热销与滞销商品分析", FormatRange(start, end));
                AppendList(builder, "本月热销商品 Top 10", hotProducts, 10, delegate(ProductSalesRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，销量 " + FormatNumber(item.SalesQuantity) + "，销售额 " + FormatMoney(item.SalesAmount);
                });
                AppendList(builder, "本月利润贡献商品 Top 10", profitProducts, 10, delegate(ProductProfitRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，销量 " + FormatNumber(item.SalesQuantity) + "，毛利 " + FormatMoney(item.GrossProfit);
                });
                AppendList(builder, "当前有库存但本月未进入热销榜的商品参考", PickPotentialSlowProducts(inventoryItems, hotProducts), 10, delegate(Product item, int index)
                {
                    return FormatIndex(index) + item.Name + "，当前库存 " + FormatNumber(item.CurrentStock) + "，成本均价 " + FormatMoney(item.AverageCost);
                });

                builder.AppendLine();
                builder.AppendLine("【请你完成】");
                builder.AppendLine("请分析热销、滞销和进货建议。数据不足时请明确说明，不要编造销量。");
                AppendPlainTextOutputRule(builder);

                return Success("热销与滞销商品分析", FormatRange(start, end), builder.ToString());
            }
            catch (Exception ex)
            {
                return BusinessSummaryResult.Fail("热销与滞销商品分析", "生成热销滞销摘要失败：" + ex.Message);
            }
        }

        public BusinessSummaryResult BuildInventorySnapshotSummary()
        {
            try
            {
                DateTime today = DateTime.Today;
                IList<Product> inventoryItems = _reportService.GetInventoryItemsForExport();
                IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
                IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProducts();
                IList<ProductSalesRankItem> monthSales = _reportService.GetProductSalesRank(ReportService.GetMonthStart(today), ReportService.GetNextMonthStart(today));

                int productCount = 0;
                int zeroStockCount = 0;
                decimal totalStock = 0;
                foreach (Product item in inventoryItems)
                {
                    productCount++;
                    totalStock += item.CurrentStock;
                    if (item.CurrentStock <= 0)
                    {
                        zeroStockCount++;
                    }
                }

                StringBuilder builder = CreateSummaryHeader("当前库存状态总览", "截至 " + today.ToString("yyyy-MM-dd"));
                builder.AppendLine("【核心数据】");
                builder.AppendLine("商品总数：" + productCount + " 个");
                builder.AppendLine("总库存数量：" + FormatNumber(totalStock));
                builder.AppendLine("库存为 0 或不足 0 的商品数：" + zeroStockCount + " 个");
                builder.AppendLine("低库存商品数：" + lowStockItems.Count + " 个");
                builder.AppendLine("临期商品数：" + expiringItems.Count + " 个");
                builder.AppendLine();
                AppendInventoryDetails(builder, "当前全部商品库存明细", inventoryItems, 10000);
                AppendList(builder, "库存为 0 或不足 0 的商品", FilterZeroStock(inventoryItems), 12, delegate(Product item, int index)
                {
                    return FormatIndex(index) + item.Name + "，当前库存 " + FormatNumber(item.CurrentStock);
                });
                AppendList(builder, "低库存商品", lowStockItems, 12, delegate(LowStockReportItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，当前库存 " + FormatNumber(item.CurrentStock) + "，预警线 " + FormatNumber(item.MinStockAlert);
                });
                AppendList(builder, "临期商品", expiringItems, 12, delegate(ExpiringProductReportItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，剩余 " + FormatNumber(item.QuantityRemaining) + "，到期日 " + item.ExpiryDate.ToString("yyyy-MM-dd");
                });
                AppendList(builder, "库存较多商品参考", PickHighStockProducts(inventoryItems), 12, delegate(Product item, int index)
                {
                    return FormatIndex(index) + item.Name + "，当前库存 " + FormatNumber(item.CurrentStock) + "，成本均价 " + FormatMoney(item.AverageCost);
                });
                AppendList(builder, "最近热销但需关注库存的商品", monthSales, 8, delegate(ProductSalesRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，本月销量 " + FormatNumber(item.SalesQuantity) + "，销售额 " + FormatMoney(item.SalesAmount);
                });
                AppendList(builder, "有库存但近期销量不高的商品参考", PickPotentialSlowProducts(inventoryItems, monthSales), 10, delegate(Product item, int index)
                {
                    return FormatIndex(index) + item.Name + "，当前库存 " + FormatNumber(item.CurrentStock);
                });
                builder.AppendLine("【请你完成】");
                builder.AppendLine("请根据当前库存总览，判断库存整体是否健康，哪些商品优先补货，哪些商品需要避免继续压货。不要编造数据。");
                AppendPlainTextOutputRule(builder);

                return Success("当前库存状态总览", "截至 " + today.ToString("yyyy-MM-dd"), builder.ToString());
            }
            catch (Exception ex)
            {
                return BusinessSummaryResult.Fail("当前库存状态总览", "生成库存状态摘要失败：" + ex.Message);
            }
        }

        public BusinessSummaryResult BuildLiveContextForUserQuestion(string userQuestion)
        {
            AiIntentResult intent = _intentRouter.Route(userQuestion);
            if (!intent.HasBusinessContext)
            {
                return BusinessSummaryResult.Fail("普通对话", "NO_BUSINESS_CONTEXT");
            }

            List<BusinessSummaryResult> summaries = new List<BusinessSummaryResult>();
            foreach (string key in intent.IntentKeys)
            {
                if (key == "today")
                {
                    summaries.Add(BuildTodaySummary());
                    continue;
                }

                if (key == "week")
                {
                    summaries.Add(BuildWeekSummary());
                    continue;
                }

                if (key == "month")
                {
                    summaries.Add(BuildMonthSummary());
                    continue;
                }

                if (key == "inventorySnapshot")
                {
                    summaries.Add(BuildInventorySnapshotSummary());
                    continue;
                }

                if (key == "inventoryRisk")
                {
                    summaries.Add(BuildInventoryRiskSummary());
                    continue;
                }

                if (key == "credit")
                {
                    summaries.Add(BuildCreditRiskSummary());
                    continue;
                }

                if (key == "hotSlow")
                {
                    summaries.Add(BuildHotAndSlowProductsSummary());
                    continue;
                }

                if (key == "purchaseAdvice")
                {
                    summaries.Add(BuildInventoryRiskSummary());
                    summaries.Add(BuildHotAndSlowProductsSummary());
                }
            }

            List<string> dataSourceLabels = new List<string>();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("以下是小铺掌柜本地数据库实时统计摘要，请只基于这些数据回答，不要编造。");
            builder.AppendLine();
            bool appendedSummary = false;
            foreach (BusinessSummaryResult summary in summaries)
            {
                if (summary == null || !summary.Success)
                {
                    continue;
                }

                AddSummarySourceLabel(dataSourceLabels, summary.Title);
                builder.AppendLine(summary.SummaryText);
                builder.AppendLine();
                appendedSummary = true;
            }

            if (!appendedSummary)
            {
                return BusinessSummaryResult.Fail("普通对话", "NO_BUSINESS_CONTEXT");
            }

            return Success("实时经营数据上下文", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), builder.ToString(), string.Join("、", dataSourceLabels.ToArray()));
        }

        private BusinessSummaryResult BuildPeriodSummary(string title, DateTime start, DateTime end, string instruction, bool includeRisk)
        {
            try
            {
                ReportSummary summary = _reportService.GetSummary(start, end);
                IList<ProductSalesRankItem> salesRank = _reportService.GetProductSalesRank(start, end);
                IList<ProductProfitRankItem> profitRank = _reportService.GetProductProfitRank(start, end);
                IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
                IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProducts();
                IList<ScrapSummaryItem> scrapItems = _reportService.GetScrapSummary(start, end);
                IList<CreditRecord> credits = _reportService.GetOutstandingCreditRecordsForExport();
                IList<ProfitTrendPoint> trend = _reportService.GetProfitTrend(start, end, TimeSpan.FromDays(1), 0);

                StringBuilder builder = CreateSummaryHeader(title, FormatRange(start, end));
                builder.AppendLine("【核心数据】");
                builder.AppendLine("销售总额：" + FormatMoney(summary.SalesReceivable));
                builder.AppendLine("实收金额：" + FormatMoney(summary.SalesPaid));
                builder.AppendLine("新增赊账：" + FormatMoney(summary.NewCredit));
                builder.AppendLine("未结清赊账总额：" + FormatMoney(summary.OutstandingCredit));
                builder.AppendLine("订单数量：" + summary.SalesOrderCount + " 单");
                builder.AppendLine("商品销售数量：" + FormatNumber(summary.SoldQuantity));
                builder.AppendLine("商品成本：" + FormatMoney(summary.ProductCost));
                builder.AppendLine("估算毛利：" + FormatMoney(summary.GrossProfit));
                builder.AppendLine("报废损失：" + FormatMoney(summary.ScrapLoss));
                builder.AppendLine("估算净利润：" + FormatMoney(summary.NetProfit));
                builder.AppendLine();

                AppendList(builder, "热销商品", salesRank, 10, delegate(ProductSalesRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，销量 " + FormatNumber(item.SalesQuantity) + "，销售额 " + FormatMoney(item.SalesAmount);
                });
                AppendList(builder, "利润贡献商品", profitRank, 8, delegate(ProductProfitRankItem item, int index)
                {
                    return FormatIndex(index) + item.ProductName + "，毛利 " + FormatMoney(item.GrossProfit) + "，销售额 " + FormatMoney(item.SalesAmount);
                });
                AppendList(builder, "按天利润趋势", trend, 10, delegate(ProfitTrendPoint item, int index)
                {
                    return FormatIndex(index) + item.Label + "，毛利 " + FormatMoney(item.GrossProfit) + "，报废损失 " + FormatMoney(item.ScrapLoss) + "，净利润 " + FormatMoney(item.NetProfit);
                });

                if (includeRisk)
                {
                    AppendList(builder, "库存风险", lowStockItems, 10, delegate(LowStockReportItem item, int index)
                    {
                        return FormatIndex(index) + item.ProductName + "，当前库存 " + FormatNumber(item.CurrentStock) + "，预警线 " + FormatNumber(item.MinStockAlert);
                    });
                    AppendList(builder, "临期商品", expiringItems, 10, delegate(ExpiringProductReportItem item, int index)
                    {
                        return FormatIndex(index) + item.ProductName + "，剩余 " + FormatNumber(item.QuantityRemaining) + "，到期日 " + item.ExpiryDate.ToString("yyyy-MM-dd");
                    });
                    AppendList(builder, "报废损失明细", scrapItems, 8, delegate(ScrapSummaryItem item, int index)
                    {
                        return FormatIndex(index) + item.ProductName + "，数量 " + FormatNumber(item.Quantity) + "，损失 " + FormatMoney(item.LossAmount) + "，原因 " + SafeText(item.Reason);
                    });
                    AppendList(builder, "未结清赊账记录", credits, 8, delegate(CreditRecord item, int index)
                    {
                        return FormatIndex(index) + SafeCustomerName(item.DebtorName) + "，剩余欠款 " + FormatMoney(item.RemainingAmount) + "，赊账日期 " + item.CreditDate.ToString("yyyy-MM-dd");
                    });
                }

                builder.AppendLine("【异常提醒】");
                AppendReminder(builder, summary.SalesReceivable <= 0, "当前周期销售额为 0，需要确认是否还没有录入销售单。");
                AppendReminder(builder, summary.NewCredit > summary.SalesPaid && summary.NewCredit > 0, "新增赊账高于实收金额，建议关注收款节奏。");
                AppendReminder(builder, lowStockItems.Count > 0, "存在低库存商品，建议检查是否需要补货。");
                AppendReminder(builder, expiringItems.Count > 0, "存在临期商品，建议优先处理。");
                builder.AppendLine();
                builder.AppendLine("【请你完成】");
                builder.AppendLine(instruction);
                builder.AppendLine("请用简单、口语化、适合小卖铺老板理解的方式分析，并给出 3 到 5 条建议。不要编造数据。");
                AppendPlainTextOutputRule(builder);

                return Success(title, FormatRange(start, end), builder.ToString());
            }
            catch (Exception ex)
            {
                return BusinessSummaryResult.Fail(title, "生成经营摘要失败：" + ex.Message);
            }
        }

        private static StringBuilder CreateSummaryHeader(string title, string period)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("【分析类型】");
            builder.AppendLine(title);
            builder.AppendLine();
            builder.AppendLine("【统计周期】");
            builder.AppendLine(period);
            builder.AppendLine();
            return builder;
        }

        private static void AppendList<T>(StringBuilder builder, string title, IList<T> items, int limit, Func<T, int, string> formatter)
        {
            builder.AppendLine("【" + title + "】");
            if (items == null || items.Count == 0)
            {
                builder.AppendLine("暂无数据。");
                builder.AppendLine();
                return;
            }

            int count = Math.Min(limit, items.Count);
            for (int index = 0; index < count; index++)
            {
                builder.AppendLine(formatter(items[index], index + 1));
            }

            builder.AppendLine();
        }

        private static IList<Product> FilterZeroStock(IList<Product> items)
        {
            List<Product> result = new List<Product>();
            if (items == null)
            {
                return result;
            }

            foreach (Product item in items)
            {
                if (item.CurrentStock <= 0)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static IList<Product> PickPotentialSlowProducts(IList<Product> inventoryItems, IList<ProductSalesRankItem> hotProducts)
        {
            List<Product> result = new List<Product>();
            if (inventoryItems == null)
            {
                return result;
            }

            foreach (Product product in inventoryItems)
            {
                if (product.CurrentStock <= 0 || IsInHotProducts(product, hotProducts))
                {
                    continue;
                }

                result.Add(product);
                if (result.Count >= 20)
                {
                    break;
                }
            }

            return result;
        }

        private static IList<Product> PickHighStockProducts(IList<Product> inventoryItems)
        {
            List<Product> result = new List<Product>();
            if (inventoryItems == null)
            {
                return result;
            }

            foreach (Product product in inventoryItems)
            {
                if (product.CurrentStock <= 0)
                {
                    continue;
                }

                result.Add(product);
            }

            result.Sort(delegate(Product left, Product right)
            {
                return right.CurrentStock.CompareTo(left.CurrentStock);
            });

            if (result.Count > 20)
            {
                result.RemoveRange(20, result.Count - 20);
            }

            return result;
        }

        private static bool IsInHotProducts(Product product, IList<ProductSalesRankItem> hotProducts)
        {
            if (product == null || hotProducts == null)
            {
                return false;
            }

            foreach (ProductSalesRankItem item in hotProducts)
            {
                if (item.ProductId == product.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendReminder(StringBuilder builder, bool condition, string text)
        {
            if (condition)
            {
                builder.AppendLine("- " + text);
            }
        }

        private static void AppendPlainTextOutputRule(StringBuilder builder)
        {
            builder.AppendLine("输出格式要求：只用纯文本回答，不要使用 Markdown 语法，不要使用 #、##、**、```、表格、引用块或代码块。可以使用普通编号 1. 2. 3. 分行说明。");
        }

        private static void AppendInventoryCoreData(
            StringBuilder builder,
            IList<Product> inventoryItems,
            IList<LowStockReportItem> lowStockItems,
            IList<ExpiringProductReportItem> expiringItems)
        {
            int totalCount = 0;
            int activeCount = 0;
            decimal totalStock = 0m;
            if (inventoryItems != null)
            {
                foreach (Product item in inventoryItems)
                {
                    totalCount++;
                    if (IsActiveProduct(item))
                    {
                        activeCount++;
                    }

                    totalStock += item.CurrentStock;
                }
            }

            builder.AppendLine("【库存核心数据】");
            builder.AppendLine("商品总数：" + totalCount + " 个");
            builder.AppendLine("在售商品数：" + activeCount + " 个");
            builder.AppendLine("当前总库存数量：" + FormatNumber(totalStock));
            builder.AppendLine("低库存商品数：" + (lowStockItems == null ? 0 : lowStockItems.Count) + " 个");
            builder.AppendLine("临期商品数：" + (expiringItems == null ? 0 : expiringItems.Count) + " 个");
            builder.AppendLine();
        }

        private static void AppendInventoryDetails(StringBuilder builder, string title, IList<Product> items, int limit)
        {
            builder.AppendLine("【" + title + "】");
            if (items == null || items.Count == 0)
            {
                builder.AppendLine("暂无数据。");
                builder.AppendLine();
                return;
            }

            int count = Math.Min(limit, items.Count);
            for (int index = 0; index < count; index++)
            {
                Product item = items[index];
                builder.AppendLine(FormatIndex(index + 1)
                    + SafeText(item.Name)
                    + "，分类 " + SafeText(item.CategoryName)
                    + "，规格 " + SafeText(item.Specification)
                    + "，当前库存 " + FormatNumber(item.CurrentStock)
                    + "，默认售价 " + FormatMoney(item.DefaultPrice)
                    + "，库存均价 " + FormatMoney(item.AverageCost)
                    + "，最低库存 " + FormatNumber(item.MinStockAlert)
                    + "，保质期 " + (item.RequiresExpiry ? "启用" : "未启用")
                    + "，到期日期 " + (item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : "未填写")
                    + "，状态 " + SafeText(item.Status)
                    + "，库存状态 " + BuildInventoryStatus(item));
            }

            builder.AppendLine();
        }

        private static string BuildInventoryStatus(Product product)
        {
            if (product == null)
            {
                return "未知";
            }

            if (product.CurrentStock <= 0)
            {
                return "无库存";
            }

            if (product.CurrentStock <= product.MinStockAlert)
            {
                return "低库存";
            }

            if (product.RequiresExpiry && product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date <= DateTime.Today.AddDays(15))
            {
                return "临期关注";
            }

            return "正常";
        }

        private static bool IsActiveProduct(Product product)
        {
            return product != null && string.Equals(product.Status, "在售", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddSummarySourceLabel(List<string> labels, string title)
        {
            if (labels == null || string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            string label = title;
            if (title.Contains("今日") || title.Contains("收入"))
            {
                label = "今日销售摘要";
            }
            else if (title.Contains("库存"))
            {
                label = "库存摘要";
            }
            else if (title.Contains("赊账"))
            {
                label = "赊账记录";
            }
            else if (title.Contains("热销") || title.Contains("滞销"))
            {
                label = "商品销售排行";
            }
            else if (title.Contains("本周"))
            {
                label = "本周经营摘要";
            }
            else if (title.Contains("本月") || title.Contains("月报"))
            {
                label = "本月经营摘要";
            }

            foreach (string existing in labels)
            {
                if (string.Equals(existing, label, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            labels.Add(label);
        }

        private static BusinessSummaryResult Success(string title, string period, string summaryText)
        {
            return Success(title, period, summaryText, string.Empty);
        }

        private static BusinessSummaryResult Success(string title, string period, string summaryText, string dataSourceLabel)
        {
            return new BusinessSummaryResult
            {
                Success = true,
                Title = title,
                Period = period,
                SummaryText = summaryText,
                JsonText = dataSourceLabel ?? string.Empty,
                ErrorMessage = string.Empty
            };
        }

        private static string FormatRange(DateTime start, DateTime end)
        {
            DateTime inclusiveEnd = end.AddDays(-1);
            if (start.Date == inclusiveEnd.Date)
            {
                return start.ToString("yyyy-MM-dd");
            }

            return start.ToString("yyyy-MM-dd") + " 至 " + inclusiveEnd.ToString("yyyy-MM-dd");
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("N2") + " 元";
        }

        private static string FormatNumber(decimal value)
        {
            return value.ToString("N3");
        }

        private static string FormatIndex(int index)
        {
            return index + ". ";
        }

        private static string SafeCustomerName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未留姓名客户" : value.Trim();
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();
        }
    }
}
