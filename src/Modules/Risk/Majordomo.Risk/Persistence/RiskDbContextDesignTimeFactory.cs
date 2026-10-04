using Majordomo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Majordomo.Risk.Persistence;

/// <summary>Usata solo da `dotnet ef` per generare le migrazioni del modulo.</summary>
internal sealed class RiskDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RiskDbContext>
{
    public RiskDbContext CreateDbContext(string[] args) => new(PersistenceExtensions.DesignTimeOptions<RiskDbContext>(RiskDbContext.Schema));
}
