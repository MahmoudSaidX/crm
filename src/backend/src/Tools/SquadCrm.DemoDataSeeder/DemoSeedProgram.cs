using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.BranchManagement.DemoData;
using SquadCrm.Modules.CustomerManagement.DemoData;
using SquadCrm.Modules.DepartmentManagement.DemoData;
using SquadCrm.Modules.RoleManagement.DemoData;
using SquadCrm.Modules.StaffIdentity.DemoData;
using SquadCrm.Modules.TicketManagement.DemoData;

namespace SquadCrm.Tools.DemoDataSeeder;

/// <summary>
/// Development demo-data seeder. Explicitly invoked (<c>scripts/seed-demo</c>)
/// — nothing in the application host references it, so it cannot run at startup
/// or after a migration.
/// <para>
/// The tool only orchestrates each module's own contributor in dependency order.
/// It owns no entity, no DbContext and no business rule; a module that needs
/// demo data implements <see cref="IDemoDataContributor"/> in its own assembly
/// and is added to the ordered list below.
/// </para>
/// </summary>
public static class DemoSeedProgram
{
    /// <summary>
    /// Fixed generator seed, so a run against a clean database always produces
    /// the same dataset.
    /// </summary>
    public const int RandomSeed = 20260911;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
            {
                Console.WriteLine(Usage);
                return 0;
            }

            DemoDataEnvironmentGuard.EnsureNonProduction(
                Environment.GetEnvironmentVariable(DemoDataEnvironmentGuard.EnvironmentVariable));

            DemoDataSize size = ParseSize(args);
            DemoDataScope scope = new(size, RandomSeed, DateTimeOffset.UtcNow, new DemoDataReferences());

            Console.WriteLine("DEVELOPMENT DEMO DATA — never run this against production.");
            Console.WriteLine($"Dataset size: {size.ToString().ToLowerInvariant()}");
            Console.WriteLine(
                $"Target: {CustomerDemoDataContributor.CountFor(size)} customers, "
                + $"{TicketDemoDataContributor.CountFor(size)} tickets.");
            Console.WriteLine();

            // Dependency order, listed explicitly: staff before roles (roles are
            // assigned to staff subjects), organizational catalogs before
            // customers, catalogs and customers before tickets.
            IDemoDataContributor[] contributors =
            [
                new StaffIdentityDemoDataContributor(),
                new RoleManagementDemoDataContributor(),
                new DepartmentDemoDataContributor(),
                new BranchDemoDataContributor(),
                new TicketCatalogDemoDataContributor(),
                new CustomerDemoDataContributor(),
                new TicketDemoDataContributor(),
            ];

            foreach (IDemoDataContributor contributor in contributors)
            {
                DemoDataOutcome outcome = await contributor.SeedAsync(scope, cancellationToken);
                Console.WriteLine($"{outcome.Contributor}:");
                foreach (KeyValuePair<string, int> entry in outcome.Created)
                {
                    Console.WriteLine($"  {entry.Key}: {entry.Value}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Demo data is ready. Sign in with admin@squadcrm.local (see README).");
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private const string Usage = """
        Squad CRM development demo data seeder.

        Usage: scripts/seed-demo [--size small|medium|large]

          --size   Dataset size. Default: medium.
                   small  ~20 customers / ~50 tickets
                   medium ~150 customers / ~400 tickets
                   large  ~1000 customers / ~5000 tickets

        Runs only when ASPNETCORE_ENVIRONMENT is Development or Test.
        Idempotent: running it twice does not duplicate data, and it never
        deletes anything. The demo password may be supplied through
        SQUADCRM_DEMO_PASSWORD.
        """;

    private static DemoDataSize ParseSize(string[] args)
    {
        int position = Array.FindIndex(args, argument => string.Equals(argument, "--size", StringComparison.Ordinal));
        if (position < 0)
        {
            return DemoDataSize.Medium;
        }

        if (position + 1 >= args.Length)
        {
            throw new InvalidOperationException("--size requires a value: small, medium or large.");
        }

        return args[position + 1].ToLowerInvariant() switch
        {
            "small" => DemoDataSize.Small,
            "medium" => DemoDataSize.Medium,
            "large" => DemoDataSize.Large,
            _ => throw new InvalidOperationException(
                $"Unknown dataset size '{args[position + 1]}'. Use small, medium or large."),
        };
    }
}
