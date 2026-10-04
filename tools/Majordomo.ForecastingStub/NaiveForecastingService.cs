using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Majordomo.Forecasting.Contracts.V1;

namespace Majordomo.ForecastingStub;

/// <summary>Implementazione naive del contratto: punto = ultimo valore + drift medio; quantili da volatilità × √h.</summary>
internal sealed class NaiveForecastingService : ForecastingService.ForecastingServiceBase
{
    private const int MaxHorizon = 256;

    private static readonly ModelInfo[] Models =
    [
        Info("timesfm-3", "stub"), Info("timesfm-2.5", "stub"), Info("statsforecast-ets", "stub"), Info("statsforecast-arima", "stub"),
    ];

    public override Task<ListModelsResponse> ListModels(ListModelsRequest request, ServerCallContext context)
    {
        var response = new ListModelsResponse();
        response.Models.AddRange(Models);
        return Task.FromResult(response);
    }

    public override Task<ForecastResponse> Forecast(ForecastRequest request, ServerCallContext context)
    {
        var started = Stopwatch.GetTimestamp();
        if (request.Horizon <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "horizon deve essere > 0"));
        }

        var quantiles = request.Quantiles.Count > 0 ? request.Quantiles.ToArray() : [0.1, 0.5, 0.9];
        var response = new ForecastResponse
        {
            Model = new ModelRef { Family = string.IsNullOrEmpty(request.Model?.Family) ? "timesfm-2.5" : request.Model.Family, Version = "stub" },
            GeneratedAt = Timestamp.FromDateTime(DateTime.UtcNow),
        };
        foreach (var series in request.Series)
        {
            response.Forecasts.Add(ForecastSeries(series, request.Horizon, quantiles));
        }

        response.InferenceMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return Task.FromResult(response);
    }

    internal static SeriesForecast ForecastSeries(Series series, int horizon, double[] quantiles)
    {
        var result = new SeriesForecast { SeriesId = series.SeriesId, InputFingerprint = Fingerprint(series) };
        var values = series.Values;
        if (values.Count < 2)
        {
            result.Error = new SeriesError { Code = SeriesError.Types.Code.ContextTooShort, Message = "Servono almeno 2 valori." };
            return result;
        }

        if (values.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
        {
            result.Error = new SeriesError { Code = SeriesError.Types.Code.InvalidValues, Message = "Valori NaN o infiniti." };
            return result;
        }

        if (horizon > MaxHorizon)
        {
            result.Error = new SeriesError { Code = SeriesError.Types.Code.HorizonTooLong, Message = $"Orizzonte massimo {MaxHorizon}." };
            return result;
        }

        var window = values.Skip(Math.Max(0, values.Count - 50)).ToArray();
        var diffs = window.Zip(window.Skip(1), (a, b) => b - a).ToArray();
        var drift = diffs.Average();
        var sigma = Math.Sqrt(diffs.Select(d => (d - drift) * (d - drift)).Average());
        var last = values[^1];

        for (var h = 1; h <= horizon; h++)
        {
            result.Point.Add(last + drift * h);
        }

        foreach (var q in quantiles)
        {
            var z = InverseNormal(q);
            var qf = new QuantileForecast { Quantile = q };
            for (var h = 1; h <= horizon; h++)
            {
                qf.Values.Add(last + drift * h + z * sigma * Math.Sqrt(h));
            }

            result.Quantiles.Add(qf);
        }

        return result;
    }

    /// <summary>Approssimazione di Tukey (lambda = 0,14) della funzione quantile normale standard.</summary>
    internal static double InverseNormal(double p) => 4.91 * (Math.Pow(p, 0.14) - Math.Pow(1 - p, 0.14));

    private static string Fingerprint(Series series)
    {
        var raw = series.SeriesId + "|" + series.StepSeconds + "|" + string.Join(';', series.Values.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
    }

    private static ModelInfo Info(string family, string version) => new()
    {
        Model = new ModelRef { Family = family, Version = version },
        MaxContextLength = 16384,
        MaxHorizon = MaxHorizon,
        SupportsCovariates = false,
        SupportsMultivariate = false,
        WeightsSha256 = "n/a",
        License = "stub (nessun modello reale)",
    };
}
