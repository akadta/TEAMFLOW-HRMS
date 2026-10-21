using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("employees")]
[Index(nameof(UserId), IsUnique = true, Name = "ux_employees_user_id")]
[Index(nameof(CompanyId), nameof(EmployeeCode), IsUnique = true, Name = "ux_employees_company_code")]
[Index(nameof(OfficialEmail), IsUnique = true, Name = "ux_employees_official_email")]
[Index(nameof(AadharNumber), IsUnique = true, Name = "ux_employees_aadhar")]
[Index(nameof(PanNumber), IsUnique = true, Name = "ux_employees_pan")]
public class Employee
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    [Column("company_id")] public int CompanyId { get; set; }
    [ForeignKey("CompanyId")] public Company Company { get; set; } = null!;

    [Column("company_role_id")] public int? CompanyRoleId { get; set; }
    [ForeignKey("CompanyRoleId")] public CompanyRole? CompanyRole { get; set; }

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

    public List<EmployeeDocument> Documents { get; set; } = new();
    public List<EmployeeHistory> Histories { get; set; } = new();
}

[Table("employee_documents")]
public class EmployeeDocument
{
    [Column("id")] public int Id { get; set; }

    [Column("employee_id")] public int EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee Employee { get; set; } = null!;

    [Column("type")] public string Type { get; set; } = "Other";
    [Column("title")] public string Title { get; set; } = string.Empty;
    [Column("description")] public string? Description { get; set; }
    [Column("file_path")] public string? FilePath { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("employee_histories")]
public class EmployeeHistory
{
    [Column("id")] public int Id { get; set; }

    [Column("employee_id")] public int EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee Employee { get; set; } = null!;

    [Column("employer")] public string Employer { get; set; } = string.Empty;
    [Column("start_date")] public DateOnly? StartDate { get; set; }
    [Column("end_date")] public DateOnly? EndDate { get; set; }
    [Column("reason_for_leaving")] public string? ReasonForLeaving { get; set; }
    [Column("relieving_letter")] public string? RelievingLetter { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}