using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("attendance_checkins")]
[Index(nameof(UserId), nameof(Date), nameof(SlotFrom), IsUnique = true, Name = "ux_checkins_user_date_slot")]
[Index(nameof(CompanyId), nameof(Date), Name = "ix_checkins_company_date")]
public class AttendanceCheckin
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("employee_id")] public int? EmployeeId { get; set; }
    [Column("company_id")] public int CompanyId { get; set; }
    [Column("work_date")] public DateOnly Date { get; set; }
    [Column("slot_from")] public string SlotFrom { get; set; } = string.Empty;
    [Column("slot_to")] public string SlotTo { get; set; } = string.Empty;
    [Column("at")] public DateTime At { get; set; } = DateTime.UtcNow;
    [Column("lat")] public double? Lat { get; set; }
    [Column("lng")] public double? Lng { get; set; }
    [Column("accuracy")] public double? Accuracy { get; set; }
}

[Table("login_locations")]
[Index(nameof(UserId), nameof(At), Name = "ix_login_locations_user_at")]
public class LoginLocation
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("company_id")] public int CompanyId { get; set; }
    [Column("at")] public DateTime At { get; set; } = DateTime.UtcNow;
    [Column("lat")] public double Lat { get; set; }
    [Column("lng")] public double Lng { get; set; }
    [Column("accuracy")] public double? Accuracy { get; set; }
}