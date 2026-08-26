using Npgsql;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// AC 3 and AC 5 — the module owns its schema, its table and its migration
/// history, and nothing leaked into the PostgreSQL <c>public</c> schema.
/// <para>
/// These are the claims <c>SquadCrm.ArchitectureTests</c> deliberately cannot
/// make: they are real database state, read back from
/// <c>information_schema</c> rather than inferred from the EF model.
/// </para>
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class SchemaOwnershipTests(PostgresTestDatabase database)
{
    private const string ModuleSchema = "architecture_fixture";
    private const string ProbeTable = "persistence_probe";
    private const string HistoryTable = "__ef_migrations_history";

    [Fact]
    public async Task Schema_ExistsForTheOwningModule()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        await using NpgsqlCommand command = new(
            "select count(*) from information_schema.schemata where schema_name = @schema",
            connection);
        command.Parameters.AddWithValue("schema", ModuleSchema);

        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Table_ExistsInModuleSchema()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        Assert.True(await TableExistsAsync(connection, ModuleSchema, ProbeTable));
    }

    [Fact]
    public async Task Columns_UseSnakeCaseNames()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        await using NpgsqlCommand command = new(
            """
            select column_name
            from information_schema.columns
            where table_schema = @schema and table_name = @table
            order by column_name
            """,
            connection);
        command.Parameters.AddWithValue("schema", ModuleSchema);
        command.Parameters.AddWithValue("table", ProbeTable);

        List<string> columns = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        Assert.Equal(["id", "label", "recorded_at_utc"], columns);
    }

    [Fact]
    public async Task MigrationHistory_LivesInModuleSchema()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        Assert.True(await TableExistsAsync(connection, ModuleSchema, HistoryTable));

        await using NpgsqlCommand rows = new(
            $"select count(*) from {ModuleSchema}.{HistoryTable}",
            connection);

        Assert.True(Convert.ToInt64(await rows.ExecuteScalarAsync()) >= 1);
    }

    /// <summary>
    /// The test that catches a module forgetting
    /// <c>MigrationsHistoryTable(...)</c> or <c>HasDefaultSchema(...)</c>: either
    /// mistake lands objects in <c>public</c>, where two modules would collide.
    /// </summary>
    [Fact]
    public async Task PublicSchema_HoldsNoSquadCrmTables()
    {
        await using NpgsqlConnection connection = await database.OpenConnectionAsync();

        await using NpgsqlCommand command = new(
            "select table_name from information_schema.tables where table_schema = 'public'",
            connection);

        List<string> tables = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.True(
            tables.Count == 0,
            "The PostgreSQL `public` schema must hold no Squad CRM table. Found: "
            + string.Join(", ", tables));
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table)
    {
        await using NpgsqlCommand command = new(
            """
            select count(*)
            from information_schema.tables
            where table_schema = @schema and table_name = @table
            """,
            connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1L;
    }
}
