using Majordomo.Backtesting;
using Majordomo.Execution;
using Majordomo.Forecasting.Client;
using Majordomo.Infrastructure;
using Majordomo.MarketData;
using Majordomo.Notifications;
using Majordomo.Observability;
using Majordomo.Portfolio;
using Majordomo.Risk;
using Majordomo.Strategy;

var builder = Host.CreateApplicationBuilder(args);

builder.AddMajordomoObservability("majordomo-worker");
builder.Services
    .AddMajordomoInfrastructure(builder.Configuration)
    .AddWorkerHostRole()
    .AddForecastingClient(builder.Configuration)
    .AddStrategyModule(builder.Configuration).AddStrategyWorker()
    .AddRiskModule().AddRiskWorker()
    .AddExecutionModule(builder.Configuration).AddExecutionWorker()
    .AddPortfolioModule()
    .AddMarketDataModule()
    .AddBacktestingModule().AddBacktestingWorker()
    .AddNotificationsModule(builder.Configuration);

await builder.Build().RunAsync();
