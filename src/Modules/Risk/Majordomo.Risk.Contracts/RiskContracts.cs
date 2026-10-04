namespace Majordomo.Risk.Contracts;

public enum TradeDirection
{
    Long,
    Short,
}

public sealed record TradeIntent(
    Guid JobId, Guid DecisionId, string Epic, TradeDirection Direction, decimal EntryPrice,
    decimal StopDistancePoints, decimal? TakeProfitDistancePoints);

public sealed record RiskLimits(
    decimal RiskPerTradePct,
    decimal MaxDailyLossPct,
    decimal MaxDrawdownPct,
    decimal MaxLeverage,
    decimal MaxExposurePerInstrumentPct,
    decimal MaxTotalExposurePct,
    int MaxOpenPositions,
    decimal MinStopDistancePoints,
    int MaxOrdersPerDay,
    TimeSpan CooldownAfterLoss);

public sealed record InstrumentRules(decimal MinDealSize, decimal MaxDealSize, decimal DealSizeStep, decimal MinStopDistance, decimal ValuePerPointPerUnit)
{
    /// <summary>Regole generiche dello scheletro: in produzione arrivano dal catalogo strumenti (MarketData).</summary>
    public static InstrumentRules Default { get; } = new(0.01m, 1000m, 0.01m, 0m, 1m);
}

/// <summary>Stato di rischio corrente fornito da Portfolio (letto, non posseduto, dal Risk).</summary>
public sealed record RiskSnapshot(
    decimal Allocation, decimal Equity, decimal PeakEquity, decimal RealizedPnlToday,
    decimal ExposureOnInstrument, decimal TotalExposure, int OpenPositions, int OrdersToday,
    DateTimeOffset? LastLossAt, bool HasUnknownOrderOnInstrument, DateTimeOffset Now);

public sealed record RiskAssessment(bool Approved, decimal Size, string? Reason)
{
    public static RiskAssessment Reject(string reason) => new(false, 0, reason);
}

public interface IRiskModule
{
    /// <summary>True se il kill switch blocca il job (globale o per job).</summary>
    Task<bool> IsTradingBlockedAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Gate obbligatorio: funzione pura e deterministica (ADR-0005).</summary>
    RiskAssessment Evaluate(TradeIntent intent, RiskLimits limits, InstrumentRules rules, RiskSnapshot snapshot);
}
