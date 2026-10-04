using Majordomo.CapitalCom;
using Majordomo.Execution.Contracts;
using Majordomo.Execution.Domain;
using Majordomo.Execution.Persistence;
using Majordomo.Infrastructure;
using Majordomo.Infrastructure.Leadership;
using Majordomo.Infrastructure.Messaging;
using Majordomo.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Majordomo.Execution.Application;

internal sealed class ExecutionService(ExecutionDbContext db, ICapitalComGateway gateway, TimeProvider clock, ILogger<ExecutionService> logger)
    : IExecutionModule
{
    public async Task<OrderSubmissionResult> SubmitAsync(SubmitOrder command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            JobId = command.JobId,
            DecisionId = command.DecisionId,
            Epic = command.Epic,
            Direction = command.Direction,
            Size = command.Size,
            StopLevel = command.StopLevel,
            ProfitLevel = command.ProfitLevel,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken); // l'intento è persistito PRIMA della chiamata al broker

        try
        {
            order.DealReference = await gateway.OpenPositionAsync(
                new OpenPositionRequest(order.Epic, order.Direction, order.Size, order.StopLevel, order.ProfitLevel), cancellationToken);
            order.Status = OrderStatus.Submitted;
            var confirmation = await gateway.GetConfirmationAsync(order.DealReference, cancellationToken);
            Apply(order, confirmation);
        }
        catch (CapitalComException ex) when ((int)ex.StatusCode is >= 400 and < 500 && order.DealReference is null)
        {
            order.Status = OrderStatus.Rejected;
            order.RejectReason = ex.Message;
            db.EnqueueOutboxMessage(IntegrationEventTypes.OrderRejected, order.JobId.ToString(),
                new { orderId = order.Id, jobId = order.JobId, epic = order.Epic, reason = order.RejectReason }, clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or CapitalComException or TimeoutException)
        {
            // Esito incerto: nessun retry (rischio doppia apertura), si attende la riconciliazione.
            logger.LogWarning(ex, "Esito incerto per l'ordine {OrderId}: stato Unknown", order.Id);
            order.Status = OrderStatus.Unknown;
        }

        order.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(CancellationToken.None);
        return new OrderSubmissionResult(order.Id, order.Status.ToString());
    }

    internal void Apply(Order order, DealConfirmation? confirmation)
    {
        if (confirmation is null)
        {
            return;
        }

        if (confirmation.IsAccepted)
        {
            order.Status = OrderStatus.Accepted;
            order.DealId = confirmation.DealId;
            db.EnqueueOutboxMessage(IntegrationEventTypes.PositionOpened, order.JobId.ToString(),
                new { orderId = order.Id, jobId = order.JobId, epic = order.Epic, direction = order.Direction, size = order.Size, dealId = order.DealId },
                clock.GetUtcNow());
        }
        else
        {
            order.Status = OrderStatus.Rejected;
            order.RejectReason = confirmation.Reason ?? confirmation.DealStatus;
            db.EnqueueOutboxMessage(IntegrationEventTypes.OrderRejected, order.JobId.ToString(),
                new { orderId = order.Id, jobId = order.JobId, epic = order.Epic, reason = order.RejectReason }, clock.GetUtcNow());
        }
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetDealOwnersAsync(CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking().Where(o => o.DealId != null)
            .Select(o => new { o.DealId, o.JobId })
            .ToDictionaryAsync(o => o.DealId!, o => o.JobId, cancellationToken);

    public async Task<ExecutionStats> GetStatsAsync(Guid jobId, string epic, DateTimeOffset dayStart, CancellationToken cancellationToken)
    {
        var ordersToday = await db.Orders.CountAsync(o => o.JobId == jobId && o.CreatedAt >= dayStart, cancellationToken);
        var unknown = await db.Orders.AnyAsync(o => o.JobId == jobId && o.Epic == epic
            && (o.Status == OrderStatus.Unknown || o.Status == OrderStatus.Pending || o.Status == OrderStatus.Submitted), cancellationToken);
        return new ExecutionStats(ordersToday, unknown);
    }

    public Task<int> CountAcceptedOrdersAsync(Guid? jobId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        db.Orders.CountAsync(o => (jobId == null || o.JobId == jobId) && o.Status == OrderStatus.Accepted
            && o.CreatedAt >= from && o.CreatedAt < to, cancellationToken);
}

/// <summary>Riconciliazione periodica degli ordini con esito incerto (solo worker leader).</summary>
internal sealed class OrderReconciliationService(IServiceScopeFactory scopes, ILeaderState leader, ILogger<OrderReconciliationService> logger)
    : LeaderGatedPeriodicService(leader, logger)
{
    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<ICapitalComGateway>();
        var service = scope.ServiceProvider.GetRequiredService<ExecutionService>();
        var uncertain = await db.Orders
            .Where(o => o.Status == OrderStatus.Unknown || o.Status == OrderStatus.Submitted)
            .OrderBy(o => o.CreatedAt).Take(20).ToListAsync(cancellationToken);
        foreach (var order in uncertain)
        {
            if (order.DealReference is null)
            {
                // TODO(T-12): ricerca per epic/size/ora in GET /history/activity.
                MajordomoTelemetry.ReconciliationDrift.Add(1, new KeyValuePair<string, object?>("kind", "order_without_reference"));
                continue;
            }

            service.Apply(order, await gateway.GetConfirmationAsync(order.DealReference, cancellationToken));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
