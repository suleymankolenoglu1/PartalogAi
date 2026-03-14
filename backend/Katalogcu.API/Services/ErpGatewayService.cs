using System.Security.Cryptography;
using System.Text;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

internal interface IErpGatewayStrategy
{
    string Provider { get; }

    bool CanHandle(string provider);

    Task<ErpProductAvailabilityResult?> GetProductAvailabilityAsync(
        ErpProductAvailabilityRequest request,
        CancellationToken cancellationToken);
}

internal sealed class SnapshotErpGatewayStrategy : IErpGatewayStrategy
{
    private readonly IErpInventorySnapshotRepository _erpInventorySnapshotRepository;

    public SnapshotErpGatewayStrategy(IErpInventorySnapshotRepository erpInventorySnapshotRepository)
    {
        _erpInventorySnapshotRepository = erpInventorySnapshotRepository;
    }

    public string Provider => "snapshot";

    public bool CanHandle(string provider)
    {
        return string.Equals(provider, Provider, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ErpProductAvailabilityResult?> GetProductAvailabilityAsync(
        ErpProductAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _erpInventorySnapshotRepository.GetSnapshotAsync(
            request.OwnerUserId,
            request.ProductId,
            request.PartCode,
            request.PreferredProvider,
            cancellationToken);

        if (snapshot == null)
        {
            return null;
        }

        var availableStock = snapshot.AvailableStock;
        var requestedQuantity = Math.Max(1, request.RequestedQuantity);
        var isAvailable = !availableStock.HasValue || availableStock.Value >= requestedQuantity;

        return new ErpProductAvailabilityResult
        {
            ProductId = snapshot.ProductId,
            PartCode = snapshot.PartCode,
            ProductName = snapshot.ProductName,
            UnitPrice = snapshot.UnitPrice,
            AvailableStock = availableStock,
            IsAvailable = isAvailable,
            Provider = snapshot.Provider,
            Currency = snapshot.Currency,
            ExternalProductId = snapshot.ExternalProductId,
            SynchronizedAtUtc = snapshot.LastSyncedAtUtc
        };
    }
}

internal sealed class ErpGatewayService : IErpGatewayService
{
    private readonly IReadOnlyList<IErpGatewayStrategy> _strategies;
    private readonly IOptions<ErpGatewayOptions> _options;

    public ErpGatewayService(IEnumerable<IErpGatewayStrategy> strategies, IOptions<ErpGatewayOptions> options)
    {
        _strategies = strategies.ToList();
        _options = options;
    }

    public async Task<ErpProductAvailabilityResult?> GetProductAvailabilityAsync(
        ErpProductAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var provider = string.IsNullOrWhiteSpace(request.PreferredProvider)
            ? _options.Value.DefaultProvider
            : request.PreferredProvider;

        var strategy = _strategies.FirstOrDefault(x => x.CanHandle(provider));
        if (strategy == null)
        {
            return null;
        }

        return await strategy.GetProductAvailabilityAsync(request, cancellationToken);
    }

    public static ErpWebhookClientOptions? ResolveWebhookClient(ErpGatewayOptions options, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        foreach (var client in options.WebhookClients)
        {
            if (IsApiKeyMatch(client.ApiKey, apiKey))
            {
                return client;
            }
        }

        return null;
    }

    private static bool IsApiKeyMatch(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected.Trim());
        var actualBytes = Encoding.UTF8.GetBytes(actual.Trim());

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
