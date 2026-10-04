using Majordomo.Infrastructure.Persistence;
using Majordomo.Risk.Domain;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Risk.Persistence;

public sealed class RiskDbContext(DbContextOptions<RiskDbContext> options) : DbContext(options)
{
    public const string Schema = "risk";

    public DbSet<KillSwitch> KillSwitches => Set<KillSwitch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedInfraTables();
        modelBuilder.Entity<KillSwitch>(b =>
        {
            b.ToTable("kill_switch");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Scope).HasConversion<string>().HasMaxLength(10);
            b.Property(x => x.ActivatedBy).HasMaxLength(200);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.Version).IsRowVersion();
        });
    }
}
