using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.Seeders;

public static class MainPermissionSeeder
{
    private record PermDef(string Name, string Slug);
    private record HeadingDef(string Name, string Slug, List<PermDef> Permissions);

    // ORDER HERE = ORDER IN THE UI (same order as the sidebar menu)
    private static readonly List<HeadingDef> Definition = new()
    {
        // ---------- Company ----------
        new("Company", "company", new()
        {
            new("Register Company", "company.register"),
            new("Manage Company", "company.list"),
            new("Company Roles", "company.roles"),
        }),

        // ---------- Employees ----------
        new("Employees", "employees", new()
        {
            new("Register Employee", "employees.register"),
            new("Manage Employee", "employees.list"),
            new("Edit Employee", "employees.edit"),
        }),

        // ---------- My Companies ----------
        new("My Companies", "my-companies", new()
        {
            new("View My Companies", "my-companies.view"),
            new("View Attendance", "my-attendance.view"),
            new("Leave Requests", "leave.approval"),
            new("View Team Overview", "team.view"),
            new("Assign Tasks to Employees", "tasks.assign"),
            new("Task Approvals", "tasks.approvals"),
        }),

        // ---------- Allocations ----------
        new("Allocations", "allocations", new()
        {
            new("Reporting Managers", "allocations.reporting-managers"),
            new("Hierarchy Tree", "hierarchy.tree"),
        }),

        // ---------- Main Team ----------
        new("Main Team", "main-team", new()
        {
            new("Manage Roles", "roles.manage"),
            new("Register Main Employee", "main-employees.register"),
            new("Manage Main Employees", "main-employees.list"),
            new("Edit Main Employee", "main-employees.edit"),
        }),

        // ---------- Settings (all settings permissions together) ----------
        new("Settings", "settings", new()
        {
            new("Holiday Calendar", "attendance.holidays"),
            new("Attendance Timing", "attendance.timing"),
            new("Leave Settings", "leave.setting"),
            new("Task Settings", "tasks.settings"),
        }),
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        var seenHeadings = new List<int>();
        var seenPerms = new List<int>();

        for (var h = 0; h < Definition.Count; h++)
        {
            var hd = Definition[h];
            var heading = await db.MainPermissionHeadings.FirstOrDefaultAsync(x => x.Slug == hd.Slug);
            if (heading is null)
            {
                heading = new MainPermissionHeading { Name = hd.Name, Slug = hd.Slug, SortOrder = h };
                db.MainPermissionHeadings.Add(heading);
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
                var perm = await db.MainPermissions.FirstOrDefaultAsync(x => x.Slug == pd.Slug);
                if (perm is null)
                {
                    perm = new MainPermission
                    {
                        MainPermissionHeadingId = heading.Id,
                        Name = pd.Name,
                        Slug = pd.Slug,
                        SortOrder = p
                    };
                    db.MainPermissions.Add(perm);
                }
                else
                {
                    perm.MainPermissionHeadingId = heading.Id; // moves existing perms to the new heading
                    perm.Name = pd.Name;
                    perm.SortOrder = p;
                    perm.IsActive = true;
                    perm.UpdatedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync();
                seenPerms.Add(perm.Id);
            }
        }

        var stalePerms = await db.MainPermissions.Where(p => !seenPerms.Contains(p.Id)).ToListAsync();
        if (stalePerms.Count > 0) { db.MainPermissions.RemoveRange(stalePerms); await db.SaveChangesAsync(); }

        var staleHeadings = await db.MainPermissionHeadings.Where(h => !seenHeadings.Contains(h.Id)).ToListAsync();
        if (staleHeadings.Count > 0) { db.MainPermissionHeadings.RemoveRange(staleHeadings); await db.SaveChangesAsync(); }
    }
}