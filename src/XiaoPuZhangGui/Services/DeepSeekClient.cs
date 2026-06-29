using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class DeepSeekClient
    {
        private const int ChatTimeoutSeconds = 60;
        private const int TestTimeoutSeconds = 8;

        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;

        public DeepSeekClient(string baseUrl, string model, string apiKey)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
            _model = string.IsNullOrWhiteSpace(model) ? "deepseek-v4-flash" : model.Trim();
            _apiKey = apiKey ?? string.Empty;
        }

        public async Task<DeepSeekConnectionResult> TestConnectionAsync()
        {
            AiSettings settings = new AiSettings
            {
                AiBaseUrl = _baseUrl,
                AiModel = _model,
                AiApiKey = _apiKey
            };

            AiResponseResult result = await SendChatAsync(
                settings,
                new List<AiChatMessage>
                {
                    AiChatMessage.System("你是小铺掌柜 AI 连接测试助手，只需要简短回复。"),
                    AiChatMessage.User("请回复：连接成功")
                },
                CancellationToken.None);

            return new DeepSeekConnectionResult
            {
                Success = result.Success,
                Message = result.Success ? "连接成功，当前模型：" + _model : result.ErrorMessage
            };
        }

        public async Task<AiResponseResult> SendChatAsync(
            AiSettings settings,
            List<AiChatMessage> messages,
            CancellationToken cancellationToken)
        {
            return await SendChatAsync(settings, messages, cancellationToken, false);
        }

        public async Task<AiResponseResult> SendJsonChatAsync(
            AiSettings settings,
            List<AiChatMessage> messages,
            CancellationToken cancellationToken)
        {
            return await SendChatAsync(settings, messages, cancellationToken, true);
        }

        public async Task<AiActionDraftParseResult> ParseActionDraftAsync(
            string userText,
            AiStoreProfile profile,
            BusinessSummaryResult liveContext,
            long conversationId,
            CancellationToken cancellationToken)
        {
            AiSettings settings = new AiSettings
            {
                AiBaseUrl = _baseUrl,
                AiModel = _model,
                AiApiKey = _apiKey
            };

            AiActionDraftService draftService = new AiActionDraftService();
            List<AiChatMessage> messages = BuildActionParseMessages(userText, profile, liveContext, false);
            AiResponseResult result = await SendChatAsync(settings, messages, cancellationToken, true);
            if (result.Success)
            {
                AiActionDraftParseResult parsed = draftService.CreateDraftFromJson(conversationId, userText, result.Content);
                if (parsed.Success)
                {
                    return parsed;
                }
            }

            List<AiChatMessage> retryMessages = BuildActionParseMessages(userText, profile, liveContext, true);
            AiResponseResult retry = await SendChatAsync(settings, retryMessages, cancellationToken, true);
            if (!retry.Success)
            {
                return AiActionDraftParseResult.Fail(retry.ErrorMessage);
            }

            AiActionDraftParseResult retryParsed = draftService.CreateDraftFromJson(conversationId, userText, retry.Content);
            return retryParsed.Success
                ? retryParsed
                : AiActionDraftParseResult.Fail("AI 没有识别成功，请换一种说法或手动登记。");
        }

        private async Task<AiResponseResult> SendChatAsync(
            AiSettings settings,
            List<AiChatMessage> messages,
            CancellationToken cancellationToken,
            bool requireJson)
        {
            if (settings == null)
            {
                return Fail("AI 配置为空，请先检查 API 设置。");
            }

            string apiKey = string.IsNullOrWhiteSpace(settings.AiApiKey) ? _apiKey : settings.AiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Fail("API Key 为空，请先配置 DeepSeek API Key。");
            }

            if (messages == null || messages.Count == 0)
            {
                return Fail("没有可发送给 AI 的消息。");
            }

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(ChatTimeoutSeconds);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("XiaoPuZhangGui-AI-Edition");

                    string requestJson = BuildRequestJson(settings, messages, requireJson);
                    using (StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                    using (HttpResponseMessage response = await client.PostAsync(
                        NormalizeBaseUrl(settings.AiBaseUrl) + "/chat/completions",
                        content,
                        cancellationToken))
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            return Fail(BuildHttpErrorMessage(response.StatusCode, responseText));
                        }

                        if (string.IsNullOrWhiteSpace(responseText))
                        {
                            return Fail("AI 服务返回内容为空，请稍后再试。");
                        }

                        string assistantContent = ParseAssistantContent(responseText);
                        if (string.IsNullOrWhiteSpace(assistantContent))
                        {
                            return Fail("AI 服务返回内容为空，请稍后再试。");
                        }

                        return new AiResponseResult
                        {
                            Success = true,
                            Content = assistantContent.Trim(),
                            ErrorMessage = string.Empty
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Fail("AI 请求已取消。");
                }

                return Fail("请求超时，请稍后再试。");
            }
            catch (HttpRequestException)
            {
                return Fail("网络不可用或 AI 服务暂时无法访问。");
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.Timeout)
                {
                    return Fail("请求超时，请稍后再试。");
                }

                return Fail("网络不可用或 AI 服务暂时无法访问。");
            }
            catch (InvalidOperationException)
            {
                return Fail("JSON 解析失败，AI 服务返回格式暂时无法识别。");
            }
            catch
            {
                return Fail("AI 服务暂时不可用，本地功能不受影响。");
            }
        }

        private static string BuildRequestJson(AiSettings settings, List<AiChatMessage> messages)
        {
            return BuildRequestJson(settings, messages, false);
        }

        private static string BuildRequestJson(AiSettings settings, List<AiChatMessage> messages, bool requireJson)
        {
            List<Dictionary<string, string>> messagePayload = new List<Dictionary<string, string>>();
            foreach (AiChatMessage message in messages)
            {
                if (message == null || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                string role = NormalizeRole(message.Role);
                messagePayload.Add(new Dictionary<string, string>
                {
                    { "role", role },
                    { "content", message.Content }
                });
            }

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "model", string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel.Trim() },
                { "messages", messagePayload },
                { "temperature", 0.3 },
                { "stream", false }
            };

            if (requireJson)
            {
                payload["temperature"] = 0.1;
                payload["response_format"] = new Dictionary<string, string>
                {
                    { "type", "json_object" }
                };
            }

            return new JavaScriptSerializer().Serialize(payload);
        }

        private static List<AiChatMessage> BuildActionParseMessages(
            string userText,
            AiStoreProfile profile,
            BusinessSummaryResult liveContext,
            bool retry)
        {
            string systemPrompt =
                "你是“小铺掌柜 AI 动作解析助手”。请输出 json。你的任务不是聊天回答，而是把小卖铺老板的自然语言经营操作拆解为结构化 json 动作草稿。"
                + "用户可能一句话里包含多个商品、多个动作、模糊单位、口语价格。你必须尽量理解，但不能编造关键字段。"
                + "缺失字段放入 missingFields，不确定内容放入 warnings，并给出 confidence。你不能输出解释文字，只能输出合法 json。"
                + "你不能要求直接操作数据库，所有动作都只是草稿，最终由本地程序和用户确认。"
                + "你只负责解析明确的经营操作。查询价格、查询库存、查看赊账、分析报表、库存建议、经营分析都不是动作草稿。只有用户明确要求入库、销售、赊账登记、库存修正、改价、删除或撤销时，才输出 action。否则应输出 intent=query、analysis 或 chat，并且 actions 必须为空，不要生成 unknown action。"
                + "动作类型只能是 purchase_in、sale_record、inventory_adjust、credit_register、product_price_update、delete_or_undo_request、unknown。"
                + "价格口语要转数字，例如 8毛=0.8，一块五=1.5。"
                + "一箱24瓶，两箱要折算 quantity=48，unit=瓶，并在 warnings 或 notes 保留箱规说明。"
                + "必须区分单位和规格：瓶、包、袋、条、件、个、箱、盒、支、根、听、罐、桶、杯、提、板是 unit，不是 productSpec；500ml、1L、48g、250g 才是 productSpec。"
                + "例如“两包滕王阁”应输出 productName=滕王阁、quantity=2、unit=包、productSpec为空。规格为空不能放入 missingFields。"
                + "销售动作 sale_record 如果用户说“收了38元”，actualReceivedAmount=38；如果说“按标价卖”，actualReceivedAmount 可留空，由本地系统按商品售价计算；如果说“便宜了两块钱”，notes 写“熟人优惠 2 元”。"
                + "无法确定时设置 needUserClarification=true。";

            if (retry)
            {
                systemPrompt += "上一次输出无法解析。只输出合法 JSON，不要输出解释文字，不要使用 Markdown。";
            }

            StringBuilder userPrompt = new StringBuilder();
            userPrompt.AppendLine("请把下面用户输入解析成动作草稿 JSON。");
            userPrompt.AppendLine();
            userPrompt.AppendLine("JSON 格式示例：");
            userPrompt.AppendLine("{");
            userPrompt.AppendLine("\"intent\":\"business_action\",");
            userPrompt.AppendLine("\"summary\":\"用户想登记一批进货，共识别出 2 条入库草稿\",");
            userPrompt.AppendLine("\"actions\":[{\"actionType\":\"purchase_in\",\"productName\":\"可乐\",\"productSpec\":\"1升\",\"category\":\"饮料\",\"quantity\":72,\"unit\":\"瓶\",\"purchasePrice\":3.5,\"salePrice\":5,\"productionDate\":null,\"expiryDate\":null,\"shelfLifeEnabled\":false,\"actualReceivedAmount\":null,\"missingFields\":[],\"warnings\":[],\"confidence\":0.92}],");
            userPrompt.AppendLine("\"needUserClarification\":false,");
            userPrompt.AppendLine("\"clarificationQuestion\":null");
            userPrompt.AppendLine("}");
            userPrompt.AppendLine();
            userPrompt.AppendLine("店铺记忆：");
            userPrompt.AppendLine(profile == null ? "未填写店铺记忆。" : profile.ToPromptText());
            userPrompt.AppendLine();
            userPrompt.AppendLine("可用本地摘要，只用于辅助识别，不代表完整数据库：");
            if (liveContext != null && liveContext.Success)
            {
                userPrompt.AppendLine(liveContext.SummaryText ?? string.Empty);
            }
            else
            {
                userPrompt.AppendLine("本次没有可用本地摘要。");
            }

            userPrompt.AppendLine();
            userPrompt.AppendLine("用户输入：");
            userPrompt.AppendLine(userText ?? string.Empty);
            userPrompt.AppendLine();
            userPrompt.AppendLine("请输出 json。");

            return new List<AiChatMessage>
            {
                AiChatMessage.System(systemPrompt),
                AiChatMessage.User(userPrompt.ToString())
            };
        }

        private static string ParseAssistantContent(string responseText)
        {
            object parsed = new JavaScriptSerializer().DeserializeObject(responseText);
            Dictionary<string, object> root = parsed as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("choices"))
            {
                throw new InvalidOperationException("Missing choices");
            }

            object[] choices = root["choices"] as object[];
            if (choices == null || choices.Length == 0)
            {
                throw new InvalidOperationException("Empty choices");
            }

            Dictionary<string, object> choice = choices[0] as Dictionary<string, object>;
            if (choice == null || !choice.ContainsKey("message"))
            {
                throw new InvalidOperationException("Missing message");
            }

            Dictionary<string, object> message = choice["message"] as Dictionary<string, object>;
            if (message == null || !message.ContainsKey("content"))
            {
                throw new InvalidOperationException("Missing content");
            }

            return message["content"] == null ? string.Empty : message["content"].ToString();
        }

        private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseText)
        {
            int code = (int)statusCode;
            string errorMessage = TryReadErrorMessage(responseText);
            string lowerError = string.IsNullOrWhiteSpace(errorMessage) ? string.Empty : errorMessage.ToLowerInvariant();
            if (code == 401 || code == 403)
            {
                return "API Key 可能错误、已过期或没有权限，请检查后再试。";
            }

            if (code == 402 || lowerError.Contains("insufficient") || lowerError.Contains("quota") || lowerError.Contains("balance"))
            {
                return "AI 服务余额可能不足，请检查 DeepSeek 账户余额或套餐。";
            }

            if (code == 400 || code == 404)
            {
                return "API 地址或模型名称可能已变更，请检查当前模型和 API 地址。";
            }

            if (code == 408)
            {
                return "请求超时，请稍后再试。";
            }

            if (code == 429)
            {
                return "AI 服务请求过于频繁，请稍后再试。";
            }

            if (code >= 500)
            {
                return "AI 服务暂时不可用，本地功能不受影响。";
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return "AI 服务返回错误：" + errorMessage;
            }

            return "API 服务返回错误，状态码：" + code;
        }

        private static string TryReadErrorMessage(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return string.Empty;
            }

            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(responseText) as Dictionary<string, object>;
                if (root != null && root.ContainsKey("error"))
                {
                    Dictionary<string, object> error = root["error"] as Dictionary<string, object>;
                    if (error != null && error.ContainsKey("message"))
                    {
                        return error["message"] == null ? string.Empty : error["message"].ToString();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeRole(string role)
        {
            if (role == "system" || role == "assistant" || role == "user")
            {
                return role;
            }

            return "user";
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return "https://api.deepseek.com";
            }

            return baseUrl.Trim().TrimEnd('/');
        }

        private static AiResponseResult Fail(string message)
        {
            return new AiResponseResult
            {
                Success = false,
                Content = string.Empty,
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "AI 服务暂时不可用，本地功能不受影响。" : message
            };
        }
    }
}
