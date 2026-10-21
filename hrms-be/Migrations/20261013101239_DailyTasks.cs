using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace hrms_be.Migrations
{
    /// <inheritdoc />
    public partial class DailyTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approval",
                table: "work_tasks",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "approval_note",
                table: "work_tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at",
                table: "work_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progress",
                table: "work_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "start_at",
                table: "work_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_approvals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<int>(type: "integer", nullable: false),
                    group_name = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    approver_user_id = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "text", nullable: false),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_approvals_work_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "work_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<int>(type: "integer", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    approval_required = table.Column<bool>(type: "boolean", nullable: false),
                    config = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_task_approvals_approver",
                table: "task_approvals",
                columns: new[] { "approver_user_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "IX_task_approvals_task_id",
                table: "task_approvals",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ux_task_settings_scope",
                table: "task_settings",
                columns: new[] { "company_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_approvals");

            migrationBuilder.DropTable(
                name: "task_settings");

            migrationBuilder.DropColumn(
                name: "approval",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "approval_note",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "due_at",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "progress",
                table: "work_tasks");

            migrationBuilder.DropColumn(
                name: "start_at",
                table: "work_tasks");
        }
    }
}
