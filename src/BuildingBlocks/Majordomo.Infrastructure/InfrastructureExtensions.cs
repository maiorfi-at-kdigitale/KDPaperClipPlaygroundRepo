using Majordomo.Infrastructure.Leadership;
using Majordomo.Infrastructure.Messaging;
using Majordomo.Infrastructure.Operations;
using Majordomo.Infrastructure.Persistence;
using Majordomo.Infrastructure.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Majordomo.Infrastructure;

public static class InfrastructureExtensions
{
    /// <summary>Servizi infrastrutturali comuni ad API e worker.</summary>
    public static IServiceCollection AddMajordomoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMajordomoPersistence(configuration);
        services.AddScoped<IInbox, EfInbox>();
        services.AddScoped<IOperationStore, EfOperationStore>();
        services.TryAddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.Section));
        services.AddOptions<LeadershipOptions>().Bind(configuration.GetSection(LeadershipOptions.Section));
        services.AddIdempotency();
        return services;
    }

    /// <summary>Host API: nessuna leadership, nessun servizio periodico.</summary>
    public static IServiceCollection AddApiHostRole(this IServiceCollection services)
    {
        services.AddSingleton<ILeaderState, NeverLeader>();
        return services;
    }

    /// <summary>Host worker: leader election via advisory lock + dispatcher dell'outbox.</summary>
    public static IServiceCollection AddWorkerHostRole(this IServiceCollection services)
    {
        services.AddSingleton<LeaderState>();
        services.AddSingleton<ILeaderState>(sp => sp.GetRequiredService<LeaderState>());
        services.AddHostedService<PostgresLeaderElectionService>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
