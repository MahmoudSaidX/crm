using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.SystemConfiguration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSystemConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system_configuration");

            migrationBuilder.CreateTable(
                name: "configuration_value",
                schema: "system_configuration",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    updated_by_handle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_value", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration_value",
                schema: "system_configuration");
        }
    }
}
