using System.Text.RegularExpressions;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal static class AuthService
    {
        private static readonly Regex PinRegex = new Regex(@"^\d{6}$", RegexOptions.Compiled);

        public static bool IsValidPin(string pin)
        {
            return PinRegex.IsMatch(pin ?? string.Empty);
        }

        public static string InitializeFirstRun(string storeName, string pin)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            config.StoreName = string.IsNullOrWhiteSpace(storeName) ? "小铺掌柜" : storeName.Trim();
            SetPin(config, pin);

            string recoveryKey = RecoveryKeyGenerator.Generate();
            SetRecoveryKey(config, recoveryKey);
            config.IsInitialized = true;

            AppConfigService.Save(config);
            return recoveryKey;
        }

        public static bool VerifyPin(string pin)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            return config.IsInitialized &&
                   IsValidPin(pin) &&
                   HashHelper.VerifySecret(pin, config.PinSalt, config.PinHash);
        }

        public static bool VerifyRecoveryKey(string recoveryKey)
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            string normalizedKey = RecoveryKeyGenerator.Normalize(recoveryKey);

            return config.IsInitialized &&
                   HashHelper.VerifySecret(normalizedKey, config.RecoveryKeySalt, config.RecoveryKeyHash);
        }

        public static bool ResetPinWithRecoveryKey(string recoveryKey, string newPin)
        {
            if (!IsValidPin(newPin) || !VerifyRecoveryKey(recoveryKey))
            {
                return false;
            }

            AppConfig config = AppConfigService.LoadOrCreateDefault();
            SetPin(config, newPin);
            AppConfigService.Save(config);
            return true;
        }

        public static string RegenerateRecoveryKey()
        {
            AppConfig config = AppConfigService.LoadOrCreateDefault();
            string recoveryKey = RecoveryKeyGenerator.Generate();
            SetRecoveryKey(config, recoveryKey);
            AppConfigService.Save(config);
            return recoveryKey;
        }

        private static void SetPin(AppConfig config, string pin)
        {
            string salt = HashHelper.CreateSalt();
            config.PinSalt = salt;
            config.PinHash = HashHelper.HashSecret(pin, salt);
        }

        private static void SetRecoveryKey(AppConfig config, string recoveryKey)
        {
            string normalizedKey = RecoveryKeyGenerator.Normalize(recoveryKey);
            string salt = HashHelper.CreateSalt();
            config.RecoveryKeySalt = salt;
            config.RecoveryKeyHash = HashHelper.HashSecret(normalizedKey, salt);
        }
    }
}
