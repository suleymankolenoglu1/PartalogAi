using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid ProductId) : IRequest<OperationResult<bool>>;
