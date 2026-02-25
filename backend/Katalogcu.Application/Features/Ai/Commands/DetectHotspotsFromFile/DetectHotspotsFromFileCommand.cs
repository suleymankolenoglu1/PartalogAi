using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Features.Ai.Commands.DetectHotspotsFromFile;

public sealed record DetectHotspotsFromFileCommand(IFormFile File, Guid PageId)
    : IRequest<OperationResult<IReadOnlyList<Hotspot>>>;
