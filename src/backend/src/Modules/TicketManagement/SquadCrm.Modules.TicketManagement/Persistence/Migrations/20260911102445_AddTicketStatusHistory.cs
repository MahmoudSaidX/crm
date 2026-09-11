using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.TicketManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_status_history",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    new_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_status_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_status_history_ticket_id_changed_at_utc",
                schema: "ticket_management",
                table: "ticket_status_history",
                columns: new[] { "ticket_id", "changed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_status_history",
                schema: "ticket_management");
        }
    }
}
