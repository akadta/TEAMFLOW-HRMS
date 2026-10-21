using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("main_employees")]
[Index(nameof(UserId), IsUnique = true, Name = "ux_main_employees_user_id")]
[Index(nameof(EmployeeCode), IsUnique = true, Name = "ux_main_employees_code")]
[Index(nameof(OfficialEmail), IsUnique = true, Name = "ux_main_employees_official_email")]
[Index(nameof(AadharNumber), IsUnique = true, Name = "ux_main_employees_aadhar")]
[Index(nameof(PanNumber), IsUnique = true, Name = "ux_main_employees_pan")]
public class MainEmployee
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    [Column("role_id")] public int RoleId { get; set; }
    [ForeignKey("RoleId")] public Role Role { get; set; } = null!;

    [Column("employee_code")] public string EmployeeCode { get; set; } = string.Empty;
    [Column("official_email")] public string? OfficialEmail { get; set; }
    [Column("emergency_number")] public string? EmergencyNumber { get; set; }

    [Column("date_of_birth")] public DateOnly? DateOfBirth { get; set; }
    [Column("gender")] public string? Gender { get; set; }
    [Column("blood_group")] public string? BloodGroup { get; set; }
    [Column("marital_status")] public string? MaritalStatus { get; set; }
    [Column("nationality")] public string? Nationality { get; set; }
    [Column("aadhar_number")] public string? AadharNumber { get; set; }
    [Column("pan_number")] public string? PanNumber { get; set; }
    [Column("address")] public string? Address { get; set; }
    [Column("profile_picture")] public string? ProfilePicture { get; set; }

    [Column("highest_qualification")] public string? HighestQualification { get; set; }
    [Column("institution")] public string? Institution { get; set; }
    [Column("year_of_passing")] public int? YearOfPassing { get; set; }

    [Column("work_type")] public string? WorkType { get; set; }

    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}