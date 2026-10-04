using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Majordomo.Execution.Persistence;

/// <summary>Usata solo da `dotnet ef` per generare le migrazioni del modulo.</summary>
internal sealed class ExecutionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ExecutionDbContext>
{
    public ExecutionDbContext CreateDbContext(string[] args) => new(PersistenceExtensions.DesignTimeOptions<ExecutionDbContext>(ExecutionDbContext.Schema));
}
