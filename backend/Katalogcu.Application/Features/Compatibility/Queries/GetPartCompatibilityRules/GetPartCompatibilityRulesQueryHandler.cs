using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Queries.GetPartCompatibilityRules;

public sealed class GetPartCompatibilityRulesQueryHandler
    : IRequestHandler<GetPartCompatibilityRulesQuery, OperationResult<IReadOnlyList<PartCompatibilityRuleDto>>>
{
    private readonly ICompatibilityRepository _compatibilityRepository;

    public GetPartCompatibilityRulesQueryHandler(ICompatibilityRepository compatibilityRepository)
    {
        _compatibilityRepository = compatibilityRepository;
    }

    public async Task<OperationResult<IReadOnlyList<PartCompatibilityRuleDto>>> Handle(
        GetPartCompatibilityRulesQuery request,
        CancellationToken cancellationToken)
    {
        var rules = await _compatibilityRepository.GetRulesForCatalogItemIdsAsync([request.CatalogItemId], cancellationToken);
        return OperationResult<IReadOnlyList<PartCompatibilityRuleDto>>.Success(rules.Select(CompatibilityMapper.ToDto).ToList());
    }
}
