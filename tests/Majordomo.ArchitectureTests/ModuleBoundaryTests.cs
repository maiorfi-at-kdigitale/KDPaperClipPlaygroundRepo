using System.Reflection;
using Shouldly;
using Xunit;

namespace Majordomo.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly string[] Modules = ["Strategy", "Risk", "Execution", "Portfolio", "MarketData", "Backtesting", "Notifications"];

    public static TheoryData<string> ModuleNames() => [.. Modules];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Un_modulo_referenzia_gli_altri_moduli_solo_tramite_Contracts(string module)
    {
        var assembly = Assembly.Load($"Majordomo.{module}");
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => Modules.Any(m => m != module && name == $"Majordomo.{m}"))
            .ToList();
        forbidden.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void I_Contracts_non_dipendono_da_infrastruttura(string module)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load($"Majordomo.{module}.Contracts");
        }
        catch (FileNotFoundException)
        {
            return; // modulo senza API pubblica (es. Notifications)
        }

        assembly.GetReferencedAssemblies().Select(a => a.Name!)
            .Where(n => n.StartsWith("Majordomo.", StringComparison.Ordinal) && !n.EndsWith(".Contracts", StringComparison.Ordinal))
            .ShouldBeEmpty();
        assembly.GetReferencedAssemblies().Select(a => a.Name!)
            .ShouldNotContain(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
