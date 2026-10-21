using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    company_role_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employees_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employees_company_roles_company_role_id",
                        column: x => x.company_role_id,
                        principalTable: "company_roles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_employees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_documents_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_histories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    employer = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason_for_leaving = table.Column<string>(type: "text", nullable: true),
                    relieving_letter = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_histories_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_documents_employee_id",
                table: "employee_documents",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_histories_employee_id",
                table: "employee_histories",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employees_company_role_id",
                table: "employees",
                column: "company_role_id");

            migrationBuilder.CreateIndex(
                name: "ux_employees_aadhar",
                table: "employees",
                column: "aadhar_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_company_code",
                table: "employees",
                columns: new[] { "company_id", "employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_official_email",
                table: "employees",
                column: "official_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_pan",
                table: "employees",
                column: "pan_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employees_user_id",
                table: "employees",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_documents");

            migrationBuilder.DropTable(
                name: "employee_histories");

            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}
