using Majordomo.Infrastructure.Messaging;
using Majordomo.Strategy.Contracts;
using Majordomo.Strategy.Domain;
using Majordomo.Strategy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Majordomo.Strategy.Application;

/// <summary>Implementazione dell'API pubblica del modulo e salvataggio con outbox nella stessa transazione.</summary>
internal sealed class StrategyService(StrategyDbContext db, TimeProvider clock, ILogger<StrategyService> logger) : IStrategyModule
{
    public async Task<ActiveJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        return job is null ? null : ToActive(job);
    }

    public async Task<IReadOnlyList<ActiveJob>> GetActiveJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await db.Jobs.AsNoTracking()
            .Where(x => x.Status == JobStatus.PaperTrading || x.Status == JobStatus.Live)
            .ToListAsync(cancellationToken);
        return jobs.Select(ToActive).ToList();
    }

    public async Task<int> HaltJobsAsync(IReadOnlyCollection<Guid>? jobIds, string reason, CancellationToken cancellationToken)
    {
        var query = db.Jobs.Where(x => x.Status == JobStatus.PaperTrading || x.Status == JobStatus.Live
            || x.Status == JobStatus.Paused || x.Status == JobStatus.Backtesting);
        if (jobIds is { Count: > 0 })
        {
            query = query.Where(x => jobIds.Contains(x.Id));
        }

        var jobs = await query.ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var halted = jobs.Count(job => job.Halt(reason, now));
        await SaveAsync(jobs, cancellationToken);
        logger.LogWarning("Halt di {Count} job: {Reason}", halted, reason);
        return halted;
    }

    /// <summary>Salva gli aggregati traducendo gli eventi di dominio in messaggi outbox (stessa transazione).</summary>
    public async Task SaveAsync(IEnumerable<TradingJob> jobs, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        foreach (var job in jobs)
        {
            foreach (var e in job.DomainEvents.OfType<JobStatusChanged>())
            {
                db.EnqueueOutboxMessage(IntegrationEventTypes.JobStatusChanged, e.JobId.ToString(),
                    new { jobId = e.JobId, from = e.From.ToString(), to = e.To.ToString(), reason = e.Reason }, now);
                if (e.To == JobStatus.Halted)
                {
                    db.EnqueueOutboxMessage(IntegrationEventTypes.JobHalted, e.JobId.ToString(),
                        new { jobId = e.JobId, reason = e.Reason }, now);
                }
            }

            job.ClearDomainEvents();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ActiveJob ToActive(TradingJob job) =>
        new(job.Id, job.Name, job.Status, job.ParameterSetVersion, ParametersValidator.Parse(job.ParametersJson));
}
