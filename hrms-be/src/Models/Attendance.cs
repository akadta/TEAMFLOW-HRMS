using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("attendances")]
[Index(nameof(UserId), nameof(Date), IsUnique = true, Name = "ux_attendances_user_date")]
[Index(nameof(CompanyId), nameof(Date), Name = "ix_attendances_company_date")]
[Index(nameof(Date), Name = "ix_attendances_date")]
public class Attendance
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    // null = company owner
    [Column("employee_id")] public int? EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee? Employee { get; set; }

    [Column("company_id")] public int CompanyId { get; set; }

    // IST calendar date
    [Column("work_date")] public DateOnly Date { get; set; }

    // present | absent | holiday
    [Column("status")] public string Status { get; set; } = "present";

    [Column("check_in_at")] public DateTime? CheckInAt { get; set; }

    // normal | request | override | system
    [Column("source")] public string Source { get; set; } = "normal";

    [Column("late_minutes")] public int LateMinutes { get; set; }
    [Column("request_id")] public int? RequestId { get; set; }
    [Column("note")] public string? Note { get; set; }
    [Column("lat")] public double? Lat { get; set; }
    [Column("lng")] public double? Lng { get; set; }
    [Column("accuracy")] public double? Accuracy { get; set; }
    [Column("photo_path")] public string? PhotoPath { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("attendance_requests")]
[Index(nameof(CompanyId), nameof(Status), Name = "ix_att_requests_company_status")]
[Index(nameof(UserId), nameof(Date), Name = "ix_att_requests_user_date")]
public class AttendanceRequest
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    [Column("employee_id")] public int? EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee? Employee { get; set; }

    [Column("company_id")] public int CompanyId { get; set; }
    [Column("work_date")] public DateOnly Date { get; set; }
    [Column("reason")] public string Reason { get; set; } = string.Empty;

    // pending | approved | rejected
    [Column("status")] public string Status { get; set; } = "pending";

    [Column("late_minutes")] public int LateMinutes { get; set; }
    [Column("requested_at")] public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Column("decided_by_user_id")] public int? DecidedByUserId { get; set; }
    [Column("decided_at")] public DateTime? DecidedAt { get; set; }
    [Column("decision_note")] public string? DecisionNote { get; set; }

    public List<AttendanceRequestFile> Files { get; set; } = new();
}

[Table("attendance_request_files")]
public class AttendanceRequestFile
{
    [Column("id")] public int Id { get; set; }

    [Column("request_id")] public int RequestId { get; set; }
    [ForeignKey("RequestId")] public AttendanceRequest Request { get; set; } = null!;

    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_path")] public string FilePath { get; set; } = string.Empty;
}