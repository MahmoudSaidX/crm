using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPermissions : Migration
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
                    { "tickets.create", "Create support tickets.", "Ticket Management", "Create tickets" },
                    { "tickets.view", "View support tickets.", "Ticket Management", "View tickets" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tickets.create");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "tickets.view");
        }
    }
}
