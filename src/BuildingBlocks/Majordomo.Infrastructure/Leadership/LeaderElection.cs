using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Majordomo.Infrastructure.Leadership;

/// <summary>Indica se l'istanza corrente del worker è il leader (unico esecutore di ordini).</summary>
public interface ILeaderState
{
    bool IsLeader { get; }
}

internal sealed class LeaderState : ILeaderState
{
    private volatile bool _isLeader;

    public bool IsLeader => _isLeader;

    public void Set(bool value) => _isLeader = value;
}

/// <summary>Usato dall'API: non è mai leader, quindi i servizi periodici non partono.</summary>
internal sealed class NeverLeader : ILeaderState
{
    public bool IsLeader => false;
}

public sealed class LeadershipOptions
{
    public const string Section = "Leadership";

    /// <summary>Chiave dell'advisory lock PostgreSQL condivisa da tutte le istanze del worker.</summary>
    public long LockKey { get; set; } = 7_310_001;

    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Leader election tramite <c>pg_try_advisory_lock</c> su una connessione dedicata (RFC-001 §5.2):
/// il lock vive finché vive la connessione, quindi un worker che muore lo rilascia automaticamente.
/// </summary>
internal sealed class PostgresLeaderElectionService(
    NpgsqlDataSource dataSource,
    LeaderState state,
    IOptions<LeadershipOptions> options,
    ILogger<PostgresLeaderElectionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!state.IsLeader)
                    {
                        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
                        cmd.Parameters.AddWithValue("key", o.LockKey);
                        var acquired = (bool)(await cmd.ExecuteScalarAsync(stoppingToken))!;
                        if (acquired)
                        {
                            state.Set(true);
                            logger.LogInformation("Leadership acquisita (advisory lock {LockKey})", o.LockKey);
                        }
                    }
                    else
                    {
                        await using var ping = new NpgsqlCommand("SELECT 1", connection);
                        await ping.ExecuteScalarAsync(stoppingToken);
                    }

                    await Task.Delay(o.CheckInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (state.IsLeader)
                {
                    logger.LogWarning(ex, "Leadership persa: connessione al database interrotta");
                }
                else
                {
                    logger.LogWarning(ex, "Database non raggiungibile per la leader election, nuovo tentativo");
                }

                state.Set(false);
                await Task.Delay(o.CheckInterval, stoppingToken);
            }
        }

        state.Set(false);
    }
}
