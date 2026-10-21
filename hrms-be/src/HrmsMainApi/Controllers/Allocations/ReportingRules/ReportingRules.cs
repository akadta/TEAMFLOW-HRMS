using hrms.Data;
using hrms.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public static class ReportingRules
{
    public const int MaxManagers = 10;

    // anyone who can sit in the hierarchy: company employee, company owner or main team member
    public static async Task<bool> IsParticipantAsync(AppDbContext db, int userId) =>
        await db.Employees.AnyAsync(e => e.UserId == userId)
        || await db.Companies.AnyAsync(c => c.UserId == userId)
        || await db.MainEmployees.AnyAsync(m => m.UserId == userId);

    public static Task<bool> IsMainAsync(AppDbContext db, int userId) =>
        db.MainEmployees.AnyAsync(m => m.UserId == userId);

    /// Would "managerId manages userId" create a loop? Walks UP from the manager.
    /// Every company employee also reports to the owner of their company, so that link counts too.
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

    /// Validates managers being ADDED. userId = null for a brand-new person.
    /// Main team members can only report to other main team members.
    public static async Task<string?> ValidateAsync(AppDbContext db, int? userId, List<int> managerIds, bool personIsMain = false)
    {
        if (managerIds.Count > MaxManagers) return $"Max {MaxManagers} reporting managers.";

        // a company owner can never report to someone inside their own company
        int? ownedCompanyId = null;
        if (userId != null)
            ownedCompanyId = await db.Companies.Where(c => c.UserId == userId)
                .Select(c => (int?)c.Id).FirstOrDefaultAsync();

        foreach (var mid in managerIds)
        {
            if (userId == mid) return "A person cannot report to themselves.";

            if (ownedCompanyId != null &&
                await db.Employees.AnyAsync(e => e.UserId == mid && e.CompanyId == ownedCompanyId))
                return "A company owner cannot report to an employee of their own company.";

            var isMain = await db.MainEmployees.AnyAsync(m => m.UserId == mid && m.IsActive);

            if (personIsMain)
            {
                if (!isMain) return "A main team member can only report to another main team member.";
            }
            else
            {
                var ok = isMain
                    || await db.Employees.AnyAsync(e => e.UserId == mid && e.IsActive)
                    || await db.Companies.AnyAsync(c => c.UserId == mid && c.IsActive);
                if (!ok) return "Reporting manager not found or inactive.";
            }

            if (userId != null && await WouldLoopAsync(db, userId.Value, mid))
                return "This would create a reporting loop.";
        }

        return null;
    }

    /// Makes the stored links equal `desired`. Does not save.
    public static async Task SyncAsync(AppDbContext db, int userId, List<int> desired)
    {
        var existing = await db.ReportingManagers.Where(r => r.UserId == userId).ToListAsync();

        foreach (var r in existing.Where(r => !desired.Contains(r.ManagerUserId)))
            db.ReportingManagers.Remove(r);

        foreach (var mid in desired.Where(m => existing.All(r => r.ManagerUserId != m)))
            db.ReportingManagers.Add(new ReportingManager { UserId = userId, ManagerUserId = mid });
    }
}

/// FUTURE USE (not wired anywhere yet).
/// If main employee Y is a manager (directly or higher up) of company owner X, Y can see company X.
/// FUTURE USE (not wired anywhere yet).
/// A main employee Y sees every employee of the companies whose owner reports to Y,
/// except employees excluded for Y. A main employee above Y sees whatever Y sees.
public static class ReportingScope
{
    // this main employee plus every main employee below them (main -> main links)
    private static async Task<List<int>> MainTeamBelowAsync(AppDbContext db, int mainUserId)
    {
        var mains = new HashSet<int> { mainUserId };
        var frontier = new List<int> { mainUserId };

        while (frontier.Count > 0)
        {
            var next = await db.ReportingManagers.AsNoTracking()
                .Where(r => frontier.Contains(r.ManagerUserId) &&
                            db.MainEmployees.Any(m => m.UserId == r.UserId))
                .Select(r => r.UserId)
                .ToListAsync();

            frontier = next.Where(mains.Add).ToList();
        }

        return mains.ToList();
    }

    /// company employees (user ids) this main employee can see
    public static async Task<HashSet<int>> VisibleEmployeeUserIdsAsync(AppDbContext db, int mainUserId)
    {
        var visible = new HashSet<int>();

        foreach (var z in await MainTeamBelowAsync(db, mainUserId))
        {
            // the whole company of every owner z manages, minus the exclusions made for z
            visible.UnionWith(await db.Employees.AsNoTracking()
                .Where(e => db.ReportingManagers.Any(r => r.ManagerUserId == z && r.UserId == e.Company.UserId) &&
                            !db.ReportingExclusions.Any(x => x.UserId == e.UserId && x.ManagerUserId == z))
                .Select(e => e.UserId)
                .ToListAsync());

            // plus anyone linked to z directly
            visible.UnionWith(await db.ReportingManagers.AsNoTracking()
                .Where(r => r.ManagerUserId == z)
                .Select(r => r.UserId)
                .ToListAsync());
        }

        return visible;
    }

    /// companies whose owner reports to this main employee (or to a main employee below them)
    public static async Task<List<int>> VisibleCompanyIdsAsync(AppDbContext db, int mainUserId)
    {
        var mains = await MainTeamBelowAsync(db, mainUserId);

        return await db.Companies.AsNoTracking()
            .Where(c => db.ReportingManagers.Any(r => r.UserId == c.UserId && mains.Contains(r.ManagerUserId)))
            .Select(c => c.Id)
            .ToListAsync();
    }

}