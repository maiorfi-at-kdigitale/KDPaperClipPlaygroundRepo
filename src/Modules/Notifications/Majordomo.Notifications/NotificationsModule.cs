using System.Net.Http.Json;
using Majordomo.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.Notifications;

public sealed class NotificationsOptions
{
    public const string Section = "Notifications";

    /// <summary>Endpoint HTTP che riceve gli eventi CloudEvents (vuoto = solo log).</summary>
    public string? WebhookUrl { get; set; }
}

/// <summary>Publisher degli eventi di integrazione: invocato dall'OutboxDispatcher (at-least-once).</summary>
internal sealed class WebhookIntegrationEventPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<NotificationsOptions> options,
    ILogger<WebhookIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public const string HttpClientName = "notifications-webhook";

    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        logger.LogInformation("Evento {Type} id={Id} subject={Subject}", envelope.Type, envelope.Id, envelope.Subject);
        if (string.IsNullOrWhiteSpace(options.Value.WebhookUrl))
        {
            return;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(options.Value.WebhookUrl, new
        {
            specversion = envelope.SpecVersion,
            id = envelope.Id,
            type = envelope.Type,
            source = envelope.Source,
            time = envelope.Time,
            subject = envelope.Subject,
            traceparent = envelope.TraceParent,
            data = envelope.Data,
        }, cancellationToken);
        response.EnsureSuccessStatusCode(); // un errore lascia il messaggio in outbox per il retry
    }
}

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NotificationsOptions>().Bind(configuration.GetSection(NotificationsOptions.Section));
        services.AddHttpClient(WebhookIntegrationEventPublisher.HttpClientName).AddStandardResilienceHandler();
        services.Replace(ServiceDescriptor.Singleton<IIntegrationEventPublisher, WebhookIntegrationEventPublisher>());
        return services;
    }
}
