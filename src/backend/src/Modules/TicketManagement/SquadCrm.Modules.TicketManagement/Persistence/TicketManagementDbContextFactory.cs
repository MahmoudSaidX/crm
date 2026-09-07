using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public sealed class TicketManagementDbContextFactory : IDesignTimeDbContextFactory<TicketManagementDbContext>
{
    public TicketManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<TicketManagementDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                TicketManagementSchema.MigrationsHistoryTable, TicketManagementSchema.Name));
        return new TicketManagementDbContext(options.Options);
    }
}
