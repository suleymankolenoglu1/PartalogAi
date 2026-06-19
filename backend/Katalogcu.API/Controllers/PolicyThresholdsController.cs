using Katalogcu.API.Services;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.PolicyThresholds.Commands.EvaluatePolicyThreshold;
using Katalogcu.Application.Features.PolicyThresholds.Commands.PromoteRegressionCases;
using Katalogcu.Application.Features.PolicyThresholds.Commands.SetPolicyThresholdActive;
using Katalogcu.Application.Features.PolicyThresholds.Commands.UpsertPolicyThreshold;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyOperations;
using Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyRegressionCases;
using Katalogcu.Application.Features.PolicyThresholds.Queries.GetPolicyThresholds;
using Katalogcu.Application.Features.PolicyThresholds.Queries.ValidatePolicyThresholdScopeAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Katalogcu.API.Controllers;

[Authorize]
[Route("api/policy-thresholds")]
[ApiController]
public sealed class PolicyThresholdsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IPolicyThresholdActorContext _actorContext;
    private readonly IPolicyThresholdEvaluationTokenService _evaluationTokenService;

    public PolicyThresholdsController(
        ISender sender,
        IPolicyThresholdActorContext actorContext,
        IPolicyThresholdEvaluationTokenService evaluationTokenService)
    {
        _sender = sender;
        _actorContext = actorContext;
        _evaluationTokenService = evaluationTokenService;
    }

    public sealed class PolicyThresholdRequest : PolicyThresholdRequestDto
    {
    }

    public sealed class PolicyEvalCaseDto : PolicyThresholdEvalCaseDto
    {
    }

    public sealed class PolicyThresholdEvalRequest
    {
        public PolicyThresholdRequest Policy { get; set; } = new();
        public List<PolicyEvalCaseDto> Cases { get; set; } = [];
    }

    public sealed class PolicyRegressionPromoteRequest
    {
        public string? Jsonl { get; set; }
        public string? Note { get; set; }
        public string? EvaluationToken { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetPolicyThresholds(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? scopeType = null,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var result = await _sender.Send(
            new GetPolicyThresholdsQuery(includeInactive, scopeType, _actorContext.BuildActor()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToPolicyActionResult(result);
        }

        return Ok(new PolicyThresholdListResponseDto
        {
            Items = result.Value ?? []
        });
    }

    [HttpGet("operations")]
    public async Task<IActionResult> GetPolicyOperations(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var result = await _sender.Send(
            new GetPolicyOperationsQuery(take, _actorContext.BuildActor()),
            cancellationToken);
        return result.IsSuccess
            ? Ok(new PolicyThresholdOperationsResponseDto { Items = result.Value ?? [] })
            : ToPolicyActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePolicyThreshold(
        [FromBody] PolicyThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var validationError = ValidateRequest(request, out var scopeType, out var scopeKey);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!request.RequireEvaluation && !_actorContext.IsPlatformAdmin)
        {
            return BadRequest(new { message = "Eval bypass sadece platform admin tarafından yapılabilir." });
        }

        if (request.RequireEvaluation &&
            !_evaluationTokenService.TryApplyPolicyEvaluationToken(request, scopeType, scopeKey, _actorContext.UserId, out var evalError))
        {
            return BadRequest(new { message = evalError ?? "Policy aktifleşmeden önce eval geçmelidir." });
        }

        var result = await _sender.Send(
            new UpsertPolicyThresholdCommand(null, request, _actorContext.BuildActor()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePolicyThreshold(
        Guid id,
        [FromBody] PolicyThresholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var validationError = ValidateRequest(request, out var scopeType, out var scopeKey);
        if (validationError is not null)
        {
            return validationError;
        }

        if (!request.RequireEvaluation && !_actorContext.IsPlatformAdmin)
        {
            return BadRequest(new { message = "Eval bypass sadece platform admin tarafından yapılabilir." });
        }

        if (request.RequireEvaluation &&
            !_evaluationTokenService.TryApplyPolicyEvaluationToken(request, scopeType, scopeKey, _actorContext.UserId, out var evalError))
        {
            return BadRequest(new { message = evalError ?? "Policy aktifleşmeden önce eval geçmelidir." });
        }

        var result = await _sender.Send(
            new UpsertPolicyThresholdCommand(id, request, _actorContext.BuildActor()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivatePolicyThreshold(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var result = await _sender.Send(
            new SetPolicyThresholdActiveCommand(id, false, _actorContext.BuildActor()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> ActivatePolicyThreshold(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var result = await _sender.Send(
            new SetPolicyThresholdActiveCommand(id, true, _actorContext.BuildActor()),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluatePolicyThreshold(
        [FromBody] PolicyThresholdEvalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var validationError = ValidateRequest(request.Policy, out var scopeType, out var scopeKey);
        if (validationError is not null)
        {
            return validationError;
        }

        var accessError = await ValidateScopeAccessAsync(scopeType, scopeKey, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var result = await _sender.Send(
            new EvaluatePolicyThresholdCommand(
                request.Policy,
                (request.Cases ?? []).Cast<PolicyThresholdEvalCaseDto>().ToList(),
                scopeType,
                scopeKey),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToPolicyActionResult(result);
        }

        var eval = result.Value!;
        var summary = new PolicyThresholdEvalResponseDto
        {
            Passed = eval.Passed,
            Total = eval.Total,
            PassedCount = eval.PassedCount,
            FailedCount = eval.FailedCount,
            PassRate = eval.PassRate,
            ThresholdSource = eval.ThresholdSource,
            EvaluationToken = eval.Passed && !string.IsNullOrWhiteSpace(eval.CasesHash)
                ? _evaluationTokenService.CreateToken(
                    request.Policy,
                    scopeType,
                    scopeKey,
                    _actorContext.UserId,
                    eval.Total,
                    eval.CasesHash)
                : null,
            Results = eval.Results
        };

        return Ok(summary);
    }

    [HttpPost("regression-cases")]
    public async Task<IActionResult> PromoteRegressionCases(
        [FromBody] PolicyRegressionPromoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var cases = PolicyRegressionCaseParser.ParseDrafts(request.Jsonl);
        if (cases.Count == 0)
        {
            return BadRequest(new { message = "Regression set'e eklenecek geçerli eval case bulunamadı." });
        }

        if (!_evaluationTokenService.ValidateRegressionPromotionToken(
                request.EvaluationToken,
                cases,
                _actorContext.UserId,
                out var evalError))
        {
            return BadRequest(new { message = evalError ?? "Regression set'e eklemeden önce geçerli eval token gerekli." });
        }

        var result = await _sender.Send(
            new PromoteRegressionCasesCommand(cases, request.Note, _actorContext.BuildActor()),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    [HttpGet("regression-cases")]
    public async Task<IActionResult> GetRegressionCases(
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default)
    {
        if (!_actorContext.CanManagePolicies)
        {
            return Forbid();
        }

        var result = await _sender.Send(new GetPolicyRegressionCasesQuery(take), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToPolicyActionResult(result);
    }

    private IActionResult? ValidateRequest(PolicyThresholdRequest request, out string scopeType, out string scopeKey)
    {
        var normalized = PolicyThresholdRules.ValidateAndNormalize(request);
        if (!normalized.IsSuccess)
        {
            scopeType = string.Empty;
            scopeKey = string.Empty;
            return ToPolicyActionResult(normalized);
        }

        (scopeType, scopeKey) = normalized.Value;
        return null;
    }

    private async Task<IActionResult?> ValidateScopeAccessAsync(
        string scopeType,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ValidatePolicyThresholdScopeAccessQuery(scopeType, scopeKey, _actorContext.BuildActor()),
            cancellationToken);

        return result.IsSuccess ? null : ToPolicyActionResult(result);
    }

    private IActionResult ToPolicyActionResult<T>(OperationResult<T> result)
    {
        return result.ErrorCode switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(new { message = result.ErrorMessage }),
            "validation" => BadRequest(new { message = result.ErrorMessage }),
            _ => StatusCode(500, new { message = result.ErrorMessage ?? "Policy threshold işlemi tamamlanamadı." })
        };
    }

}
