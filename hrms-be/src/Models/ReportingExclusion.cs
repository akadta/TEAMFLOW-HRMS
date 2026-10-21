using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

/// "ManagerUserId (a main employee) must NOT get automatic access to UserId (a company employee)"
[Table("reporting_exclusions")]
[Index(nameof(UserId), nameof(ManagerUserId), IsUnique = true, Name = "ux_reporting_exclusions_pair")]
[Index(nameof(ManagerUserId), Name = "ix_reporting_exclusions_manager")]
public class ReportingExclusion
{
    [Column("id")] public int Id { get; set; }

    [Column("user_id")] public int UserId { get; set; }
    [ForeignKey("UserId")] public User User { get; set; } = null!;

    [Column("manager_user_id")] public int ManagerUserId { get; set; }
    [ForeignKey("ManagerUserId")] public User ManagerUser { get; set; } = null!;

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}