using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AddMainPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "main_permission_headings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_main_permission_headings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "main_permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    main_permission_heading_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_main_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_main_permissions_main_permission_headings_main_permission_h~",
                        column: x => x.main_permission_heading_id,
                        principalTable: "main_permission_headings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "main_employee_permissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    main_employee_id = table.Column<int>(type: "integer", nullable: false),
                    main_permission_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_main_employee_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_main_employee_permissions_main_employees_main_employee_id",
                        column: x => x.main_employee_id,
                        principalTable: "main_employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_main_employee_permissions_main_permissions_main_permission_~",
                        column: x => x.main_permission_id,
                        principalTable: "main_permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_main_employee_permissions_main_permission_id",
                table: "main_employee_permissions",
                column: "main_permission_id");

            migrationBuilder.CreateIndex(
                name: "ux_main_employee_permissions",
                table: "main_employee_permissions",
                columns: new[] { "main_employee_id", "main_permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_main_permission_headings_slug",
                table: "main_permission_headings",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_main_permissions_main_permission_heading_id",
                table: "main_permissions",
                column: "main_permission_heading_id");

            migrationBuilder.CreateIndex(
                name: "ux_main_permissions_slug",
                table: "main_permissions",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "main_employee_permissions");

            migrationBuilder.DropTable(
                name: "main_permissions");

            migrationBuilder.DropTable(
                name: "main_permission_headings");
        }
    }
}
