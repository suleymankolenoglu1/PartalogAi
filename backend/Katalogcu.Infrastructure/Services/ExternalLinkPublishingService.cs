using System.Net;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Infrastructure.Services;

public sealed class ExternalLinkPublishingService : IExternalLinkPublishingService
{
    private readonly ICatalogExternalMatchRepository _repository;
    private readonly ISafeExternalHttpClient _httpClient;

    public ExternalLinkPublishingService(
        ICatalogExternalMatchRepository repository,
        ISafeExternalHttpClient httpClient)
    {
        _repository = repository;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<PublishedExternalLinkDto>> GetPublishedLinksByCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        var matches = await _repository.GetPublishedMatchesByCatalogIdAsync(catalogId, userId, cancellationToken);
        return matches.Select(MapPublishedLink).ToList();
    }

    public async Task<PublishedExternalLinkDto?> GetPublishedLinkByCatalogItemAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken)
    {
        var match = await _repository.GetPublishedMatchByCatalogItemIdAsync(catalogItemId, userId, cancellationToken);
        return match is null ? null : MapPublishedLink(match);
    }

    public async Task<ExternalLinkHealthRefreshResult> RefreshLinkHealthAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _repository.GetMatchByIdForLinkRefreshAsync(matchId, cancellationToken)
            ?? throw new InvalidOperationException($"Catalog external match not found: {matchId}");

        if (match.ExternalProductId is null)
        {
            throw new InvalidOperationException($"External product missing for match: {matchId}");
        }

        var checkedAtUtc = DateTime.UtcNow;
        var targetUrl = ResolveTargetUrl(match);
        var result = await ExecuteHeadRequestAsync(targetUrl, cancellationToken);

        var linkCheck = new ExternalProductLinkCheck
        {
            Id = Guid.NewGuid(),
            ExternalProductId = match.ExternalProductId.Value,
            CheckedAtUtc = checkedAtUtc,
            Method = "HEAD",
            StatusCode = result.StatusCode,
            IsReachable = result.IsReachable,
            FinalUrl = result.FinalUrl,
            ErrorSummary = result.ErrorSummary,
            CreatedDate = checkedAtUtc
        };

        await _repository.AddLinkCheckAsync(linkCheck, cancellationToken);

        match.LastLinkCheckAtUtc = checkedAtUtc;
        match.LastLinkStatusCode = result.StatusCode;
        match.IsLinkHealthy = result.IsReachable;
        match.UpdatedDate = checkedAtUtc;

        await _repository.SaveChangesAsync(cancellationToken);

        return new ExternalLinkHealthRefreshResult
        {
            MatchId = match.Id,
            ExternalProductId = match.ExternalProductId.Value,
            Status = match.Status,
            IsReachable = result.IsReachable,
            StatusCode = result.StatusCode,
            CheckedAtUtc = checkedAtUtc,
            FinalUrl = result.FinalUrl,
            ErrorSummary = result.ErrorSummary
        };
    }

    public void MarkBroken(CatalogItemExternalMatch match, DateTime changedAtUtc)
    {
        match.Status = "broken_link";
        match.IsLinkHealthy = false;
        match.UpdatedDate = changedAtUtc;
    }

    public void RestoreApproved(CatalogItemExternalMatch match, DateTime changedAtUtc)
    {
        match.Status = "approved";
        match.IsActive = true;
        match.IsLinkHealthy = true;
        match.UpdatedDate = changedAtUtc;
    }

    private static PublishedExternalLinkDto MapPublishedLink(CatalogItemExternalMatch match)
    {
        return new PublishedExternalLinkDto
        {
            MatchId = match.Id,
            CatalogId = match.CatalogId,
            CatalogItemId = match.CatalogItemId,
            ExternalSiteId = match.ExternalSiteId,
            ExternalProductId = match.ExternalProductId,
            Url = match.ExternalProductUrl ?? string.Empty,
            Title = match.ExternalProductTitle,
            Status = match.Status,
            IsLinkHealthy = match.IsLinkHealthy,
            LastLinkCheckAtUtc = match.LastLinkCheckAtUtc,
            LastLinkStatusCode = match.LastLinkStatusCode
        };
    }

    private static string ResolveTargetUrl(CatalogItemExternalMatch match)
    {
        return match.ExternalProductUrl
               ?? match.ExternalProduct?.CanonicalUrl
               ?? match.ExternalProduct?.SourceUrl
               ?? throw new InvalidOperationException($"External product URL missing for match: {match.Id}");
    }

    private async Task<(bool IsReachable, int? StatusCode, string? FinalUrl, string? ErrorSummary)> ExecuteHeadRequestAsync(
        string targetUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(
                HttpMethod.Head,
                targetUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var statusCode = (int)response.StatusCode;
            var isReachable = statusCode >= (int)HttpStatusCode.OK && statusCode < (int)HttpStatusCode.BadRequest;
            var errorSummary = isReachable ? null : $"HEAD returned {(int)response.StatusCode}";

            return (
                IsReachable: isReachable,
                StatusCode: statusCode,
                FinalUrl: response.RequestMessage?.RequestUri?.ToString(),
                ErrorSummary: errorSummary);
        }
        catch (Exception)
        {
            return (
                IsReachable: false,
                StatusCode: null,
                FinalUrl: targetUrl,
                ErrorSummary: "Dış bağlantı güvenli biçimde doğrulanamadı veya erişilemedi.");
        }
    }
}
