using Majordomo.ForecastingStub;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();
app.MapGrpcService<NaiveForecastingService>();
app.MapGrpcReflectionService();
app.MapGet("/", () => "Majordomo Forecasting Stub (gRPC su HTTP/2). Usare un client gRPC: ListModels / Forecast.");
await app.RunAsync();
