namespace Majordomo.Execution.Domain;

public enum OrderStatus
{
    Pending,
    Submitted,
    Accepted,
    Rejected,
    Unknown,
    Failed,
}

public sealed class Order
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public Guid DecisionId { get; set; }

    public required string Epic { get; set; }

    public required string Direction { get; set; }

    public decimal Size { get; set; }

    public decimal StopLevel { get; set; }

    public decimal? ProfitLevel { get; set; }

    public OrderStatus Status { get; set; }

    public string? DealReference { get; set; }

    public string? DealId { get; set; }

    public string? RejectReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; set; }
}
