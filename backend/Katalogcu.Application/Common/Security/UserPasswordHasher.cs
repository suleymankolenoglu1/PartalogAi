using System.Security.Cryptography;

namespace Katalogcu.Application.Common.Security;

internal static class UserPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int IterationCount = 120_000;

    public static void CreatePasswordHash(string password, out string hash, out string salt)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Şifre boş olamaz.", nameof(password));
        }

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, IterationCount, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(HashSize);
        hash = Convert.ToBase64String(hashBytes);
        salt = Convert.ToBase64String(saltBytes);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(storedHash) ||
            string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        byte[] saltBytes;
        byte[] expectedHashBytes;
        try
        {
            saltBytes = Convert.FromBase64String(storedSalt);
            expectedHashBytes = Convert.FromBase64String(storedHash);
        }
        catch
        {
            return false;
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, IterationCount, HashAlgorithmName.SHA256);
        var actualHashBytes = pbkdf2.GetBytes(HashSize);
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }
}
