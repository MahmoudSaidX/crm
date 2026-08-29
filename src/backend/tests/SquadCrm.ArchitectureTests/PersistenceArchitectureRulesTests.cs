using System.Reflection;
using NetArchTest.Rules;

namespace SquadCrm.ArchitectureTests;

/// <summary>
/// Persistence-boundary rules: where EF Core and Npgsql are allowed to appear,
/// and where a <c>DbContext</c> is allowed to live.
/// <para>
/// <b>These rules are purely static.</b> They assert over assembly references
/// and over IL type dependencies (NetArchTest), and they match
/// <c>DbContext</c> by full base-type name via reflection — so this project needs
/// no EF Core package reference of its own, constructs no context and invokes no
/// design-time factory.
/// </para>
/// <para>
/// <b>What they cannot prove.</b> Structure is all they see: assembly and
/// namespace dependency direction, and context placement. They say nothing about
/// which SQL actually runs, and they deliberately do not inspect the EF model.
/// The default schema, real table placement and the emptiness of the PostgreSQL
/// <c>public</c> schema are proven by
/// <c>SquadCrm.Persistence.IntegrationTests</c> against a real server. A module
/// issuing raw SQL against another module's schema remains a <b>coding
/// convention</b> until per-module database roles are deliberately introduced —
/// explicitly not CRM-106's scope.
/// </para>
/// </summary>
public sealed class PersistenceArchitectureRulesTests
{
    private const string DbContextFullName = "Microsoft.EntityFrameworkCore.DbContext";

    /// <summary>
    /// A contract is the cross-module vocabulary. A provider or ORM type leaking
    /// into it would make every consumer depend on this module's storage choice.
    /// </summary>
    [Fact]
    public void ModuleContracts_MustNotDependOnEfCoreOrNpgsql()
    {
        Assembly[] contracts = SquadCrmAssemblies.All
            .Where(assembly => assembly.GetName().Name!
                .EndsWith(SquadCrmAssemblies.ContractsSuffix, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(contracts);

        foreach (Assembly assembly in contracts)
        {
            AssertReferencesNoEfCoreOrNpgsql(assembly);
        }
    }

    /// <summary>
    /// <b>The rule that keeps BuildingBlocks provider-neutral.</b> CLAUDE.md
    /// requires external providers to stay behind provider-neutral ports;
    /// PostgreSQL is one such provider. "Just one little Npgsql helper" in
    /// BuildingBlocks must fail here rather than be discovered later, when every
    /// module already depends on it.
    /// <para>
    /// This whole-assembly rule already covers CRM-204's new <c>Http/</c> and
    /// <c>Security/</c> folders — they live in the same <c>SquadCrm.BuildingBlocks</c>
    /// assembly — so CRM-204 deliberately adds no duplicate rule for them.
    /// </para>
    /// </summary>
    [Fact]
    public void BuildingBlocks_MustNotDependOnEfCoreOrNpgsql()
    {
        AssertReferencesNoEfCoreOrNpgsql(SquadCrmAssemblies.BuildingBlocks);

        TestResult result = Types.InAssembly(SquadCrmAssemblies.BuildingBlocks)
            .ShouldNot()
            .HaveDependencyOnAny(SquadCrmAssemblies.EfCoreAndNpgsqlPrefixes)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(SquadCrmAssemblies.BuildingBlocksName, result));
    }

    /// <summary>
    /// BuildingBlocks is the innermost layer: it may not reach into a module's
    /// internals nor into a provider-specific infrastructure adapter.
    /// </summary>
    [Fact]
    public void BuildingBlocks_MustNotDependOnModulePersistenceOrInfrastructure()
    {
        TestResult result = Types.InAssembly(SquadCrmAssemblies.BuildingBlocks)
            .ShouldNot()
            .HaveDependencyOnAny(
                SquadCrmAssemblies.ModulesNamespacePrefix,
                SquadCrmAssemblies.InfrastructureNamespacePrefix)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(SquadCrmAssemblies.BuildingBlocksName, result));

        IReadOnlyList<string> references =
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.BuildingBlocks);

