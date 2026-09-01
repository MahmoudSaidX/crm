using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.DepartmentManagement.Persistence;

public sealed class DepartmentManagementDbContextFactory : IDesignTimeDbContextFactory<DepartmentManagementDbContext>
{
    public DepartmentManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<DepartmentManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                DepartmentManagementSchema.MigrationsHistoryTable, DepartmentManagementSchema.Name));
        return new DepartmentManagementDbContext(options.Options);
    }
}
