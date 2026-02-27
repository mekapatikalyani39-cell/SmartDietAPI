using System.Security.Cryptography;
namespace SmartDietAPI.Models
{
    public static class PasswordHasher
    {
        public static (byte[] hash, byte[] salt) Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 64);
            return (hash, salt);
        }

        public static bool Verify(string password, byte[] salt, byte[] hash)
        {
            var computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 64);
            return CryptographicOperations.FixedTimeEquals(computed, hash);
        }
    }
}
