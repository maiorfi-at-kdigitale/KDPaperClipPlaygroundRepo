using Majordomo.CapitalCom;
using Majordomo.Execution.Application;
using Majordomo.Execution.Contracts;
using Majordomo.Execution.Persistence;
using Majordomo.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.Execution;

public static class ExecutionModule
{
    public static IServiceCollection AddExecutionModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCapitalComGateway(configuration);
        services.AddModuleDbContext<ExecutionDbContext>(ExecutionDbContext.Schema);
        services.AddScoped<ExecutionService>();
        services.AddScoped<IExecutionModule>(sp => sp.GetRequiredService<ExecutionService>());
        return services;
    }

    public static IServiceCollection AddExecutionWorker(this IServiceCollection services)
    {
        services.AddHostedService<OrderReconciliationService>();
        return services;
    }
}
