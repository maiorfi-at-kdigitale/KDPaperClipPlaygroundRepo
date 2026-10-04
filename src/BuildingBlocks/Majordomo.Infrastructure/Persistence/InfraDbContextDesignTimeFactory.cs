using Microsoft.EntityFrameworkCore.Design;

namespace Majordomo.Infrastructure.Persistence;

/// <summary>Factory di design-time per <c>dotnet ef migrations</c>.</summary>
internal sealed class InfraDbContextDesignTimeFactory : IDesignTimeDbContextFactory<InfraDbContext>
{
    public InfraDbContext CreateDbContext(string[] args) =>
        new(PersistenceExtensions.DesignTimeOptions<InfraDbContext>(InfraDbContext.Schema));
}
