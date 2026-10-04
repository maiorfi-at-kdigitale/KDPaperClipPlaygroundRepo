using Majordomo.SharedKernel;

namespace Majordomo.Risk.Domain;

public enum KillSwitchScope
{
    Global,
    Job,
}

/// <summary>Stato del kill switch: riga singola (Id = 1).</summary>
public sealed class KillSwitch
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public bool Active { get; set; }

    public KillSwitchScope Scope { get; set; }

    public List<Guid> JobIds { get; set; } = [];

    public bool ClosePositions { get; set; }

    public string? ActivatedBy { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public string? Reason { get; set; }

    public uint Version { get; set; }

    public bool Blocks(Guid jobId) => Active && (Scope == KillSwitchScope.Global || JobIds.Contains(jobId));

    public void Activate(KillSwitchScope scope, Guid? jobId, string reason, bool closePositions, string user, DateTimeOffset now)
    {
        if (scope == KillSwitchScope.Job && jobId is null)
        {
            throw new RequestValidationException("jobId", "jobId obbligatorio con scope 'job'.");
        }

        if (scope == KillSwitchScope.Global || !Active || Scope == KillSwitchScope.Job)
        {
            Scope = Active && Scope == KillSwitchScope.Global ? KillSwitchScope.Global : scope;
        }

        if (scope == KillSwitchScope.Job && jobId is { } id && !JobIds.Contains(id))
        {
            JobIds.Add(id);
        }

        Active = true;
        ClosePositions = closePositions;
        ActivatedBy = user;
        ActivatedAt = now;
        Reason = reason;
    }

    public void Release()
    {
        if (!Active)
        {
            throw new DomainException("Il kill switch non è attivo.", "kill_switch_inactive");
        }

        Active = false;
        JobIds = [];
        ClosePositions = false;
    }
}
