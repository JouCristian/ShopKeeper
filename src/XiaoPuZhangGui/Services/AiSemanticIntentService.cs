using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiSemanticIntentService
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public async Task<AiSemanticIntentResult> ClassifyAsync(
            string userText,
            IList<AiStoredMessage> recentMessages,
            AiSettings settings,
            AiStoreProfile profile,
            CancellationToken cancellationToken)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                return Fail("AI API Key is empty.");
            }

            if (string.IsNullOrWhiteSpace(userText))
            {
                return Fail("User text is empty.");
            }

            DeepSeekClient client = new DeepSeekClient(settings.AiBaseUrl, settings.AiModel, settings.AiApiKey);
            AiResponseResult response = await client.SendJsonChatAsync(
                settings,
                BuildMessages(userText, recentMessages, profile),
                cancellationToken);

            if (!response.Success)
            {
                return Fail(response.ErrorMessage);
            }

            return Parse(response.Content);
        }

        private static List<AiChatMessage> BuildMessages(string userText, IList<AiStoredMessage> recentMessages, AiStoreProfile profile)
        {
            string systemPrompt =
                "你是“小铺掌柜 AI智能版”的隐藏语义理解器。你不回答用户，只把用户自然语言理解成稳定 JSON 任务单。\n"
                + "DeepSeek 负责理解和规划；本地程序负责查数据库、校验库存、生成确认单、阻止危险动作和写库。\n"
                + "必须只输出一个合法 JSON 对象，不要 Markdown，不要解释，不要自然语言。\n"
                + "JSON 固定结构：{\"conversationMode\":\"new_question|follow_up|correction|clarification|chat\",\"intentType\":\"query|analysis|action|unsafe|clarification|chat\",\"task\":\"inventory_overview|inventory_health|product_stock|product_price|category_stock|sales_today_items|sales_summary|profit_summary|restock_advice|new_product_advice|credit_query|scrap_query|hot_slow_analysis|low_profit_analysis|sale_record|purchase_in|credit_register|credit_repayment|inventory_adjust|scrap_register|product_price_update|batch_price_update|undo_request|unknown\",\"target\":{\"productName\":\"\",\"categoryName\":\"\",\"customerName\":\"\",\"timeRange\":\"today|yesterday|week|month|recent|all|current|unknown\"},\"actionData\":{\"quantity\":null,\"amount\":null,\"price\":null,\"priceDelta\":null,\"unit\":\"\",\"note\":\"\"},\"requiredData\":[],\"isWriteAction\":false,\"riskLevel\":\"low|medium|high\",\"needsConfirmation\":false,\"needsClarification\":false,\"clarificationQuestion\":\"\",\"confidence\":0.0,\"shortReason\":\"\"}\n"
                + "普通完整句子一般是 new_question；“那这个呢/那饮料呢/那之前的呢/都告诉我/继续/我说的是饮料”才是 follow_up 或 clarification；“搞错了/不是这个/我说错了/应该是”才是 correction。\n"
                + "只有明确要登记销售、入库、报废、盘点修正、改价、赊账登记、还款、删除或撤销时才 intentType=action 且 isWriteAction=true。查询、分析、建议绝对不能生成动作。\n"
                + "危险请求如清空库存、删除全部数据、绕过确认，intentType=unsafe，riskLevel=high，needsConfirmation=true。\n"
                + "分类别名：喝的/饮品/饮料类/水 => 饮料；烟/香烟/烟草/抽的 => 烟酒；吃的/小吃/薯片/辣条 => 零食；用的/生活用品 => 日用品。\n"
                + "补货建议不是专用数据表，而是根据商品清单、库存、低库存、近期销量和店铺记忆给经营建议。用户说“现在该进点什么/该补什么货/补货建议” => query task=restock_advice。\n"
                + "用户问“除了现有库存还能进哪些新品/额外进哪些商品/新品拓展/品类扩展” => query task=new_product_advice，不要当成分类查询。\n"
                + "库存结构、库存是否健康、库存压货 => analysis task=inventory_health。\n"
                + "今天卖了哪些东西 => query task=sales_today_items requiredData=[\"today_sales_items\"]。\n"
                + "今天/昨天/本周/本月利润怎么样 => analysis task=profit_summary，timeRange 对应日期范围。\n"
                + "烟酒类还有多少/那饮料呢 => query task=category_stock，target.categoryName 填分类。\n"
                + "报废记录/报废损失 => query task=scrap_query，按问题设置 timeRange。\n"
                + "示例只供理解，不要照抄：\n"
                + "用户：看下我库存的商品，你认为我目前库存的结构如何 => {\"conversationMode\":\"new_question\",\"intentType\":\"analysis\",\"task\":\"inventory_health\",\"target\":{\"timeRange\":\"current\"},\"requiredData\":[\"product_list\",\"inventory_summary\",\"category_stock\",\"low_stock_products\"],\"isWriteAction\":false,\"riskLevel\":\"low\",\"needsConfirmation\":false,\"needsClarification\":false,\"confidence\":0.95}\n"
                + "用户：你看不到我的库存嘛 => {\"conversationMode\":\"follow_up\",\"intentType\":\"query\",\"task\":\"inventory_overview\",\"target\":{\"timeRange\":\"current\"},\"requiredData\":[\"product_list\",\"inventory_summary\"],\"confidence\":0.9}\n"
                + "用户：我今天卖了哪些东西 => {\"conversationMode\":\"new_question\",\"intentType\":\"query\",\"task\":\"sales_today_items\",\"target\":{\"timeRange\":\"today\"},\"requiredData\":[\"today_sales_items\"],\"confidence\":0.95}\n"
                + "用户：根据库存建议我进什么货 => {\"conversationMode\":\"new_question\",\"intentType\":\"query\",\"task\":\"restock_advice\",\"requiredData\":[\"product_list\",\"inventory_summary\",\"low_stock_products\",\"recent_sales\"],\"confidence\":0.95}\n"
                + "用户：除了库存里这些货还可以额外进哪些商品 => {\"conversationMode\":\"new_question\",\"intentType\":\"query\",\"task\":\"new_product_advice\",\"requiredData\":[\"product_list\",\"category_stock\",\"store_profile\"],\"confidence\":0.95}\n"
                + "用户：报废2瓶可乐 => {\"conversationMode\":\"new_question\",\"intentType\":\"action\",\"task\":\"scrap_register\",\"target\":{\"productName\":\"可乐\"},\"actionData\":{\"quantity\":2,\"unit\":\"瓶\"},\"isWriteAction\":true,\"riskLevel\":\"medium\",\"needsConfirmation\":true,\"confidence\":0.9}\n"
                + "用户：把所有饮料都涨价1块 => {\"conversationMode\":\"new_question\",\"intentType\":\"action\",\"task\":\"batch_price_update\",\"target\":{\"categoryName\":\"饮料\"},\"actionData\":{\"priceDelta\":1},\"requiredData\":[\"product_list\",\"category_stock\"],\"isWriteAction\":true,\"riskLevel\":\"high\",\"needsConfirmation\":true,\"confidence\":0.95}\n";

            List<AiChatMessage> messages = new List<AiChatMessage>();
            messages.Add(AiChatMessage.System(systemPrompt));
            if (profile != null)
            {
                messages.Add(AiChatMessage.System("店铺记忆：\n" + profile.ToPromptText()));
            }

            string history = BuildRecentHistory(recentMessages);
            if (!string.IsNullOrWhiteSpace(history))
            {
                messages.Add(AiChatMessage.System("最近对话上下文：\n" + history));
            }

            messages.Add(AiChatMessage.User("请识别这句话的业务语义，只输出 JSON：\n" + userText));
            return messages;
        }

        private static string BuildRecentHistory(IList<AiStoredMessage> recentMessages)
        {
            if (recentMessages == null || recentMessages.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();
            int start = Math.Max(0, recentMessages.Count - 8);
            for (int index = start; index < recentMessages.Count; index++)
            {
                AiStoredMessage message = recentMessages[index];
                if (message == null || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                if (message.MessageType == "action_draft" || message.MessageType == "error")
                {
                    continue;
                }

                string content = message.Content.Replace("\r", " ").Replace("\n", " ");
                if (content.Length > 180)
                {
                    content = content.Substring(0, 180);
                }

                lines.Add((message.Role == "user" ? "用户：" : "助手：") + content);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static AiSemanticIntentResult Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Fail("Semantic JSON is empty.");
            }

            try
            {
                string json = ExtractJsonObject(content);
                Dictionary<string, object> root = Serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null)
                {
                    return Fail("Semantic JSON root is invalid.");
                }

                AiSemanticIntentResult result = new AiSemanticIntentResult
                {
                    Success = true,
                    RawJson = json,
                    ConversationMode = ReadString(root, "conversationMode", "mode"),
                    IntentType = ReadString(root, "intentType", "routeType", "intent"),
                    SemanticTask = NormalizeTask(ReadString(root, "task", "queryKind", "analysisKey", "actionType")),
                    RouteType = NormalizeRoute(ReadString(root, "intentType", "routeType", "intent")),
                    QueryKind = NormalizeQueryKind(ReadString(root, "queryKind", "queryType")),
                    AnalysisKey = NormalizeAnalysisKey(ReadString(root, "analysisKey", "analysisType")),
                    ActionType = ReadString(root, "actionType"),
                    SubjectText = ReadString(root, "subjectText", "subject"),
                    ProductName = ReadString(root, "productName"),
                    CategoryName = ReadString(root, "categoryName", "category"),
                    CustomerName = ReadString(root, "customerName"),
                    TimeRange = NormalizeTimeRange(ReadString(root, "timeRange")),
                    NormalizedText = ReadString(root, "normalizedText"),
                    Confidence = ReadDecimal(root, "confidence"),
                    NeedsClarification = ReadBool(root, "needsClarification", "needUserClarification"),
                    ClarificationQuestion = ReadString(root, "clarificationQuestion"),
                    IsWriteAction = ReadBool(root, "isWriteAction", "writeAction"),
                    NeedsConfirmation = ReadBool(root, "needsConfirmation", "needConfirmation"),
                    RiskLevel = ReadString(root, "riskLevel"),
                    ShortReason = ReadString(root, "shortReason", "reason")
                };

                ReadTarget(root, result);
                ReadActionData(root, result);
                ReadRequiredData(root, result);
                MapSemanticTask(result);
                PromoteAmbiguousSemantic(result);
                return result;
            }
            catch (Exception ex)
            {
                return Fail("Semantic JSON parse failed: " + ex.Message);
            }
        }

        private static void PromoteAmbiguousSemantic(AiSemanticIntentResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.QueryKind == "inventory_health" || result.QueryKind == "inventory_structure")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = "inventoryRisk";
                result.QueryKind = string.Empty;
            }

            if (result.QueryKind == "hot_slow_products")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = "hotSlow";
                result.QueryKind = string.Empty;
            }

            if (result.QueryKind == "sales_summary" || result.QueryKind == "profit_summary")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                if (string.IsNullOrWhiteSpace(result.AnalysisKey))
                {
                    result.AnalysisKey = string.IsNullOrWhiteSpace(result.TimeRange) ? "today" : result.TimeRange;
                }

                result.QueryKind = string.Empty;
            }

            if (result.RouteType == AiIntentResult.RouteAnalysis && string.IsNullOrWhiteSpace(result.AnalysisKey))
            {
                result.AnalysisKey = "inventoryRisk";
            }

            if (result.RouteType == AiIntentResult.RouteQuery && string.IsNullOrWhiteSpace(result.QueryKind))
            {
                result.QueryKind = "all_inventory";
            }

            if (result.Confidence <= 0m)
            {
                result.Confidence = 0.82m;
            }
        }

        private static void ReadTarget(Dictionary<string, object> root, AiSemanticIntentResult result)
        {
            Dictionary<string, object> target = ReadDictionary(root, "target");
            if (target == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(result.ProductName))
            {
                result.ProductName = ReadString(target, "productName", "product");
            }

            if (string.IsNullOrWhiteSpace(result.CategoryName))
            {
                result.CategoryName = ReadString(target, "categoryName", "category");
            }

            if (string.IsNullOrWhiteSpace(result.CustomerName))
            {
                result.CustomerName = ReadString(target, "customerName", "customer");
            }

            if (string.IsNullOrWhiteSpace(result.TimeRange))
            {
                result.TimeRange = NormalizeTimeRange(ReadString(target, "timeRange", "range"));
            }
        }

        private static void ReadActionData(Dictionary<string, object> root, AiSemanticIntentResult result)
        {
            Dictionary<string, object> actionData = ReadDictionary(root, "actionData");
            if (actionData == null)
            {
                return;
            }

            result.ActionQuantity = ReadPlainDecimal(actionData, "quantity");
            result.ActionAmount = ReadPlainDecimal(actionData, "amount");
            result.ActionPrice = ReadPlainDecimal(actionData, "price");
            result.ActionPriceDelta = ReadPlainDecimal(actionData, "priceDelta");
            result.ActionUnit = ReadString(actionData, "unit");
            result.ActionNote = ReadString(actionData, "note");
        }

        private static void ReadRequiredData(Dictionary<string, object> root, AiSemanticIntentResult result)
        {
            if (!root.ContainsKey("requiredData") || root["requiredData"] == null)
            {
                return;
            }

            object[] values = root["requiredData"] as object[];
            if (values == null)
            {
                return;
            }

            foreach (object value in values)
            {
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    result.RequiredData.Add(value.ToString().Trim());
                }
            }
        }

        private static Dictionary<string, object> ReadDictionary(Dictionary<string, object> root, string key)
        {
            if (!root.ContainsKey(key) || root[key] == null)
            {
                return null;
            }

            return root[key] as Dictionary<string, object>;
        }

        private static void MapSemanticTask(AiSemanticIntentResult result)
        {
            if (result == null)
            {
                return;
            }

            string task = result.SemanticTask;
            if (string.IsNullOrWhiteSpace(task))
            {
                return;
            }

            if (result.IntentType == "unsafe" || task == "undo_request")
            {
                result.RouteType = AiIntentResult.RouteUnknown;
                result.NeedsClarification = false;
                return;
            }

            if (task == "inventory_overview")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "all_inventory";
            }
            else if (task == "inventory_health")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = "inventoryRisk";
            }
            else if (task == "product_stock")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "product_stock";
            }
            else if (task == "product_price")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "product_price";
            }
            else if (task == "category_stock")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "category_stock";
            }
            else if (task == "sales_today_items")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = "today";
            }
            else if (task == "sales_summary" || task == "profit_summary")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = string.IsNullOrWhiteSpace(result.TimeRange) ? "today" : result.TimeRange;
            }
            else if (task == "restock_advice")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "restock_advice";
            }
            else if (task == "new_product_advice")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "new_product_advice";
            }
            else if (task == "credit_query")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "credit_customers";
            }
            else if (task == "scrap_query")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = result.TimeRange == "today" || result.TimeRange == "month" ? "scrap_loss" : "scrap_records";
            }
            else if (task == "hot_slow_analysis")
            {
                result.RouteType = AiIntentResult.RouteAnalysis;
                result.AnalysisKey = "hotSlow";
            }
            else if (task == "low_profit_analysis")
            {
                result.RouteType = AiIntentResult.RouteQuery;
                result.QueryKind = "low_profit_products";
            }
            else if (task == "sale_record" || task == "purchase_in" || task == "credit_register" || task == "credit_repayment"
                || task == "inventory_adjust" || task == "scrap_register" || task == "product_price_update" || task == "batch_price_update")
            {
                result.RouteType = AiIntentResult.RouteAction;
                result.ActionType = MapActionType(task);
                result.IsWriteAction = true;
                result.NeedsConfirmation = true;
            }
        }

        private static string ExtractJsonObject(string content)
        {
            string value = content.Trim();
            int start = value.IndexOf('{');
            int end = value.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return value.Substring(start, end - start + 1);
            }

            return value;
        }

        private static string ReadString(Dictionary<string, object> root, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (root.ContainsKey(key) && root[key] != null)
                {
                    return root[key].ToString().Trim();
                }
            }

            return string.Empty;
        }

        private static decimal ReadDecimal(Dictionary<string, object> root, string key)
        {
            if (!root.ContainsKey(key) || root[key] == null)
            {
                return 0m;
            }

            decimal value;
            return decimal.TryParse(root[key].ToString(), out value) ? Math.Max(0m, Math.Min(1m, value)) : 0m;
        }

        private static decimal ReadPlainDecimal(Dictionary<string, object> root, string key)
        {
            if (!root.ContainsKey(key) || root[key] == null)
            {
                return 0m;
            }

            decimal value;
            return decimal.TryParse(root[key].ToString(), out value) ? value : 0m;
        }

        private static bool ReadBool(Dictionary<string, object> root, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (!root.ContainsKey(key) || root[key] == null)
                {
                    continue;
                }

                bool value;
                if (bool.TryParse(root[key].ToString(), out value))
                {
                    return value;
                }
            }

            return false;
        }

        private static string NormalizeRoute(string route)
        {
            string value = (route ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "query" || value == "search")
            {
                return AiIntentResult.RouteQuery;
            }

            if (value == "analysis" || value == "analyze")
            {
                return AiIntentResult.RouteAnalysis;
            }

            if (value == "action" || value == "business_action" || value == "write")
            {
                return AiIntentResult.RouteAction;
            }

            if (value == "chat")
            {
                return AiIntentResult.RouteChat;
            }

            if (value == "clarification" || value == "unknown")
            {
                return AiIntentResult.RouteUnknown;
            }

            return AiIntentResult.RouteUnknown;
        }

        private static string NormalizeQueryKind(string queryKind)
        {
            string value = (queryKind ?? string.Empty).Trim();
            if (value == "category_query" || value == "category_inventory")
            {
                return "category_stock";
            }

            if (value == "inventory_list" || value == "inventory" || value == "product_list")
            {
                return "all_inventory";
            }

            if (value == "low_profit")
            {
                return "low_profit_products";
            }

            if (value == "scrap")
            {
                return "scrap_records";
            }

            return value;
        }

        private static string NormalizeTask(string task)
        {
            string value = (task ?? string.Empty).Trim();
            if (value == "category_query" || value == "category_inventory")
            {
                return "category_stock";
            }

            if (value == "inventory_list" || value == "inventory" || value == "product_list" || value == "all_inventory")
            {
                return "inventory_overview";
            }

            if (value == "inventory_structure" || value == "inventoryRisk")
            {
                return "inventory_health";
            }

            if (value == "product_expansion" || value == "category_expansion" || value == "new_products" || value == "new_product_suggestion")
            {
                return "new_product_advice";
            }

            if (value == "scrap_records" || value == "scrap_loss")
            {
                return "scrap_query";
            }

            if (value == "low_profit_products")
            {
                return "low_profit_analysis";
            }

            return value;
        }

        private static string MapActionType(string task)
        {
            if (task == "purchase_in")
            {
                return AiActionTypes.PurchaseIn;
            }

            if (task == "sale_record")
            {
                return AiActionTypes.SaleRecord;
            }

            if (task == "credit_register" || task == "credit_repayment")
            {
                return AiActionTypes.CreditRegister;
            }

            if (task == "inventory_adjust" || task == "scrap_register")
            {
                return AiActionTypes.InventoryAdjust;
            }

            if (task == "product_price_update" || task == "batch_price_update")
            {
                return AiActionTypes.ProductPriceUpdate;
            }

            return AiActionTypes.Unknown;
        }

        private static string NormalizeAnalysisKey(string analysisKey)
        {
            string value = (analysisKey ?? string.Empty).Trim();
            if (value == "inventory_health" || value == "inventory_structure" || value == "restock")
            {
                return "inventoryRisk";
            }

            if (value == "hot_slow_products")
            {
                return "hotSlow";
            }

            return value;
        }

        private static string NormalizeTimeRange(string timeRange)
        {
            string value = (timeRange ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "today" || value == "今日" || value == "今天")
            {
                return "today";
            }

            if (value == "yesterday" || value == "昨日" || value == "昨天")
            {
                return "yesterday";
            }

            if (value == "week" || value == "本周")
            {
                return "week";
            }

            if (value == "month" || value == "本月")
            {
                return "month";
            }

            return value;
        }

        private static AiSemanticIntentResult Fail(string message)
        {
            return new AiSemanticIntentResult
            {
                Success = false,
                ErrorMessage = message ?? string.Empty
            };
        }
    }
}
