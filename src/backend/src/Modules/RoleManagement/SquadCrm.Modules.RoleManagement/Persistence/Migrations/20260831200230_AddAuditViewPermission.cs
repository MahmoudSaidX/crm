using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditViewPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "role_management",
                table: "permission_definition",
                columns: new[] { "code", "description", "module", "name" },
                values: new object[] { "audit.view", "View the audit trail of administrative actions.", "Audit", "View audit records" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "role_management",
                table: "permission_definition",
                keyColumn: "code",
                keyValue: "audit.view");
        }
    }
}
