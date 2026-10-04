extern alias stub;
using System.Text.Json;
using Majordomo.Infrastructure.Web;
using Majordomo.SharedKernel;
using Majordomo.Strategy.Application;
using Majordomo.Strategy.Domain;
using Shouldly;
using stub::Majordomo.Forecasting.Contracts.V1;
using stub::Majordomo.ForecastingStub;
using Xunit;

namespace Majordomo.UnitTests;

public sealed class SignalAndValidationTests
{
    [Theory]
    [InlineData(100, 101, 99.5, 102, SignalDirection.Long)]
    [InlineData(100, 99, 98, 100.5, SignalDirection.Short)]
    [InlineData(100, 100.05, 99, 101, SignalDirection.Flat)]
    public void ExpectedReturnThreshold_produce_la_direzione_attesa(double last, double expected, double q10, double q90, SignalDirection direction) =>
        SignalStrategies.ExpectedReturnThreshold(last, expected, q10, q90, 0.2, 0.5, allowShort: true).Direction.ShouldBe(direction);

    [Fact]
    public void Short_non_ammesso_produce_flat() =>
        SignalStrategies.ExpectedReturnThreshold(100, 99, 98, 100.5, 0.2, 0.5, allowShort: false).Direction.ShouldBe(SignalDirection.Flat);

    [Fact]
    public void Parametri_validi_superano_lo_schema()
    {
        using var doc = JsonDocument.Parse(SampleParameters.Valid);
        Should.NotThrow(() => new ParametersValidator().ValidateAndNormalize(doc.RootElement));
        ParametersValidator.Parse(SampleParameters.Valid).Universe.Epics.ShouldContain("US100");
    }

    [Fact]
    public void Parametri_non_validi_producono_errori_di_validazione()
    {
        using var doc = JsonDocument.Parse("""{ "mode": "paper" }""");
        var ex = Should.Throw<RequestValidationException>(() => new ParametersValidator().ValidateAndNormalize(doc.RootElement));
        ex.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("W/\"42\"", 42u)]
    [InlineData("\"7\"", 7u)]
    public void ETag_round_trip(string header, uint version)
    {
        ETags.TryParse(header, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(version);
        ETags.TryParse(ETags.From(version), out var again).ShouldBeTrue();
        again.ShouldBe(version);
    }

    [Fact]
    public void Stub_forecasting_restituisce_orizzonte_e_quantili_ordinati()
    {
        var series = new Series { SeriesId = "US100", StepSeconds = 900 };
        series.Values.AddRange(Enumerable.Range(0, 60).Select(i => 100 + Math.Sin(i / 3.0)));
        var f = NaiveForecastingService.ForecastSeries(series, 8, [0.1, 0.5, 0.9]);
        f.Error.ShouldBeNull();
        f.Point.Count.ShouldBe(8);
        f.Quantiles[0].Values[^1].ShouldBeLessThan(f.Quantiles[2].Values[^1]);
    }
}

internal static class SampleParameters
{
    public const string Valid = """
    {
      "universe": { "epics": ["US100"], "resolution": "MINUTE_15", "horizonSteps": 8, "contextLength": 512 },
      "model": { "family": "timesfm-2.5", "quantiles": [0.1, 0.5, 0.9] },
      "signal": { "strategy": "ExpectedReturnThreshold", "minConfidence": 0.6, "expectedReturnThresholdPct": 0.2 },
      "risk": {
        "profile": "conservative",
        "allocation": { "amount": 10000, "currency": "EUR" },
        "riskPerTradePct": 0.5, "maxDailyLossPct": 2, "maxDrawdownPct": 10, "maxLeverage": 5, "maxOpenPositions": 3,
        "stopLoss": { "method": "quantileBand", "multiplier": 1.0, "minDistancePoints": 5 }
      },
      "operationalLimits": { "maxOrdersPerDay": 10 },
      "mode": "paper"
    }
    """;
}
