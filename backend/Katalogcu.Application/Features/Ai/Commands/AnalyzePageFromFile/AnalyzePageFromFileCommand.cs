using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Ai.Common;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Katalogcu.Application.Features.Ai.Commands.AnalyzePageFromFile;

public sealed record AnalyzePageFromFileCommand(IFormFile File)
    : IRequest<OperationResult<PageAnalysisResult>>;
