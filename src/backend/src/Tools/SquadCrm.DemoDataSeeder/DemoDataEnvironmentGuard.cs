namespace SquadCrm.Tools.DemoDataSeeder;

/// <summary>
/// Code-enforced environment gate for the demo seeder. Production refusal is a
/// rule, not documentation: the guard runs before any connection string is read
/// and there is no configuration value, environment variable or command-line
/// switch that can turn it off.
/// </summary>
public static class DemoDataEnvironmentGuard
{
    public const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";

    /// <summary>The only environments demo data may be written to.</summary>
    private static readonly string[] Allowed = ["Development", "Test"];

    /// <summary>Throws unless <paramref name="environment"/> is exactly one of the allowed values.</summary>
    public static void EnsureNonProduction(string? environment)
    {
        // An allowlist, not a "not Production" check: an unset, empty or unknown
        // environment must also refuse, because a seeder that guesses is a
        // seeder that eventually guesses "Production".
        if (Allowed.Contains(environment, StringComparer.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Demo data seeding is allowed only when {EnvironmentVariable} is "
            + $"{string.Join(" or ", Allowed)}. Current value: "
            + $"'{environment ?? "(not set)"}'. Refusing to run.");
    }
}
