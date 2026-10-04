using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Majordomo.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Majordomo";

    /// <summary>
    /// Registra la NpgsqlDataSource condivisa, il contesto <c>infra</c> e il runner delle migrazioni.
    /// </summary>
    public static IServiceCollection AddMajordomoPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} non configurata.");

        services.AddNpgsqlDataSource(connectionString);
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section));
        services.AddSingleton<MigrationRunner>();
        services.AddModuleDbContext<InfraDbContext>(InfraDbContext.Schema);
        services.AddHealthChecks().AddDbContextCheck<InfraDbContext>("database", tags: ["ready"]);
        return services;
    }

    /// <summary>
    /// Registra il DbContext di un modulo: stesso database, schema dedicato, tabella delle migrazioni
    /// nello schema del modulo, naming snake_case, retry sugli errori transitori (RFC-001 §8).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((sp, options) =>
            ConfigureModule(options, sp.GetRequiredService<NpgsqlDataSource>(), schema));
        services.AddSingleton(new MigrationTarget(typeof(TContext), schema));
        return services;
    }

    internal static void ConfigureModule(DbContextOptionsBuilder options, NpgsqlDataSource dataSource, string schema) =>
        options
            .UseNpgsql(dataSource, npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history", schema)
                .EnableRetryOnFailure(3)
                .CommandTimeout(30))
            .UseSnakeCaseNamingConvention();

    /// <summary>Opzioni per i factory di design-time (dotnet ef) dei moduli.</summary>
    public static DbContextOptions<TContext> DesignTimeOptions<TContext>(string schema)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder
            .UseNpgsql("Host=localhost;Database=majordomo;Username=majordomo;Password=design-time",
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();
        return builder.Options;
    }
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Applica le migrazioni all'avvio dell'API. Solo sviluppo locale: in produzione le applica la pipeline.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

public sealed record MigrationTarget(Type ContextType, string Schema);

/// <summary>Applica le migrazioni di tutti i contesti registrati, prima <c>infra</c> poi i moduli.</summary>
public sealed class MigrationRunner(IServiceProvider services, IEnumerable<MigrationTarget> targets, ILogger<MigrationRunner> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var ordered = targets.DistinctBy(t => t.ContextType)
            .OrderBy(t => t.ContextType == typeof(InfraDbContext) ? 0 : 1);

        foreach (var target in ordered)
        {
            await using var scope = services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(target.ContextType);
            logger.LogInformation("Applicazione migrazioni {Context} (schema {Schema})", target.ContextType.Name, target.Schema);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
