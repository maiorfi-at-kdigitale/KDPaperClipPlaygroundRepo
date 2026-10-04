using Majordomo.Risk.Contracts;
using Majordomo.Risk.Domain;
using Shouldly;
using Xunit;

namespace Majordomo.UnitTests;

public sealed class PreTradeRiskEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly RiskLimits Limits = new(
        RiskPerTradePct: 0.5m, MaxDailyLossPct: 2m, MaxDrawdownPct: 10m, MaxLeverage: 5m,
        MaxExposurePerInstrumentPct: 100m, MaxTotalExposurePct: 300m, MaxOpenPositions: 3,
        MinStopDistancePoints: 5m, MaxOrdersPerDay: 10, CooldownAfterLoss: TimeSpan.FromMinutes(30));

    private static TradeIntent Intent(decimal stop = 20m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "US100", TradeDirection.Long, 100m, stop, 30m);

    private static RiskSnapshot Snapshot(
        decimal equity = 10_000m, decimal peak = 10_000m, decimal pnlToday = 0m, int open = 0, int ordersToday = 0,
        DateTimeOffset? lastLoss = null, bool unknown = false) =>
        new(10_000m, equity, peak, pnlToday, 0m, 0m, open, ordersToday, lastLoss, unknown, Now);

    [Fact]
    public void Approva_e_dimensiona_in_base_al_rischio_per_trade()
    {
        var result = PreTradeRiskEvaluator.Evaluate(Intent(), Limits, InstrumentRules.Default, Snapshot());
        result.Approved.ShouldBeTrue();
        result.Size.ShouldBe(2.5m); // 10.000 × 0,5% / 20 punti
    }

    [Theory]
    [InlineData("MaxDrawdownReached")]
    [InlineData("MaxDailyLossReached")]
    [InlineData("MaxOpenPositionsReached")]
    [InlineData("MaxOrdersPerDayReached")]
    [InlineData("CooldownAfterLoss")]
    [InlineData("UnresolvedOrderOnInstrument")]
    public void Rifiuta_quando_un_limite_e_superato(string expected)
    {
        var snapshot = expected switch
        {
            "MaxDrawdownReached" => Snapshot(equity: 8_900m, peak: 10_000m),
            "MaxDailyLossReached" => Snapshot(pnlToday: -250m),
            "MaxOpenPositionsReached" => Snapshot(open: 3),
            "MaxOrdersPerDayReached" => Snapshot(ordersToday: 10),
            "CooldownAfterLoss" => Snapshot(lastLoss: Now.AddMinutes(-5)),
            _ => Snapshot(unknown: true),
        };

        var result = PreTradeRiskEvaluator.Evaluate(Intent(), Limits, InstrumentRules.Default, snapshot);
        result.Approved.ShouldBeFalse();
        result.Reason.ShouldBe(expected);
    }

    [Fact]
    public void Rifiuta_ordini_senza_stop_o_con_stop_troppo_vicino()
    {
        PreTradeRiskEvaluator.Evaluate(Intent(stop: 0m), Limits, InstrumentRules.Default, Snapshot()).Reason.ShouldBe("StopTooClose");
        PreTradeRiskEvaluator.Evaluate(Intent(stop: 2m), Limits, InstrumentRules.Default, Snapshot()).Reason.ShouldBe("StopTooClose");
    }
}
