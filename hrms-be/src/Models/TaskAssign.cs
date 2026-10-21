using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("task_assignments")]
[Index(nameof(CompanyId), nameof(Status), Name = "ix_task_assign_company_status")]
[Index(nameof(Status), nameof(SendAt), Name = "ix_task_assign_status_send")]
public class TaskAssignment
{
    [Column("id")] public int Id { get; set; }
    [Column("company_id")] public int CompanyId { get; set; }
    [Column("created_by_user_id")] public int CreatedByUserId { get; set; }

    // company = owner / manager of the company, main = main employee
    [Column("source")] public string Source { get; set; } = "company";

    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("due_at")] public DateTime? DueAt { get; set; }

    // null = send at once
    [Column("send_at")] public DateTime? SendAt { get; set; }

    // awaiting_owner | scheduled | sent | declined | cancelled
    [Column("status")] public string Status { get; set; } = "scheduled";
    [Column("decision_note")] public string? DecisionNote { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("sent_at")] public DateTime? SentAt { get; set; }

    public List<TaskAssignmentTarget> Targets { get; set; } = new();
    public List<TaskAssignmentFile> Files { get; set; } = new();
}

[Table("task_assignment_targets")]
public class TaskAssignmentTarget
{
    [Column("id")] public int Id { get; set; }

    [Column("assignment_id")] public int AssignmentId { get; set; }
    [ForeignKey("AssignmentId")] public TaskAssignment Assignment { get; set; } = null!;

    [Column("user_id")] public int UserId { get; set; }
    [Column("employee_id")] public int? EmployeeId { get; set; }

    // the employee's task, created when the assignment is sent
    [Column("task_id")] public int? TaskId { get; set; }
}

[Table("task_assignment_files")]
public class TaskAssignmentFile
{
    [Column("id")] public int Id { get; set; }

    [Column("assignment_id")] public int AssignmentId { get; set; }
    [ForeignKey("AssignmentId")] public TaskAssignment Assignment { get; set; } = null!;

    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_path")] public string FilePath { get; set; } = string.Empty;
    [Column("size")] public long Size { get; set; }
}

[Table("notifications")]
[Index(nameof(UserId), nameof(SeenAt), Name = "ix_notifications_user_seen")]
public class Notification
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("kind")] public string Kind { get; set; } = "info";
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("text")] public string Text { get; set; } = string.Empty;
    [Column("link")] public string? Link { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("seen_at")] public DateTime? SeenAt { get; set; }
}