using System;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiIntentRouter
    {
        public AiIntentResult Route(string userQuestion)
        {
            string text = (userQuestion ?? string.Empty).Trim();
            string normalized = Normalize(text);

            AiIntentResult result = new AiIntentResult();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                MarkUnknown(result);
                return result;
            }

            ScoreQuery(result, normalized);
            ScoreAnalysis(result, normalized);
            ScoreAction(result, normalized);
            ScoreChat(result, normalized);

            if (result.ActionConfidence >= 0.75m && HasExplicitActionCue(normalized) && !LooksLikeExplicitQuestion(normalized))
            {
                result.QueryConfidence = 0m;
                result.QueryKind = string.Empty;
            }

            if (result.QueryConfidence > 0m && result.QueryConfidence >= result.ActionConfidence && result.QueryConfidence >= result.AnalysisConfidence)
            {
                result.RouteType = AiIntentResult.RouteQuery;
                return result;
            }

            if (result.AnalysisConfidence > 0m && result.AnalysisConfidence >= result.ActionConfidence)
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                AddAnalysisIntentKey(result, result.AnalysisKey);
                return result;
            }

            if (result.ActionConfidence >= 0.75m && result.ActionConfidence > result.QueryConfidence && result.ActionConfidence > result.AnalysisConfidence)
            {
                result.RouteType = AiIntentResult.RouteAction;
                return result;
            }

            if (LooksVagueBusinessText(normalized))
            {
                MarkUnknown(result);
                return result;
            }

            result.RouteType = AiIntentResult.RouteChat;
            result.ChatConfidence = Math.Max(result.ChatConfidence, 0.6m);
            return result;
        }

        public AiIntentResult RouteKnownAnalysis(string analysisKey)
        {
            AiIntentResult result = new AiIntentResult
            {
                RouteType = AiIntentResult.RouteAnalysis,
                AnalysisKey = string.IsNullOrWhiteSpace(analysisKey) ? "today" : analysisKey.Trim(),
                AnalysisConfidence = 1m
            };
            AddAnalysisIntentKey(result, result.AnalysisKey);
            return result;
        }

        private static void ScoreQuery(AiIntentResult result, string text)
        {
            if (LooksLikeNewProductAdvice(text))
            {
                result.QueryKind = "new_product_advice";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.99m);
            }

            if (LooksLikeRestockAdvice(text))
            {
                result.QueryKind = "restock_advice";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.99m);
            }

            if (result.QueryKind != "restock_advice" && LooksLikeCategoryLowStockQuery(text))
            {
                result.QueryKind = "category_low_stock";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.99m);
            }
            else if (result.QueryKind != "restock_advice" && LooksLikeCategoryQuery(text))
            {
                result.QueryKind = "category_stock";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.98m);
            }

            bool hasSpecificBusinessQuery = result.QueryKind == "restock_advice"
                || result.QueryKind == "new_product_advice"
                || result.QueryKind == "category_low_stock"
                || result.QueryKind == "category_stock";

            if (!hasSpecificBusinessQuery && ContainsAny(text, "多少钱", "售价", "卖多少", "价格", "多少元", "多贵"))
            {
                result.QueryKind = "product_price";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.95m);
            }

            if (!hasSpecificBusinessQuery && ContainsAny(text, "库存多少", "库存有多少", "还剩", "剩多少", "有多少", "库存", "还有多少", "还有几", "还剩几", "剩几"))
            {
                result.QueryKind = "product_stock";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.92m);
            }

            if (!hasSpecificBusinessQuery && ContainsAny(text, "有哪些商品", "现在有什么商品", "商品列表", "全部商品", "所有商品", "店里有什么"))
            {
                result.QueryKind = "all_inventory";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.96m);
            }

            if (ContainsAny(text, "谁还欠", "谁欠账", "谁欠钱", "谁赊账", "有谁赊账", "有没有人赊账", "有哪些赊账客户", "赊账客户", "欠多少钱", "还欠账", "欠款多少", "欠账客户", "未结清赊账", "没结清", "未还清", "未收回", "谁没给钱", "谁还没结账", "谁还欠钱", "还没还钱", "欠账情况"))
            {
                result.QueryKind = "credit_customers";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.94m);
            }

            if (ContainsAny(text, "快过期", "临期", "要过期", "哪些商品过期"))
            {
                result.QueryKind = "expiring_products";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.93m);
            }

            if (ContainsAny(text, "库存低", "低库存", "快没货", "快没了", "没货了", "快卖完", "缺货", "缺货商品", "哪些商品少", "库存不够", "不够卖", "哪些货该补", "哪些商品需要补货", "该补哪些"))
            {
                result.QueryKind = "low_stock";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.93m);
            }

            if (ContainsAny(text, "报废记录", "所有报废记录", "全部报废记录", "查报废", "查一下报废"))
            {
                result.QueryKind = "scrap_records";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.97m);
            }

            if (ContainsAny(text, "报废损失", "有没有报废", "最近有没有报废", "报废多少", "报废损失多少", "这个月报废损失"))
            {
                result.QueryKind = "scrap_loss";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.96m);
            }

            if (ContainsAny(text, "不赚钱", "毛利低", "利润低", "利润少", "没利润", "亏钱"))
            {
                result.QueryKind = "low_profit_products";
                result.QueryConfidence = Math.Max(result.QueryConfidence, 0.94m);
            }

            if (result.QueryConfidence > 0m && ContainsAny(text, "好奇", "只是想问", "查一下", "看看", "查询"))
            {
                result.QueryConfidence = Math.Min(1m, result.QueryConfidence + 0.03m);
            }
        }

        private static void ScoreAnalysis(AiIntentResult result, string text)
        {
            if (ContainsAny(text, "分析今日收入", "今日收入", "今天收入", "今日经营", "今天经营", "营业额分析"))
            {
                SetAnalysis(result, "today", 0.95m);
            }

            if (ContainsAny(text, "本周经营", "本周小结", "周报", "这一周", "这周"))
            {
                SetAnalysis(result, "week", 0.95m);
            }

            if (ContainsAny(text, "本月经营", "本月月报", "月报", "这个月", "本月"))
            {
                SetAnalysis(result, "month", 0.95m);
            }

            if (ContainsAny(text, "库存结构", "库存状态", "库存情况", "库存补货建议", "补货建议", "分析库存", "看看库存"))
            {
                SetAnalysis(result, "inventoryRisk", 0.96m);
            }

            if (ContainsAny(text, "赊账客户提醒", "赊账提醒", "收款提醒", "欠款提醒"))
            {
                SetAnalysis(result, "credit", 0.96m);
            }

            if (ContainsAny(text, "热销与滞销", "热销", "滞销", "不好卖", "卖得不好", "卖得好", "卖得慢", "卖得最好", "卖不动", "商品排行"))
            {
                SetAnalysis(result, "hotSlow", 0.95m);
            }

            if (ContainsAny(text, "今天利润", "今日利润", "利润怎么样", "毛利怎么样", "净利润怎么样", "今天毛利", "今日毛利"))
            {
                SetAnalysis(result, "today", 0.95m);
            }

            if (ContainsAny(text, "分析", "建议", "小结", "报告", "报表"))
            {
                result.AnalysisConfidence = Math.Max(result.AnalysisConfidence, 0.72m);
                if (string.IsNullOrWhiteSpace(result.AnalysisKey))
                {
                    result.AnalysisKey = "today";
                }
            }
        }

        private static void ScoreAction(AiIntentResult result, string text)
        {
            if (LooksLikeExplicitQuestion(text) || LooksLikeRestockAdvice(text) || ContainsAny(text, "补货建议", "库存建议", "赊账客户提醒", "库存结构", "分析"))
            {
                return;
            }

            if (ContainsAny(text, "搞错了", "不对", "重新记", "刚才说错了", "说错了")
                && ContainsAny(text, "瓶", "包", "袋", "条", "件", "个", "盒", "罐", "支"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.88m);
            }

            if (ContainsAny(text, "涨价", "降价", "都涨", "都降") && ContainsAny(text, "所有", "全部", "整类", "这一类"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.86m);
                result.IntentKeys.Add("batch_price_update");
            }

            if (ContainsAny(text, "进货", "入库", "新进", "进了", "采购了", "买了一批", "登记进货"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.88m);
            }

            if (ContainsAny(text, "卖了", "卖出", "售出", "顾客买了", "刚才卖出"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.88m);
            }

            if (ContainsAny(text, "收了") && ContainsAny(text, "卖", "顾客", "瓶", "包", "件", "个", "元"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.82m);
            }

            if (ContainsAny(text, "赊账", "赊了", "赊", "记账", "先欠着", "没给钱", "没付钱", "记他", "记她")
                || (ContainsAny(text, "拿了") && ContainsAny(text, "欠", "没给钱", "没付钱", "记他", "记她")))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.86m);
            }

            if (ContainsAny(text, "实际只剩", "实际剩", "盘点发现", "库存改成", "修正库存", "只剩"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.86m);
            }

            if (ContainsAny(text, "报废", "扔掉", "丢掉", "坏了", "过期处理"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.88m);
            }

            if (ContainsAny(text, "以后卖", "价格改成", "售价改成", "调价", "调到")
                || (ContainsAny(text, "以后", "今后", "从现在开始") && ContainsAny(text, "卖", "售价", "价格")))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.86m);
            }

            if (ContainsAny(text, "撤销", "删掉刚才", "弄错了", "取消那条"))
            {
                result.ActionConfidence = Math.Max(result.ActionConfidence, 0.84m);
            }
        }

        private static void ScoreChat(AiIntentResult result, string text)
        {
            if (ContainsAny(text, "你好", "谢谢", "你是谁", "会什么", "怎么用"))
            {
                result.ChatConfidence = 0.8m;
            }
        }

        private static bool LooksVagueBusinessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            if (ContainsAny(text, "弄一下", "处理一下", "看一下这个", "帮我看看"))
            {
                return true;
            }

            if (text.Length <= 8 && ContainsAny(text, "可乐", "水", "烟", "薯片", "商品", "库存", "账"))
            {
                return true;
            }

            return false;
        }

        private static bool HasExplicitActionCue(string text)
        {
            return ContainsAny(text,
                "进货", "入库", "新进", "进了", "采购了", "买了一批", "登记进货",
                "卖了", "卖出", "售出", "顾客买了", "刚才卖出",
                "赊账", "赊了", "赊", "记账", "先欠着", "没给钱", "没付钱", "记他", "记她", "拿了",
                "实际只剩", "实际剩", "盘点发现", "库存改成", "修正库存", "只剩",
                "报废", "扔掉", "丢掉", "坏了", "过期处理",
                "以后卖", "以后", "价格改成", "售价改成", "调价", "调到",
                "撤销", "删掉刚才", "弄错了", "取消那条");
        }

        private static bool LooksLikeExplicitQuestion(string text)
        {
            return ContainsAny(text, "只是", "好奇", "查询", "查一下", "请问", "看看", "哪些", "谁", "多少", "几", "有没有", "是否", "吗", "呢")
                || ContainsAny(text, "报废损失", "卖得好", "卖得慢", "卖不动", "不赚钱", "毛利低", "利润低")
                || LooksLikeRestockAdvice(text)
                || (ContainsAny(text, "多少钱", "售价是多少", "价格是多少", "还剩多少", "库存多少", "还有多少", "还有几", "还剩几") && !HasExplicitActionCue(text));
        }

        private static bool LooksLikeCategoryQuery(string text)
        {
            if (!ContainsCategoryAlias(text))
            {
                return false;
            }

            return ContainsAny(text, "有哪些", "有什么", "哪些", "商品", "库存", "分类", "类", "查一下", "查询", "看看", "不知道", "还有多少", "还有几", "还剩", "剩多少", "呢", "嘛", "那", "我说的是", "就是")
                || ContainsAny(text, "目前库存", "当前库存", "现在库存", "库存里", "店里");
        }

        private static bool LooksLikeCategoryLowStockQuery(string text)
        {
            return ContainsCategoryAlias(text)
                && ContainsAny(text, "快没货", "快没了", "低库存", "库存低", "缺货", "该补", "补货", "需要补", "不够卖");
        }

        private static bool LooksLikeRestockAdvice(string text)
        {
            if (ContainsAny(text, "哪些货该进", "哪些货该补", "该进了", "该补什么", "补什么货", "进什么货", "进点什么", "补点什么"))
            {
                return true;
            }

            if (ContainsAny(text, "建议", "根据库存", "库存情况", "最近销售", "该补", "该进", "补货", "进货")
                && ContainsAny(text, "什么货", "什么", "哪些", "进点", "补点", "看看", "分析"))
            {
                return true;
            }

            return ContainsAny(text,
                "根据库存建议进什么货",
                "根据我的库存里的商品",
                "建议我进一些什么货",
                "建议进一些什么货",
                "建议进什么货",
                "该补哪些货",
                "哪些货该补",
                "哪些商品需要补货",
                "现在应该进点什么",
                "应该进点什么",
                "该进什么",
                "进货建议",
                "补货建议",
                "库存情况帮我给点进货建议",
                "帮我看看该进什么",
                "只看饮料的话该补什么",
                "该补什么");
        }

        private static bool LooksLikeNewProductAdvice(string text)
        {
            if (ContainsAny(text, "新品", "新商品", "额外进", "还可以进", "还能进", "扩品", "拓展", "品类扩展"))
            {
                return true;
            }

            return ContainsAny(text, "除了", "之外", "不存在", "没有的")
                && ContainsAny(text, "进哪些", "进什么", "进点什么", "商品", "货", "售卖", "卖");
        }

        private static bool ContainsCategoryAlias(string text)
        {
            if (ContainsAny(text, "饮料", "饮品", "喝的", "烟酒", "香烟", "烟草", "抽的", "零食", "吃的", "小吃", "薯片", "辣条", "日用品", "生活用品", "用的", "食品", "酒水"))
            {
                return true;
            }

            if (ContainsAny(text, "烟") && ContainsAny(text, "有哪些", "有什么", "哪些", "分类", "类", "库存", "目前", "当前", "现在"))
            {
                return true;
            }

            if (ContainsAny(text, "水") && ContainsAny(text, "有哪些", "有什么", "哪些", "喝的", "饮品", "饮料", "分类", "类"))
            {
                return true;
            }

            return false;
        }

        private static void SetAnalysis(AiIntentResult result, string analysisKey, decimal confidence)
        {
            if (confidence >= result.AnalysisConfidence)
            {
                result.AnalysisKey = analysisKey;
                result.AnalysisConfidence = confidence;
            }
        }

        private static void MarkUnknown(AiIntentResult result)
        {
            result.RouteType = AiIntentResult.RouteUnknown;
            result.FollowUpQuestion = "你是想查询信息，还是要执行入库、销售、改价等操作？可以直接说“查售价”“查库存”“登记入库”这类更明确的话。";
        }

        private static void AddAnalysisIntentKey(AiIntentResult result, string analysisKey)
        {
            if (string.IsNullOrWhiteSpace(analysisKey))
            {
                return;
            }

            string key = analysisKey;
            if (analysisKey == "inventory")
            {
                key = "inventoryRisk";
            }
            else if (analysisKey == "hot_slow")
            {
                key = "hotSlow";
            }

            if (!ContainsKey(result, key))
            {
                result.IntentKeys.Add(key);
            }
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsKey(AiIntentResult result, string key)
        {
            foreach (string existingKey in result.IntentKeys)
            {
                if (existingKey == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
