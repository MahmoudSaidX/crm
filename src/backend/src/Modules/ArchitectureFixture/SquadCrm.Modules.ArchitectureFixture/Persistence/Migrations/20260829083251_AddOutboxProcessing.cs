using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.ArchitectureFixture.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_message_pending",
                schema: "architecture_fixture",
                table: "outbox_message");

            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                schema: "architecture_fixture",
                table: "outbox_message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "leased_until_utc",
                schema: "architecture_fixture",
                table: "outbox_message",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at_utc",
                schema: "architecture_fixture",
                table: "outbox_message",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "integration_event_receipt",
                schema: "architecture_fixture",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    probe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    probe_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_event_receipt", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_pending",
                schema: "architecture_fixture",
                table: "outbox_message",
                columns: new[] { "processed_at_utc", "next_attempt_at_utc", "occurred_at_utc" },
                filter: "processed_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_event_receipt",
                schema: "architecture_fixture");

            migrationBuilder.DropIndex(
                name: "ix_outbox_message_pending",
                schema: "architecture_fixture",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "lease_id",
                schema: "architecture_fixture",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "leased_until_utc",
                schema: "architecture_fixture",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                schema: "architecture_fixture",
                table: "outbox_message");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_pending",
                schema: "architecture_fixture",
                table: "outbox_message",
                column: "processed_at_utc",
                filter: "processed_at_utc IS NULL");
        }
    }
}
