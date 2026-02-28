using Katalogcu.Domain.Enums;

namespace Katalogcu.API.Services;

public sealed class PlanLimits
{
    public int? MaxCatalogCount { get; init; }
    public int? MaxAiQueriesPerMonth { get; init; }
    public bool AiEnabled { get; init; }
}

public static class PlanLimitRules
{
    public static PlanLimits For(SubscriptionPlan plan)
    {
        return plan switch
        {
            SubscriptionPlan.CatalogWithAI => new PlanLimits
            {
                MaxCatalogCount = 10,
                MaxAiQueriesPerMonth = 500,
                AiEnabled = true
            },
            SubscriptionPlan.CatalogWithAIAndEcommerce => new PlanLimits
            {
                MaxCatalogCount = null,
                MaxAiQueriesPerMonth = null,
                AiEnabled = true
            },
            _ => new PlanLimits
            {
                MaxCatalogCount = 5,
                MaxAiQueriesPerMonth = 0,
                AiEnabled = false
            }
        };
    }
}
