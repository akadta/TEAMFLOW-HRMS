using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("attendance_settings")]
[Index(nameof(CompanyId), nameof(UserId), IsUnique = true, Name = "ux_attendance_settings_scope")]
public class AttendanceSetting
{
    [Column("id")] public int Id { get; set; }

    // null = global row
    [Column("company_id")] public int? CompanyId { get; set; }
    [ForeignKey("CompanyId")] public Company? Company { get; set; }

    [Column("holidays_active")] public bool HolidaysActive { get; set; }
    [Column("timing_active")] public bool TimingActive { get; set; }

    // IST wall-clock "HH:mm"
    [Column("time_from")] public string TimeFrom { get; set; } = "09:00";
    [Column("time_to")] public string TimeTo { get; set; } = "18:00";

    [Column("grace_until")] public string? GraceUntil { get; set; }

    // owner also marks attendance and appears in the report
    [Column("owner_attends")] public bool OwnerAttends { get; set; }
    // weekdays (0=Sun..6=Sat) the owner attends, e.g. "1,2,3,4,5"
    [Column("owner_days")] public string OwnerDays { get; set; } = "";
    // employee-wise row (CompanyId is set too). null = the global / company row
    [Column("user_id")] public int? UserId { get; set; }
    [Column("require_location")] public bool RequireLocation { get; set; }
    [Column("require_photo")] public bool RequirePhoto { get; set; }
    [Column("login_location")] public bool LoginLocation { get; set; }
    // json [{from,to}] = check-in windows
    [Column("periods")] public string Periods { get; set; } = "[]";

    // json: [{day,off,from,to}] only for days that are off or custom
    [Column("day_rules")] public string DayRules { get; set; } = "[]";

    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}