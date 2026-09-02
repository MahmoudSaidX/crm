using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.BrandingManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBrandingManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "branding_management");

            migrationBuilder.CreateTable(
                name: "branding_asset",
                schema: "branding_management",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branding_asset", x => x.kind);
                });

            migrationBuilder.CreateTable(
                name: "branding_setting",
                schema: "branding_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_display_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    organization_display_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    product_display_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_display_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    theme_tokens_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_handle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branding_setting", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branding_asset",
                schema: "branding_management");

            migrationBuilder.DropTable(
                name: "branding_setting",
                schema: "branding_management");
        }
    }
}
