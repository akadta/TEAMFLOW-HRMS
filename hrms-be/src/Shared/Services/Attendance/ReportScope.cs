using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.Shared.Services;

public static class ReportScope
{
    // null = no limit (company owner). Otherwise the employee ids this user may see:
    // direct reports (ReportingManagers.ManagerUserId == userId), plus themselves if includeSelf.
    public static async Task<List<int>?> EmployeeIdsAsync(
        AppDbContext db, bool isOwner, int userId, int companyId, int? selfEmployeeId, bool includeSelf)
    {
        if (isOwner) return null;

        var ids = await db.ReportingManagers.AsNoTracking()
            .Where(r => r.ManagerUserId == userId)
            .Join(db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId && e.IsActive),
                r => r.UserId, e => e.UserId, (r, e) => e.Id)
            .ToListAsync();

        if (includeSelf && selfEmployeeId is { } me) ids.Add(me);

        return ids.Distinct().ToList();
    }
}