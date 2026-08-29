using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SquadCrm.Modules.StaffIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialStaffIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staff_identity");

            migrationBuilder.CreateTable(
                name: "authentication_event",
                schema: "staff_identity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_event", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_user",
                schema: "staff_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_session",
                schema: "staff_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_session_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_session", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_session_staff_user_staff_user_id",
                        column: x => x.staff_user_id,
                        principalSchema: "staff_identity",
                        principalTable: "staff_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_session_staff_user_id",
                schema: "staff_identity",
                table: "refresh_session",
                column: "staff_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_session_token_hash",
                schema: "staff_identity",
                table: "refresh_session",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_user_normalized_email",
                schema: "staff_identity",
                table: "staff_user",
                column: "normalized_email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authentication_event",
                schema: "staff_identity");

            migrationBuilder.DropTable(
                name: "refresh_session",
                schema: "staff_identity");

            migrationBuilder.DropTable(
                name: "staff_user",
                schema: "staff_identity");
        }
    }
}
