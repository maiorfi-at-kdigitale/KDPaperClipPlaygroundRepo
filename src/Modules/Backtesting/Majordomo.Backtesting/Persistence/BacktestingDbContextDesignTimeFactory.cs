using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Majordomo.Backtesting.Persistence;

/// <summary>Usata solo da `dotnet ef` per generare le migrazioni del modulo.</summary>
internal sealed class BacktestingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BacktestingDbContext>
{
    public BacktestingDbContext CreateDbContext(string[] args) => new(PersistenceExtensions.DesignTimeOptions<BacktestingDbContext>(BacktestingDbContext.Schema));
}
