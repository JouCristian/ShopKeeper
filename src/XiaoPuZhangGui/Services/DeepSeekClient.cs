using System;
using System.Net;
using System.Threading.Tasks;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class DeepSeekClient
    {
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _apiKey;

        public DeepSeekClient(string baseUrl, string model, string apiKey)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepseek.com" : baseUrl.Trim().TrimEnd('/');
            _model = string.IsNullOrWhiteSpace(model) ? "deepseek-v4-flash" : model.Trim();
            _apiKey = apiKey ?? string.Empty;
        }

        public Task<DeepSeekConnectionResult> TestConnectionAsync()
        {
            return Task.Run(delegate
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    return Fail("请先填写 API Key");
                }

                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_baseUrl + "/models");
                    request.Method = "GET";
                    request.Timeout = 5000;
                    request.ReadWriteTimeout = 5000;
                    request.Accept = "application/json";
                    request.UserAgent = "XiaoPuZhangGui-AI-Edition";
                    request.Headers[HttpRequestHeader.Authorization] = "Bearer " + _apiKey;

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                        {
                            return new DeepSeekConnectionResult
                            {
                                Success = true,
                                Message = "连接成功，当前模型：" + _model
                            };
                        }

                        return Fail("AI 服务暂时不可用，状态码：" + (int)response.StatusCode);
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        int statusCode = (int)response.StatusCode;
                        response.Close();
                        if (statusCode == 401 || statusCode == 403)
                        {
                            return Fail("API Key 可能错误或没有权限");
                        }

                        if (statusCode == 429)
                        {
                            return Fail("AI 服务请求过于频繁，请稍后再试");
                        }

                        if (statusCode >= 500)
                        {
                            return Fail("AI 服务暂时不可用");
                        }

                        return Fail("AI 服务返回异常，状态码：" + statusCode);
                    }

                    if (ex.Status == WebExceptionStatus.Timeout)
                    {
                        return Fail("请求超时");
                    }

                    if (ex.Status == WebExceptionStatus.NameResolutionFailure ||
                        ex.Status == WebExceptionStatus.ConnectFailure)
                    {
                        return Fail("网络不可用");
                    }

                    return Fail("AI 服务暂时不可用");
                }
                catch
                {
                    return Fail("AI 连接测试失败，请检查网络和 API 配置");
                }
            });
        }

        private static DeepSeekConnectionResult Fail(string message)
        {
            return new DeepSeekConnectionResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
