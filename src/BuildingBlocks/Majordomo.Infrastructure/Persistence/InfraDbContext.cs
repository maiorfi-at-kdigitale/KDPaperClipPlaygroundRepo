using Majordomo.Infrastructure.Messaging;
using Majordomo.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;

namespace Majordomo.Infrastructure.Persistence;

/// <summary>
/// Contesto dello schema <c>infra</c>: proprietario (anche delle migrazioni) di outbox, inbox e operazioni.
/// I contesti dei moduli mappano outbox e operazioni escludendole dalle proprie migrazioni,
/// così possono scriverle nella stessa transazione dei propri cambiamenti di stato (ADR-0003).
/// </summary>
public sealed class InfraDbContext(DbContextOptions<InfraDbContext> options) : DbContext(options)
{
    public const string Schema = "infra";

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<Operation> Operations => Set<Operation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapSharedInfraTables(excludeFromMigrations: false);

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages", Schema);
            b.HasKey(x => new { x.MessageId, x.Consumer });
            b.Property(x => x.Consumer).HasMaxLength(200);
        });
    }
}

public static class SharedInfraModelBuilderExtensions
{
    /// <summary>Mappa le tabelle condivise di <c>infra</c> (outbox, operazioni).</summary>
    public static ModelBuilder MapSharedInfraTables(this ModelBuilder modelBuilder, bool excludeFromMigrations = true)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", InfraDbContext.Schema, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(200);
            b.Property(x => x.Subject).HasMaxLength(200);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.Property(x => x.TraceParent).HasMaxLength(100);
            b.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        });

        modelBuilder.Entity<Operation>(b =>
        {
            b.ToTable("operations", InfraDbContext.Schema, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(50);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            b.Property(x => x.Error).HasMaxLength(2000);
            b.Property(x => x.ResultUrl).HasMaxLength(500);
            b.HasIndex(x => new { x.Kind, x.Status });
        });

        return modelBuilder;
    }
}
