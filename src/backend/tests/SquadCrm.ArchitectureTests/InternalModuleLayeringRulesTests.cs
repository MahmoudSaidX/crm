using System.Reflection;
using NetArchTest.Rules;

namespace SquadCrm.ArchitectureTests;

/// <summary>
/// Internal module layering (ADR-012). The module remains the primary
/// boundary; these rules police the layers <b>inside</b> one module:
/// <c>Domain</c>, <c>Application</c>, <c>Presentation</c>, <c>Infrastructure</c>.
/// <para>
/// Only directions that would actually cost something are asserted. Domain is
/// kept free of HTTP, EF Core and background-processing technology so business
/// rules stay testable without a host or a database, and Presentation is kept
/// off the module's <c>DbContext</c> so an endpoint cannot quietly become a
/// second place where persistence decisions live. Application → Presentation is
/// deliberately <b>not</b> forbidden: request/response records are the
/// module's HTTP vocabulary and its services take them directly, which is the
/// established pattern here and not worth a parallel set of mapper types.
/// </para>
/// <para>
/// Layer membership is read from namespaces, which is exactly how the folders
/// are laid out — see <c>CLAUDE.md</c>, "Backend Module Internal Architecture".
/// Modules that own no code in a layer simply contribute no types to that rule.
/// </para>
/// </summary>
public sealed class InternalModuleLayeringRulesTests
{
    private const string DbContextFullName = "Microsoft.EntityFrameworkCore.DbContext";

    private const string DomainSuffix = ".Domain";
    private const string ApplicationSuffix = ".Application";
    private const string PresentationSuffix = ".Presentation";
    private const string InfrastructureSuffix = ".Infrastructure";

    /// <summary>
    /// Technology families a domain type may never reach for. Hangfire is
    /// included because a domain rule that schedules a job has stopped being a
    /// domain rule; ASP.NET Core because an entity that knows about HTTP cannot
    /// be reused by a background workflow.
    /// </summary>
    private static readonly string[] ForbiddenDomainTechnologies =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Hangfire",
        "Microsoft.AspNetCore",
    ];

    public static TheoryData<string> ModuleAssemblyNames
    {
        get
        {
            TheoryData<string> data = [];
            foreach (Assembly assembly in ModuleAssemblies())
            {
                data.Add(assembly.GetName().Name!);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblyNames))]
    public void Domain_MustNotDependOnPresentation(string assemblyName)
    {
        AssertNoDependency(assemblyName, DomainSuffix, [assemblyName + PresentationSuffix]);
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblyNames))]
    public void Domain_MustNotDependOnApplicationOrInfrastructure(string assemblyName)
    {
        AssertNoDependency(
            assemblyName,
            DomainSuffix,
            [assemblyName + ApplicationSuffix, assemblyName + InfrastructureSuffix]);
    }

    /// <summary>
    /// The rule that gives the Domain layer its value: entities, domain events
    /// and policies must not be written against the ORM, the database driver,
    /// the scheduler or the web framework.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleAssemblyNames))]
    public void Domain_MustNotDependOnPersistenceOrHostingTechnology(string assemblyName)
    {
        AssertNoDependency(assemblyName, DomainSuffix, ForbiddenDomainTechnologies);
    }

    /// <summary>
    /// Presentation maps HTTP to an application service and back. A
    /// <c>DbContext</c> reached from an endpoint is a query that no test, audit
    /// rule or transaction boundary in the Application layer can see.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleAssemblyNames))]
    public void Presentation_MustNotDependOnAnyDbContext(string assemblyName)
    {
        Assembly assembly = ModuleAssemblies().Single(a => a.GetName().Name == assemblyName);

        string[] contexts = SquadCrmAssemblies.All
            .SelectMany(LoadableTypes)
            .Where(IsDbContext)
            .Select(type => type.FullName!)
            .ToArray();

        Assert.NotEmpty(contexts);

        string[] offenders = Dependencies(assembly, assemblyName + PresentationSuffix, contexts);

        Assert.True(
            offenders.Length == 0,
            $"{assemblyName}: Presentation types reach a DbContext directly: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// <c>&lt;Module&gt;Module.cs</c> is a composition root, not a controller:
    /// it registers services and delegates route mapping. Endpoint handler
    /// bodies drifting back into it is the regression this catches — a module
    /// root type that both registers a <c>DbContext</c> and produces an
    /// <c>IResult</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleAssemblyNames))]
    public void ModuleCompositionRoot_MustNotDeclareHttpHandlers(string assemblyName)
    {
        Assembly assembly = ModuleAssemblies().Single(a => a.GetName().Name == assemblyName);

        string[] offenders = LoadableTypes(assembly)
            .Where(type => type.Namespace == assemblyName && type.Name.EndsWith("Module", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance
                | BindingFlags.DeclaredOnly))
            .Where(method => IsHttpResult(method.ReturnType))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{assemblyName}: endpoint handlers belong in Presentation/Endpoints, not in the module "
            + "composition root: " + string.Join(", ", offenders));
    }

    private static bool IsHttpResult(Type returnType)
    {
        Type type = returnType;
        if (type.IsGenericType
            && type.GetGenericTypeDefinition().FullName is "System.Threading.Tasks.Task`1"
                or "System.Threading.Tasks.ValueTask`1")
        {
            type = type.GetGenericArguments()[0];
        }

        return type.FullName == "Microsoft.AspNetCore.Http.IResult";
    }

    private static void AssertNoDependency(string assemblyName, string layerSuffix, string[] forbidden)
    {
        Assembly assembly = ModuleAssemblies().Single(a => a.GetName().Name == assemblyName);
        string[] offenders = Dependencies(assembly, assemblyName + layerSuffix, forbidden);

        Assert.True(
            offenders.Length == 0,
            $"{assemblyName}{layerSuffix} must not depend on {string.Join(" / ", forbidden)}. "
            + "Offending types: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Returns the types under <paramref name="layerNamespace"/> that reach any
    /// of <paramref name="forbidden"/>, or nothing when the module owns no code
    /// in that layer (NetArchTest treats an empty selection as a failure, which
    /// would otherwise punish a module for the layers it legitimately lacks).
    /// </summary>
    private static string[] Dependencies(Assembly assembly, string layerNamespace, string[] forbidden)
    {
        bool layerHasTypes = LoadableTypes(assembly).Any(type =>
            type.Namespace is not null
            && (type.Namespace == layerNamespace
                || type.Namespace.StartsWith(layerNamespace + ".", StringComparison.Ordinal)));

        if (!layerHasTypes || forbidden.Length == 0)
        {
            return [];
        }

        TestResult result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith(layerNamespace)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        return result.IsSuccessful ? [] : (result.FailingTypeNames ?? []).ToArray();
    }

    private static IEnumerable<Assembly> ModuleAssemblies() =>
        SquadCrmAssemblies.All
            .Where(assembly => SquadCrmAssemblies.IsModuleImplementation(assembly.GetName().Name!))
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal);

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
            return exception.Types.OfType<Type>();
        }
    }
}
