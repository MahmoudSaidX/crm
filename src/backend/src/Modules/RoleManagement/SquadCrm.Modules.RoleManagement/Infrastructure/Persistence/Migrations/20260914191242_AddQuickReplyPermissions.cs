using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickReplyPermissions : Migration
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
                    { "quickreplies.view", "View quick reply templates.", "Quick Reply Management", "View quick replies" },
                    { "quickreplies.manage", "Create and edit one's own personal quick reply templates.", "Quick Reply Management", "Manage quick replies" },
                    { "quickreplies.manageglobal", "Create and edit global quick reply templates shared with everyone.", "Quick Reply Management", "Manage global quick replies" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "quickreplies.view");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "quickreplies.manage");

            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "quickreplies.manageglobal");
        }
    }
}
