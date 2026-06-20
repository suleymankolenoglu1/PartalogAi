using FluentValidation;
using Katalogcu.Application.Features.Compatibility.Commands.CreateMachineModel;
using Katalogcu.Application.Features.Compatibility.Commands.CreatePartCompatibilityRule;
using Katalogcu.Application.Features.Compatibility.Queries.GetMachineModels;
using Katalogcu.Application.Features.Compatibility.Queries.GetPartCompatibilityRules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Katalogcu.API.Controllers;

[Authorize(Policy = "PrivilegedUser")]
[Route("api/[controller]")]
[ApiController]
public sealed class CompatibilityController : ControllerBase
{
    private readonly ISender _sender;

    public CompatibilityController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("machine-models")]
    public async Task<IActionResult> GetMachineModels()
    {
        var result = await _sender.Send(new GetMachineModelsQuery());
        return result.IsSuccess ? Ok(result.Value) : StatusCode(500, result.ErrorMessage);
    }

    [HttpPost("machine-models")]
    public async Task<IActionResult> CreateMachineModel([FromBody] CreateMachineModelRequest request)
    {
        try
        {
            var result = await _sender.Send(new CreateMachineModelCommand(
                request.Brand,
                request.Model,
                request.Variant,
                request.MachineGroup,
                request.AliasesJson));

            return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ErrorMessage);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("catalog-items/{catalogItemId:guid}/rules")]
    public async Task<IActionResult> GetRules(Guid catalogItemId)
    {
        var result = await _sender.Send(new GetPartCompatibilityRulesQuery(catalogItemId));
        return result.IsSuccess ? Ok(result.Value) : StatusCode(500, result.ErrorMessage);
    }

    [HttpPost("catalog-items/{catalogItemId:guid}/rules")]
    public async Task<IActionResult> CreateRule(Guid catalogItemId, [FromBody] CreatePartCompatibilityRuleRequest request)
    {
        try
        {
            var result = await _sender.Send(new CreatePartCompatibilityRuleCommand(
                catalogItemId,
                request.MachineModelId,
                request.CompatibilityLevel,
                request.SourceType,
                request.Confidence,
                request.Notes));

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return result.ErrorCode == "not_found"
                ? NotFound(result.ErrorMessage)
                : BadRequest(result.ErrorMessage);
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public sealed class CreateMachineModelRequest
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public string? MachineGroup { get; set; }
        public string? AliasesJson { get; set; }
    }

    public sealed class CreatePartCompatibilityRuleRequest
    {
        public Guid MachineModelId { get; set; }
        public string CompatibilityLevel { get; set; } = string.Empty;
        public string SourceType { get; set; } = "Manual";
        public decimal Confidence { get; set; }
        public string? Notes { get; set; }
    }
}
