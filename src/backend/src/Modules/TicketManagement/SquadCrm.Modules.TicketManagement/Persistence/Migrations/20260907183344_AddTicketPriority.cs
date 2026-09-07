using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.TicketManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_priority",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    arabic_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    english_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_priority", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_priority_normalized_code",
                schema: "ticket_management",
                table: "ticket_priority",
                column: "normalized_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_priority",
                schema: "ticket_management");
        }
    }
}
