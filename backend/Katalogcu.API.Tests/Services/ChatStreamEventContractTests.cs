using System.Text.RegularExpressions;
using Katalogcu.API.Services;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class ChatStreamEventContractTests
{
    [Fact]
    public void SharedFrontendFixture_MatchesBackendSseSerializationContract()
    {
        var expected = NormalizeSse(ReadSharedFrontendFixture());
        var actual = NormalizeSse(string.Concat(
            ChatStreamEventContract.ToSseDataLine(
                ChatStreamEventContract.CreateSources(
                    new[]
                    {
                        new
                        {
                            id = "part-1",
                            catalogItemId = "ci-1",
                            code = "4109410",
                            name = "Yağ deposu contası",
                            pageNumber = "12",
                            similarity = 0.91
                        }
                    })),
            ChatStreamEventContract.ToSseDataLine(ChatStreamEventContract.CreateToken("Parça ")),
            ChatStreamEventContract.ToSseDataLine(ChatStreamEventContract.CreateToken("4109410 bulundu.")),
            ChatStreamEventContract.ToSseDataLine(ChatStreamEventContract.CreateDone())));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SharedFrontendFixture_ReplaysAsValidBackendEvents()
    {
        var fixture = ReadSharedFrontendFixture();
        var dataLines = fixture
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(4, dataLines.Length);

        var eventTypes = new List<string>();
        foreach (var line in dataLines)
        {
            Assert.True(ChatStreamEventContract.TryExtractDataPayload(line, out var payload));
            Assert.True(
                ChatStreamEventContract.TryParseDataPayload(payload, out var streamEvent, out var error),
                error);
            Assert.NotNull(streamEvent);
            eventTypes.Add(streamEvent.Type);
        }

        Assert.Equal(["sources", "token", "token", "done"], eventTypes);
    }

    private static string ReadSharedFrontendFixture()
    {
        var repoRoot = FindRepoRoot();
        var fixturePath = Path.Combine(
            repoRoot,
            "frontend",
            "katalogcu-frontend",
            "src",
            "app",
            "core",
            "services",
            "chat-stream-contract.fixture.ts");
        var source = File.ReadAllText(fixturePath);
        var match = Regex.Match(
            source,
            @"PUBLIC_CHAT_HAPPY_PATH_SSE\s*=\s*String\.raw`(?<sse>[\s\S]*?)`;",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, $"Fixture string could not be extracted from {fixturePath}");
        return match.Groups["sse"].Value;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "frontend")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be found.");
    }

    private static string NormalizeSse(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n\n";
}
