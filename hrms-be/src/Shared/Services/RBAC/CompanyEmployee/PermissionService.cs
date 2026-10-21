using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public class PermissionService
{
    private readonly AppDbContext _db;
    public PermissionService(AppDbContext db) => _db = db;

    // owner = everything, employee = only what was assigned
    public async Task<List<string>> SlugsAsync(bool isOwner, int? employeeId)
    {
        if (isOwner)
            return await _db.Permissions.AsNoTracking().Where(p => p.IsActive).Select(p => p.Slug).ToListAsync();

        if (employeeId == null) return new();

        return await _db.EmployeePermissions.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.Employee.IsActive && x.Permission.IsActive)
            .Select(x => x.Permission.Slug)
            .ToListAsync();
    }

    public Task<bool> HasAnyAsync(int employeeId, string[] slugs) =>
        _db.EmployeePermissions.AnyAsync(x =>
            x.EmployeeId == employeeId &&
            x.Employee.IsActive &&
            x.Permission.IsActive &&
            slugs.Contains(x.Permission.Slug));
}