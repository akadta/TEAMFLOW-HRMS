using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public class MainPermissionService
{
    public const string SuperAdminSlug = "super-admin";

    private readonly AppDbContext _db;
    public MainPermissionService(AppDbContext db) => _db = db;

    // super admin = user linked to an ACTIVE MainRole with slug "super-admin"
    public Task<bool> IsSuperAdminAsync(int userId) =>
        _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == userId &&
            u.MainRole != null &&
            u.MainRole.IsActive &&
            u.MainRole.Slug == SuperAdminSlug);

    public async Task<(bool IsSuper, List<string> Slugs)> LoadAsync(int userId)
    {
        if (await IsSuperAdminAsync(userId))
        {
            var all = await _db.MainPermissions.AsNoTracking()
                .Where(p => p.IsActive && p.Heading.IsActive)
                .Select(p => p.Slug)
                .ToListAsync();
            return (true, all);
        }

        var slugs = await _db.MainEmployeePermissions.AsNoTracking()
            .Where(x => x.MainEmployee.UserId == userId
                     && x.MainEmployee.IsActive
                     && x.MainEmployee.Role.IsActive
                     && x.MainPermission.IsActive)
            .Select(x => x.MainPermission.Slug)
            .ToListAsync();
        return (false, slugs);
    }

    public async Task<bool> HasAnyAsync(int userId, string[] slugs)
    {
        if (await IsSuperAdminAsync(userId)) return true;

        return await _db.MainEmployeePermissions.AnyAsync(x =>
            x.MainEmployee.UserId == userId
            && x.MainEmployee.IsActive
            && x.MainEmployee.Role.IsActive
            && x.MainPermission.IsActive
            && slugs.Contains(x.MainPermission.Slug));
    }
}