using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Features.Ai.Commands.ExtractTableFromFile;

public sealed record ExtractTableFromFileCommand(IFormFile File, int PageNumber)
    : IRequest<OperationResult<IReadOnlyList<ProductItemDto>>>;
