using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.CustomerManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCustomerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer_management");

            migrationBuilder.CreateTable(
                name: "customer",
                schema: "customer_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_first_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_last_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_customer_number",
                schema: "customer_management",
                table: "customer",
                column: "customer_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_duplicate_match",
                schema: "customer_management",
                table: "customer",
                columns: new[] { "normalized_first_name", "normalized_last_name", "department_match_id", "branch_match_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer",
                schema: "customer_management");
        }
    }
}
