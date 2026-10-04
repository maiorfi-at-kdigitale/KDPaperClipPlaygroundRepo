using Majordomo.Infrastructure;
using Majordomo.Infrastructure.Leadership;
using Majordomo.Infrastructure.Messaging;
using Majordomo.Infrastructure.Operations;
using Majordomo.Risk.Contracts;
using Majordomo.Risk.Domain;
using Majordomo.Risk.Persistence;
using Majordomo.Strategy.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Majordomo.Risk.Application;

internal sealed class RiskService(RiskDbContext db) : IRiskModule
{
    public async Task<bool> IsTradingBlockedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var ks = await db.KillSwitches.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return ks?.Blocks(jobId) ?? false;
    }

    public RiskAssessment Evaluate(TradeIntent intent, RiskLimits limits, InstrumentRules rules, RiskSnapshot snapshot) =>
        PreTradeRiskEvaluator.Evaluate(intent, limits, rules, snapshot);

    public async Task<KillSwitch> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var ks = await db.KillSwitches.FirstOrDefaultAsync(cancellationToken);
        if (ks is null)
        {
            ks = new KillSwitch();
            db.KillSwitches.Add(ks);
        }

        return ks;
    }

    /// <summary>Attiva il kill switch, crea l'operazione asincrona e l'evento, in un'unica transazione.</summary>
    public async Task<Operation> ActivateAsync(KillSwitchScope scope, Guid? jobId, string reason, bool closePositions, string user,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ks = await GetOrCreateAsync(cancellationToken);
        ks.Activate(scope, jobId, reason, closePositions, user, now);
        var operation = Operation.Create(OperationKind.KillSwitch, jobId, now);
        db.Set<Operation>().Add(operation);
        db.EnqueueOutboxMessage(IntegrationEventTypes.KillSwitchActivated, jobId?.ToString(),
            new { scope = scope.ToString().ToLowerInvariant(), jobId, reason, activatedBy = user, operationId = operation.Id }, now);
        await db.SaveChangesAsync(cancellationToken);
        return operation;
    }
}

/// <summary>Esegue le operazioni di kill switch in coda (solo worker leader): halt dei job e, se richiesto, chiusura posizioni.</summary>
internal sealed class KillSwitchExecutor(IServiceScopeFactory scopes, ILeaderState leader, ILogger<KillSwitchExecutor> logger)
    : LeaderGatedPeriodicService(leader, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(2);

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
        var operations = scope.ServiceProvider.GetRequiredService<IOperationStore>();
        var pending = await db.Set<Operation>().AsNoTracking()
            .Where(o => o.Kind == OperationKind.KillSwitch && o.Status == OperationStatus.Queued)
            .OrderBy(o => o.CreatedAt).Take(10).ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var ks = await db.KillSwitches.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var strategy = scope.ServiceProvider.GetRequiredService<IStrategyModule>();
        foreach (var op in pending)
        {
            await operations.MarkRunningAsync(op.Id, cancellationToken);
            try
            {
                var targets = ks is { Active: true, Scope: KillSwitchScope.Job } ? ks.JobIds : null;
                var halted = await strategy.HaltJobsAsync(targets, "KillSwitch: " + (ks?.Reason ?? "n/d"), cancellationToken);
                if (ks?.ClosePositions == true)
                {
                    // TODO(T-11/T-14): chiusura posizioni tramite Execution; nello scheletro solo log.
                    logger.LogWarning("Kill switch: chiusura posizioni richiesta (non ancora implementata nello scheletro)");
                }

                await operations.CompleteAsync(op.Id, true, "/v1/kill-switch", null, cancellationToken);
                logger.LogWarning("Kill switch eseguito: {Halted} job in Halted", halted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Esecuzione kill switch {OperationId} fallita", op.Id);
                await operations.CompleteAsync(op.Id, false, null, ex.Message, cancellationToken);
            }
        }
    }
}
