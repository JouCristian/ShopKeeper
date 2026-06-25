using System;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiIntentRouter
    {
        public AiIntentResult Route(string userQuestion)
        {
            string text = userQuestion ?? string.Empty;
            AiIntentResult result = new AiIntentResult();

            AddIfMatch(result, "today", text, "今天", "今日", "收入", "销售额", "实收", "利润", "订单", "营业额");
            AddIfMatch(result, "week", text, "本周", "这周", "周报", "最近一周");
            AddIfMatch(result, "month", text, "本月", "这个月", "月报", "最近一个月");
            AddIfMatch(result, "inventorySnapshot", text, "库存状态", "库存情况", "目前的库存", "当前库存");
            AddIfMatch(result, "inventoryRisk", text, "库存", "补货", "缺货", "快没了", "没货", "临期", "过期", "积压");
            AddIfMatch(result, "credit", text, "赊账", "欠账", "欠款", "未结清", "谁还欠", "客户欠");
            AddIfMatch(result, "hotSlow", text, "热销", "卖得最好", "卖得不好", "滞销", "不好卖", "销量", "排行");
            AddIfMatch(result, "purchaseAdvice", text, "进货", "采购", "补哪些", "少进哪些", "下次进货");

            return result;
        }

        private static void AddIfMatch(AiIntentResult result, string key, string text, params string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!ContainsKey(result, key))
                    {
                        result.IntentKeys.Add(key);
                    }

                    return;
                }
            }
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
