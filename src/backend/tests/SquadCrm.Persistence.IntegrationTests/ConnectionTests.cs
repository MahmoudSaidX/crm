using System.Data;
using Npgsql;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// AC 1 — the backend connects to PostgreSQL using environment-based
/// configuration. Requires a running server; fails, never skips, without one.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class ConnectionTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task Connection_OpensAgainstConfiguredPostgres()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        Assert.Equal(ConnectionState.Open, connection.State);
    }
}
