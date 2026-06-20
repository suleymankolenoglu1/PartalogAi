using System.Reflection;
using FluentValidation;
using Katalogcu.Application.Common.Behaviors;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Features.PolicyThresholds.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Katalogcu.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IPolicyThresholdAccessService, PolicyThresholdAccessService>();
        services.AddScoped<IPolicyThresholdAuditWriter, PolicyThresholdAuditWriter>();

        return services;
    }
}
