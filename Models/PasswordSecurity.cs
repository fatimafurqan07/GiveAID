using System;
using System.Security.Cryptography;

namespace GiveAID_Project.Models
{
    public static class PasswordSecurity
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int CurrentIterations = 120000;
        private const int LegacyIterations = 10000;
        private const string Version = "PBKDF2-SHA256";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            var salt = new byte[SaltSize];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(password, salt, CurrentIterations, HashAlgorithmName.SHA256))
            {
                return string.Join("$", Version, CurrentIterations,
                    Convert.ToBase64String(salt), Convert.ToBase64String(derive.GetBytes(HashSize)));
            }
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash)) return false;
            try
            {
                return storedHash.StartsWith(Version + "$", StringComparison.Ordinal)
                    ? VerifyCurrent(password, storedHash)
                    : VerifyLegacy(password, storedHash);
            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
            catch (ArgumentException) { return false; }
        }

        public static bool NeedsRehash(string storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.StartsWith(Version + "$", StringComparison.Ordinal)) return true;
            var parts = storedHash.Split('$');
            int iterations;
            return parts.Length != 4 || !int.TryParse(parts[1], out iterations) || iterations < CurrentIterations;
        }

        private static bool VerifyCurrent(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            int iterations;
            if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out iterations) || iterations < 10000) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expected.Length != HashSize) return false;
            using (var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return FixedTimeEquals(derive.GetBytes(HashSize), expected);
        }

        private static bool VerifyLegacy(string password, string storedHash)
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            if (salt.Length != SaltSize || expected.Length != HashSize) return false;
            using (var derive = new Rfc2898DeriveBytes(password, salt, LegacyIterations))
                return FixedTimeEquals(derive.GetBytes(HashSize), expected);
        }

        private static bool FixedTimeEquals(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length) return false;
            var difference = 0;
            for (var index = 0; index < first.Length; index++) difference |= first[index] ^ second[index];
            return difference == 0;
        }
    }
}