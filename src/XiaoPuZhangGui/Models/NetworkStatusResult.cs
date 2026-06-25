using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class NetworkStatusResult
    {
        public bool IsNetworkAvailable { get; set; }

        public string Message { get; set; }

        public DateTime CheckedAt { get; set; }

        public static NetworkStatusResult Unknown()
        {
            return new NetworkStatusResult
            {
                IsNetworkAvailable = false,
                Message = "尚未检测网络状态",
                CheckedAt = DateTime.MinValue
            };
        }
    }
}
