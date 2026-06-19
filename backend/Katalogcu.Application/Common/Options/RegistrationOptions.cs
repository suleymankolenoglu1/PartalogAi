namespace Katalogcu.Application.Common.Options;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public string OwnerInviteCodes { get; init; } = string.Empty;
}
