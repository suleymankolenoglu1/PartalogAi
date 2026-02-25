using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Katalogcu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IChatQueryService, ChatQueryService>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogProcessingRepository, CatalogProcessingRepository>();
        services.AddScoped<ICatalogAiJobRepository, CatalogAiJobRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IHotspotRepository, HotspotRepository>();
        return services;
    }
}
