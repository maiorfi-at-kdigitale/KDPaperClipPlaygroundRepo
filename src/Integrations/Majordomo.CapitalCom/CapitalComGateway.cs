using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Majordomo.Observability;

namespace Majordomo.CapitalCom;

internal sealed class CapitalComGateway(HttpClient http) : ICapitalComGateway
{
    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/v1/ping", cancellationToken);
        Track("ping", response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<CapitalCandle>> GetCandlesAsync(string epic, string resolution, int max, CancellationToken cancellationToken)
    {
        var url = $"api/v1/prices/{Uri.EscapeDataString(epic)}?resolution={Uri.EscapeDataString(resolution)}&max={Math.Clamp(max, 1, 1000)}";
        using var response = await http.GetAsync(url, cancellationToken);
        Track("prices", response.StatusCode);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<PricesResponse>(cancellationToken);
        return body?.Prices.Select(p => new CapitalCandle(
                p.SnapshotTimeUtc ?? p.SnapshotTime,
                p.OpenPrice.Mid, p.HighPrice.Mid, p.LowPrice.Mid, p.ClosePrice.Mid))
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<CapitalPosition>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/v1/positions", cancellationToken);
        Track("positions", response.StatusCode);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<PositionsResponse>(cancellationToken);
        return body?.Positions.Select(p => new CapitalPosition(
                p.Position.DealId, p.Market.Epic, p.Position.Direction, p.Position.Size, p.Position.Level,
                p.Position.StopLevel, p.Position.ProfitLevel, p.Position.Upl, p.Position.Currency,
                p.Position.CreatedDateUtc ?? DateTimeOffset.MinValue))
            .ToList() ?? [];
    }

    public async Task<string> OpenPositionAsync(OpenPositionRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            epic = request.Epic,
            direction = request.Direction,
            size = request.Size,
            stopLevel = request.StopLevel,
            profitLevel = request.ProfitLevel,
            guaranteedStop = false,
        };
        using var response = await http.PostAsJsonAsync("api/v1/positions", payload, cancellationToken);
        Track("positions.open", response.StatusCode);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<DealReferenceResponse>(cancellationToken);
        return body?.DealReference ?? throw new CapitalComException("Risposta di apertura senza dealReference.", response.StatusCode);
    }

    public async Task<DealConfirmation?> GetConfirmationAsync(string dealReference, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"api/v1/confirms/{Uri.EscapeDataString(dealReference)}", cancellationToken);
        Track("confirms", response.StatusCode);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ConfirmResponse>(cancellationToken);
        return body is null ? null : new DealConfirmation(dealReference, body.DealStatus, body.DealId, body.Reason);
    }

    private static void Track(string endpoint, HttpStatusCode status) =>
        MajordomoTelemetry.CapitalApiRequests.Add(1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status", (int)status));

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new CapitalComException($"Capital.com ha risposto {(int)response.StatusCode}: {Truncate(error)}", response.StatusCode);
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];

    // ---- DTO del broker: restano dentro l'ACL ----
    private sealed record PricesResponse(List<PriceDto> Prices);

    private sealed record PriceDto(
        DateTimeOffset SnapshotTime,
        [property: JsonPropertyName("snapshotTimeUTC")] DateTimeOffset? SnapshotTimeUtc,
        BidAsk OpenPrice,
        BidAsk ClosePrice,
        BidAsk HighPrice,
        BidAsk LowPrice);

    private sealed record BidAsk(decimal Bid, decimal Ask)
    {
        public decimal Mid => (Bid + Ask) / 2m;
    }

    private sealed record PositionsResponse(List<PositionEnvelope> Positions);

    private sealed record PositionEnvelope(PositionDto Position, MarketDto Market);

    private sealed record PositionDto(
        string DealId,
        string Direction,
        decimal Size,
        decimal Level,
        decimal? StopLevel,
        decimal? ProfitLevel,
        decimal Upl,
        string Currency,
        [property: JsonPropertyName("createdDateUTC")] DateTimeOffset? CreatedDateUtc);

    private sealed record MarketDto(string Epic);

    private sealed record DealReferenceResponse(string DealReference);

    private sealed record ConfirmResponse(string DealStatus, string? DealId, string? Reason);
}

/// <summary>Errore restituito dal broker, con lo status HTTP originale.</summary>
public sealed class CapitalComException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
