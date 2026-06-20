using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Compatibility.Common;
using MediatR;

namespace Katalogcu.Application.Features.Compatibility.Queries.GetPartCompatibilityRules;

public sealed record GetPartCompatibilityRulesQuery(Guid CatalogItemId)
    : IRequest<OperationResult<IReadOnlyList<PartCompatibilityRuleDto>>>;
