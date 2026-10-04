using Majordomo.SharedKernel;
using Majordomo.Strategy.Contracts;

namespace Majordomo.Strategy.Domain;

public enum TransitionAction
{
    Promote,
    Pause,
    Resume,
    Archive,
}

/// <summary>Evento di dominio: cambio di stato del job.</summary>
public sealed record JobStatusChanged(Guid JobId, JobStatus From, JobStatus To, string? Reason);

/// <summary>Aggregato TradingJob (docs/capital-majordomo/domain/domain-model.md).</summary>
public sealed class TradingJob : AggregateRoot
{
    private TradingJob()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public JobStatus Status { get; private set; }

    public JobStatus? StatusBeforePause { get; private set; }

    public string? HaltReason { get; private set; }

    public int ParameterSetVersion { get; private set; }

    public string ParametersJson { get; private set; } = "{}";

    /// <summary>Token di concorrenza ottimistica (xmin PostgreSQL), esposto come ETag.</summary>
    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TradingJob Create(string name, string? description, string parametersJson, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Description = description,
        Status = JobStatus.Draft,
        ParameterSetVersion = 1,
        ParametersJson = parametersJson,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>Ogni modifica crea una nuova ParameterSetVersion; non ammessa mentre il job opera.</summary>
    public void ReplaceParameters(string parametersJson, DateTimeOffset now)
    {
        if (Status is JobStatus.Backtesting or JobStatus.PaperTrading or JobStatus.Live or JobStatus.Archived)
        {
            throw new DomainException($"Parametri non modificabili nello stato {Status}: mettere in pausa il job.", "job_not_editable");
        }

        ParametersJson = parametersJson;
        ParameterSetVersion++;
        UpdatedAt = now;
    }

    /// <summary>Indica se la transizione richiede il ruolo risk-admin (rischio crescente, ADR-0005/0006).</summary>
    public bool RequiresRiskAdmin(TransitionAction action) =>
        (action == TransitionAction.Promote && Status == JobStatus.PaperTrading)
        || (action == TransitionAction.Resume && Status == JobStatus.Halted);

    public void Apply(TransitionAction action, string? reason, bool hasEvidence, DateTimeOffset now)
    {
        var target = (action, Status) switch
        {
            (TransitionAction.Promote, JobStatus.Draft) => JobStatus.Backtesting,
            (TransitionAction.Promote, JobStatus.Backtesting) => RequireEvidence(hasEvidence, JobStatus.PaperTrading),
            (TransitionAction.Promote, JobStatus.PaperTrading) => RequireEvidence(hasEvidence, JobStatus.Live),
            (TransitionAction.Pause, JobStatus.Backtesting or JobStatus.PaperTrading or JobStatus.Live) => JobStatus.Paused,
            (TransitionAction.Resume, JobStatus.Paused) => StatusBeforePause ?? JobStatus.Draft,
            (TransitionAction.Resume, JobStatus.Halted) => JobStatus.Paused,
            (TransitionAction.Archive, not JobStatus.Live and not JobStatus.Archived) => JobStatus.Archived,
            _ => throw new DomainException($"Transizione '{action}' non ammessa dallo stato {Status}.", "invalid_transition"),
        };

        if (target == JobStatus.Paused && Status != JobStatus.Halted)
        {
            StatusBeforePause = Status;
        }

        if (Status == JobStatus.Halted)
        {
            HaltReason = null;
            StatusBeforePause = null;
        }

        ChangeStatus(target, reason, now);
    }

    public bool Halt(string reason, DateTimeOffset now)
    {
        if (Status is not (JobStatus.PaperTrading or JobStatus.Live or JobStatus.Paused or JobStatus.Backtesting))
        {
            return false;
        }

        HaltReason = reason;
        ChangeStatus(JobStatus.Halted, reason, now);
        return true;
    }

    private static JobStatus RequireEvidence(bool hasEvidence, JobStatus target) => hasEvidence
        ? target
        : throw new DomainException($"La promozione a {target} richiede un'evidenza (evidenceOperationId) valida (ADR-0006).", "evidence_required");

    private void ChangeStatus(JobStatus target, string? reason, DateTimeOffset now)
    {
        var from = Status;
        Status = target;
        UpdatedAt = now;
        Raise(new JobStatusChanged(Id, from, target, reason));
    }
}
