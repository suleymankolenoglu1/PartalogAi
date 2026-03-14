namespace Katalogcu.API.Services;

public sealed class ErpGatewayOptions
{
    public const string SectionName = "ErpGateway";

    public string DefaultProvider { get; set; } = "snapshot";
    public List<ErpWebhookClientOptions> WebhookClients { get; set; } = [];
}

public sealed class ErpWebhookClientOptions
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public string Provider { get; set; } = "snapshot";
}
