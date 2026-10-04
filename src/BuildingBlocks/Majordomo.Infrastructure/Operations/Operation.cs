using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Infrastructure.Operations;

public enum OperationKind
{
    Backtest,
    KillSwitch,
}

public enum OperationStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

/// <summary>Operazione asincrona esposta da <c>GET /v1/operations/{id}</c> (OpenAPI v1).</summary>
public sealed class Operation
{
    public Guid Id { get; set; }

    public OperationKind Kind { get; set; }

    public OperationStatus Status { get; set; }

    public Guid? JobId { get; set; }

    public string? ResultUrl { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Operation Create(OperationKind kind, Guid? jobId, DateTimeOffset now, OperationStatus status = OperationStatus.Queued) => new()
    {
        Id = Guid.CreateVersion7(),
        Kind = kind,
        Status = status,
        JobId = jobId,
        CreatedAt = now,
        UpdatedAt = now,
    };
}

public interface IOperationStore
{
    Task<Operation?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task CompleteAsync(Guid id, bool succeeded, string? resultUrl, string? error, CancellationToken cancellationToken);

    Task MarkRunningAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class EfOperationStore(InfraDbContext db, TimeProvider clock) : IOperationStore
{
    public Task<Operation?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Operations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task MarkRunningAsync(Guid id, CancellationToken cancellationToken) =>
        db.Operations.Where(x => x.Id == id && x.Status == OperationStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, OperationStatus.Running)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), cancellationToken);

    public Task CompleteAsync(Guid id, bool succeeded, string? resultUrl, string? error, CancellationToken cancellationToken) =>
        db.Operations.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, succeeded ? OperationStatus.Succeeded : OperationStatus.Failed)
                .SetProperty(x => x.ResultUrl, resultUrl)
                .SetProperty(x => x.Error, error)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), cancellationToken);
}
