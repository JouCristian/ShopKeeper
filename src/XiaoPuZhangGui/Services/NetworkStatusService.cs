using System;
using System.Net;
using System.Threading.Tasks;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class NetworkStatusService
    {
        public Task<NetworkStatusResult> CheckAsync()
        {
            return CheckAsync("https://api.deepseek.com", 5000);
        }

        public Task<NetworkStatusResult> CheckAsync(string healthCheckUrl, int timeoutMilliseconds)
        {
            return Task.Run(delegate
            {
                DateTime checkedAt = DateTime.Now;
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(healthCheckUrl);
                    request.Method = "GET";
                    request.Timeout = timeoutMilliseconds;
                    request.ReadWriteTimeout = timeoutMilliseconds;
                    request.UserAgent = "XiaoPuZhangGui-AI-Edition";

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        return new NetworkStatusResult
                        {
                            IsNetworkAvailable = true,
                            Message = "网络可用",
                            CheckedAt = checkedAt
                        };
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        response.Close();
                        return new NetworkStatusResult
                        {
                            IsNetworkAvailable = true,
                            Message = "网络可用，AI 服务返回：" + (int)response.StatusCode,
                            CheckedAt = checkedAt
                        };
                    }

                    return new NetworkStatusResult
                    {
                        IsNetworkAvailable = false,
                        Message = BuildNetworkMessage(ex),
                        CheckedAt = checkedAt
                    };
                }
                catch
                {
                    return new NetworkStatusResult
                    {
                        IsNetworkAvailable = false,
                        Message = "网络不可用或检测地址暂时无法访问",
                        CheckedAt = checkedAt
                    };
                }
            });
        }

        private static string BuildNetworkMessage(WebException ex)
        {
            if (ex.Status == WebExceptionStatus.Timeout)
            {
                return "网络检测超时";
            }

            if (ex.Status == WebExceptionStatus.NameResolutionFailure ||
                ex.Status == WebExceptionStatus.ConnectFailure ||
                ex.Status == WebExceptionStatus.ProxyNameResolutionFailure)
            {
                return "网络不可用";
            }

            return "网络不可用或 AI 服务暂时无法访问";
        }
    }
}
