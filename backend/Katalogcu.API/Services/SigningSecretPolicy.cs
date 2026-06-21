using System.Security.Cryptography;

namespace Katalogcu.API.Services;

public static class SigningSecretPolicy
{
    private static readonly string[] ForbiddenFragments =
    [
        "CHANGE_ME",
        "YourSuperSecret",
        "YourPublicLinkSecret",
        "lokal-dev-",
        "catalog_only_local_"
    ];

    public static bool IsAcceptable(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Trim().Length < 32)
        {
            return false;
        }

        return ForbiddenFragments.All(fragment =>
            !secret.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
