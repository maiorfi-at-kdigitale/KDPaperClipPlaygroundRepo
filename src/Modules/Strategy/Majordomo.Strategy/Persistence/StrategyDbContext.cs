using Majordomo.Infrastructure.Persistence;
using Majordomo.Strategy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Strategy.Persistence;

public sealed class StrategyDbContext(DbContextOptions<StrategyDbContext> options) : DbContext(options)
{
    public const string Schema = "strategy";

    public DbSet<TradingJob> Jobs => Set<TradingJob>();

    public DbSet<Decision> Decisions => Set<Decision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedInfraTables();

        modelBuilder.Entity<TradingJob>(b =>
        {
            b.ToTable("trading_jobs");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Name).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.StatusBeforePause).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.HaltReason).HasMaxLength(500);
            b.Property(x => x.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Decision>(b =>
        {
            b.ToTable("decisions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Sequence).UseIdentityAlwaysColumn();
            b.HasIndex(x => x.Sequence).IsUnique();
            b.HasIndex(x => new { x.JobId, x.Sequence });
            b.Property(x => x.Epic).HasMaxLength(40);
            b.Property(x => x.Model).HasMaxLength(100);
            b.Property(x => x.ForecastFingerprint).HasMaxLength(100);
            b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.TraceId).HasMaxLength(64);
        });
    }
}
