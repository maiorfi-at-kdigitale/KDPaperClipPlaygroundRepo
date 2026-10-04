using Majordomo.Risk.Contracts;

namespace Majordomo.Portfolio.Contracts;

public interface IPortfolioModule
{
    /// <summary>Snapshot di rischio del job (equity, esposizione, ordini del giorno) letto dal Risk engine.</summary>
    Task<RiskSnapshot> GetRiskSnapshotAsync(Guid jobId, string epic, decimal allocation, CancellationToken cancellationToken);
}
