// Codice di riferimento (non compilato in CI): illustra l'aggregato TradingJob e la valutazione
// pre-trade del rischio secondo docs/capital-majordomo/domain/domain-model.md.
// Target: .NET 10, C# 14, nullable abilitato. Nessuna dipendenza infrastrutturale.
#nullable enable
namespace Majordomo.Domain;

public enum JobMode { Backtest, Paper, Live }
public enum JobStatus { Draft, Backtesting, PaperTrading, Live, Paused, Halted, Archived }
public enum Direction { Long, Short }

public readonly record struct Epic(string Value);
public readonly record struct JobId(Guid Value);

public sealed record RiskPolicy(
    decimal RiskPerTradePct,      // es. 0.5 = 0,5% dell'allocazione
    decimal MaxDailyLossPct,      // es. 2
    decimal MaxDrawdownPct,       // es. 10
    decimal MaxLeverage,          // es. 5
    decimal MaxExposurePerInstrumentPct,
    decimal MaxTotalExposurePct,
    int MaxOpenPositions,
    decimal MinStopDistancePoints,
    int MaxOrdersPerDay,
    TimeSpan CooldownAfterLoss)
{
    public void Validate()
    {
        if (RiskPerTradePct is <= 0 or > 5) throw new DomainException("RiskPerTradePct fuori range (0, 5].");
        if (MaxDailyLossPct <= 0 || MaxDrawdownPct <= 0) throw new DomainException("Limiti di perdita obbligatori.");
        if (MaxLeverage < 1) throw new DomainException("MaxLeverage deve essere >= 1.");
        if (MaxOpenPositions < 1) throw new DomainException("MaxOpenPositions deve essere >= 1.");
    }
}

public sealed record InstrumentRules(decimal MinDealSize, decimal MaxDealSize, decimal DealSizeStep,
    decimal MinStopDistance, decimal ValuePerPointPerUnit, decimal MarginFactor);

public sealed record TradeIntent(JobId JobId, Guid DecisionId, Epic Epic, Direction Direction,
    decimal EntryPrice, decimal StopDistancePoints, decimal? TakeProfitDistancePoints);

/// <summary>Stato di rischio corrente fornito da Portfolio (letto, non posseduto, dal Risk).</summary>
public sealed record RiskSnapshot(decimal Allocation, decimal Equity, decimal PeakEquity,
    decimal RealizedPnlToday, decimal ExposureOnInstrument, decimal TotalExposure,
    int OpenPositions, int OrdersToday, DateTimeOffset? LastLossAt, bool KillSwitchActive,
    bool HasUnknownOrderOnInstrument);

public abstract record RiskAssessment
{
    public sealed record Approved(decimal Size, decimal StopDistancePoints, decimal? TakeProfitDistancePoints) : RiskAssessment;
    public sealed record Rejected(string Reason) : RiskAssessment;
}

