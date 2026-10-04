using System.Text.Json;
using Majordomo.Backtesting.Contracts;
using Majordomo.Infrastructure.Web;
using Majordomo.SharedKernel;
using Majordomo.Strategy.Application;
using Majordomo.Strategy.Contracts;
using Majordomo.Strategy.Domain;
using Majordomo.Strategy.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Strategy.Endpoints;

public sealed record CreateJobRequest(string? Name, string? Description, JsonElement Parameters);

public sealed record TransitionRequest(string? Action, string? Reason, Guid? EvidenceOperationId);

public sealed record TradingJobResponse(
    Guid Id, string Name, string? Description, string Status, string? HaltReason, int ParameterSetVersion,
    JsonElement Parameters, string Etag, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    internal static TradingJobResponse From(TradingJob j)
    {
        using var doc = JsonDocument.Parse(j.ParametersJson);
        return new(j.Id, j.Name, j.Description, j.Status.ToString(), j.HaltReason, j.ParameterSetVersion,
            doc.RootElement.Clone(), ETags.From(j.Version), j.CreatedAt, j.UpdatedAt);
    }
}

public sealed record DecisionResponse(
    Guid Id, Guid JobId, int ParameterSetVersion, string Epic, DateTimeOffset BarClose, string? Model,
    string? ForecastFingerprint, SignalResponse? Signal, string Outcome, string? Reason, Guid? OrderId, string? TraceId);

public sealed record SignalResponse(string Direction, double Strength, double Confidence);

public sealed record DecisionPage(IReadOnlyList<DecisionResponse> Items, string? NextCursor);

public static class StrategyEndpoints
{
    public static IEndpointRouteBuilder MapStrategyEndpoints(this IEndpointRouteBuilder v1)
    {
        var jobs = v1.MapGroup("/jobs").WithTags("jobs");

        jobs.MapGet("/", async (JobStatus? status, StrategyDbContext db, CancellationToken ct) =>
        {
            var query = db.Jobs.AsNoTracking();
            if (status is { } s)
            {
                query = query.Where(x => x.Status == s);
            }

            var list = await query.OrderBy(x => x.CreatedAt).Take(500).ToListAsync(ct);
            return TypedResults.Ok(list.Select(TradingJobResponse.From).ToList());
        }).RequireAuthorization(MajordomoPolicies.CanRead).WithName("ListJobs");

        jobs.MapPost("/", async (CreateJobRequest request, StrategyDbContext db, ParametersValidator validator, TimeProvider clock, CancellationToken ct) =>
        {
            var name = request.Name?.Trim();
            if (name is null || name.Length is < 3 or > 100)
            {
                throw new RequestValidationException("name", "Il nome deve avere fra 3 e 100 caratteri.");
            }

            var job = TradingJob.Create(name, request.Description, validator.ValidateAndNormalize(request.Parameters), clock.GetUtcNow());
            db.Jobs.Add(job);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/v1/jobs/{job.Id}", TradingJobResponse.From(job));
        }).RequireAuthorization(MajordomoPolicies.CanOperate).WithIdempotency().WithName("CreateJob");

        jobs.MapGet("/{jobId:guid}", async (Guid jobId, StrategyDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var job = await Find(db, jobId, ct, tracking: false);
            http.Response.Headers.ETag = ETags.From(job.Version);
            return TypedResults.Ok(TradingJobResponse.From(job));
        }).RequireAuthorization(MajordomoPolicies.CanRead).WithName("GetJob");

        jobs.MapPut("/{jobId:guid}/parameters", async (Guid jobId, [FromHeader(Name = "If-Match")] string? ifMatch, JsonElement parameters,
            StrategyDbContext db, StrategyService service, ParametersValidator validator, TimeProvider clock, HttpContext http, CancellationToken ct) =>
        {
            if (!ETags.TryParse(ifMatch, out var expected))
            {
                return Results.Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "Header If-Match obbligatorio");
            }

            var job = await Find(db, jobId, ct);
            if (job.Version != expected)
            {
                throw new PreconditionFailedException("ETag non corrispondente: il job è stato modificato.");
            }

            db.Entry(job).Property(x => x.Version).OriginalValue = expected;
            job.ReplaceParameters(validator.ValidateAndNormalize(parameters), clock.GetUtcNow());
            await service.SaveAsync([job], ct);
            http.Response.Headers.ETag = ETags.From(job.Version);
            return Results.Ok(TradingJobResponse.From(job));
        }).RequireAuthorization(MajordomoPolicies.CanOperate).WithIdempotency().WithName("ReplaceJobParameters");

        jobs.MapPost("/{jobId:guid}/transitions", async (Guid jobId, TransitionRequest request, StrategyDbContext db,
            StrategyService service, IBacktestingModule backtesting, TimeProvider clock, HttpContext http, CancellationToken ct) =>
        {
            if (!Enum.TryParse<TransitionAction>(request.Action, ignoreCase: true, out var action))
            {
                throw new RequestValidationException("action", "Valori ammessi: promote, pause, resume, archive.");
            }

            var job = await Find(db, jobId, ct);
            if (job.RequiresRiskAdmin(action) && !http.User.IsInRole(MajordomoRoles.RiskAdmin))
            {
                throw new ForbiddenOperationException("Questa transizione richiede il ruolo risk-admin.");
            }

            var hasEvidence = request.EvidenceOperationId is { } evidence
                && await backtesting.HasSucceededBacktestAsync(jobId, evidence, ct);
            job.Apply(action, request.Reason, hasEvidence, clock.GetUtcNow());
            await service.SaveAsync([job], ct);
            return TypedResults.Accepted($"/v1/jobs/{job.Id}", TradingJobResponse.From(job));
        }).RequireAuthorization(MajordomoPolicies.CanOperate).WithIdempotency().WithName("TransitionJob");

        jobs.MapGet("/{jobId:guid}/decisions", async (Guid jobId, DateTimeOffset? from, DateTimeOffset? to, string? cursor, int? limit,
            StrategyDbContext db, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var query = db.Decisions.AsNoTracking().Where(x => x.JobId == jobId);
            if (from is { } f)
            {
                query = query.Where(x => x.BarClose >= f);
            }

            if (to is { } t)
            {
                query = query.Where(x => x.BarClose < t);
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                if (!long.TryParse(cursor, out var seq))
                {
                    throw new RequestValidationException("cursor", "Cursore non valido.");
                }

                query = query.Where(x => x.Sequence < seq);
            }

            var page = await query.OrderByDescending(x => x.Sequence).Take(take + 1).ToListAsync(ct);
            var next = page.Count > take ? page[take - 1].Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            return TypedResults.Ok(new DecisionPage(page.Take(take).Select(d => new DecisionResponse(
                d.Id, d.JobId, d.ParameterSetVersion, d.Epic, d.BarClose, d.Model, d.ForecastFingerprint,
                d.Direction is { } dir ? new SignalResponse(dir.ToString(), d.Strength ?? 0, d.Confidence ?? 0) : null,
                d.Outcome.ToString(), d.Reason, d.OrderId, d.TraceId)).ToList(), next));
        }).RequireAuthorization(MajordomoPolicies.CanRead).WithName("ListDecisions");

        return v1;
    }

    private static async Task<TradingJob> Find(StrategyDbContext db, Guid jobId, CancellationToken ct, bool tracking = true)
    {
        var query = tracking ? db.Jobs : db.Jobs.AsNoTracking();
        return await query.FirstOrDefaultAsync(x => x.Id == jobId, ct)
            ?? throw new NotFoundException($"Job {jobId} non trovato.");
    }
}
