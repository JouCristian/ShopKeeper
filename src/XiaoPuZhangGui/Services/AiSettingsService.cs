using System;
using System.Security.Cryptography;
using System.Text;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiSettingsService
    {
        public AiSettings Load()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            return new AiSettings
            {
                AiEnabled = config.AiEnabled,
                AiProvider = string.IsNullOrWhiteSpace(config.AiProvider) ? "DeepSeek" : config.AiProvider,
                AiBaseUrl = string.IsNullOrWhiteSpace(config.AiBaseUrl) ? "https://api.deepseek.com" : config.AiBaseUrl,
                AiModel = string.IsNullOrWhiteSpace(config.AiModel) ? "deepseek-v4-flash" : config.AiModel,
                HasApiKey = !string.IsNullOrWhiteSpace(config.AiApiKeyEncrypted),
                AiApiKeyMasked = config.AiApiKeyMasked ?? string.Empty,
                LastConnectionTestTime = config.LastConnectionTestTime ?? string.Empty
            };
        }

        public void Save(AiSettings settings, string plainApiKey)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            config.AiEnabled = settings.AiEnabled;
            config.AiProvider = string.IsNullOrWhiteSpace(settings.AiProvider) ? "DeepSeek" : settings.AiProvider.Trim();
            config.AiBaseUrl = NormalizeBaseUrl(settings.AiBaseUrl);
            config.AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel.Trim();

            if (!string.IsNullOrWhiteSpace(plainApiKey))
            {
                string trimmedKey = plainApiKey.Trim();
                config.AiApiKeyEncrypted = Encrypt(trimmedKey);
                config.AiApiKeyMasked = MaskApiKey(trimmedKey);
            }

            config.LastConnectionTestTime = settings.LastConnectionTestTime ?? string.Empty;
            AppConfigService.Save(config);
        }

        public string GetApiKey()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            if (string.IsNullOrWhiteSpace(config.AiApiKeyEncrypted))
            {
                return string.Empty;
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(config.AiApiKeyEncrypted);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void UpdateLastConnectionTestTime(DateTime time)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            config.LastConnectionTestTime = time.ToString("yyyy-MM-dd HH:mm:ss");
            AppConfigService.Save(config);
        }

        public void Clear()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            config.AiEnabled = false;
            config.AiProvider = "DeepSeek";
            config.AiBaseUrl = "https://api.deepseek.com";
            config.AiModel = "deepseek-v4-flash";
            config.AiApiKeyEncrypted = string.Empty;
            config.AiApiKeyMasked = string.Empty;
            config.LastConnectionTestTime = string.Empty;
            AppConfigService.Save(config);
        }

        private static string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return "https://api.deepseek.com";
            }

            return baseUrl.Trim().TrimEnd('/');
        }

        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return string.Empty;
            }

            string prefix = apiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) ? "sk-" : string.Empty;
            string suffix = apiKey.Length > 4 ? apiKey.Substring(apiKey.Length - 4) : apiKey;
            return prefix + "************" + suffix;
        }
    }
}
