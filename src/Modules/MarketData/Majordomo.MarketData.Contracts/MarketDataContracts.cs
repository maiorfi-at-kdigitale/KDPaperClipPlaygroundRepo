namespace Majordomo.MarketData.Contracts;

public sealed record Candle(DateTimeOffset Time, decimal Open, decimal High, decimal Low, decimal Close);

public interface IMarketDataModule
{
    /// <summary>Ultime <paramref name="count"/> candele chiuse (ordine cronologico crescente).</summary>
    Task<IReadOnlyList<Candle>> GetRecentCandlesAsync(string epic, string resolution, int count, CancellationToken cancellationToken);
}
