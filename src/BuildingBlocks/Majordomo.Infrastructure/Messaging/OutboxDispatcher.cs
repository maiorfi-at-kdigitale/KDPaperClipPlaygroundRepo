using System.Diagnostics;
using Majordomo.Infrastructure.Leadership;
using Majordomo.Infrastructure.Persistence;
using Majordomo.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Dispatcher della transactional outbox (ADR-0003). Gira solo sull'istanza leader del worker.
/// Consegna at-least-once: i gestori interni deduplicano tramite <see cref="IInbox"/>,
/// i consumatori esterni sull'<c>id</c> CloudEvents.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    ILeaderState leader,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger)
    : LeaderGatedPeriodicService(leader, logger)
{
    protected override TimeSpan Interval => options.Value.PollingInterval;

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<InfraDbContext>();
        var publisher = sp.GetRequiredService<IIntegrationEventPublisher>();
        var handlers = sp.GetServices<IInternalMessageHandler>().ToLookup(h => h.MessageType, StringComparer.Ordinal);
        var now = clock.GetUtcNow();
        var o = options.Value;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var batch = await db.OutboxMessages
                .FromSql($"""
                    SELECT * FROM infra.outbox_messages
                    WHERE processed_at IS NULL AND attempts < {o.MaxAttempts}
                      AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                    ORDER BY occurred_at
                    LIMIT {o.BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            foreach (var message in batch)
            {
                using var activity = MajordomoTelemetry.ActivitySource.StartActivity(
                    $"outbox dispatch {message.Type}", ActivityKind.Producer, message.TraceParent);
                try
                {
                    if (message.IsIntegrationEvent)
                    {
                        await publisher.PublishAsync(IntegrationEventEnvelope.FromOutbox(message), ct);
                    }
                    else
                    {
                        foreach (var handler in handlers[message.Type])
                        {
                            await handler.HandleAsync(message, ct);
                        }
                    }

                    message.ProcessedAt = clock.GetUtcNow();
                    MajordomoTelemetry.OutboxLag.Record((message.ProcessedAt.Value - message.OccurredAt).TotalMilliseconds,
                        new KeyValuePair<string, object?>("type", message.Type));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Attempts++;
                    message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                    message.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts)));
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    logger.LogWarning(ex, "Consegna fallita del messaggio {MessageId} ({Type}), tentativo {Attempt}",
                        message.Id, message.Type, message.Attempts);
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }, cancellationToken);
    }
}

/// <summary>Deduplicazione lato consumatore (tabella <c>infra.inbox_messages</c>).</summary>
public interface IInbox
{
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);
}

internal sealed class EfInbox(InfraDbContext db, TimeProvider clock) : IInbox
{
    public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) =>
        db.InboxMessages.AnyAsync(x => x.MessageId == messageId && x.Consumer == consumer, cancellationToken);

    public async Task MarkProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken)
    {
        db.InboxMessages.Add(new InboxMessage { MessageId = messageId, Consumer = consumer, ProcessedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(cancellationToken);
    }
}
