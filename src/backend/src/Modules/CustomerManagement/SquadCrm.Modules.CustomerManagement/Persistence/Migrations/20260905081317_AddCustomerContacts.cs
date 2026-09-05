using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadCrm.Modules.CustomerManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_contact",
                schema: "customer_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_contact", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_contact_customer_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customer_management",
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_contact_active_primary",
                schema: "customer_management",
                table: "customer_contact",
                columns: new[] { "customer_id", "type" },
                unique: true,
                filter: "is_primary = true AND is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_contact",
                schema: "customer_management");
        }
    }
}
