using System;
using System.Security.Cryptography;

namespace XiaoPuZhangGui.Utils
{
    internal static class HashHelper
    {
        private const int SaltLength = 16;
        private const int HashLength = 32;
        private const int Iterations = 50000;

        public static string CreateSalt()
        {
            byte[] salt = new byte[SaltLength];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(salt);
            }

            return Convert.ToBase64String(salt);
        }

        public static string HashSecret(string secret, string saltBase64)
        {
            if (secret == null)
            {
                secret = string.Empty;
            }

            byte[] salt = Convert.FromBase64String(saltBase64);
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(secret, salt, Iterations))
            {
                return Convert.ToBase64String(deriveBytes.GetBytes(HashLength));
            }
        }

        public static bool VerifySecret(string secret, string saltBase64, string expectedHashBase64)
        {
            if (string.IsNullOrWhiteSpace(saltBase64) || string.IsNullOrWhiteSpace(expectedHashBase64))
            {
                return false;
            }

            byte[] actualHash = Convert.FromBase64String(HashSecret(secret, saltBase64));
            byte[] expectedHash = Convert.FromBase64String(expectedHashBase64);
            return FixedTimeEquals(actualHash, expectedHash);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }
}
