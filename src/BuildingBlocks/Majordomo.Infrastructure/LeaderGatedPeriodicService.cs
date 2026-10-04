using Majordomo.Infrastructure.Leadership;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Majordomo.Infrastructure;

/// <summary>
/// Servizio periodico eseguito solo dall'istanza leader del worker (fail-closed: senza leadership
/// non si fa nulla). Gli errori di un'iterazione vengono loggati e non fermano il servizio.
/// </summary>
public abstract class LeaderGatedPeriodicService(ILeaderState leader, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Interval { get; }

    protected abstract Task ExecuteOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                if (!leader.IsLeader)
                {
                    continue;
                }

                try
                {
                    await ExecuteOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Iterazione di {Service} fallita", GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // arresto ordinato
        }
    }
}
