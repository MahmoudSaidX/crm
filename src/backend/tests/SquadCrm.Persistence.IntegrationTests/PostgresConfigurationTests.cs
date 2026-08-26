using Microsoft.Extensions.Configuration;
using Npgsql;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Unit tests for the single PostgreSQL configuration implementation.
/// <b>No server is contacted</b> — these run with the database down.
/// </summary>
public sealed class PostgresConfigurationTests
{
    /// <summary>
    /// A value that must never appear in a message, log line or test output.
    /// Mirrors the sentinel style of <c>SquadCrmApiFactory.SentinelMessage</c>.
    /// </summary>
    private const string SentinelPassword = "SENTINEL-POSTGRES-PASSWORD";

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

    private static (string Key, string? Value)[] Complete() =>
    [
        (PostgresOptions.HostKey, "localhost"),
        (PostgresOptions.PortKey, "5432"),
        (PostgresOptions.DatabaseKey, "squadcrm"),
        (PostgresOptions.UsernameKey, "squadcrm"),
        (PostgresOptions.PasswordKey, SentinelPassword),
    ];

    [Fact]
    public void MissingKeys_AreAllReportedInOneException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Configuration().ReadPostgresOptions());

        // One exception naming EVERY offending key, so a developer fixes the
        // configuration in one pass instead of one key per run.
        Assert.Contains(PostgresOptions.HostKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(PostgresOptions.PortKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(PostgresOptions.DatabaseKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(PostgresOptions.UsernameKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(PostgresOptions.PasswordKey, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    public void InvalidPort_IsRejectedNamingTheKeyButNotTheValue(string port)
    {
        (string Key, string? Value)[] values = Complete();
        values[1] = (PostgresOptions.PortKey, port);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Configuration(values).ReadPostgresOptions());

        Assert.Contains(PostgresOptions.PortKey, exception.Message, StringComparison.Ordinal);

        // Operator input may carry anything; it is never echoed back.
        Assert.DoesNotContain(port, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteConfiguration_IsRead()
    {
        PostgresOptions options = Configuration(Complete()).ReadPostgresOptions();

        Assert.Equal("localhost", options.Host);
        Assert.Equal(5432, options.Port);
        Assert.Equal("squadcrm", options.Database);
        Assert.Equal("squadcrm", options.Username);
        Assert.Equal(SentinelPassword, options.Password);
    }

    [Fact]
    public void PasswordWithSeparators_RoundTripsThroughTheBuilder()
    {
        const string AwkwardPassword = "p;a=s'w\"o rd";

        (string Key, string? Value)[] values = Complete();
        values[4] = (PostgresOptions.PasswordKey, AwkwardPassword);

        string connectionString = Configuration(values).ReadPostgresOptions().BuildConnectionString();

        // Escaped by NpgsqlConnectionStringBuilder, never by concatenation.
        NpgsqlConnectionStringBuilder parsed = new(connectionString);
        Assert.Equal(AwkwardPassword, parsed.Password);
        Assert.Equal("squadcrm", parsed.Database);
        Assert.Equal(5432, parsed.Port);
    }

    [Fact]
    public void Describe_NeverContainsThePassword()
    {
        PostgresOptions options = Configuration(Complete()).ReadPostgresOptions();

        string description = options.Describe();

        Assert.DoesNotContain(SentinelPassword, description, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Host=localhost", description, StringComparison.Ordinal);
        Assert.Contains("Username=squadcrm", description, StringComparison.Ordinal);
    }

    [Fact]
    public void GetConnectionString_FailsWhenTheCompositionRootDidNotDeriveIt()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Configuration().GetSquadCrmPostgresConnectionString());

        Assert.Contains(PostgresOptions.ConnectionStringName, exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddSquadCrmPostgres", exception.Message, StringComparison.Ordinal);
    }
}
