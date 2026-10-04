namespace Majordomo.CapitalCom;

/// <summary>Modello interno (lato Majordomo) di una candela di prezzo mid.</summary>
public sealed record CapitalCandle(DateTimeOffset Time, decimal Open, decimal High, decimal Low, decimal Close);

/// <summary>Posizione aperta sul conto broker.</summary>
public sealed record CapitalPosition(
    string DealId,
    string Epic,
    string Direction,
    decimal Size,
    decimal OpenLevel,
    decimal? StopLevel,
    decimal? ProfitLevel,
    decimal UnrealizedPnl,
    string Currency,
    DateTimeOffset OpenedAt);

/// <summary>Richiesta di apertura posizione: lo stop è sempre obbligatorio (ADR-0005).</summary>
public sealed record OpenPositionRequest(string Epic, string Direction, decimal Size, decimal StopLevel, decimal? ProfitLevel);

/// <summary>Esito della conferma di un deal.</summary>
public sealed record DealConfirmation(string DealReference, string DealStatus, string? DealId, string? Reason)
{
    public bool IsAccepted => string.Equals(DealStatus, "ACCEPTED", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Porta verso Capital.com: l'unico punto del sistema che conosce il modello del broker.</summary>
public interface ICapitalComGateway
{
    Task<bool> PingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CapitalCandle>> GetCandlesAsync(string epic, string resolution, int max, CancellationToken cancellationToken);

    Task<IReadOnlyList<CapitalPosition>> GetPositionsAsync(CancellationToken cancellationToken);

    /// <summary>Apre una posizione e restituisce il dealReference. Mai ritentata automaticamente (non idempotente).</summary>
    Task<string> OpenPositionAsync(OpenPositionRequest request, CancellationToken cancellationToken);

    Task<DealConfirmation?> GetConfirmationAsync(string dealReference, CancellationToken cancellationToken);
}
