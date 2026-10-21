using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("work_tasks")]
[Index(nameof(UserId), nameof(Kind), Name = "ix_work_tasks_user_kind")]
[Index(nameof(CompanyId), nameof(Kind), Name = "ix_work_tasks_company_kind")]
[Index(nameof(Status), nameof(DueAt), Name = "ix_work_tasks_status_due")]
[Index(nameof(CreatedAt), Name = "ix_work_tasks_created")]
public class WorkTask
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    [Column("employee_id")] public int? EmployeeId { get; set; }
    [Column("company_id")] public int CompanyId { get; set; }

    // self | daily | assigned
    [Column("kind")] public string Kind { get; set; } = "self";

    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }

    // todo | in_progress | on_hold | done
    [Column("status")] public string Status { get; set; } = "todo";

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("completed_at")] public DateTime? CompletedAt { get; set; }

    [Column("start_at")] public DateTime? StartAt { get; set; }
    [Column("due_at")] public DateTime? DueAt { get; set; }
    [Column("progress")] public int Progress { get; set; }

    // none | pending | approved | rejected
    [Column("approval")] public string Approval { get; set; } = "none";
    [Column("approval_note")] public string? ApprovalNote { get; set; }

    public List<TaskApproval> Approvals { get; set; } = new();

    public List<WorkTaskFile> Files { get; set; } = new();

}

[Table("work_task_files")]
public class WorkTaskFile
{
    [Column("id")] public int Id { get; set; }

    [Column("task_id")] public int TaskId { get; set; }
    [ForeignKey("TaskId")] public WorkTask Task { get; set; } = null!;

    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_path")] public string FilePath { get; set; } = string.Empty;
    [Column("size")] public long Size { get; set; }
}