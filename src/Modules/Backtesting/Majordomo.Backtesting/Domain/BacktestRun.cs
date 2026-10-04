namespace Majordomo.Backtesting.Domain;

public sealed class BacktestRun
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public int ParameterSetVersion { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public decimal SlippagePoints { get; set; }

    public string? ResultJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
