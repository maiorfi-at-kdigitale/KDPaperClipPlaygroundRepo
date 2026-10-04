using Majordomo.Risk.Contracts;

namespace Majordomo.Risk.Domain;

/// <summary>Valutazione pre-trade: funzione pura, stesso input → stessa decisione (backtest == produzione).</summary>
public static class PreTradeRiskEvaluator
{
    public static RiskAssessment Evaluate(TradeIntent intent, RiskLimits limits, InstrumentRules rules, RiskSnapshot s)
    {
        if (s.HasUnknownOrderOnInstrument)
        {
            return RiskAssessment.Reject("UnresolvedOrderOnInstrument");
        }

        if (s.PeakEquity > 0 && (s.PeakEquity - s.Equity) / s.PeakEquity * 100m >= limits.MaxDrawdownPct)
        {
            return RiskAssessment.Reject("MaxDrawdownReached");
        }

        if (s.Allocation > 0 && -s.RealizedPnlToday / s.Allocation * 100m >= limits.MaxDailyLossPct)
        {
            return RiskAssessment.Reject("MaxDailyLossReached");
        }

        if (s.OpenPositions >= limits.MaxOpenPositions)
        {
            return RiskAssessment.Reject("MaxOpenPositionsReached");
        }

        if (s.OrdersToday >= limits.MaxOrdersPerDay)
        {
            return RiskAssessment.Reject("MaxOrdersPerDayReached");
        }

        if (s.LastLossAt is { } lastLoss && lastLoss + limits.CooldownAfterLoss > s.Now)
        {
            return RiskAssessment.Reject("CooldownAfterLoss");
        }

        var minStop = Math.Max(limits.MinStopDistancePoints, rules.MinStopDistance);
        if (intent.StopDistancePoints <= 0 || intent.StopDistancePoints < minStop)
        {
            return RiskAssessment.Reject("StopTooClose");
        }

        var riskAmount = s.Allocation * limits.RiskPerTradePct / 100m;
        var rawSize = riskAmount / (intent.StopDistancePoints * rules.ValuePerPointPerUnit);
        var size = Math.Min(Math.Floor(rawSize / rules.DealSizeStep) * rules.DealSizeStep, rules.MaxDealSize);
        if (size < rules.MinDealSize)
        {
            return RiskAssessment.Reject("SizeBelowMinimum");
        }

        var notional = size * intent.EntryPrice;
        var equity = Math.Max(s.Equity, 0.01m);
        if ((s.TotalExposure + notional) / equity > limits.MaxLeverage)
        {
            return RiskAssessment.Reject("MaxLeverageExceeded");
        }

        if ((s.ExposureOnInstrument + notional) / s.Allocation * 100m > limits.MaxExposurePerInstrumentPct)
        {
            return RiskAssessment.Reject("MaxInstrumentExposureExceeded");
        }

        if ((s.TotalExposure + notional) / s.Allocation * 100m > limits.MaxTotalExposurePct)
        {
            return RiskAssessment.Reject("MaxTotalExposureExceeded");
        }

        return new RiskAssessment(true, size, null);
    }
}
