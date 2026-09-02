using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.BrandingManagement.Persistence;

public sealed class BrandingManagementDbContextFactory : IDesignTimeDbContextFactory<BrandingManagementDbContext>
{
    public BrandingManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<BrandingManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                BrandingManagementSchema.MigrationsHistoryTable, BrandingManagementSchema.Name));
        return new BrandingManagementDbContext(options.Options);
    }
}
