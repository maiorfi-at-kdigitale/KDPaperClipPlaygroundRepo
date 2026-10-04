using Majordomo.Infrastructure.Persistence;
using Majordomo.Risk.Application;
using Majordomo.Risk.Contracts;
using Majordomo.Risk.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.Risk;

public static class RiskModule
{
    public static IServiceCollection AddRiskModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<RiskDbContext>(RiskDbContext.Schema);
        services.AddScoped<RiskService>();
        services.AddScoped<IRiskModule>(sp => sp.GetRequiredService<RiskService>());
        return services;
    }

    public static IServiceCollection AddRiskWorker(this IServiceCollection services)
    {
        services.AddHostedService<KillSwitchExecutor>();
        return services;
    }
}
