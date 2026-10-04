using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Majordomo.Infrastructure.Messaging;

/// <summary>Busta CloudEvents 1.0 (structured mode) degli eventi di integrazione (AsyncAPI v1).</summary>
public sealed record IntegrationEventEnvelope(
    string Id,
    string Type,
    DateTimeOffset Time,
    string? Subject,
    string? TraceParent,
    JsonElement Data)
{
    public string SpecVersion => "1.0";

    public string Source => "urn:k-digitale:majordomo";

    public static IntegrationEventEnvelope FromOutbox(OutboxMessage message)
    {
        using var doc = JsonDocument.Parse(message.Payload);
        return new(message.Id.ToString(), message.Type, message.OccurredAt, message.Subject, message.TraceParent,
            doc.RootElement.Clone());
    }
}

/// <summary>
/// Punto di estensione del trasporto degli eventi di integrazione (ADR-0003): in v1 webhook firmato,
/// in futuro un broker (NATS/Azure Service Bus/RabbitMQ) senza cambiare il payload.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>Gestore di un messaggio interno (<c>internal.*</c>) consegnato dal dispatcher dell'outbox.</summary>
public interface IInternalMessageHandler
{
    /// <summary>Tipo di messaggio gestito (più gestori possono sottoscrivere lo stesso tipo).</summary>
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>Publisher di fallback: scrive l'evento nei log strutturati.</summary>
public sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        logger.LogInformation("Evento di integrazione {Type} id={Id} subject={Subject}: {Data}",
            envelope.Type, envelope.Id, envelope.Subject, envelope.Data.GetRawText());
        return Task.CompletedTask;
    }
}
