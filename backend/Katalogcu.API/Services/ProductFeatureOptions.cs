using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class ProductFeatureOptions
{
    public bool EnableAi { get; set; } = true;
    public bool EnableEcommerce { get; set; } = true;
    public bool EnableUpgradePrompts { get; set; } = true;
}

public interface IProductFeaturePolicy
{
    bool AiEnabled { get; }
    bool EcommerceEnabled { get; }
    bool UpgradePromptsEnabled { get; }
}

public sealed class ProductFeaturePolicy : IProductFeaturePolicy
{
    private readonly IOptionsMonitor<ProductFeatureOptions> _options;

    public ProductFeaturePolicy(IOptionsMonitor<ProductFeatureOptions> options)
    {
        _options = options;
    }

    public bool AiEnabled => _options.CurrentValue.EnableAi;
    public bool EcommerceEnabled => _options.CurrentValue.EnableEcommerce;
    public bool UpgradePromptsEnabled => _options.CurrentValue.EnableUpgradePrompts;
}
