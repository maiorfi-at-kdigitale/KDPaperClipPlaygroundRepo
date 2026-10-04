using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Majordomo.CapitalCom;

public static class CapitalComExtensions
{
    public static IServiceCollection AddCapitalComGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<CapitalComOptions>()
            .Bind(configuration.GetSection(CapitalComOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => o.AllowLive || !o.TargetsLive,
                "CapitalCom:BaseUrl punta al conto Live ma CapitalCom:AllowLive è false (ADR-0005).")
            .ValidateOnStart();

        services.AddSingleton<CapitalSession>();
        services.AddTransient<SessionHandler>();
        services.AddSingleton<CapitalRateLimiter>();
        services.AddTransient<OutboundRateLimitHandler>();

        services.AddHttpClient(CapitalSession.HttpClientName, ConfigureBase)
            .AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());

        services.AddHttpClient<ICapitalComGateway, CapitalComGateway>(ConfigureBase)
            .AddHttpMessageHandler<SessionHandler>()
            .AddHttpMessageHandler<OutboundRateLimitHandler>()
            // Retry solo su metodi sicuri: l'apertura di una posizione non viene mai ritentata (ADR-0004).
            .AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());

        return services;
    }

    private static void ConfigureBase(IServiceProvider sp, HttpClient client)
    {
        var o = sp.GetRequiredService<IOptions<CapitalComOptions>>().Value;
        client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds * 3);
    }
}
