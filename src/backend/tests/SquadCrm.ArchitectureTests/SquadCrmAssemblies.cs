using System.Reflection;
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Infrastructure.FileStorage;
using SquadCrm.Modules.ArchitectureFixture;
using SquadCrm.Modules.ArchitectureFixture.Contracts;
using SquadCrm.Modules.Audit;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Bootstrap;
using SquadCrm.Modules.StaffIdentity.Contracts;
using RoleAuthorizationBootstrap = SquadCrm.Tools.RoleManagementBootstrap.BootstrapProgram;

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
    public const string InfrastructureNamespacePrefix = "SquadCrm.Infrastructure.";
    public const string InfrastructurePostgresName = "SquadCrm.Infrastructure.Postgres";
    public const string InfrastructureFileStorageName = "SquadCrm.Infrastructure.FileStorage";

    /// <summary>Assembly-name prefixes of the EF Core and Npgsql package families.</summary>
    public static readonly string[] EfCoreAndNpgsqlPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    /// <summary>Namespace suffix every module's persistence internals live under.</summary>
    public const string PersistenceNamespaceSuffix = ".Persistence";

    public static Assembly Api { get; } = typeof(Program).Assembly;

    public static Assembly BuildingBlocks { get; } = typeof(IModule).Assembly;

    public static Assembly Abstractions { get; } = typeof(IDomainEvent).Assembly;

    public static Assembly InfrastructurePostgres { get; } = typeof(PostgresOptions).Assembly;

    public static Assembly InfrastructureFileStorage { get; } = typeof(FileStorageOptions).Assembly;

    public static Assembly ArchitectureFixture { get; } = typeof(ArchitectureFixtureModule).Assembly;

    public static Assembly ArchitectureFixtureContracts { get; } = typeof(ModuleInfoResponse).Assembly;

    public static Assembly StaffIdentity { get; } = typeof(StaffIdentityModule).Assembly;

    public static Assembly StaffIdentityContracts { get; } = typeof(StaffSubjectReference).Assembly;

    public static Assembly StaffIdentityBootstrap { get; } = typeof(BootstrapProgram).Assembly;

    public static Assembly RoleManagement { get; } = typeof(RoleManagementModule).Assembly;

    public static Assembly RoleManagementBootstrap { get; } = typeof(RoleAuthorizationBootstrap).Assembly;

    public static Assembly Audit { get; } = typeof(AuditModule).Assembly;

    public static Assembly AuditContracts { get; } = typeof(IAuditRecorder).Assembly;

    public static Assembly ApiTests { get; } = typeof(SquadCrm.Api.Tests.HealthEndpointTests).Assembly;

    public static Assembly UnitTests { get; } = typeof(SquadCrm.UnitTests.DomainEventTests).Assembly;

    /// <summary>Every first-party assembly, production and test.</summary>
    public static IReadOnlyList<Assembly> All { get; } =
    [
        Api,
        BuildingBlocks,
        Abstractions,
        InfrastructurePostgres,
        InfrastructureFileStorage,
        ArchitectureFixture,
        ArchitectureFixtureContracts,
        StaffIdentity,
        StaffIdentityContracts,
        StaffIdentityBootstrap,
        RoleManagement,
        RoleManagementBootstrap,
        Audit,
        AuditContracts,
        ApiTests,
        UnitTests,
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
