using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.ArchitectureFixture.Persistence;
using SquadCrm.Modules.Audit.Persistence;
using SquadCrm.Modules.BranchManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Persistence;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Persistence;
using SquadCrm.Modules.SystemConfiguration.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Brings the real PostgreSQL database into a known state once per test class
/// collection: the module's migrations are applied through the module's own
/// design-time factory, so these tests exercise exactly the configuration
/// <c>dotnet ef</c> and the running application use.
/// <para>
/// <b>These tests fail; they never skip.</b> A green run must mean the database
/// was really exercised. When the server is unreachable the fixture throws an
/// actionable message built from <see cref="PostgresConfiguration.Describe"/>, so
/// no password and no assembled connection string can appear in test output.
/// </para>
/// <para>
/// Applying migrations here is test setup, not startup migration: the API host
/// never calls <c>Database.Migrate()</c>.
/// </para>
/// </summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
    /// <summary>xUnit collection name shared by every suite that touches the real database.</summary>
    public const string CollectionName = "PostgreSQL";

    private readonly PostgresOptions _maintenanceOptions;
    private readonly PostgresOptions _options;
    private readonly string _originalDatabase;
    private bool _databaseCreated;

    public PostgresTestDatabase()
    {
        // The same single implementation the API and the design-time factory use.
        // No test-local key reading, port parsing or connection-string assembly.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        _maintenanceOptions = configuration.ReadPostgresOptions();
        _originalDatabase = _maintenanceOptions.Database;
        _options = _maintenanceOptions with
        {
            Database = $"squadcrm_tests_{Guid.NewGuid():N}"[..30],
        };

        // Keep the production design-time factory as the single context path,
        // while redirecting only this test process to its isolated database.
        Environment.SetEnvironmentVariable(PostgresOptions.DatabaseKey, _options.Database);
    }

    /// <summary>Redacted coordinates, safe to print. Never includes the password.</summary>
    public string Description => _options.Describe();

    public async Task InitializeAsync()
    {
        await using NpgsqlConnection maintenance = new(MaintenanceConnectionString());

        try
        {
            await maintenance.OpenAsync();
            string databaseIdentifier = new NpgsqlCommandBuilder().QuoteIdentifier(_options.Database);
            await using NpgsqlCommand createDatabase = new(
                $"CREATE DATABASE {databaseIdentifier}",
                maintenance);
            await createDatabase.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(Unreachable(), exception);
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            throw new InvalidOperationException(Unreachable(), exception);
        }

        await using ArchitectureFixtureDbContext context = CreateContext();
        await context.Database.MigrateAsync();
        await using StaffIdentityDbContext staffIdentity = CreateStaffIdentityContext();
        await staffIdentity.Database.MigrateAsync();
        await using RoleManagementDbContext roleManagement = CreateRoleManagementContext();
        await roleManagement.Database.MigrateAsync();
        await using AuditDbContext audit = CreateAuditContext();
        await audit.Database.MigrateAsync();
        await using DepartmentManagementDbContext departmentManagement = CreateDepartmentManagementContext();
        await departmentManagement.Database.MigrateAsync();
        await using BranchManagementDbContext branchManagement = CreateBranchManagementContext();
        await branchManagement.Database.MigrateAsync();
        await using SystemConfigurationDbContext systemConfiguration = CreateSystemConfigurationContext();
        await systemConfiguration.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_databaseCreated)
            {
                NpgsqlConnection.ClearAllPools();
                await using NpgsqlConnection maintenance = new(MaintenanceConnectionString());
                await maintenance.OpenAsync();
                string databaseIdentifier = new NpgsqlCommandBuilder().QuoteIdentifier(_options.Database);
                await using NpgsqlCommand dropDatabase = new(
                    $"DROP DATABASE IF EXISTS {databaseIdentifier} WITH (FORCE)",
                    maintenance);
                await dropDatabase.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(PostgresOptions.DatabaseKey, _originalDatabase);
        }
    }

    /// <summary>
    /// Builds the module's context through the module's <b>own</b> design-time
    /// factory, so a divergence between test wiring and real wiring is impossible.
    /// </summary>
    public static ArchitectureFixtureDbContext CreateContext() =>
        new ArchitectureFixtureDbContextFactory().CreateDbContext([]);

    public static StaffIdentityDbContext CreateStaffIdentityContext() =>
        new StaffIdentityDbContextFactory().CreateDbContext([]);

    public static RoleManagementDbContext CreateRoleManagementContext() =>
        new RoleManagementDbContextFactory().CreateDbContext([]);

    public static AuditDbContext CreateAuditContext() =>
        new AuditDbContextFactory().CreateDbContext([]);

    public static DepartmentManagementDbContext CreateDepartmentManagementContext() =>
        new DepartmentManagementDbContextFactory().CreateDbContext([]);

    public static BranchManagementDbContext CreateBranchManagementContext() =>
        new BranchManagementDbContextFactory().CreateDbContext([]);

    public static SystemConfigurationDbContext CreateSystemConfigurationContext() =>
        new SystemConfigurationDbContextFactory().CreateDbContext([]);

    /// <summary>Opens a raw connection for <c>information_schema</c> assertions.</summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        NpgsqlConnection connection = new(_options.BuildConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private string Unreachable() =>
        $"PostgreSQL is not reachable at {_options.Host}:{_options.Port}. "
        + "Run `docker compose up -d` from the repository root, then load "
        + "`env/backend.env` into the shell. "
        + $"Coordinates in use (password omitted): {Description}";

    private string MaintenanceConnectionString() =>
        (_maintenanceOptions with { Database = "postgres" }).BuildConnectionString();
}

/// <summary>Binds the fixture to every suite that requires a real server.</summary>
[CollectionDefinition(PostgresTestDatabase.CollectionName)]
public sealed class PostgresCollection : ICollectionFixture<PostgresTestDatabase>;
