using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.StaffIdentity.Persistence;

public sealed class StaffIdentityDbContextFactory : IDesignTimeDbContextFactory<StaffIdentityDbContext>
{
    public StaffIdentityDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<StaffIdentityDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(StaffIdentitySchema.MigrationsHistoryTable, StaffIdentitySchema.Name));
        return new StaffIdentityDbContext(options.Options);
    }
}
