using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> target this class library directly — no runtime host,
/// no startup project hack, no <c>Program.cs</c> change.
/// <para>
/// It reads <b>the process environment only</b>. It never locates or parses
/// <c>env/backend.env</c>: loading that file into the shell is a documented
/// developer workflow step (<c>set -a &amp;&amp; . ./env/backend.env &amp;&amp; set +a</c>),
/// not application behaviour.
/// </para>
/// <para>
/// Validation, port parsing, connection-string assembly and redaction all come
/// from <see cref="PostgresConfiguration"/> — the same implementation the API
/// composition root uses — so a missing key fails design time and runtime with
/// one identical message, naming keys and never values.
/// </para>
/// <para>
/// Public because the persistence integration suite constructs the context
/// through this same factory rather than re-implementing configuration reading;
/// <c>dotnet ef</c> itself would find it either way.
/// </para>
/// </summary>
public sealed class ArchitectureFixtureDbContextFactory
    : IDesignTimeDbContextFactory<ArchitectureFixtureDbContext>
{
    public ArchitectureFixtureDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();

        DbContextOptionsBuilder<ArchitectureFixtureDbContext> options = new();
        ArchitectureFixtureDbContextOptions.Apply(options, connectionString);

        return new ArchitectureFixtureDbContext(options.Options);
    }
}
