using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("task_settings")]
[Index(nameof(CompanyId), nameof(UserId), IsUnique = true, Name = "ux_task_settings_scope")]
public class TaskSetting
{
    [Column("id")] public int Id { get; set; }
    [Column("company_id")] public int? CompanyId { get; set; }
    [Column("user_id")] public int? UserId { get; set; }
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("approval_required")] public bool ApprovalRequired { get; set; }
    [Column("config")] public string Config { get; set; } = "{}";
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("task_approvals")]
[Index(nameof(ApproverUserId), nameof(Decision), Name = "ix_task_approvals_approver")]
public class TaskApproval
{
    [Column("id")] public int Id { get; set; }

    [Column("task_id")] public int TaskId { get; set; }
    [ForeignKey("TaskId")] public WorkTask Task { get; set; } = null!;

    [Column("group_name")] public string Group { get; set; } = string.Empty;
    [Column("mode")] public string Mode { get; set; } = "any";
    [Column("approver_user_id")] public int ApproverUserId { get; set; }

    // pending | approved | rejected | skipped
    [Column("decision")] public string Decision { get; set; } = "pending";
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("note")] public string? Note { get; set; }
}