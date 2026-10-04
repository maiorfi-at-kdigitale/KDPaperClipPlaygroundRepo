using Majordomo.CapitalCom;
using Majordomo.Execution.Contracts;
using Majordomo.Portfolio.Contracts;
using Majordomo.Risk.Contracts;
using Majordomo.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.Portfolio;

public sealed record PositionResponse(
    string DealId, Guid? JobId, string Epic, string Direction, decimal Size, decimal OpenLevel, decimal? StopLevel,
    decimal? ProfitLevel, decimal UnrealizedPnl, string Currency, DateTimeOffset OpenedAt);

public sealed record PerformanceReportResponse(
    DateTimeOffset From, DateTimeOffset To, decimal StartingEquity, decimal EndingEquity, decimal NetPnl, decimal MaxDrawdownPct,
    decimal Sharpe, decimal ProfitFactor, int Trades, decimal HitRate, decimal ForecastMase, decimal QuantileCoverage);

internal sealed class PortfolioService(ICapitalComGateway gateway, IExecutionModule execution, TimeProvider clock) : IPortfolioModule
{
    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(CancellationToken ct)
    {
        var owners = await execution.GetDealOwnersAsync(ct);
        return (await gateway.GetPositionsAsync(ct))
            .Select(p => new PositionResponse(p.DealId, owners.TryGetValue(p.DealId, out var j) ? j : null, p.Epic, p.Direction, p.Size,
                p.OpenLevel, p.StopLevel, p.ProfitLevel, p.UnrealizedPnl, p.Currency, p.OpenedAt))
            .ToList();
    }

    public async Task<RiskSnapshot> GetRiskSnapshotAsync(Guid jobId, string epic, decimal allocation, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var positions = (await GetPositionsAsync(cancellationToken)).Where(p => p.JobId == jobId).ToList();
        var stats = await execution.GetStatsAsync(jobId, epic, new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero), cancellationToken);
        var equity = allocation + positions.Sum(p => p.UnrealizedPnl);
        // TODO(T-12): PnL realizzato, picco di equity e ultima perdita da storico/eventi.
        return new RiskSnapshot(allocation, equity, Math.Max(allocation, equity), 0m,
            positions.Where(p => p.Epic == epic).Sum(p => p.Size * p.OpenLevel), positions.Sum(p => p.Size * p.OpenLevel),
            positions.Count, stats.OrdersToday, null, stats.HasUnknownOrderOnInstrument, now);
    }
}

public static class PortfolioModule
{
    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddScoped<PortfolioService>();
        services.AddScoped<IPortfolioModule>(sp => sp.GetRequiredService<PortfolioService>());
        return services;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/positions", async (PortfolioService portfolio, CancellationToken ct) => TypedResults.Ok(await portfolio.GetPositionsAsync(ct)))
            .RequireAuthorization(MajordomoPolicies.CanRead).WithTags("execution").WithName("ListPositions");

        v1.MapGet("/reports/performance", async (Guid? jobId, DateTimeOffset? from, DateTimeOffset? to, IExecutionModule execution, CancellationToken ct) =>
        {
            if (from is not { } f || to is not { } t || f >= t)
            {
                throw new RequestValidationException("from", "from e to sono obbligatori e from < to.");
            }

            // Scheletro: metriche reali (equity curve, Sharpe, MASE) arrivano con T-10/T-12.
            var trades = await execution.CountAcceptedOrdersAsync(jobId, f, t, ct);
            return TypedResults.Ok(new PerformanceReportResponse(f, t, 0, 0, 0, 0, 0, 0, trades, 0, 0, 0));
        }).RequireAuthorization(MajordomoPolicies.CanRead).WithTags("reports").WithName("GetPerformanceReport");

        return v1;
    }
}
