using Majordomo.Infrastructure.Operations;
using Majordomo.Infrastructure.Web;
using Majordomo.Risk.Application;
using Majordomo.Risk.Domain;
using Majordomo.Risk.Persistence;
using Majordomo.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Risk.Endpoints;

public sealed record KillSwitchRequest(string? Scope, Guid? JobId, string? Reason, bool? ClosePositions);

public sealed record KillSwitchReleaseRequest(string? Reason);

public sealed record KillSwitchStateResponse(bool Active, string? Scope, IReadOnlyList<Guid> JobIds, string? ActivatedBy, DateTimeOffset? ActivatedAt, string? Reason)
{
    internal static KillSwitchStateResponse From(KillSwitch? k) => k is null
        ? new(false, null, [], null, null, null)
        : new(k.Active, k.Active ? k.Scope.ToString().ToLowerInvariant() : null, k.JobIds, k.ActivatedBy, k.ActivatedAt, k.Reason);
}

public sealed record OperationResponse(Guid Id, string Kind, string Status, string? ResultUrl, string? Error)
{
    public static OperationResponse From(Operation o) =>
        new(o.Id, o.Kind == OperationKind.KillSwitch ? "killSwitch" : "backtest", o.Status.ToString(), o.ResultUrl, o.Error);
}

public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder v1)
    {
        var group = v1.MapGroup("/kill-switch").WithTags("risk");

        group.MapGet("/", async (RiskDbContext db, CancellationToken ct) =>
                TypedResults.Ok(KillSwitchStateResponse.From(await db.KillSwitches.AsNoTracking().FirstOrDefaultAsync(ct))))
            .RequireAuthorization(MajordomoPolicies.CanRead).WithName("GetKillSwitch");

        // Attivare il kill switch riduce il rischio: basta il ruolo operator.
        group.MapPost("/", async (KillSwitchRequest request, RiskService risk, TimeProvider clock, HttpContext http, CancellationToken ct) =>
        {
            if (!Enum.TryParse<KillSwitchScope>(request.Scope, ignoreCase: true, out var scope))
            {
                throw new RequestValidationException("scope", "Valori ammessi: global, job.");
            }

            if (request.Reason is not { Length: >= 3 })
            {
                throw new RequestValidationException("reason", "Motivazione obbligatoria (min 3 caratteri).");
            }

            var op = await risk.ActivateAsync(scope, request.JobId, request.Reason, request.ClosePositions ?? true,
                http.User.UserName(), clock.GetUtcNow(), ct);
            return TypedResults.Accepted($"/v1/operations/{op.Id}", OperationResponse.From(op));
        }).RequireAuthorization(MajordomoPolicies.CanOperate).WithIdempotency().WithName("ActivateKillSwitch");

        // Rilasciare il kill switch aumenta il rischio: richiede risk-admin.
        group.MapPost("/release", async (KillSwitchReleaseRequest request, RiskService risk, RiskDbContext db, CancellationToken ct) =>
        {
            if (request.Reason is not { Length: >= 3 })
            {
                throw new RequestValidationException("reason", "Motivazione obbligatoria (min 3 caratteri).");
            }

            var ks = await risk.GetOrCreateAsync(ct);
            ks.Release();
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(KillSwitchStateResponse.From(ks));
        }).RequireAuthorization(MajordomoPolicies.CanAdministerRisk).WithName("ReleaseKillSwitch");

        return v1;
    }
}
