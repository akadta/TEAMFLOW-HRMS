using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class DashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_created",
                table: "work_tasks",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_work_tasks_status_due",
                table: "work_tasks",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendances_date",
                table: "attendances",
                column: "work_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_tasks_created",
                table: "work_tasks");

            migrationBuilder.DropIndex(
                name: "ix_work_tasks_status_due",
                table: "work_tasks");

            migrationBuilder.DropIndex(
                name: "ix_attendances_date",
                table: "attendances");
        }
    }
}
