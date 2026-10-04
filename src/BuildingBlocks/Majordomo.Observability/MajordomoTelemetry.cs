using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Majordomo.Observability;

/// <summary>
/// Sorgenti di telemetria applicativa (RFC-001 §10). Un'unica ActivitySource/Meter per il processo;
/// i nomi delle metriche seguono la convenzione del documento di design.
/// </summary>
public static class MajordomoTelemetry
{
    public const string SourceName = "Majordomo";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Histogram<double> DecisionCycleDuration =
        Meter.CreateHistogram<double>("majordomo.decision_cycle.duration", unit: "ms",
            description: "Durata del ciclo decisionale (candela chiusa -> esito).");

    public static readonly Counter<long> DecisionOutcomes =
        Meter.CreateCounter<long>("majordomo.decision_cycle.outcomes", description: "Esiti dei cicli decisionali per tipo.");

    public static readonly Counter<long> CapitalApiRequests =
        Meter.CreateCounter<long>("capital.api.requests", description: "Richieste verso Capital.com per endpoint e status.");

    public static readonly Histogram<double> CapitalApiRateLimitWait =
        Meter.CreateHistogram<double>("capital.api.rate_limit.wait", unit: "ms", description: "Attesa imposta dal rate limiter in uscita.");

    public static readonly Histogram<double> ForecastLatency =
        Meter.CreateHistogram<double>("forecast.latency", unit: "ms", description: "Latenza delle chiamate al forecasting.");

    public static readonly Histogram<double> OutboxLag =
        Meter.CreateHistogram<double>("outbox.lag", unit: "ms", description: "Ritardo fra scrittura e consegna dei messaggi outbox.");

    public static readonly Counter<long> ReconciliationDrift =
        Meter.CreateCounter<long>("reconciliation.drift", description: "Divergenze rilevate dalla riconciliazione.");
}
