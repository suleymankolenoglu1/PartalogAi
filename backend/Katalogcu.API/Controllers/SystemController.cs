using Katalogcu.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IProductFeaturePolicy _featurePolicy;

    public SystemController(IProductFeaturePolicy featurePolicy)
    {
        _featurePolicy = featurePolicy;
    }

    [HttpGet("features")]
    public IActionResult GetFeatures()
    {
        return Ok(new
        {
            aiEnabled = _featurePolicy.AiEnabled,
            chatbotEnabled = _featurePolicy.ChatbotEnabled,
            catalogAnalysisEnabled = _featurePolicy.CatalogAnalysisEnabled,
            ecommerceEnabled = _featurePolicy.EcommerceEnabled,
            upgradePromptsEnabled = _featurePolicy.UpgradePromptsEnabled
        });
    }
}
