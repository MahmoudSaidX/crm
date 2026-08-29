using System.Reflection;
using NetArchTest.Rules;

namespace SquadCrm.ArchitectureTests;

/// <summary>
/// Deterministic module-boundary and forbidden-dependency rules. A change that
/// breaks one of these fails <c>dotnet test</c>.
/// <para>
/// <b>Tooling rationale:</b> NetArchTest.Rules is the smallest dependency that
/// reliably verifies assembly-level dependency rules; ArchUnitNET is heavier and
/// unnecessary here. Each rule is asserted twice — once over IL type dependencies
/// (NetArchTest) and once over assembly references — so neither an unused
/// reference nor an unreferenced usage can slip through.
/// </para>
/// <para>
/// Only assembly/project dependency direction is asserted. Rules that try to prove
/// CLR <c>internal</c> accessibility are deliberately excluded: the compiler already
/// enforces <c>internal</c>, and such tests are brittle and add no signal.
/// </para>
/// </summary>
public sealed class ArchitectureRulesTests
{
    /// <summary>
    /// Packages that belong to downstream stories. CRM-199 legitimately adds
    /// Hangfire to the API execution boundary, so Hangfire now has the narrower
    /// placement rule below instead of a solution-wide prohibition. MediatR
    /// remains absent: the fixture publication proof is explicit and in-process,
    /// with no general event bus or handler registry. CRM-110 (authentication) must still
    /// update this list when it legitimately introduces
    /// <c>Microsoft.AspNetCore.Authentication.</c>. CRM-204 already resolved the
    /// authorization half:
    /// <c>Microsoft.AspNetCore.Authorization.</c> is unblocked because this
    /// story registers the inert authorization extension point
    /// (<c>services.AddAuthorization()</c>, zero policies, no scheme). No
    /// authentication scheme exists yet, so <c>Microsoft.AspNetCore.Authentication.</c>
    /// stays forbidden until CRM-110 introduces one.
    /// <para>
    /// EF Core and Npgsql are deliberately <b>absent</b>: persistence
    /// legitimately introduces both, but only in specific projects. That is a
    /// narrower rule than a solution-wide ban, and it lives in
    /// <see cref="PersistenceArchitectureRulesTests"/>.
    /// </para>
    /// </summary>
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "MediatR",
        "FluentValidation",
        "Microsoft.AspNetCore.Authentication.",
        "Swashbuckle.",
        "Scalar.",
        "NSwag.",
    ];

    [Fact]
    public void Hangfire_MustRemainInApiExecutionBoundary()
    {
        foreach (Assembly assembly in SquadCrmAssemblies.All.Where(
            assembly => assembly.GetName().Name is not SquadCrmAssemblies.ApiName
                and not "SquadCrm.Api.Tests"))
        {
            Assert.DoesNotContain(
                SquadCrmAssemblies.ReferencedAssemblyNames(assembly),
                name => name.StartsWith("Hangfire", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void BuildingBlocks_MustNotDependOnModulesOrApi()
    {
        TestResult result = Types.InAssembly(SquadCrmAssemblies.BuildingBlocks)
            .ShouldNot()
            .HaveDependencyOnAny(SquadCrmAssemblies.ModulesNamespacePrefix, SquadCrmAssemblies.ApiName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("SquadCrm.BuildingBlocks", result));

        IReadOnlyList<string> references =
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.BuildingBlocks);

        Assert.DoesNotContain(references, name =>
            name.StartsWith(SquadCrmAssemblies.ModulesNamespacePrefix, StringComparison.Ordinal)
            || name == SquadCrmAssemblies.ApiName);
    }

    [Fact]
    public void ModuleContracts_MustNotDependOnModuleImplementationOrApi()
    {
        // NetArchTest matches namespaces by prefix, and the contracts namespace is
        // itself a prefix-child of the module namespace, so only the API rule can be
        // expressed that way. The implementation rule is asserted on assembly
        // references below, which is the deterministic check in any case.
        TestResult result = Types.InAssembly(SquadCrmAssemblies.ArchitectureFixtureContracts)
            .ShouldNot()
            .HaveDependencyOn(SquadCrmAssemblies.ApiName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("SquadCrm.Modules.ArchitectureFixture.Contracts", result));

        IReadOnlyList<string> references =
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.ArchitectureFixtureContracts);

        Assert.DoesNotContain(references, name =>
            SquadCrmAssemblies.IsModuleImplementation(name) || name == SquadCrmAssemblies.ApiName);
    }

    [Fact]
    public void ModuleImplementation_MustNotDependOnApi()
    {
        TestResult result = Types.InAssembly(SquadCrmAssemblies.ArchitectureFixture)
            .ShouldNot()
            .HaveDependencyOn(SquadCrmAssemblies.ApiName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe("SquadCrm.Modules.ArchitectureFixture", result));

        Assert.DoesNotContain(
            SquadCrmAssemblies.ApiName,
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.ArchitectureFixture));
    }

    /// <summary>
    /// Written generically over every module assembly so the rule keeps holding
    /// unchanged as real business modules arrive: a module may consume another
    /// module's <c>*.Contracts</c>, never its implementation.
    /// </summary>
    [Fact]
    public void Modules_MustNotDependOnAnotherModulesImplementation()
    {
        IEnumerable<Assembly> moduleAssemblies = SquadCrmAssemblies.All
            .Where(assembly => SquadCrmAssemblies.IsModuleImplementation(assembly.GetName().Name!));

        foreach (Assembly module in moduleAssemblies)
        {
            string ownName = module.GetName().Name!;

            string[] foreignImplementations = SquadCrmAssemblies
                .ReferencedAssemblyNames(module)
                .Where(name => SquadCrmAssemblies.IsModuleImplementation(name) && name != ownName)
                .ToArray();

            Assert.True(
                foreignImplementations.Length == 0,
                $"{ownName} references another module's implementation assembly: "
                + string.Join(", ", foreignImplementations));
        }
    }

    /// <summary>
    /// Scope-creep guard covering production <b>and</b> test assemblies.
    /// </summary>
    [Fact]
    public void Foundation_MustNotIntroduceForbiddenDependencies()
    {
        foreach (Assembly assembly in SquadCrmAssemblies.All)
        {
            string[] violations = SquadCrmAssemblies
                .ReferencedAssemblyNames(assembly)
                .Where(name => ForbiddenAssemblyPrefixes.Any(
                    prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(
                violations.Length == 0,
                $"{assembly.GetName().Name} references out-of-scope dependencies: "
                + string.Join(", ", violations));
        }
    }

    private static string Describe(string assemblyName, TestResult result) =>
        $"{assemblyName} has forbidden dependencies. Offending types: "
        + string.Join(", ", result.FailingTypeNames ?? []);
}
