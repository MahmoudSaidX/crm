using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace SquadCrm.Infrastructure.Postgres;

/// <summary>
/// The <b>single</b> implementation that reads, validates and renders the
/// PostgreSQL coordinates. Both the application composition root and every
/// module's design-time <c>IDesignTimeDbContextFactory</c> call these methods,
/// so runtime and <c>dotnet ef</c> can never disagree about what is required,
/// how the port is parsed, or how the connection string is assembled.
/// <para>
/// Nothing else in the solution may read <c>POSTGRES_*</c> or build a
/// connection string. This type never logs, throws or returns the password or
/// the assembled connection string — use <see cref="Describe"/> for diagnostics.
/// </para>
/// </summary>
public static class PostgresConfiguration
{
    /// <summary>
    /// Reads and validates the five <c>POSTGRES_*</c> keys, failing fast with a
    /// single exception that names <b>every</b> offending key. Values are never
    /// echoed: a key name is safe to print, a value is not.
    /// </summary>
    /// <exception cref="InvalidOperationException">A key is missing, empty or invalid.</exception>
    public static PostgresOptions ReadPostgresOptions(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<string> problems = [];

        string host = Required(configuration, PostgresOptions.HostKey, problems);
        string database = Required(configuration, PostgresOptions.DatabaseKey, problems);
        string username = Required(configuration, PostgresOptions.UsernameKey, problems);
        string password = Required(configuration, PostgresOptions.PasswordKey, problems);
        string rawPort = Required(configuration, PostgresOptions.PortKey, problems);

        int port = 0;

        if (!string.IsNullOrWhiteSpace(rawPort)
            && (!int.TryParse(rawPort, out port)
                || port < PostgresOptions.MinimumPort
                || port > PostgresOptions.MaximumPort))
        {
            // The offending value is deliberately not included — it is operator
            // input and may carry anything.
            problems.Add(
                $"Configuration key '{PostgresOptions.PortKey}' must be an integer between "
                + $"{PostgresOptions.MinimumPort} and {PostgresOptions.MaximumPort}.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "PostgreSQL configuration is incomplete or invalid. Set the POSTGRES_* values "
                + "from 'env/backend.env' (see the repository README) before starting the "
                + "application or running 'dotnet ef'. Problems: "
                + string.Join(" ", problems));
        }

        return new PostgresOptions(host, port, database, username, password);
    }

    /// <summary>
    /// Assembles the Npgsql connection string. Every part goes through
    /// <see cref="NpgsqlConnectionStringBuilder"/>, so a password or host
    /// containing <c>;</c>, <c>=</c> or a quote is escaped rather than breaking
    /// (or silently altering) the connection string.
    /// </summary>
    public static string BuildConnectionString(this PostgresOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // No pooling/timeout tuning: the Npgsql defaults are correct for CRM-106.
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.Username,
            Password = options.Password,
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// The <b>only</b> permitted rendering of the coordinates for logs, exception
    /// messages and test output. The password is omitted, never masked-in-place,
    /// so it cannot leak through a formatting mistake.
    /// </summary>
    public static string Describe(this PostgresOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return $"Host={options.Host};Port={options.Port};Database={options.Database};"
            + $"Username={options.Username}";
    }

    /// <summary>
    /// Composition root only. Reads the operator contract once, registers the
    /// resulting <see cref="PostgresOptions"/>, and publishes the derived
    /// connection string internally as
    /// <c>ConnectionStrings:SquadCrmPostgres</c> so that every module can obtain
    /// it from the <see cref="IConfiguration"/> it already receives — without a
    /// provider reference and without a second operator-facing setting.
    /// </summary>
    public static TBuilder AddSquadCrmPostgres<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        PostgresOptions options = builder.Configuration.ReadPostgresOptions();

        // Added last, so it wins over anything an environment or file source
        // might coincidentally define under the same internal name.
        builder.Configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(
                $"ConnectionStrings:{PostgresOptions.ConnectionStringName}",
                options.BuildConnectionString()),
        ]);

        builder.Services.AddSingleton(options);

        return builder;
    }

    /// <summary>
    /// Module-facing accessor for the connection string derived by
    /// <see cref="AddSquadCrmPostgres{TBuilder}"/>. Modules call this instead of
    /// hard-coding the key name or re-reading <c>POSTGRES_*</c> themselves.
    /// </summary>
    /// <exception cref="InvalidOperationException">The composition root did not run the derivation.</exception>
    public static string GetSquadCrmPostgresConnectionString(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString =
            configuration.GetConnectionString(PostgresOptions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{PostgresOptions.ConnectionStringName}' has not been derived. "
                + $"Call {nameof(AddSquadCrmPostgres)} in the composition root before registering "
                + "modules.");
        }

        return connectionString;
    }

    private static string Required(IConfiguration configuration, string key, List<string> problems)
    {
        string? value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"Configuration key '{key}' is missing or empty.");
            return string.Empty;
        }

        return value;
    }
}
