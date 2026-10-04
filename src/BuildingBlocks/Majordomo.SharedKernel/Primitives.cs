namespace Majordomo.SharedKernel;

/// <summary>Violazione di un'invariante di dominio. Mappata a 409 (conflitto di stato) dalla API.</summary>
public sealed class DomainException(string message, string code = "domain_rule_violated") : Exception(message)
{
    /// <summary>Codice stabile, leggibile dai client (es. "transition_not_allowed").</summary>
    public string Code { get; } = code;
}

/// <summary>Risorsa richiesta inesistente. Mappata a 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Operazione vietata dalla policy (non dall'autenticazione). Mappata a 403.</summary>
public sealed class ForbiddenOperationException(string message) : Exception(message);

/// <summary>Base per gli aggregati: raccoglie gli eventi di dominio interni al modulo.</summary>
public abstract class AggregateRoot
{
    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> DomainEvents => _domainEvents;

    protected void Raise(object domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Ruoli dell'interfaccia di gestione (RFC-001 §9).</summary>
public static class MajordomoRoles
{
    public const string Viewer = "viewer";
    public const string Operator = "operator";
    public const string RiskAdmin = "risk-admin";
}

/// <summary>Nomi delle policy di autorizzazione.</summary>
public static class MajordomoPolicies
{
    public const string CanRead = "majordomo.read";
    public const string CanOperate = "majordomo.operate";
    public const string CanAdministerRisk = "majordomo.risk-admin";
}
