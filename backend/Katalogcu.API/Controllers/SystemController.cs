using Katalogcu.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IProductFeaturePolicy _featurePolicy;
    private readonly IAiCapacityGuard _aiCapacityGuard;
    private readonly IProductionReadinessService _productionReadinessService;

    public SystemController(
        IProductFeaturePolicy featurePolicy,
        IAiCapacityGuard aiCapacityGuard,
        IProductionReadinessService productionReadinessService)
    {
        _featurePolicy = featurePolicy;
        _aiCapacityGuard = aiCapacityGuard;
        _productionReadinessService = productionReadinessService;
    }

    [HttpGet("features")]
    public IActionResult GetFeatures()
    {
        return Ok(new
        {
            aiEnabled = _featurePolicy.ChatbotEnabled || _featurePolicy.CatalogAnalysisEnabled,
            chatbotEnabled = _featurePolicy.ChatbotEnabled,
            catalogAnalysisEnabled = _featurePolicy.CatalogAnalysisEnabled,
            ecommerceEnabled = _featurePolicy.EcommerceEnabled,
            upgradePromptsEnabled = _featurePolicy.UpgradePromptsEnabled,
            planManagementEnabled = _featurePolicy.PlanManagementEnabled
        });
    }

    [HttpGet("ai-capacity")]
    public async Task<IActionResult> GetAiCapacity(CancellationToken cancellationToken)
    {
        var snapshot = await _aiCapacityGuard.GetSnapshotAsync(cancellationToken);
        return Ok(new
        {
            activeChats = snapshot.GlobalActiveChats,
            globalConcurrentChats = snapshot.GlobalConcurrentChats,
            perUserConcurrentChats = snapshot.PerUserConcurrentChats,
            mode = snapshot.Mode,
            distributed = snapshot.Distributed,
            saturated = snapshot.GlobalActiveChats >= snapshot.GlobalConcurrentChats
        });
    }

    [Authorize(Roles = "admin")]
    [HttpGet("production-readiness")]
    public async Task<IActionResult> GetProductionReadiness(CancellationToken cancellationToken)
    {
        var report = await _productionReadinessService.CheckAsync(cancellationToken);
        return report.Status == "blocked"
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, report)
            : Ok(report);
    }
}
