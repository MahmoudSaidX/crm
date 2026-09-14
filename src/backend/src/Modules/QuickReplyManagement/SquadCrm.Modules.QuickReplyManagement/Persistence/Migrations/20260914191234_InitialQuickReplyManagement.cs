using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.QuickReplyManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialQuickReplyManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quick_reply_management");

            migrationBuilder.CreateTable(
                name: "quick_reply",
                schema: "quick_reply_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    arabic_content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    english_content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quick_reply", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quick_reply_owner_user_id",
                schema: "quick_reply_management",
                table: "quick_reply",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_quick_reply_global_normalized_name",
                schema: "quick_reply_management",
                table: "quick_reply",
                column: "normalized_name",
                unique: true,
                filter: "scope = 'Global'");

            migrationBuilder.CreateIndex(
                name: "ux_quick_reply_personal_owner_normalized_name",
                schema: "quick_reply_management",
                table: "quick_reply",
                columns: new[] { "normalized_name", "owner_user_id" },
                unique: true,
                filter: "scope = 'Personal'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quick_reply",
                schema: "quick_reply_management");
        }
    }
}
