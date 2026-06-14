using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Katalogcu.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Katalogcu.API.Tests.Services;

public sealed class ChatQueryServiceTests
{
    [Fact]
    public void ExtractSearchTokens_PreservesRawTurkishTokenForDatabaseFiltering()
    {
        var tokens = ChatQueryService.ExtractSearchTokens("yağ deposu contası");

        Assert.Contains(tokens, token => token.Raw == "yağ" && token.Normalized == "YAG");
        Assert.Contains(tokens, token => token.Raw == "deposu" && token.Normalized == "DEPOSU");
        Assert.Contains(tokens, token => token.Raw == "contası" && token.Normalized == "CONTASI");
    }

    [Fact]
    public void ExtractSearchTokens_DropsShortNumericNoiseButKeepsRealIdentifiers()
    {
        var tokens = ChatQueryService.ExtractSearchTokens("ref 12 4109410 yağ");

        Assert.DoesNotContain(tokens, token => token.Normalized == "12");
        Assert.Contains(tokens, token => token.Normalized == "4109410");
        Assert.Contains(tokens, token => token.Normalized == "YAG");
    }

    [Fact]
    public void BuildContainsLikePattern_EscapesUserWildcardCharacters()
    {
        var pattern = ChatQueryService.BuildContainsLikePattern("A_10%\\");

        Assert.Equal("%A\\_10\\%\\\\%", pattern);
    }

    [Fact]
    public void ApplyNameSearchFilters_TranslatesToEscapedPostgresIlikeSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=katalogcu_test;Username=test;Password=test",
                npgsql => npgsql.UseVector())
            .Options;
        using var context = new AppDbContext(options);
        var tokens = ChatQueryService.ExtractSearchTokens("yağ A_10%");

        var query = ChatQueryService.ApplyNameSearchFilters(
            context.CatalogItems.AsNoTracking(),
            tokens);

        var sql = query.ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("ESCAPE", sql);
        Assert.Contains("\"SearchText\"", sql);
        Assert.Contains("\"Dimensions\"", sql);
    }

    [Fact]
    public void ScoreNameMatch_UsesCanonicalSearchTextWhenDisplayNameIsWeak()
    {
        var item = new CatalogItem
        {
            PartCode = "4109410",
            RefNumber = "12",
            PartName = "Unknown Part",
            Description = "",
            SearchText = "Katalog Parça Adı: Yağ deposu contası | Uyumlu Makine: Yamato",
        };
        var tokens = ChatQueryService.ExtractSearchTokens("yağ deposu contası");

        var score = ChatQueryService.ScoreNameMatch(item, "yağ deposu contası", tokens);

        Assert.True(score > 0);
    }

    [Fact]
    public void ScoreNameMatch_RejectsNearMissWhenMultiTokenQueryOnlyPartiallyMatches()
    {
        var item = new CatalogItem
        {
            PartCode = "999",
            RefNumber = "9",
            PartName = "Yağ filtresi",
            Description = "Filtre elemanı",
            SearchText = "Katalog Parça Adı: Yağ filtresi",
        };
        var tokens = ChatQueryService.ExtractSearchTokens("yağ deposu");

        var score = ChatQueryService.ScoreNameMatch(item, "yağ deposu", tokens);

        Assert.Equal(0, score);
    }
}
