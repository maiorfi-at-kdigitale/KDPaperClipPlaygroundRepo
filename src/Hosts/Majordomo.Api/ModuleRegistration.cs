using Majordomo.Backtesting;
using Majordomo.Execution;
using Majordomo.Forecasting.Client;
using Majordomo.MarketData;
using Majordomo.Notifications;
using Majordomo.Portfolio;
using Majordomo.Risk;
using Majordomo.Strategy;

namespace Majordomo.Api;

internal static class ModuleRegistration
{
    /// <summary>Registra tutti i moduli (stessa composizione dell'host worker, senza servizi in background).</summary>
    public static IServiceCollection AddMajordomoModules(this IServiceCollection services, IConfiguration configuration) => services
        .AddForecastingClient(configuration)
        .AddStrategyModule(configuration)
        .AddRiskModule()
        .AddExecutionModule(configuration)
        .AddPortfolioModule()
        .AddMarketDataModule()
        .AddBacktestingModule()
        .AddNotificationsModule(configuration);
}
