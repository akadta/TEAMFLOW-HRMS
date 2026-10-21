using hrms.Data;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public static class CompanyReportingRules
{
    public const int MaxManagers = 10;

    /// Would "managerId manages userId" create a loop? Walks UP from the manager.
    /// Every employee also reports to the owner of their company, so that link counts too.
    public static async Task<bool> WouldLoopAsync(AppDbContext db, int userId, int managerId)
    {
        if (userId == managerId) return true;

        var seen = new HashSet<int> { managerId };
        var frontier = new List<int> { managerId };

        while (frontier.Count > 0)
        {
            var next = await db.ReportingManagers.AsNoTracking()
                .Where(r => frontier.Contains(r.UserId))
                .Select(r => r.ManagerUserId)
                .ToListAsync();

            next.AddRange(await db.Employees.AsNoTracking()
                .Where(e => frontier.Contains(e.UserId))
                .Select(e => e.Company.UserId)
                .ToListAsync());

            frontier = new List<int>();
            foreach (var id in next)
            {
                if (id == userId) return true;
                if (seen.Add(id)) frontier.Add(id);
            }
        }

        return false;
    }

    /// Managers being ADDED must be active employees of this company.
    /// userId = null for a brand-new employee.
    public static async Task<string?> ValidateAsync(AppDbContext db, int? userId, List<int> managerIds, int companyId)
    {
        if (managerIds.Count > MaxManagers) return $"Max {MaxManagers} reporting managers.";

        foreach (var mid in managerIds)
        {
            if (userId == mid) return "An employee cannot report to themselves.";

            var ok = await db.Employees.AnyAsync(e => e.UserId == mid && e.CompanyId == companyId && e.IsActive);
            if (!ok) return "Reporting manager must be an active employee of this company.";

            if (userId != null && await WouldLoopAsync(db, userId.Value, mid))
                return "This would create a reporting loop.";
        }

        return null;
    }
}