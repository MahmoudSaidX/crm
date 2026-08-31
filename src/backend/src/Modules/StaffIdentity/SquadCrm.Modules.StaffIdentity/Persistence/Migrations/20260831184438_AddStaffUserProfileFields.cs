using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.StaffIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branch",
                schema: "staff_identity",
                table: "staff_user",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                schema: "staff_identity",
                table: "staff_user",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                schema: "staff_identity",
                table: "staff_user",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "changed_by_handle",
                schema: "staff_identity",
                table: "authentication_event",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branch",
                schema: "staff_identity",
                table: "staff_user");

            migrationBuilder.DropColumn(
                name: "department",
                schema: "staff_identity",
                table: "staff_user");

            migrationBuilder.DropColumn(
                name: "display_name",
                schema: "staff_identity",
                table: "staff_user");

            migrationBuilder.DropColumn(
                name: "changed_by_handle",
                schema: "staff_identity",
                table: "authentication_event");
        }
    }
}
