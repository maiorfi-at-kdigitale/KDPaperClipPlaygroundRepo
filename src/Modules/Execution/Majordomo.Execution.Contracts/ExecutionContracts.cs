namespace Majordomo.Execution.Contracts;

public sealed record SubmitOrder(Guid JobId, Guid DecisionId, string Epic, string Direction, decimal Size, decimal StopLevel, decimal? ProfitLevel);

public sealed record OrderSubmissionResult(Guid OrderId, string Status);

public sealed record ExecutionStats(int OrdersToday, bool HasUnknownOrderOnInstrument);

public interface IExecutionModule
{
    /// <summary>Invia un ordine al broker. Mai ritentato: su esito incerto l'ordine resta Unknown fino alla riconciliazione.</summary>
    Task<OrderSubmissionResult> SubmitAsync(SubmitOrder command, CancellationToken cancellationToken);

    /// <summary>Mappa dealId → jobId per gli ordini originati dal sistema.</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetDealOwnersAsync(CancellationToken cancellationToken);

    Task<ExecutionStats> GetStatsAsync(Guid jobId, string epic, DateTimeOffset dayStart, CancellationToken cancellationToken);

    Task<int> CountAcceptedOrdersAsync(Guid? jobId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
