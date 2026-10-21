using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("permission_headings")]
[Index(nameof(Slug), IsUnique = true, Name = "ux_permission_headings_slug")]
public class PermissionHeading
{
    [Column("id")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Permission> Permissions { get; set; } = new();
}

[Table("permissions")]
[Index(nameof(Slug), IsUnique = true, Name = "ux_permissions_slug")]
public class Permission
{
    [Column("id")] public int Id { get; set; }

    [Column("permission_heading_id")] public int PermissionHeadingId { get; set; }
    [ForeignKey("PermissionHeadingId")] public PermissionHeading Heading { get; set; } = null!;

    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("employee_permissions")]
[Index(nameof(EmployeeId), nameof(PermissionId), IsUnique = true, Name = "ux_employee_permissions")]
public class EmployeePermission
{
    [Column("id")] public int Id { get; set; }

    [Column("employee_id")] public int EmployeeId { get; set; }
    [ForeignKey("EmployeeId")] public Employee Employee { get; set; } = null!;

    [Column("permission_id")] public int PermissionId { get; set; }
    [ForeignKey("PermissionId")] public Permission Permission { get; set; } = null!;

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}