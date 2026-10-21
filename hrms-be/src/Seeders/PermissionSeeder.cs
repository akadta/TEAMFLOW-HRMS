using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Seeders;

public static class PermissionSeeder
{
    private record PermDef(string Name, string Slug);
    private record HeadingDef(string Name, string Slug, List<PermDef> Permissions);

    // ORDER HERE = ORDER IN THE UI
    private static readonly List<HeadingDef> Definition = new()
{
    new("Team", "team", new()
    {
        new("View Team Overview", "team.view"),
    }),
    new("Tasks", "tasks", new()
    {
        new("Assign tasks to employees", "tasks.assign"),
    }),
    new("Employees", "employees", new()
    {
        new("Register Employee", "employees.register"),
        new("View Employee List", "employees.list"),
        new("Edit Employee", "employees.edit"),
    }),
    new("Roles and Access Control", "roles", new()
    {
        new("Employee Permissions", "role.permission"),
        new("Manage roles", "company.role"),
    }),
    new("Attendance", "attendance", new()
    {
        new("Approve attendance (For reporting manager)", "attendance.approval"),
        new("View attendance reports, check-ins and login locations", "attendance.report"),
        new("Approve leave requests", "leave.approval"),
    }),
};

    public static async Task SeedAsync(AppDbContext db)
    {
        var seenHeadings = new List<int>();
        var seenPerms = new List<int>();

        for (var h = 0; h < Definition.Count; h++)
        {
            var hd = Definition[h];
            var heading = await db.PermissionHeadings.FirstOrDefaultAsync(x => x.Slug == hd.Slug);
            if (heading is null)
            {
                heading = new PermissionHeading { Name = hd.Name, Slug = hd.Slug, SortOrder = h };
                db.PermissionHeadings.Add(heading);
                await db.SaveChangesAsync();
            }
            else
            {
                heading.Name = hd.Name;
                heading.SortOrder = h;
                heading.IsActive = true;
                heading.UpdatedAt = DateTime.UtcNow;
            }
            seenHeadings.Add(heading.Id);

            for (var p = 0; p < hd.Permissions.Count; p++)
            {
                var pd = hd.Permissions[p];
                var perm = await db.Permissions.FirstOrDefaultAsync(x => x.Slug == pd.Slug);
                if (perm is null)
                {
                    perm = new Permission
                    {
                        PermissionHeadingId = heading.Id,
                        Name = pd.Name,
                        Slug = pd.Slug,
                        SortOrder = p
                    };
                    db.Permissions.Add(perm);
                }
                else
                {
                    perm.PermissionHeadingId = heading.Id;
                    perm.Name = pd.Name;
                    perm.SortOrder = p;
                    perm.IsActive = true;
                    perm.UpdatedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync();
                seenPerms.Add(perm.Id);
            }
        }

        var stalePerms = await db.Permissions.Where(p => !seenPerms.Contains(p.Id)).ToListAsync();
        if (stalePerms.Count > 0) { db.Permissions.RemoveRange(stalePerms); await db.SaveChangesAsync(); }

        var staleHeadings = await db.PermissionHeadings.Where(h => !seenHeadings.Contains(h.Id)).ToListAsync();
        if (staleHeadings.Count > 0) { db.PermissionHeadings.RemoveRange(staleHeadings); await db.SaveChangesAsync(); }
    }
}