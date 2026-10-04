using Majordomo.Backtesting.Domain;
using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Backtesting.Persistence;

public sealed class BacktestingDbContext(DbContextOptions<BacktestingDbContext> options) : DbContext(options)
{
    public const string Schema = "backtesting";

    public DbSet<BacktestRun> Runs => Set<BacktestRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedInfraTables();
        modelBuilder.Entity<BacktestRun>(b =>
        {
            b.ToTable("backtest_runs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.SlippagePoints).HasPrecision(18, 6);
            b.Property(x => x.ResultJson).HasColumnName("result").HasColumnType("jsonb");
            b.HasIndex(x => x.JobId);
        });
    }
}
