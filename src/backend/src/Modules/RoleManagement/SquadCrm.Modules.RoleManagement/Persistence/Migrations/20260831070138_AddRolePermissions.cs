using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadCrm.Modules.RoleManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_change_audit_event",
                schema: "role_management",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    permission_codes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    changed_by_handle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_change_audit_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permission_definition",
                schema: "role_management",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_definition", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "staff_subject_role",
                schema: "role_management",
                columns: table => new
                {
                    staff_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_subject_role", x => new { x.staff_subject_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_staff_subject_role_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "role_management",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "role_management",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission", x => new { x.role_id, x.permission_code });
                    table.ForeignKey(
                        name: "FK_role_permission_permission_definition_permission_code",
                        column: x => x.permission_code,
                        principalSchema: "role_management",
                        principalTable: "permission_definition",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "role_management",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "role_management",
                table: "permission_definition",
                columns: new[] { "code", "description", "module", "name" },
                values: new object[,]
                {
                    { "roles.manage", "Create, update, activate, deactivate, and configure global roles.", "Role Management", "Manage roles" },
                    { "roles.view", "View global roles and their assigned permissions.", "Role Management", "View roles" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_role_permission_permission_code",
                schema: "role_management",
                table: "role_permission",
                column: "permission_code");

            migrationBuilder.CreateIndex(
                name: "IX_staff_subject_role_role_id",
                schema: "role_management",
                table: "staff_subject_role",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_change_audit_event",
                schema: "role_management");

            migrationBuilder.DropTable(
                name: "role_permission",
                schema: "role_management");

            migrationBuilder.DropTable(
                name: "staff_subject_role",
                schema: "role_management");

            migrationBuilder.DropTable(
                name: "permission_definition",
                schema: "role_management");
        }
    }
}
