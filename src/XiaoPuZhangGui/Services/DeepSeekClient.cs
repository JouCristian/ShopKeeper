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

                    string requestJson = BuildRequestJson(settings, messages);
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

            return new JavaScriptSerializer().Serialize(payload);
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
            if (code == 401 || code == 403)
            {
                return "API Key 可能错误或没有权限，请检查后再试。";
            }

            if (code == 402)
            {
                return "AI 服务余额可能不足，请检查 DeepSeek 账户。";
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

            string errorMessage = TryReadErrorMessage(responseText);
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
