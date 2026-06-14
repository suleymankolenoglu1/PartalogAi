namespace Katalogcu.API.Services;

public static class JwtSecretResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var secret = FirstNonEmpty(
            configuration["JwtSettings:SecretKey"],
            configuration["JwtSecret"],
            configuration["JWT:Secret"]);

        return secret?.Trim() ?? string.Empty;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
