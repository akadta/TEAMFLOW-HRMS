using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace hrms.Models;

[Table("main_permission_headings")]
[Index(nameof(Slug), IsUnique = true, Name = "ux_main_permission_headings_slug")]
public class MainPermissionHeading
{
    [Column("id")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<MainPermission> Permissions { get; set; } = new();
}

[Table("main_permissions")]
[Index(nameof(Slug), IsUnique = true, Name = "ux_main_permissions_slug")]
public class MainPermission
{
    [Column("id")] public int Id { get; set; }

    [Column("main_permission_heading_id")] public int MainPermissionHeadingId { get; set; }
    [ForeignKey("MainPermissionHeadingId")] public MainPermissionHeading Heading { get; set; } = null!;

    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("slug")] public string Slug { get; set; } = string.Empty;
    [Column("sort_order")] public int SortOrder { get; set; }
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("main_employee_permissions")]
[Index(nameof(MainEmployeeId), nameof(MainPermissionId), IsUnique = true, Name = "ux_main_employee_permissions")]
public class MainEmployeePermission
{
    [Column("id")] public int Id { get; set; }

    [Column("main_employee_id")] public int MainEmployeeId { get; set; }
    [ForeignKey("MainEmployeeId")] public MainEmployee MainEmployee { get; set; } = null!;

    [Column("main_permission_id")] public int MainPermissionId { get; set; }
    [ForeignKey("MainPermissionId")] public MainPermission MainPermission { get; set; } = null!;

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}