using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class ProductFeatureOptions
{
    public bool EnableChatbot { get; set; } = true;
    public bool EnableCatalogAnalysis { get; set; } = true;
    public bool EnableEcommerce { get; set; } = true;
    public bool EnableUpgradePrompts { get; set; } = true;
    public bool EnablePlanManagement { get; set; } = true;
}

public interface IProductFeaturePolicy
{
    bool AiEnabled { get; }
    bool ChatbotEnabled { get; }
    bool CatalogAnalysisEnabled { get; }
    bool EcommerceEnabled { get; }
    bool UpgradePromptsEnabled { get; }
    bool PlanManagementEnabled { get; }
}

public sealed class ProductFeaturePolicy : IProductFeaturePolicy
{
    private readonly IOptionsMonitor<ProductFeatureOptions> _options;

    public ProductFeaturePolicy(IOptionsMonitor<ProductFeatureOptions> options)
    {
        _options = options;
    }

    public bool AiEnabled => ChatbotEnabled || CatalogAnalysisEnabled;
    public bool ChatbotEnabled => _options.CurrentValue.EnableChatbot;
    public bool CatalogAnalysisEnabled => _options.CurrentValue.EnableCatalogAnalysis;
    public bool EcommerceEnabled => _options.CurrentValue.EnableEcommerce;
    public bool UpgradePromptsEnabled => _options.CurrentValue.EnableUpgradePrompts;
    public bool PlanManagementEnabled => _options.CurrentValue.EnablePlanManagement;
}
