using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Infrastructure.Messaging;

/// <summary>Messaggio della transactional outbox (<c>infra.outbox_messages</c>).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tipo: <c>majordomo.v1.*</c> = evento di integrazione pubblico (AsyncAPI);
    /// <c>internal.*</c> = comando/evento fra moduli consegnato ai gestori interni.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>Chiave di partizione/ordinamento (di norma il jobId).</summary>
    public string? Subject { get; set; }

    public required string Payload { get; set; }

    public string? TraceParent { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public bool IsIntegrationEvent => Type.StartsWith(IntegrationEventTypes.Prefix, StringComparison.Ordinal);
}

/// <summary>Registro dei messaggi già elaborati da un consumatore (deduplicazione at-least-once).</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    public required string Consumer { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}

public static class IntegrationEventTypes
{
    public const string Prefix = "majordomo.v1.";
    public const string JobStatusChanged = Prefix + "JobStatusChanged";
    public const string JobHalted = Prefix + "JobHalted";
    public const string PositionOpened = Prefix + "PositionOpened";
    public const string PositionClosed = Prefix + "PositionClosed";
    public const string OrderRejected = Prefix + "OrderRejected";
    public const string RiskLimitReached = Prefix + "RiskLimitReached";
    public const string KillSwitchActivated = Prefix + "KillSwitchActivated";
    public const string ReconciliationDriftDetected = Prefix + "ReconciliationDriftDetected";
}

public static class OutboxDbContextExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Accoda un messaggio nell'outbox tramite il contesto del modulo: verrà salvato dalla stessa
    /// <c>SaveChangesAsync</c> (quindi nella stessa transazione) dei cambiamenti di stato.
    /// </summary>
    public static OutboxMessage EnqueueOutboxMessage(this DbContext db, string type, string? subject, object data, DateTimeOffset now)
    {
        var message = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Subject = subject,
            Payload = JsonSerializer.Serialize(data, data.GetType(), JsonOptions),
            TraceParent = Activity.Current?.Id,
            OccurredAt = now,
        };
        db.Set<OutboxMessage>().Add(message);
        return message;
    }

    public static T ReadPayload<T>(this OutboxMessage message) =>
        JsonSerializer.Deserialize<T>(message.Payload, JsonOptions)
        ?? throw new InvalidOperationException($"Payload vuoto per il messaggio {message.Id}.");
}
