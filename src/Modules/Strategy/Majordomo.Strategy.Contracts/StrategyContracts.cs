namespace Majordomo.Strategy.Contracts;

public enum JobStatus
{
    Draft,
    Backtesting,
    PaperTrading,
    Live,
    Paused,
    Halted,
    Archived,
}

/// <summary>Vista tipizzata (parziale) di trading-job-parameters.v1.schema.json usata dai moduli.</summary>
public sealed record JobParameters(
    UniverseParameters Universe,
    ModelParameters Model,
    SignalParameters Signal,
    RiskParameters Risk,
    OperationalLimits OperationalLimits,
    string Mode);

public sealed record UniverseParameters(IReadOnlyList<string> Epics, string Resolution, int HorizonSteps, int ContextLength = 512);

public sealed record ModelParameters(string Family, string? Version = null, IReadOnlyList<double>? Quantiles = null);

public sealed record SignalParameters(
    string Strategy,
    double MinConfidence,
    double ExpectedReturnThresholdPct = 0.2,
    bool AllowShort = true);

public sealed record AllocationParameters(decimal Amount, string Currency);

public sealed record StopLossParameters(string Method, decimal Multiplier = 1m, decimal MinDistancePoints = 0m);

public sealed record TakeProfitParameters(string Method = "riskRewardRatio", decimal RiskRewardRatio = 1.5m);

public sealed record RiskParameters(
    string Profile,
    AllocationParameters Allocation,
    decimal RiskPerTradePct,
    decimal MaxDailyLossPct,
    decimal MaxDrawdownPct,
    decimal MaxLeverage,
    int MaxOpenPositions,
    StopLossParameters StopLoss,
    TakeProfitParameters? TakeProfit = null,
    decimal MaxExposurePerInstrumentPct = 100m,
    decimal MaxTotalExposurePct = 300m,
    bool RequireManualApproval = false);

public sealed record OperationalLimits(int MaxOrdersPerDay, int CooldownAfterLossMinutes = 0);

public sealed record ActiveJob(Guid JobId, string Name, JobStatus Status, int ParameterSetVersion, JobParameters Parameters);

public interface IStrategyModule
{
    Task<ActiveJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Job in PaperTrading o Live: gli unici che eseguono cicli decisionali.</summary>
    Task<IReadOnlyList<ActiveJob>> GetActiveJobsAsync(CancellationToken cancellationToken);

    /// <summary>Porta i job indicati (null = tutti quelli operativi) in Halted. Usato dal kill switch.</summary>
    Task<int> HaltJobsAsync(IReadOnlyCollection<Guid>? jobIds, string reason, CancellationToken cancellationToken);
}
