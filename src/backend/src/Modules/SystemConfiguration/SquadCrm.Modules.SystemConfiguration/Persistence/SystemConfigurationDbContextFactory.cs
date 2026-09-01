using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.SystemConfiguration.Persistence;

public sealed class SystemConfigurationDbContextFactory : IDesignTimeDbContextFactory<SystemConfigurationDbContext>
{
    public SystemConfigurationDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<SystemConfigurationDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                SystemConfigurationSchema.MigrationsHistoryTable, SystemConfigurationSchema.Name));
        return new SystemConfigurationDbContext(options.Options);
    }
}
