using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Infrastructure.Repositories;
using Katalogcu.Infrastructure.Services;
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
        services.AddScoped<IExternalSiteRepository, ExternalSiteRepository>();
        services.AddScoped<IManualImportFileRepository, ManualImportFileRepository>();
        services.AddScoped<ICatalogExternalMatchRepository, CatalogExternalMatchRepository>();
        services.AddScoped<IPolicyThresholdRepository, PolicyThresholdRepository>();
        services.AddScoped<ICompatibilityRepository, CompatibilityRepository>();
        services.AddScoped<IErpInventorySnapshotRepository, ErpInventorySnapshotRepository>();
        services.AddScoped<ICatalogAiJobRepository, CatalogAiJobRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IHotspotRepository, HotspotRepository>();
        services.AddSingleton<ISafeExternalHttpClient, SafeExternalHttpClient>();
        services.AddScoped<IExternalSiteFetchCrawler, ExternalSiteFetchCrawler>();
        services.AddScoped<IExternalSitePlaywrightCrawler, ExternalSitePlaywrightCrawler>();
        services.AddScoped<IExternalProductNormalizer, ExternalProductNormalizer>();
        services.AddScoped<IExternalProductUpsertService, ExternalProductUpsertService>();
        services.AddScoped<IExternalSiteCrawlOrchestrator, ExternalSiteCrawlOrchestrator>();
        services.AddScoped<ICatalogExternalMatchService, CatalogExternalMatchService>();
        services.AddScoped<ICatalogExternalMatchReviewService, CatalogExternalMatchReviewService>();
        services.AddScoped<IExternalLinkPublishingService, ExternalLinkPublishingService>();
        services.AddScoped<IManualImportService, ManualImportService>();
        services.AddScoped<IPolicyRegressionCaseStore, PolicyRegressionCaseFileStore>();
        return services;
    }
}
