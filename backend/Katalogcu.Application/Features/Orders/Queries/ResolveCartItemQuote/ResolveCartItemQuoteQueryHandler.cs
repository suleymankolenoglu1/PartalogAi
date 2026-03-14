using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using MediatR;

namespace Katalogcu.Application.Features.Orders.Queries.ResolveCartItemQuote;

public sealed class ResolveCartItemQuoteQueryHandler
    : IRequestHandler<ResolveCartItemQuoteQuery, OperationResult<ResolveCartItemQuoteResponse>>
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IErpGatewayService _erpGatewayService;

    public ResolveCartItemQuoteQueryHandler(
        ICatalogRepository catalogRepository,
        IOrderRepository orderRepository,
        IErpGatewayService erpGatewayService)
    {
        _catalogRepository = catalogRepository;
        _orderRepository = orderRepository;
        _erpGatewayService = erpGatewayService;
    }

    public async Task<OperationResult<ResolveCartItemQuoteResponse>> Handle(
        ResolveCartItemQuoteQuery request,
        CancellationToken cancellationToken)
    {
        var product = await ResolveProductAsync(request, cancellationToken);
        var scopeOwnerUserId = request.PublicUserId ?? request.AuthenticatedUserId;
        if (!scopeOwnerUserId.HasValue || scopeOwnerUserId.Value == Guid.Empty)
        {
            return OperationResult<ResolveCartItemQuoteResponse>.Failure("validation", "Katalog erişim kapsamı çözülemedi.");
        }

        if (product == null && string.IsNullOrWhiteSpace(request.PartCode))
        {
            return OperationResult<ResolveCartItemQuoteResponse>.Failure("not_found", "Ürün ERP eşleştirmesi için bulunamadı.");
        }

        if (product != null && !IsProductAccessible(product, request))
        {
            return OperationResult<ResolveCartItemQuoteResponse>.Failure("forbidden", "Sepette erişim izni olmayan ürün bulundu.");
        }

        if (product == null)
        {
            var isCatalogItemAccessible = await _catalogRepository.CatalogItemCodeExistsForAccessAsync(
                scopeOwnerUserId.Value,
                request.PartCode!.Trim(),
                request.PublicUserId.HasValue && request.PublicUserId.Value != Guid.Empty,
                request.PublicCatalogIds,
                cancellationToken);

            if (!isCatalogItemAccessible)
            {
                return OperationResult<ResolveCartItemQuoteResponse>.Failure("not_found", "Ürün ERP eşleştirmesi için bulunamadı.");
            }
        }

        var availability = await _erpGatewayService.GetProductAvailabilityAsync(
            new ErpProductAvailabilityRequest
            {
                OwnerUserId = scopeOwnerUserId.Value,
                ProductId = product?.Id,
                PartCode = product?.Code ?? request.PartCode,
                RequestedQuantity = request.Quantity
            },
            cancellationToken);

        if (availability == null)
        {
            return OperationResult<ResolveCartItemQuoteResponse>.Failure("erp_unavailable", "ERP fiyat/stok bilgisi alınamadı.");
        }

        return OperationResult<ResolveCartItemQuoteResponse>.Success(new ResolveCartItemQuoteResponse
        {
            ProductId = product?.Id,
            PartCode = availability.PartCode,
            PartName = string.IsNullOrWhiteSpace(availability.ProductName) ? product?.Name ?? availability.PartCode : availability.ProductName,
            UnitPrice = availability.UnitPrice,
            AvailableStock = availability.AvailableStock,
            IsAvailable = availability.IsAvailable,
            Provider = availability.Provider,
            Currency = availability.Currency,
            SynchronizedAtUtc = availability.SynchronizedAtUtc
        });
    }

    private async Task<Product?> ResolveProductAsync(ResolveCartItemQuoteQuery request, CancellationToken cancellationToken)
    {
        if (request.ProductId != Guid.Empty)
        {
            var byId = await _orderRepository.GetProductByIdWithCatalogAsync(request.ProductId, cancellationToken);
            if (byId != null)
            {
                return byId;
            }
        }

        if (request.PublicUserId.HasValue &&
            request.PublicUserId.Value != Guid.Empty &&
            !string.IsNullOrWhiteSpace(request.PartCode))
        {
            return await _orderRepository.GetLatestProductByCodeAsync(
                request.PartCode.Trim(),
                request.PublicUserId,
                request.PublicCatalogIds,
                cancellationToken);
        }

        return null;
    }

    private static bool IsProductAccessible(Product product, ResolveCartItemQuoteQuery request)
    {
        var productCatalog = product.Catalog;
        if (productCatalog == null)
        {
            return false;
        }

        if (request.PublicUserId.HasValue && request.PublicUserId.Value != Guid.Empty)
        {
            if (productCatalog.UserId != request.PublicUserId.Value)
            {
                return false;
            }

            if (!string.Equals(productCatalog.Status, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (request.PublicCatalogIds is { Count: > 0 } && !request.PublicCatalogIds.Contains(productCatalog.Id))
            {
                return false;
            }

            return true;
        }

        if (request.AuthenticatedUserId.HasValue && request.AuthenticatedUserId.Value != Guid.Empty)
        {
            return productCatalog.UserId == request.AuthenticatedUserId.Value;
        }

        return false;
    }
}
