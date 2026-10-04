using System.Diagnostics;
using Majordomo.Execution.Contracts;
using Majordomo.Forecasting.Client;
using Majordomo.Infrastructure;
using Majordomo.Infrastructure.Leadership;
using Majordomo.MarketData.Contracts;
using Majordomo.Observability;
using Majordomo.Portfolio.Contracts;
using Majordomo.Risk.Contracts;
using Majordomo.Strategy.Contracts;
using Majordomo.Strategy.Domain;
using Majordomo.Strategy.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.Strategy.Application;

public sealed class DecisionCycleOptions
{
    public const string Section = "Strategy:DecisionCycle";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Limite superiore al contesto richiesto al broker in questo scheletro.</summary>
    public int MaxContextLength { get; set; } = 200;
}

/// <summary>
/// Ciclo decisionale (solo worker, solo leader): prezzi → forecast → segnale → risk gate → ordine.
/// Ogni passo produce una voce nel decision log.
/// </summary>
internal sealed class DecisionCycleService(
    IServiceScopeFactory scopes,
    IOptions<DecisionCycleOptions> options,
    ILeaderState leader,
    TimeProvider clock,
    ILogger<DecisionCycleService> logger) : LeaderGatedPeriodicService(leader, logger)
{
    protected override TimeSpan Interval => options.Value.Interval;

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var strategy = scope.ServiceProvider.GetRequiredService<IStrategyModule>();
        foreach (var job in await strategy.GetActiveJobsAsync(cancellationToken))
        {
            foreach (var epic in job.Parameters.Universe.Epics)
            {
                await RunAsync(scope.ServiceProvider, job, epic, cancellationToken);
            }
        }
    }

    private async Task RunAsync(IServiceProvider sp, ActiveJob job, string epic, CancellationToken cancellationToken)
    {
        using var activity = MajordomoTelemetry.ActivitySource.StartActivity("decision-cycle");
        activity?.SetTag("majordomo.job_id", job.JobId);
        activity?.SetTag("majordomo.epic", epic);
        var started = Stopwatch.GetTimestamp();
        var p = job.Parameters;
        var decision = new Decision
        {
            Id = Guid.CreateVersion7(),
            JobId = job.JobId,
            ParameterSetVersion = job.ParameterSetVersion,
            Epic = epic,
            BarClose = clock.GetUtcNow(),
            Model = p.Model.Family,
            TraceId = Activity.Current?.TraceId.ToString(),
            CreatedAt = clock.GetUtcNow(),
        };

        try
        {
            await DecideAsync(sp, job, epic, decision, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ciclo decisionale fallito per job {JobId} su {Epic}", job.JobId, epic);
            decision.Outcome = DecisionOutcome.NoSignal;
            decision.Reason = "CycleError: " + ex.GetType().Name;
        }

        var db = sp.GetRequiredService<StrategyDbContext>();
        db.Decisions.Add(decision);
        await db.SaveChangesAsync(cancellationToken);

        MajordomoTelemetry.DecisionOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", decision.Outcome.ToString()));
        MajordomoTelemetry.DecisionCycleDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static async Task DecideAsync(IServiceProvider sp, ActiveJob job, string epic, Decision decision, CancellationToken ct)
    {
        var p = job.Parameters;
        var risk = sp.GetRequiredService<IRiskModule>();
        if (await risk.IsTradingBlockedAsync(job.JobId, ct))
        {
            decision.Outcome = DecisionOutcome.Rejected;
            decision.Reason = "KillSwitchActive";
            return;
        }

        var options = sp.GetRequiredService<IOptions<DecisionCycleOptions>>().Value;
        var candles = await sp.GetRequiredService<IMarketDataModule>()
            .GetRecentCandlesAsync(epic, p.Universe.Resolution, Math.Min(p.Universe.ContextLength, options.MaxContextLength), ct);
        if (candles.Count < 2)
        {
            decision.Outcome = DecisionOutcome.NoSignal;
            decision.Reason = "InsufficientMarketData";
            return;
        }

        decision.BarClose = candles[^1].Time;
        var quantiles = p.Model.Quantiles is { Count: > 0 } q ? q : [0.1, 0.5, 0.9];
        var forecast = await sp.GetRequiredService<IForecastingClient>().ForecastAsync(new ForecastQuery(
            p.Model.Family, p.Model.Version, epic, candles[0].Time, candles[1].Time - candles[0].Time,
            candles.Select(c => (double)c.Close).ToList(), p.Universe.HorizonSteps, quantiles), ct);
        if (forecast is null || forecast.Point.Count == 0)
        {
            decision.Outcome = DecisionOutcome.NoSignal;
            decision.Reason = "ForecastUnavailable";
            return;
        }

        decision.Model = $"{forecast.ModelFamily}:{forecast.ModelVersion}";
        decision.ForecastFingerprint = forecast.InputFingerprint;
        var lastClose = (double)candles[^1].Close;
        var expected = forecast.Point[^1];
        var q10 = forecast.Quantiles.TryGetValue(quantiles.Min(), out var lo) ? lo[^1] : expected;
        var q90 = forecast.Quantiles.TryGetValue(quantiles.Max(), out var hi) ? hi[^1] : expected;
        var signal = SignalStrategies.ExpectedReturnThreshold(lastClose, expected, q10, q90,
            p.Signal.ExpectedReturnThresholdPct, p.Signal.MinConfidence, p.Signal.AllowShort);
        decision.Direction = signal.Direction;
        decision.Strength = signal.Strength;
        decision.Confidence = signal.Confidence;
        if (signal.Direction == SignalDirection.Flat)
        {
            decision.Outcome = DecisionOutcome.NoSignal;
            decision.Reason = "BelowThreshold";
            return;
        }

        var stopDistance = Math.Max((decimal)((q90 - q10) / 2.0) * p.Risk.StopLoss.Multiplier, p.Risk.StopLoss.MinDistancePoints);
        decimal? takeProfit = p.Risk.TakeProfit is { Method: "riskRewardRatio" } tp ? stopDistance * tp.RiskRewardRatio : null;
        var snapshot = await sp.GetRequiredService<IPortfolioModule>().GetRiskSnapshotAsync(job.JobId, epic, p.Risk.Allocation.Amount, ct);
        var assessment = risk.Evaluate(
            new TradeIntent(job.JobId, decision.Id, epic, signal.Direction == SignalDirection.Long ? TradeDirection.Long : TradeDirection.Short,
                candles[^1].Close, stopDistance, takeProfit),
            new RiskLimits(p.Risk.RiskPerTradePct, p.Risk.MaxDailyLossPct, p.Risk.MaxDrawdownPct, p.Risk.MaxLeverage,
                p.Risk.MaxExposurePerInstrumentPct, p.Risk.MaxTotalExposurePct, p.Risk.MaxOpenPositions,
                p.Risk.StopLoss.MinDistancePoints, p.OperationalLimits.MaxOrdersPerDay,
                TimeSpan.FromMinutes(p.OperationalLimits.CooldownAfterLossMinutes)),
            InstrumentRules.Default,
            snapshot);
        if (!assessment.Approved)
        {
            decision.Outcome = DecisionOutcome.Rejected;
            decision.Reason = assessment.Reason;
            return;
        }

        if (p.Risk.RequireManualApproval)
        {
            decision.Outcome = DecisionOutcome.Rejected;
            decision.Reason = "ManualApprovalRequired";
            return;
        }

        var buy = signal.Direction == SignalDirection.Long;
        var entry = candles[^1].Close;
        var order = await sp.GetRequiredService<IExecutionModule>().SubmitAsync(new SubmitOrder(
            job.JobId, decision.Id, epic, buy ? "BUY" : "SELL", assessment.Size,
            buy ? entry - stopDistance : entry + stopDistance,
            takeProfit is { } t ? (buy ? entry + t : entry - t) : null), ct);
        decision.Outcome = DecisionOutcome.Approved;
        decision.OrderId = order.OrderId;
        decision.Reason = order.Status;
    }
}
