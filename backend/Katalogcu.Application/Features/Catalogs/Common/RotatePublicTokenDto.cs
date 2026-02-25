namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class RotatePublicTokenDto
{
    public string Token { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public int Version { get; init; }
}
