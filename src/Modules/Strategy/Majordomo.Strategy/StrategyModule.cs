using Majordomo.Infrastructure.Persistence;
using Majordomo.Strategy.Application;
using Majordomo.Strategy.Contracts;
using Majordomo.Strategy.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.Strategy;

public static class StrategyModule
{
    public static IServiceCollection AddStrategyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<StrategyDbContext>(StrategyDbContext.Schema);
        services.AddSingleton<ParametersValidator>();
        services.AddScoped<StrategyService>();
        services.AddScoped<IStrategyModule>(sp => sp.GetRequiredService<StrategyService>());
        services.AddOptions<DecisionCycleOptions>().Bind(configuration.GetSection(DecisionCycleOptions.Section));
        return services;
    }

    /// <summary>Servizi in background del modulo: solo nell'host worker.</summary>
    public static IServiceCollection AddStrategyWorker(this IServiceCollection services)
    {
        services.AddHostedService<DecisionCycleService>();
        return services;
    }
}
