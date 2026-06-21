using System;
using System.Security.Cryptography;
using System.Text;

namespace XiaoPuZhangGui.Utils
{
    internal static class RecoveryKeyGenerator
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate()
        {
            return string.Format(
                "XPZG-{0}-{1}-{2}-{3}",
                DateTime.Now.Year,
                GeneratePart(4),
                GeneratePart(4),
                GeneratePart(4));
        }

        public static string Normalize(string recoveryKey)
        {
            return (recoveryKey ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string GeneratePart(int length)
        {
            byte[] bytes = new byte[length];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            StringBuilder builder = new StringBuilder(length);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(Alphabet[bytes[i] % Alphabet.Length]);
            }

            return builder.ToString();
        }
    }
}
