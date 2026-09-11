using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.TicketManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketUpdatedAtAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at_utc",
                schema: "ticket_management",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "ticket_management",
                table: "ticket",
                type: "integer",
                nullable: false,
                // Backfill existing rows with the same starting value new
                // tickets get from the entity's CLR default, so no ticket
                // carries a version the application never produces.
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                schema: "ticket_management",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "ticket_management",
                table: "ticket");
        }
    }
}
