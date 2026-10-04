namespace Majordomo.Backtesting.Contracts;

public interface IBacktestingModule
{
    /// <summary>True se l'operazione è un backtest concluso con successo per il job (evidenza ADR-0006).</summary>
    Task<bool> HasSucceededBacktestAsync(Guid jobId, Guid operationId, CancellationToken cancellationToken);
}
