using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.CustomerManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerConcurrencyTokenAndInactiveStatus : Migration
    {
        /// <summary>
        /// No-op: "xmin" is Postgres's built-in system column, already present
        /// on every table. This migration only informs EF Core's model
        /// snapshot that <c>Customer.Version</c> now maps to it as an
        /// optimistic concurrency token — issuing AddColumn/DropColumn DDL
        /// against a reserved system column would fail.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
