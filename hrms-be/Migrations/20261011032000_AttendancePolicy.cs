using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AttendancePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_attendance_settings_company",
                table: "attendance_settings");

            migrationBuilder.AddColumn<double>(
                name: "accuracy",
                table: "attendances",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "lat",
                table: "attendances",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "lng",
                table: "attendances",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_path",
                table: "attendances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "login_location",
                table: "attendance_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "periods",
                table: "attendance_settings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "require_location",
                table: "attendance_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_photo",
                table: "attendance_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "attendance_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendance_checkins",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    employee_id = table.Column<int>(type: "integer", nullable: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_from = table.Column<string>(type: "text", nullable: false),
                    slot_to = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: true),
                    lng = table.Column<double>(type: "double precision", nullable: true),
                    accuracy = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_checkins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_locations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    accuracy = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_locations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_attendance_settings_scope",
                table: "attendance_settings",
                columns: new[] { "company_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_checkins_company_date",
                table: "attendance_checkins",
                columns: new[] { "company_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ux_checkins_user_date_slot",
                table: "attendance_checkins",
                columns: new[] { "user_id", "work_date", "slot_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_login_locations_user_at",
                table: "login_locations",
                columns: new[] { "user_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_checkins");

            migrationBuilder.DropTable(
                name: "login_locations");

            migrationBuilder.DropIndex(
                name: "ux_attendance_settings_scope",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "accuracy",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "lat",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "lng",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "photo_path",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "login_location",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "periods",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "require_location",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "require_photo",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "attendance_settings");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_settings_company",
                table: "attendance_settings",
                column: "company_id",
                unique: true);
        }
    }
}
