using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.TicketManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_internal_note",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_internal_note", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_watcher",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    added_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_watcher", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_watcher_history",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_watcher_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_note_mention",
                schema: "ticket_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentioned_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_note_mention", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_note_mention_ticket_internal_note_note_id",
                        column: x => x.note_id,
                        principalSchema: "ticket_management",
                        principalTable: "ticket_internal_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_internal_note_ticket_id_created_at_utc",
                schema: "ticket_management",
                table: "ticket_internal_note",
                columns: new[] { "ticket_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_note_mention_mentioned_user_id_created_at_utc",
                schema: "ticket_management",
                table: "ticket_note_mention",
                columns: new[] { "mentioned_user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_note_mention_note_id_mentioned_user_id",
                schema: "ticket_management",
                table: "ticket_note_mention",
                columns: new[] { "note_id", "mentioned_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_watcher_ticket_id_user_id",
                schema: "ticket_management",
                table: "ticket_watcher",
                columns: new[] { "ticket_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_watcher_history_ticket_id_changed_at_utc",
                schema: "ticket_management",
                table: "ticket_watcher_history",
                columns: new[] { "ticket_id", "changed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_note_mention",
                schema: "ticket_management");

            migrationBuilder.DropTable(
                name: "ticket_watcher",
                schema: "ticket_management");

            migrationBuilder.DropTable(
                name: "ticket_watcher_history",
                schema: "ticket_management");

            migrationBuilder.DropTable(
                name: "ticket_internal_note",
                schema: "ticket_management");
        }
    }
}
