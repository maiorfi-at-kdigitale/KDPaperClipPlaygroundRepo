using Majordomo.Execution.Domain;
using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Execution.Persistence;

public sealed class ExecutionDbContext(DbContextOptions<ExecutionDbContext> options) : DbContext(options)
{
    public const string Schema = "execution";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedInfraTables();
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.Epic).HasMaxLength(40);
            b.Property(x => x.Direction).HasMaxLength(4);
            b.Property(x => x.Size).HasPrecision(18, 4);
            b.Property(x => x.StopLevel).HasPrecision(18, 6);
            b.Property(x => x.ProfitLevel).HasPrecision(18, 6);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.DealReference).HasMaxLength(100);
            b.Property(x => x.DealId).HasMaxLength(100);
            b.Property(x => x.RejectReason).HasMaxLength(500);
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.JobId, x.CreatedAt });
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.DecisionId).IsUnique();
        });
    }
}
