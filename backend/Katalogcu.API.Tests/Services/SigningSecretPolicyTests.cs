using Katalogcu.API.Services;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class SigningSecretPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    public void IsAcceptable_RejectsMissingWeakOrKnownSecrets(string? secret)
    {
        Assert.False(SigningSecretPolicy.IsAcceptable(secret));
    }

    [Fact]
    public void IsAcceptable_RejectsFormerRepositoryDefaults()
    {
        var formerJwtDefault = string.Concat("Your", "SuperSecret", "KeyForJwtTokenGeneration123!");
        var formerComposeDefault = string.Concat("catalog_only_", "local_jwt_", "secret_32_chars_minimum");

        Assert.False(SigningSecretPolicy.IsAcceptable(formerJwtDefault));
        Assert.False(SigningSecretPolicy.IsAcceptable(formerComposeDefault));
    }

    [Fact]
    public void Generate_ReturnsIndependentAcceptableSecrets()
    {
        var first = SigningSecretPolicy.Generate();
        var second = SigningSecretPolicy.Generate();

        Assert.True(SigningSecretPolicy.IsAcceptable(first));
        Assert.True(SigningSecretPolicy.IsAcceptable(second));
        Assert.NotEqual(first, second);
    }
}
