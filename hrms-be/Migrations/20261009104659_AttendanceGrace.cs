using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grace_until",
                table: "attendance_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grace_until",
                table: "attendance_settings");
        }
    }
}
