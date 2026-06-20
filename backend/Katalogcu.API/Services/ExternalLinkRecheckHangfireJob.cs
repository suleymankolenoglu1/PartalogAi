using Hangfire;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.ExternalLinks.Commands.MarkApprovedExternalMatchBroken;
using Katalogcu.Application.Features.ExternalLinks.Commands.RefreshApprovedExternalLinkHealth;
using Katalogcu.Application.Features.ExternalLinks.Commands.RestoreApprovedExternalMatch;
using MediatR;

namespace Katalogcu.API.Services;

public sealed class ExternalLinkRecheckHangfireJob
{
    public const string QueueName = "external-link-recheck";
    public const string RecurringJobId = "recheck-broken-links";

    private readonly ICatalogExternalMatchRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<ExternalLinkRecheckHangfireJob> _logger;

    public ExternalLinkRecheckHangfireJob(
        ICatalogExternalMatchRepository repository,
        IMediator mediator,
        ILogger<ExternalLinkRecheckHangfireJob> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    [Queue(QueueName)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var staleBeforeUtc = DateTime.UtcNow.AddHours(-24);
        var matches = await _repository.GetMatchesNeedingLinkRecheckAsync(staleBeforeUtc, cancellationToken);

        foreach (var match in matches)
        {
            var refreshResult = await _mediator.Send(new RefreshApprovedExternalLinkHealthCommand(match.Id), cancellationToken);
            if (!refreshResult.IsSuccess || refreshResult.Value is null)
            {
                _logger.LogWarning("External link health refresh başarısız: MatchId={MatchId} | Error={Error}",
                    match.Id,
                    refreshResult.ErrorMessage);
                continue;
            }

            if (string.Equals(refreshResult.Value.Status, "approved", StringComparison.OrdinalIgnoreCase) &&
                !refreshResult.Value.IsReachable)
            {
                await _mediator.Send(new MarkApprovedExternalMatchBrokenCommand(match.Id), cancellationToken);
                continue;
            }

            if (string.Equals(refreshResult.Value.Status, "broken_link", StringComparison.OrdinalIgnoreCase) &&
                refreshResult.Value.IsReachable)
            {
                await _mediator.Send(new RestoreApprovedExternalMatchCommand(match.Id), cancellationToken);
            }
        }
    }
}
