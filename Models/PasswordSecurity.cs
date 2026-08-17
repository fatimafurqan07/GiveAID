using System;
using System.Security.Cryptography;
using System.Text;

namespace GiveAID_Project.Models
{
    public static class PasswordSecurity
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 10000;

        /// <summary>
        /// Hashes a plain-text password using PBKDF2 with a cryptographic salt.
        /// Returns a formatted string: {Base64(salt)}:{Base64(hash)}
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);
                return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
            }
        }

        /// <summary>
        /// Verifies a password against the stored password hash string.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2)
            {
                // Fallback check for SHA256 hex or plain string in case of manually seeded data
                using (var sha = SHA256.Create())
                {
                    byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                    string hex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    if (string.Equals(hex, storedHash, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return string.Equals(password, storedHash, StringComparison.Ordinal);
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
                {
                    byte[] actualHash = pbkdf2.GetBytes(HashSize);
                    return SlowEquals(actualHash, expectedHash);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
