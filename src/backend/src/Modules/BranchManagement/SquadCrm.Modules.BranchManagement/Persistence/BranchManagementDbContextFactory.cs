using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.BranchManagement.Persistence;

public sealed class BranchManagementDbContextFactory : IDesignTimeDbContextFactory<BranchManagementDbContext>
{
    public BranchManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<BranchManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                BranchManagementSchema.MigrationsHistoryTable, BranchManagementSchema.Name));
        return new BranchManagementDbContext(options.Options);
    }
}
