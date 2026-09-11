using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.AgentTaskManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTaskReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reminder_at_utc",
                schema: "agent_task_management",
                table: "agent_task",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reminder_event_id",
                schema: "agent_task_management",
                table: "agent_task",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reminder_status",
                schema: "agent_task_management",
                table: "agent_task",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                // Existing tasks (CRM-143) have no reminder. EF's scaffolded
                // "" would not map back to AgentTaskReminderStatus and would
                // throw on the first read of any pre-existing row.
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reminder_triggered_at_utc",
                schema: "agent_task_management",
                table: "agent_task",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_task_scheduled_reminder_at_utc",
                schema: "agent_task_management",
                table: "agent_task",
                column: "reminder_at_utc",
                filter: "reminder_status = 'Scheduled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agent_task_scheduled_reminder_at_utc",
                schema: "agent_task_management",
                table: "agent_task");

            migrationBuilder.DropColumn(
                name: "reminder_at_utc",
                schema: "agent_task_management",
                table: "agent_task");

            migrationBuilder.DropColumn(
                name: "reminder_event_id",
                schema: "agent_task_management",
                table: "agent_task");

            migrationBuilder.DropColumn(
                name: "reminder_status",
                schema: "agent_task_management",
                table: "agent_task");

            migrationBuilder.DropColumn(
                name: "reminder_triggered_at_utc",
                schema: "agent_task_management",
                table: "agent_task");
        }
    }
}
