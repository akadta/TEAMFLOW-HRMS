using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AddMainEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "main_employees",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    employee_code = table.Column<string>(type: "text", nullable: false),
                    official_email = table.Column<string>(type: "text", nullable: true),
                    emergency_number = table.Column<string>(type: "text", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: true),
                    blood_group = table.Column<string>(type: "text", nullable: true),
                    marital_status = table.Column<string>(type: "text", nullable: true),
                    nationality = table.Column<string>(type: "text", nullable: true),
                    aadhar_number = table.Column<string>(type: "text", nullable: true),
                    pan_number = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    profile_picture = table.Column<string>(type: "text", nullable: true),
                    highest_qualification = table.Column<string>(type: "text", nullable: true),
                    institution = table.Column<string>(type: "text", nullable: true),
                    year_of_passing = table.Column<int>(type: "integer", nullable: true),
                    work_type = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_main_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_main_employees_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_main_employees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_main_employees_role_id",
                table: "main_employees",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_main_employees_aadhar",
                table: "main_employees",
                column: "aadhar_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_main_employees_code",
                table: "main_employees",
                column: "employee_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_main_employees_official_email",
                table: "main_employees",
                column: "official_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_main_employees_pan",
                table: "main_employees",
                column: "pan_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_main_employees_user_id",
                table: "main_employees",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "main_employees");
        }
    }
}
