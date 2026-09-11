using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTaskPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "role_management",
                table: "permission_definition",
                columns: new[] { "code", "description", "module", "name" },
                values: new object[,]
                {
                    { "tasks.view", "View agent tasks.", "Agent Task Management", "View tasks" },
                    { "tasks.create", "Create agent tasks.", "Agent Task Management", "Create tasks" },
                    { "tasks.edit", "Edit agent tasks.", "Agent Task Management", "Edit tasks" },
                    { "tasks.complete", "Complete or reopen agent tasks.", "Agent Task Management", "Complete tasks" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tasks.view");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tasks.create");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tasks.edit");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tasks.complete");
        }
    }
}