        Assert.DoesNotContain(references, name =>
            name.StartsWith(SquadCrmAssemblies.ModulesNamespacePrefix, StringComparison.Ordinal)
            || name.StartsWith(SquadCrmAssemblies.InfrastructureNamespacePrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The PostgreSQL adapter is a leaf: it assembles a connection string from the
    /// <c>POSTGRES_*</c> contract and nothing more. It holds no DbContext, no
    /// entity and no migration, so EF Core has no business being there; and it
    /// must not depend on a module or on the host, since both depend on it.
    /// </summary>
    [Fact]
    public void InfrastructurePostgres_MustNotDependOnEfCoreModulesOrApi()
    {
        IReadOnlyList<string> references =
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.InfrastructurePostgres);

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(references, name =>
            name.StartsWith(SquadCrmAssemblies.ModulesNamespacePrefix, StringComparison.Ordinal)
            || name == SquadCrmAssemblies.ApiName);

        TestResult result = Types.InAssembly(SquadCrmAssemblies.InfrastructurePostgres)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                SquadCrmAssemblies.ModulesNamespacePrefix,
                SquadCrmAssemblies.ApiName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(SquadCrmAssemblies.InfrastructurePostgresName, result));
    }

    [Fact]
    public void InfrastructureFileStorage_MustNotDependOnEfCoreNpgsqlModulesOrApi()
    {
        Assembly assembly = SquadCrmAssemblies.InfrastructureFileStorage;
        IReadOnlyList<string> references = SquadCrmAssemblies.ReferencedAssemblyNames(assembly);

        Assert.DoesNotContain(references, name =>
            SquadCrmAssemblies.EfCoreAndNpgsqlPrefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || name.StartsWith(SquadCrmAssemblies.ModulesNamespacePrefix, StringComparison.Ordinal)
            || name == SquadCrmAssemblies.ApiName);

        TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                SquadCrmAssemblies.ModulesNamespacePrefix,
                SquadCrmAssemblies.ApiName)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(SquadCrmAssemblies.InfrastructureFileStorageName, result));
    }

    /// <summary>
    /// The host composes modules; it never touches their EF internals. It still
    /// <i>references</i> the module assembly — that is the composition seam — so
    /// the rule is expressed at namespace level, not assembly level. The host also
    /// carries no EF Core package of its own.
    /// </summary>
    [Fact]
    public void Api_MustNotDependOnModulePersistenceInternals()
    {
        IReadOnlyList<string> references =
            SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.Api);

        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));

        string[] offenders = PersistenceNamespaceDependencies(
            SquadCrmAssemblies.Api,
            ModulePersistenceNamespaces()).ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{SquadCrmAssemblies.ApiName} depends on module persistence internals: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// Written generically over every module implementation assembly, so it keeps
    /// holding unchanged as real business modules arrive: a module's persistence
    /// namespace is private to that module.
    /// </summary>
    [Fact]
    public void Modules_MustNotDependOnAnotherModulesPersistenceNamespace()
    {
        Assembly[] modules = SquadCrmAssemblies.All
            .Where(assembly => SquadCrmAssemblies.IsModuleImplementation(assembly.GetName().Name!))
            .ToArray();

        Assert.NotEmpty(modules);

        foreach (Assembly module in modules)
        {
            string ownPersistenceNamespace =
                module.GetName().Name! + SquadCrmAssemblies.PersistenceNamespaceSuffix;

            // A module depending on its OWN persistence namespace is the pattern,
            // not a violation — only every other module's is forbidden.
            string[] foreign = ModulePersistenceNamespaces()
                .Where(candidate => candidate != ownPersistenceNamespace)
                .ToArray();

            string[] offenders = PersistenceNamespaceDependencies(module, foreign).ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{module.GetName().Name} depends on another module's persistence namespace: "
                + string.Join(", ", offenders));
        }
    }

    /// <summary>
    /// One DbContext per module, inside its owning module. This is the rule that
    /// fails if anyone reintroduces a central <c>SquadCrmDbContext</c>, or parks a
    /// context in the host, in BuildingBlocks or in the infrastructure adapter.
    /// </summary>
    [Fact]
    public void EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace()
    {
        List<string> offenders = [];
        int contextCount = 0;

        foreach (Assembly assembly in SquadCrmAssemblies.All)
        {
            string assemblyName = assembly.GetName().Name!;

            foreach (Type type in LoadableTypes(assembly).Where(IsDbContext))
            {
                contextCount++;

                string expectedNamespace = assemblyName + SquadCrmAssemblies.PersistenceNamespaceSuffix;

                if (!SquadCrmAssemblies.IsModuleImplementation(assemblyName))
                {
                    offenders.Add(
                        $"{type.FullName} lives in '{assemblyName}', which is not a module "
                        + "implementation assembly; a DbContext belongs to exactly one module.");
                    continue;
                }

                if (type.Namespace != expectedNamespace)
                {
                    offenders.Add(
                        $"{type.FullName} must live in namespace '{expectedNamespace}' "
                        + $"but lives in '{type.Namespace}'.");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(" ", offenders));

        // A vacuous pass would make this rule worthless: the fixture module owns one.
        Assert.True(contextCount > 0, "No DbContext was found in any first-party assembly.");
    }

    /// <summary>
    /// Every module's persistence namespace, discovered rather than hard-coded, so
    /// a new module is covered the moment it joins <see cref="SquadCrmAssemblies"/>.
    /// </summary>
    private static string[] ModulePersistenceNamespaces() =>
        SquadCrmAssemblies.All
            .Select(candidate => candidate.GetName().Name!)
            .Where(SquadCrmAssemblies.IsModuleImplementation)
            .Select(name => name + SquadCrmAssemblies.PersistenceNamespaceSuffix)
            .ToArray();

    /// <summary>Names the types in <paramref name="assembly"/> that reach any of <paramref name="forbiddenNamespaces"/>.</summary>
    private static IEnumerable<string> PersistenceNamespaceDependencies(
        Assembly assembly,
        string[] forbiddenNamespaces)
    {
        if (forbiddenNamespaces.Length == 0)
        {
            return [];
        }

        TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        return result.IsSuccessful ? [] : result.FailingTypeNames ?? [];
    }

    /// <summary>
    /// Matched by full base-type <b>name</b>, walking the base chain, so this
    /// project needs no EF Core reference to recognise a context.
    /// </summary>
    private static bool IsDbContext(Type type)
    {
        if (type.IsAbstract || !type.IsClass)
        {
            return false;
        }

        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.FullName == DbContextFullName)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // A partially loadable assembly must not silently pass the rule.
            return exception.Types.OfType<Type>();
        }
    }

    private static string Describe(string assemblyName, TestResult result) =>
        $"{assemblyName} has forbidden persistence dependencies. Offending types: "
        + string.Join(", ", result.FailingTypeNames ?? []);

    /// <summary>
    /// CRM-198, Ruling 2: SquadCrm.BuildingBlocks.Abstractions is deliberately
    /// dependency-free so *.Contracts assemblies can reference it without
    /// inheriting ASP.NET Core or infrastructure. A stray package/project/
    /// framework reference here would silently reintroduce that coupling for
    /// every future module.
    /// </summary>
    [Fact]
    public void Abstractions_MustHaveNoDependencies()
    {
        IReadOnlyList<string> referenced = SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.Abstractions);

        // Exact-name/prefix checks against first-party and framework names
        // only — the .NET runtime/BCL assemblies referenced by every project
        // (System.*, netstandard, mscorlib) are expected and not a violation.
        string[] violations = referenced
            .Where(name =>
                name.StartsWith("SquadCrm.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || SquadCrmAssemblies.EfCoreAndNpgsqlPrefixes.Any(
                    prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{SquadCrmAssemblies.Abstractions.GetName().Name} must have no project/framework/package "
            + "dependencies. Offending references: " + string.Join(", ", violations));
    }

    /// <summary>
    /// CRM-198, Ruling 2: a module's *.Contracts project may implement
    /// IIntegrationEvent, but must never reference the ASP.NET-Core-bearing
    /// SquadCrm.BuildingBlocks — only SquadCrm.BuildingBlocks.Abstractions.
    /// Exact-name equality is used deliberately: a substring/prefix check
    /// would false-positive on "SquadCrm.BuildingBlocks.Abstractions" itself
    /// containing "SquadCrm.BuildingBlocks" as a string prefix.
    /// </summary>
    [Fact]
    public void ContractsAssemblies_MustNotDependOnBuildingBlocks()
    {
        Assembly[] contracts = SquadCrmAssemblies.All
            .Where(assembly => assembly.GetName().Name!
                .EndsWith(SquadCrmAssemblies.ContractsSuffix, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(contracts);

        foreach (Assembly assembly in contracts)
        {
            IReadOnlyList<string> referenced = SquadCrmAssemblies.ReferencedAssemblyNames(assembly);

            Assert.DoesNotContain(SquadCrmAssemblies.BuildingBlocksName, referenced);
        }
    }

    private static void AssertReferencesNoEfCoreOrNpgsql(Assembly assembly)
    {
        string[] violations = SquadCrmAssemblies.ReferencedAssemblyNames(assembly)
            .Where(name => SquadCrmAssemblies.EfCoreAndNpgsqlPrefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{assembly.GetName().Name} references EF Core or Npgsql: " + string.Join(", ", violations));
    }
}
