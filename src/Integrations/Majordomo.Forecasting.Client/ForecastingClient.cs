using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Majordomo.Forecasting.Contracts.V1;
using Majordomo.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.Forecasting.Client;

public sealed class ForecastingOptions
{
    public const string Section = "Forecasting";

    [Required]
    [Url]
    public string Address { get; set; } = "http://localhost:50051";

    /// <summary>Deadline gRPC per chiamata: oltre, nessun segnale = nessun trade.</summary>
    public TimeSpan Deadline { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>Previsione per una singola serie, nel modello interno.</summary>
public sealed record ForecastResult(
    string ModelFamily,
    string ModelVersion,
    IReadOnlyList<double> Point,
    IReadOnlyDictionary<double, IReadOnlyList<double>> Quantiles,
    string InputFingerprint);

public sealed record ForecastQuery(
    string ModelFamily,
    string? ModelVersion,
    string SeriesId,
    DateTimeOffset Start,
    TimeSpan Step,
    IReadOnlyList<double> Values,
    int Horizon,
    IReadOnlyList<double> Quantiles);

public interface IForecastingClient
{
    /// <summary>Restituisce null se il servizio non risponde o la serie è in errore (degradazione: nessun segnale).</summary>
    Task<ForecastResult?> ForecastAsync(ForecastQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken);
}

internal sealed class GrpcForecastingClient(
    ForecastingService.ForecastingServiceClient client,
    IOptions<ForecastingOptions> options,
    ILogger<GrpcForecastingClient> logger) : IForecastingClient
{
    public async Task<ForecastResult?> ForecastAsync(ForecastQuery query, CancellationToken cancellationToken)
    {
        var request = new ForecastRequest
        {
            Model = new ModelRef { Family = query.ModelFamily, Version = query.ModelVersion ?? string.Empty },
            Horizon = query.Horizon,
            RequestId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
        };
        request.Quantiles.AddRange(query.Quantiles);
        var series = new Series
        {
            SeriesId = query.SeriesId,
            Start = Timestamp.FromDateTimeOffset(query.Start),
            StepSeconds = (long)query.Step.TotalSeconds,
        };
        series.Values.AddRange(query.Values);
        request.Series.Add(series);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await client.ForecastAsync(request,
                deadline: DateTime.UtcNow.Add(options.Value.Deadline), cancellationToken: cancellationToken);
            var forecast = response.Forecasts.FirstOrDefault(f => f.SeriesId == query.SeriesId);
            if (forecast is null || forecast.Error is { Code: not SeriesError.Types.Code.Unspecified })
            {
                logger.LogWarning("Forecast non disponibile per {SeriesId}: {Error}", query.SeriesId, forecast?.Error?.Message ?? "serie assente");
                return null;
            }

            return new ForecastResult(
                response.Model?.Family ?? query.ModelFamily,
                response.Model?.Version ?? string.Empty,
                forecast.Point.ToList(),
                forecast.Quantiles.ToDictionary(q => q.Quantile, q => (IReadOnlyList<double>)q.Values.ToList()),
                forecast.InputFingerprint);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Forecasting Service non disponibile ({Status}): nessun segnale per {SeriesId}", ex.StatusCode, query.SeriesId);
            return null;
        }
        finally
        {
            MajordomoTelemetry.ForecastLatency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("model", query.ModelFamily));
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var response = await client.ListModelsAsync(new ListModelsRequest(),
            deadline: DateTime.UtcNow.Add(options.Value.Deadline), cancellationToken: cancellationToken);
        return response.Models.Select(m => $"{m.Model.Family}:{m.Model.Version}").ToList();
    }
}

public static class ForecastingClientExtensions
{
    public static IServiceCollection AddForecastingClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForecastingOptions>()
            .Bind(configuration.GetSection(ForecastingOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddGrpcClient<ForecastingService.ForecastingServiceClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IOptions<ForecastingOptions>>().Value.Address));
        services.AddTransient<IForecastingClient, GrpcForecastingClient>();
        return services;
    }
}
