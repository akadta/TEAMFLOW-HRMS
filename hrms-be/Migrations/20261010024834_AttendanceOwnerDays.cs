using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceOwnerDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_days",
                table: "attendance_settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "owner_days",
                table: "attendance_settings");
        }
    }
}
