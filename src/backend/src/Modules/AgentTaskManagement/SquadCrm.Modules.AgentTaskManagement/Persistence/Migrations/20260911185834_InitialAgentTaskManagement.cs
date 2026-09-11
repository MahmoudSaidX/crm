using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.AgentTaskManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAgentTaskManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agent_task_management");

            migrationBuilder.CreateTable(
                name: "agent_task",
                schema: "agent_task_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_task", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_task_outbox_message",
                schema: "agent_task_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_task_outbox_message", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_task_due_at_utc",
                schema: "agent_task_management",
                table: "agent_task",
                column: "due_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_agent_task_owner_user_id",
                schema: "agent_task_management",
                table: "agent_task",
                column: "owner_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_task",
                schema: "agent_task_management");

            migrationBuilder.DropTable(
                name: "agent_task_outbox_message",
                schema: "agent_task_management");
        }
    }
}
