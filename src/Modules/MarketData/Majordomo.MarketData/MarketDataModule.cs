using Majordomo.CapitalCom;
using Majordomo.MarketData.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.MarketData;

/// <summary>
/// Prezzi via ACL con cache in memoria (chiave epic+risoluzione+count, TTL 15 s; se la cache non c'è si legge dal broker).
/// TODO(T-04): backfill su TimescaleDB e streaming WebSocket.
/// </summary>
internal sealed class MarketDataService(ICapitalComGateway gateway, IMemoryCache cache) : IMarketDataModule
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    public async Task<IReadOnlyList<Candle>> GetRecentCandlesAsync(string epic, string resolution, int count, CancellationToken cancellationToken)
    {
        var key = $"md:{epic}:{resolution}:{count}";
        if (cache.TryGetValue<IReadOnlyList<Candle>>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var candles = (await gateway.GetCandlesAsync(epic, resolution, count, cancellationToken))
            .OrderBy(c => c.Time)
            .Select(c => new Candle(c.Time, c.Open, c.High, c.Low, c.Close))
            .ToList();
        cache.Set(key, (IReadOnlyList<Candle>)candles, Ttl);
        return candles;
    }
}

public static class MarketDataModule
{
    public static IServiceCollection AddMarketDataModule(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IMarketDataModule, MarketDataService>();
        return services;
    }
}
