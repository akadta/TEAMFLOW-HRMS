using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("leave_settings")]
[Index(nameof(CompanyId), IsUnique = true, Name = "ux_leave_settings_company")]
public class LeaveSetting
{
    [Column("id")] public int Id { get; set; }

    // null = global
    [Column("company_id")] public int? CompanyId { get; set; }
    [ForeignKey("CompanyId")] public Company? Company { get; set; }

    [Column("is_active")] public bool IsActive { get; set; }
    [Column("max_days")] public int? MaxDays { get; set; }

    // json: approval groups
    [Column("config")] public string Config { get; set; } = "{}";

    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("leave_requests")]
[Index(nameof(CompanyId), nameof(Status), Name = "ix_leave_requests_company_status")]
[Index(nameof(UserId), Name = "ix_leave_requests_user")]
public class LeaveRequest
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    // null = company owner
    [Column("employee_id")] public int? EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee? Employee { get; set; }

    [Column("company_id")] public int CompanyId { get; set; }
    [Column("reason")] public string Reason { get; set; } = string.Empty;

    // pending | approved | rejected | cancelled
    [Column("status")] public string Status { get; set; } = "pending";

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("decided_by_user_id")] public int? DecidedByUserId { get; set; }
    [Column("decision_note")] public string? DecisionNote { get; set; }

    public List<LeaveDay> Days { get; set; } = new();
    public List<LeaveFile> Files { get; set; } = new();
    public List<LeaveApproval> Approvals { get; set; } = new();
}

[Table("leave_days")]
[Index(nameof(UserId), nameof(Date), Name = "ix_leave_days_user_date")]
public class LeaveDay
{
    [Column("id")] public int Id { get; set; }

    [Column("request_id")] public int RequestId { get; set; }
    [ForeignKey("RequestId")] public LeaveRequest Request { get; set; } = null!;

    [Column("user_id")] public int UserId { get; set; }
    [Column("leave_date")] public DateOnly Date { get; set; }
}

[Table("leave_files")]
public class LeaveFile
{
    [Column("id")] public int Id { get; set; }

    [Column("request_id")] public int RequestId { get; set; }
    [ForeignKey("RequestId")] public LeaveRequest Request { get; set; } = null!;

    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_path")] public string FilePath { get; set; } = string.Empty;
}

[Table("leave_approvals")]
[Index(nameof(ApproverUserId), nameof(Decision), Name = "ix_leave_approvals_approver")]
public class LeaveApproval
{
    [Column("id")] public int Id { get; set; }

    [Column("request_id")] public int RequestId { get; set; }
    [ForeignKey("RequestId")] public LeaveRequest Request { get; set; } = null!;

    // managers | owner | main | selected
    [Column("group_name")] public string Group { get; set; } = string.Empty;

    // any = first decision wins, all = everyone must approve
    [Column("mode")] public string Mode { get; set; } = "any";

    [Column("approver_user_id")] public int ApproverUserId { get; set; }

    // pending | approved | rejected | skipped
    [Column("decision")] public string Decision { get; set; } = "pending";
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("note")] public string? Note { get; set; }
}