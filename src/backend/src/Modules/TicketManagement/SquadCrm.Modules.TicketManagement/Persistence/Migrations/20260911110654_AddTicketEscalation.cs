using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.TicketManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "escalated_at_utc",
                schema: "ticket_management",
                table: "ticket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "escalation_level",
                schema: "ticket_management",
                table: "ticket",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "escalation_target_id",
                schema: "ticket_management",
                table: "ticket",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_target_type",
                schema: "ticket_management",
                table: "ticket",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_escalation_history",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_level = table.Column<int>(type: "integer", nullable: false),
                    new_level = table.Column<int>(type: "integer", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    escalated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    escalated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_escalation_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_escalation_history_ticket_id_escalated_at_utc",
                schema: "ticket_management",
                table: "ticket_escalation_history",
                columns: new[] { "ticket_id", "escalated_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_escalation_history",
                schema: "ticket_management");

            migrationBuilder.DropColumn(
                name: "escalated_at_utc",
                schema: "ticket_management",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "escalation_level",
                schema: "ticket_management",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "escalation_target_id",
                schema: "ticket_management",
                table: "ticket");

            migrationBuilder.DropColumn(
                name: "escalation_target_type",
                schema: "ticket_management",
                table: "ticket");
        }
    }
}
