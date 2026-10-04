using Majordomo.Api;
using Majordomo.Backtesting;
using Majordomo.Execution.Endpoints;
using Majordomo.Infrastructure;
using Majordomo.Infrastructure.Operations;
using Majordomo.Infrastructure.Persistence;
using Majordomo.Infrastructure.Web;
using Majordomo.Observability;
using Majordomo.Portfolio;
using Majordomo.Risk.Endpoints;
using Majordomo.SharedKernel;
using Majordomo.Strategy.Endpoints;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddMajordomoObservability("majordomo-api");
builder.Services
    .AddMajordomoInfrastructure(builder.Configuration)
    .AddApiHostRole()
    .AddMajordomoProblemDetails()
    .AddMajordomoAuthentication(builder.Configuration, builder.Environment)
    .AddMajordomoModules(builder.Configuration);
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Capital-Majordomo Management API";
    doc.Info.Version = "v1";
    return Task.CompletedTask;
}));

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
{
    // Solo sviluppo locale: in produzione le migrazioni le applica la pipeline.
    await app.Services.GetRequiredService<MigrationRunner>().MigrateAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapDevPlaygroundEndpoints();
}

app.MapMajordomoHealthChecks();

var v1 = app.MapGroup("/v1");
v1.MapStrategyEndpoints();
v1.MapBacktestingEndpoints();
v1.MapRiskEndpoints();
v1.MapExecutionEndpoints();
v1.MapPortfolioEndpoints();
v1.MapGet("/operations/{operationId:guid}", async (Guid operationId, IOperationStore operations, CancellationToken ct) =>
        TypedResults.Ok(OperationResponse.From(await operations.FindAsync(operationId, ct)
            ?? throw new NotFoundException($"Operazione {operationId} non trovata."))))
    .RequireAuthorization(MajordomoPolicies.CanRead).WithTags("jobs").WithName("GetOperation");

await app.RunAsync();

/// <summary>Esposto per i test con WebApplicationFactory.</summary>
public partial class Program;
