using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Majordomo.Strategy.Persistence;

/// <summary>Usata solo da `dotnet ef` per generare le migrazioni del modulo.</summary>
internal sealed class StrategyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StrategyDbContext>
{
    public StrategyDbContext CreateDbContext(string[] args) => new(PersistenceExtensions.DesignTimeOptions<StrategyDbContext>(StrategyDbContext.Schema));
}
