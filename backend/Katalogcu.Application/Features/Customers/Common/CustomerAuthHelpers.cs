using System.Security.Cryptography;

namespace Katalogcu.Application.Features.Customers.Common;

internal static class CustomerAuthHelpers
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ResetCodeDuration = TimeSpan.FromMinutes(10);

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return email.Trim().ToLowerInvariant();
    }

    public static bool IsPasswordStrong(string password)
    {
        return !string.IsNullOrWhiteSpace(password) && password.Length >= 8;
    }

    public static void CreatePasswordHash(string password, out string hash, out string salt)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 120_000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        hash = Convert.ToBase64String(hashBytes);
        salt = Convert.ToBase64String(saltBytes);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
            return false;

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

        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 120_000, HashAlgorithmName.SHA256);
        var actualHashBytes = pbkdf2.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }

    public static string GenerateResetCode()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return code.ToString("D6");
    }

    public static string CreateSessionToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}
