using Majordomo.Execution.Persistence;
using Majordomo.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Execution.Endpoints;

public sealed record OrderResponse(
    Guid Id, Guid JobId, Guid DecisionId, string Epic, string Direction, decimal Size, decimal StopLevel, decimal? ProfitLevel,
    string Status, string? DealReference, string? DealId, string? RejectReason, DateTimeOffset CreatedAt);

public static class ExecutionEndpoints
{
    public static IEndpointRouteBuilder MapExecutionEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/orders/{orderId:guid}", async (Guid orderId, ExecutionDbContext db, CancellationToken ct) =>
        {
            var o = await db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct)
                ?? throw new NotFoundException($"Ordine {orderId} non trovato.");
            return TypedResults.Ok(new OrderResponse(o.Id, o.JobId, o.DecisionId, o.Epic, o.Direction, o.Size, o.StopLevel, o.ProfitLevel,
                o.Status.ToString(), o.DealReference, o.DealId, o.RejectReason, o.CreatedAt));
        }).RequireAuthorization(MajordomoPolicies.CanRead).WithTags("execution").WithName("GetOrder");
        return v1;
    }
}
