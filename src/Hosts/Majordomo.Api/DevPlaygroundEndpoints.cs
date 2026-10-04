using Majordomo.CapitalCom;
using Majordomo.Forecasting.Client;
using Majordomo.MarketData.Contracts;

namespace Majordomo.Api;

/// <summary>
/// Endpoint di playground, mappati SOLO in Development e fuori dal contratto pubblico v1:
/// permettono di esercitare singolarmente i componenti (ACL Capital.com, forecasting, market data).
/// </summary>
internal static class DevPlaygroundEndpoints
{
    public static IEndpointRouteBuilder MapDevPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        var dev = app.MapGroup("/dev").WithTags("dev-playground").AllowAnonymous();

        dev.MapGet("/capital/ping", async (ICapitalComGateway gateway, CancellationToken ct) =>
            TypedResults.Ok(new { reachable = await gateway.PingAsync(ct) }));

        dev.MapGet("/market-data/{epic}/candles", async (string epic, string? resolution, int? count, IMarketDataModule md, CancellationToken ct) =>
            TypedResults.Ok(await md.GetRecentCandlesAsync(epic, resolution ?? "MINUTE_15", Math.Clamp(count ?? 50, 2, 500), ct)));

        dev.MapGet("/forecasting/models", async (IForecastingClient client, CancellationToken ct) =>
            TypedResults.Ok(await client.ListModelsAsync(ct)));

        dev.MapPost("/forecasting/forecast/{epic}", async (string epic, string? resolution, int? horizon, IMarketDataModule md,
            IForecastingClient client, CancellationToken ct) =>
        {
            var candles = await md.GetRecentCandlesAsync(epic, resolution ?? "MINUTE_15", 100, ct);
            if (candles.Count < 2)
            {
                return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Dati di mercato insufficienti");
            }

            var result = await client.ForecastAsync(new ForecastQuery("timesfm-2.5", null, epic, candles[0].Time,
                candles[1].Time - candles[0].Time, candles.Select(c => (double)c.Close).ToList(), Math.Clamp(horizon ?? 8, 1, 256),
                [0.1, 0.5, 0.9]), ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Forecasting Service non disponibile")
                : Results.Ok(new { lastClose = candles[^1].Close, result.ModelFamily, result.ModelVersion, result.Point, quantiles = result.Quantiles.ToDictionary(k => k.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), v => v.Value), result.InputFingerprint });
        });

        dev.MapGet("/whoami", (HttpContext http) => TypedResults.Ok(new
        {
            authenticated = http.User.Identity?.IsAuthenticated ?? false,
            name = http.User.Identity?.Name,
            roles = http.User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value),
        }));

        return app;
    }
}
