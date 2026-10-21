using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("reporting_managers")]
[Index(nameof(UserId), nameof(ManagerUserId), IsUnique = true, Name = "ux_reporting_managers_pair")]
[Index(nameof(ManagerUserId), Name = "ix_reporting_managers_manager")]
public class ReportingManager
{
    [Column("id")] public int Id { get; set; }

    // the person who reports
    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    // the manager (employee or company owner, any company)
    [Column("manager_user_id")] public int ManagerUserId { get; set; }
    [ForeignKey("ManagerUserId")] public User ManagerUser { get; set; } = null!;

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}