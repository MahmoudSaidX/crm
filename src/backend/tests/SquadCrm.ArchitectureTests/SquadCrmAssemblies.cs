using System.Reflection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Modules.ArchitectureFixture;
using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.ArchitectureTests;

/// <summary>
/// Resolves the solution's assemblies by referencing one type from each, so a
/// renamed or removed project breaks compilation instead of silently skipping a rule.
/// </summary>
internal static class SquadCrmAssemblies
{
    public const string ApiName = "SquadCrm.Api";
    public const string BuildingBlocksName = "SquadCrm.BuildingBlocks";
    public const string ModulesNamespacePrefix = "SquadCrm.Modules.";
    public const string ContractsSuffix = ".Contracts";

    public static Assembly Api { get; } = typeof(Program).Assembly;

    public static Assembly BuildingBlocks { get; } = typeof(IModule).Assembly;

    public static Assembly ArchitectureFixture { get; } = typeof(ArchitectureFixtureModule).Assembly;

    public static Assembly ArchitectureFixtureContracts { get; } = typeof(ModuleInfoResponse).Assembly;

    public static Assembly ApiTests { get; } = typeof(SquadCrm.Api.Tests.HealthEndpointTests).Assembly;

    /// <summary>Every first-party assembly, production and test.</summary>
    public static IReadOnlyList<Assembly> All { get; } =
    [
        Api,
        BuildingBlocks,
        ArchitectureFixture,
        ArchitectureFixtureContracts,
        ApiTests,
        typeof(SquadCrmAssemblies).Assembly,
    ];

    /// <summary>Names of the assemblies <paramref name="assembly"/> references.</summary>
    public static IReadOnlyList<string> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

    /// <summary>True when <paramref name="name"/> is a module implementation assembly (not its contracts).</summary>
    public static bool IsModuleImplementation(string name) =>
        name.StartsWith(ModulesNamespacePrefix, StringComparison.Ordinal)
        && !name.EndsWith(ContractsSuffix, StringComparison.Ordinal);
}
