using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class RoleManagementDbContextFactory : IDesignTimeDbContextFactory<RoleManagementDbContext>
{
    public RoleManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<RoleManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(RoleManagementSchema.MigrationsHistoryTable, RoleManagementSchema.Name));
        return new RoleManagementDbContext(options.Options);
    }
}
