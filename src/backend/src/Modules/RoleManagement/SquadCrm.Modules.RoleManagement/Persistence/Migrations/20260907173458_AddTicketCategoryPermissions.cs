using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCategoryPermissions : Migration
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
                    { "ticketcategories.manage", "Create, update, activate and deactivate ticket categories.", "Ticket Management", "Manage ticket categories" },
                    { "ticketcategories.view", "View ticket categories and their active state.", "Ticket Management", "View ticket categories" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "ticketcategories.manage");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "ticketcategories.view");
        }
    }
}
