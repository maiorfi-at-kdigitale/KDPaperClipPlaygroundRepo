using System.Text.Json;
using Majordomo.Backtesting.Contracts;
using Majordomo.Backtesting.Domain;
using Majordomo.Backtesting.Persistence;
using Majordomo.Infrastructure;
using Majordomo.Infrastructure.Leadership;
using Majordomo.Infrastructure.Operations;
using Majordomo.Infrastructure.Persistence;
using Majordomo.Infrastructure.Web;
using Majordomo.SharedKernel;
using Majordomo.Strategy.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Majordomo.Backtesting;

public sealed record BacktestRequest(DateTimeOffset? From, DateTimeOffset? To, decimal? SlippagePoints);

public sealed record BacktestOperationResponse(Guid Id, string Kind, string Status, string? ResultUrl, string? Error);

internal sealed class BacktestingService(BacktestingDbContext db) : IBacktestingModule
{
    public Task<bool> HasSucceededBacktestAsync(Guid jobId, Guid operationId, CancellationToken cancellationToken) =>
        db.Set<Operation>().AnyAsync(o => o.Id == operationId && o.JobId == jobId
            && o.Kind == OperationKind.Backtest && o.Status == OperationStatus.Succeeded, cancellationToken);
}

/// <summary>Esegue i backtest in coda (solo worker leader). Scheletro: risultato sintetico deterministico.</summary>
internal sealed class BacktestRunner(IServiceScopeFactory scopes, ILeaderState leader, TimeProvider clock, ILogger<BacktestRunner> logger)
    : LeaderGatedPeriodicService(leader, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BacktestingDbContext>();
        var operations = scope.ServiceProvider.GetRequiredService<IOperationStore>();
        var queued = await db.Set<Operation>().AsNoTracking()
            .Where(o => o.Kind == OperationKind.Backtest && o.Status == OperationStatus.Queued)
            .OrderBy(o => o.CreatedAt).Take(5).Select(o => o.Id).ToListAsync(cancellationToken);
        foreach (var id in queued)
        {
            var run = await db.Runs.FirstAsync(r => r.Id == id, cancellationToken);
            await operations.MarkRunningAsync(id, cancellationToken);
            // TODO(T-10): walk-forward con lo stesso codice Strategy/Risk e costi realistici.
            var days = Math.Max(1, (int)(run.To - run.From).TotalDays);
            var seed = Math.Abs(run.JobId.GetHashCode() ^ days);
            var result = new
            {
                trades = days * 2,
                netPnl = Math.Round((seed % 2000 - 800) / 10m, 2),
                maxDrawdownPct = Math.Round(seed % 120 / 10m, 2),
                sharpe = Math.Round((seed % 300 - 100) / 100m, 2),
                note = "Risultato sintetico dello scheletro: nessuna simulazione reale.",
            };
            run.ResultJson = JsonSerializer.Serialize(result);
            run.CompletedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            await operations.CompleteAsync(id, true,
                $"/v1/reports/performance?jobId={run.JobId}&from={Uri.EscapeDataString(run.From.ToString("O"))}&to={Uri.EscapeDataString(run.To.ToString("O"))}",
                null, cancellationToken);
            logger.LogInformation("Backtest {OperationId} completato per job {JobId}", id, run.JobId);
        }
    }
}

public static class BacktestingModule
{
    public static IServiceCollection AddBacktestingModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<BacktestingDbContext>(BacktestingDbContext.Schema);
        services.AddScoped<IBacktestingModule, BacktestingService>();
        return services;
    }

    public static IServiceCollection AddBacktestingWorker(this IServiceCollection services)
    {
        services.AddHostedService<BacktestRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapBacktestingEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/jobs/{jobId:guid}/backtests", async (Guid jobId, BacktestRequest request, IStrategyModule strategy,
            BacktestingDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            if (request.From is not { } from || request.To is not { } to || from >= to)
            {
                throw new RequestValidationException("from", "from e to sono obbligatori e from < to.");
            }

            if (request.SlippagePoints is < 0)
            {
                throw new RequestValidationException("slippagePoints", "Deve essere >= 0.");
            }

            var job = await strategy.GetJobAsync(jobId, ct) ?? throw new NotFoundException($"Job {jobId} non trovato.");
            var now = clock.GetUtcNow();
            var op = Operation.Create(OperationKind.Backtest, jobId, now);
            db.Set<Operation>().Add(op);
            db.Runs.Add(new BacktestRun
            {
                Id = op.Id,
                JobId = jobId,
                ParameterSetVersion = job.ParameterSetVersion,
                From = from,
                To = to,
                SlippagePoints = request.SlippagePoints ?? 0.5m,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            return TypedResults.Accepted($"/v1/operations/{op.Id}", new BacktestOperationResponse(op.Id, "backtest", op.Status.ToString(), null, null));
        }).RequireAuthorization(MajordomoPolicies.CanOperate).WithIdempotency().WithTags("jobs").WithName("RequestBacktest");
        return v1;
    }
}
