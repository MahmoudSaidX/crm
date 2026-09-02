using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.CustomerManagement.Persistence;

public sealed class CustomerManagementDbContextFactory : IDesignTimeDbContextFactory<CustomerManagementDbContext>
{
    public CustomerManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<CustomerManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                CustomerManagementSchema.MigrationsHistoryTable, CustomerManagementSchema.Name));
        return new CustomerManagementDbContext(options.Options);
    }
}
