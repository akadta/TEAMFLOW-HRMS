using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceStatusOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM attendance_request_files;
DELETE FROM attendance_requests;
DELETE FROM attendances;");
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_requests_employees_employee_id",
                table: "attendance_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_attendances_employees_employee_id",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "ux_attendances_emp_date",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "ix_att_requests_emp_date",
                table: "attendance_requests");

            migrationBuilder.AlterColumn<int>(
                name: "employee_id",
                table: "attendances",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTime>(
                name: "check_in_at",
                table: "attendances",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "attendances",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "attendances",
                type: "text",
                nullable: false,
                defaultValue: "present");

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "attendances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "owner_attends",
                table: "attendance_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "employee_id",
                table: "attendance_requests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "attendance_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_attendances_employee_id",
                table: "attendances",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_attendances_user_date",
                table: "attendances",
                columns: new[] { "user_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_att_requests_user_date",
                table: "attendance_requests",
                columns: new[] { "user_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_requests_employee_id",
                table: "attendance_requests",
                column: "employee_id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_requests_employees_employee_id",
                table: "attendance_requests",
                column: "employee_id",
                principalTable: "employees",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_requests_users_user_id",
                table: "attendance_requests",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attendances_employees_employee_id",
                table: "attendances",
                column: "employee_id",
                principalTable: "employees",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendances_users_user_id",
                table: "attendances",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendance_requests_employees_employee_id",
                table: "attendance_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_attendance_requests_users_user_id",
                table: "attendance_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_attendances_employees_employee_id",
                table: "attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_attendances_users_user_id",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "IX_attendances_employee_id",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "ux_attendances_user_date",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "ix_att_requests_user_date",
                table: "attendance_requests");

            migrationBuilder.DropIndex(
                name: "IX_attendance_requests_employee_id",
                table: "attendance_requests");

            migrationBuilder.DropColumn(
                name: "note",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "status",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "owner_attends",
                table: "attendance_settings");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "attendance_requests");

            migrationBuilder.AlterColumn<int>(
                name: "employee_id",
                table: "attendances",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "check_in_at",
                table: "attendances",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "employee_id",
                table: "attendance_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_attendances_emp_date",
                table: "attendances",
                columns: new[] { "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_att_requests_emp_date",
                table: "attendance_requests",
                columns: new[] { "employee_id", "work_date" });

            migrationBuilder.AddForeignKey(
                name: "FK_attendance_requests_employees_employee_id",
                table: "attendance_requests",
                column: "employee_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_attendances_employees_employee_id",
                table: "attendances",
                column: "employee_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
