using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingPermissions : Migration
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
                    { "branding.manage", "Update organization/product branding, logos and theme settings.", "Branding Management", "Manage branding" },
                    { "branding.view", "View organization/product branding configuration.", "Branding Management", "View branding" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "branding.manage");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "branding.view");
        }
    }
}
