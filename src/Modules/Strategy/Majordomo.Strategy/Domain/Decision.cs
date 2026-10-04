namespace Majordomo.Strategy.Domain;

public enum DecisionOutcome
{
    NoSignal,
    Rejected,
    Approved,
}

public enum SignalDirection
{
    Long,
    Short,
    Flat,
}

/// <summary>Voce append-only del decision log (audit e riproducibilità).</summary>
public sealed class Decision
{
    public Guid Id { get; set; }

    /// <summary>Sequenza monotona usata come cursore di paginazione.</summary>
    public long Sequence { get; set; }

    public Guid JobId { get; set; }

    public int ParameterSetVersion { get; set; }

    public required string Epic { get; set; }

    public DateTimeOffset BarClose { get; set; }

    public string? Model { get; set; }

    public string? ForecastFingerprint { get; set; }

    public SignalDirection? Direction { get; set; }

    public double? Strength { get; set; }

    public double? Confidence { get; set; }

    public DecisionOutcome Outcome { get; set; }

    public string? Reason { get; set; }

    public Guid? OrderId { get; set; }

    public string? TraceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