public static class PreTradeRiskEvaluator
{
    /// <summary>Funzione pura: stesso input, stessa decisione (backtest == produzione).</summary>
    public static RiskAssessment Evaluate(TradeIntent intent, RiskPolicy policy, InstrumentRules rules,
        RiskSnapshot s, DateTimeOffset now)
    {
        if (s.KillSwitchActive) return Reject("KillSwitchActive");
        if (s.HasUnknownOrderOnInstrument) return Reject("UnresolvedOrderOnInstrument");
        if (s.RealizedPnlToday <= -s.Allocation * policy.MaxDailyLossPct / 100m) return Reject("DailyLossLimitReached");
        var drawdownPct = s.PeakEquity <= 0 ? 0 : (s.PeakEquity - s.Equity) / s.PeakEquity * 100m;
        if (drawdownPct >= policy.MaxDrawdownPct) return Reject("DrawdownLimitReached");
        if (s.OpenPositions >= policy.MaxOpenPositions) return Reject("MaxOpenPositions");
        if (s.OrdersToday >= policy.MaxOrdersPerDay) return Reject("MaxOrdersPerDay");
        if (s.LastLossAt is { } l && now - l < policy.CooldownAfterLoss) return Reject("CooldownAfterLoss");

        var minStop = Math.Max(policy.MinStopDistancePoints, rules.MinStopDistance);
        if (intent.StopDistancePoints < minStop) return Reject("StopTooClose");

        // Sizing: rischio monetario / (distanza stop × valore per punto per unità)
        var riskAmount = s.Allocation * policy.RiskPerTradePct / 100m;
        var rawSize = riskAmount / (intent.StopDistancePoints * rules.ValuePerPointPerUnit);
        var size = Math.Floor(rawSize / rules.DealSizeStep) * rules.DealSizeStep; // mai per eccesso
        size = Math.Min(size, rules.MaxDealSize);
        if (size < rules.MinDealSize) return Reject("SizeBelowInstrumentMinimum");

        var notional = size * intent.EntryPrice;
        if (s.ExposureOnInstrument + notional > s.Allocation * policy.MaxExposurePerInstrumentPct / 100m)
            return Reject("MaxExposurePerInstrument");
        if (s.TotalExposure + notional > s.Allocation * policy.MaxTotalExposurePct / 100m)
            return Reject("MaxTotalExposure");
        if (s.Equity > 0 && (s.TotalExposure + notional) / s.Equity > policy.MaxLeverage)
            return Reject("MaxLeverage");

        return new RiskAssessment.Approved(size, intent.StopDistancePoints, intent.TakeProfitDistancePoints);

        static RiskAssessment Reject(string reason) => new RiskAssessment.Rejected(reason);
    }
}

public sealed class TradingJob
{
    public JobId Id { get; }
    public string Name { get; private set; }
    public JobStatus Status { get; private set; } = JobStatus.Draft;
    public int ParameterSetVersion { get; private set; } = 1;
    public RiskPolicy RiskPolicy { get; private set; }
    private readonly List<object> _events = [];
    public IReadOnlyList<object> DomainEvents => _events;

    public TradingJob(JobId id, string name, RiskPolicy policy)
    {
        policy.Validate();
        (Id, Name, RiskPolicy) = (id, name, policy);
    }

    public void ChangeRiskPolicy(RiskPolicy newPolicy, bool increasesRisk)
    {
        newPolicy.Validate();
        RiskPolicy = newPolicy;
        ParameterSetVersion++;
        if (increasesRisk && Status == JobStatus.Live) Status = JobStatus.Paused; // richiede riconferma
        _events.Add(new RiskPolicyChanged(Id, ParameterSetVersion, increasesRisk));
    }

    public void Promote(PromotionEvidence evidence)
    {
        Status = (Status, evidence.Passed) switch
        {
            (JobStatus.Draft, _) => JobStatus.Backtesting,
            (JobStatus.Backtesting, true) => JobStatus.PaperTrading,
            (JobStatus.PaperTrading, true) when evidence.LiveEnabledByRiskAdmin => JobStatus.Live,
            _ => throw new DomainException($"Promozione non ammessa da {Status}.")
        };
        _events.Add(new JobPromoted(Id, Status, evidence));
    }

    public void Halt(string reason)
    {
        if (Status is JobStatus.Archived) return;
        Status = JobStatus.Halted;
        _events.Add(new JobHalted(Id, reason));
    }

    public void ResumeAfterHalt(string confirmedBy, string motivation)
    {
        if (Status != JobStatus.Halted) throw new DomainException("Il job non è in Halt.");
        if (string.IsNullOrWhiteSpace(motivation)) throw new DomainException("Motivazione obbligatoria.");
        Status = JobStatus.Paused; // mai direttamente Live
        _events.Add(new JobResumedFromHalt(Id, confirmedBy, motivation));
    }
}

public sealed record PromotionEvidence(bool Passed, string Summary, bool LiveEnabledByRiskAdmin);
public sealed record RiskPolicyChanged(JobId JobId, int Version, bool IncreasesRisk);
public sealed record JobPromoted(JobId JobId, JobStatus NewStatus, PromotionEvidence Evidence);
public sealed record JobHalted(JobId JobId, string Reason);
public sealed record JobResumedFromHalt(JobId JobId, string ConfirmedBy, string Motivation);
public sealed class DomainException(string message) : Exception(message);
