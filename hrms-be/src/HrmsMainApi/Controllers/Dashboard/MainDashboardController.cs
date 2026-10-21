using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/dashboard")]
public class MainDashboardController : BaseController
{
    // null lists = super admin (sees everything)
    private sealed record Scope(List<int>? Companies, List<int>? Users);

    private readonly AppDbContext _db;

    public MainDashboardController(AppDbContext db) => _db = db;

    private static DateOnly[] Days(DateOnly today, int n) =>
        Enumerable.Range(0, n).Select(i => today.AddDays(i - n + 1)).ToArray();

    private async Task<Scope> ScopeAsync()
    {
        var super = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == CurrentUserId && u.MainRole != null && u.MainRole.IsActive);
        if (super) return new Scope(null, null);

        var users = (await ReportingScope.VisibleEmployeeUserIdsAsync(_db, CurrentUserId)).ToList();
        var byOwner = await ReportingScope.VisibleCompanyIdsAsync(_db, CurrentUserId);
        var byEmp = await _db.Employees.AsNoTracking().Where(e => users.Contains(e.UserId))
            .Select(e => e.CompanyId).Distinct().ToListAsync();
        return new Scope(byOwner.Union(byEmp).ToList(), users);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? days)
    {
        var sc = await ScopeAsync();
        var n = Math.Clamp(days ?? 14, 7, 31);
        var today = Ist.Today;
        var from = today.AddDays(-(n - 1));
        var nowUtc = DateTime.UtcNow;
        var fromUtc = Ist.ToUtc(from, 0);

        IQueryable<Company> Cos()
        {
            var q = _db.Companies.AsNoTracking().AsQueryable();
            if (sc.Companies != null) q = q.Where(c => sc.Companies!.Contains(c.Id));
            return q;
        }

        IQueryable<Employee> Emps()
        {
            var q = _db.Employees.AsNoTracking().Where(e => e.IsActive);
            if (sc.Users != null) q = q.Where(e => sc.Users!.Contains(e.UserId));
            return q;
        }

        IQueryable<Attendance> Att()
        {
            var q = _db.Attendances.AsNoTracking().Where(a => a.Date >= from && a.Date <= today && a.EmployeeId != null);
            if (sc.Users != null) q = q.Where(a => sc.Users!.Contains(a.UserId));
            return q;
        }

        IQueryable<WorkTask> Tasks()
        {
            var q = _db.WorkTasks.AsNoTracking().Where(t => t.Kind != "self");
            if (sc.Users != null) q = q.Where(t => sc.Users!.Contains(t.UserId));
            return q;
        }

        IQueryable<LeaveRequest> Leaves()
        {
            var q = _db.LeaveRequests.AsNoTracking().AsQueryable();
            if (sc.Users != null) q = q.Where(r => sc.Users!.Contains(r.UserId));
            return q;
        }

        var companiesTotal = await Cos().CountAsync();
        var companiesActive = await Cos().CountAsync(c => c.IsActive);
        var people = await Emps().CountAsync();

        // attendance per day
        var grp = await Att().GroupBy(a => new { a.Date, a.Status })
            .Select(g => new { g.Key.Date, g.Key.Status, N = g.Count() }).ToListAsync();
        var trend = Days(today, n).Select(d =>
        {
            var rs = grp.Where(g => g.Date == d).ToList();
            int Of(string s) => rs.Where(r => r.Status == s).Sum(r => r.N);
            return new { date = d.ToString("yyyy-MM-dd"), present = Of("present"), absent = Of("absent"), leave = Of("leave"), holiday = Of("holiday") };
        }).ToList();

        // today per company (top 10 by size)
        var perCoAtt = await Att().Where(a => a.Date == today).GroupBy(a => a.CompanyId)
            .Select(g => new
            {
                C = g.Key,
                Present = g.Count(a => a.Status == "present"),
                Absent = g.Count(a => a.Status == "absent"),
                Leave = g.Count(a => a.Status == "leave")
            }).ToListAsync();
        var perCoPeople = await Emps().GroupBy(e => e.CompanyId).Select(g => new { C = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N).Take(10).ToListAsync();
        var cids = perCoPeople.Select(x => x.C).ToList();
        var names = await _db.Companies.AsNoTracking().Where(c => cids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        var byCompany = perCoPeople.Select(x =>
        {
            var a = perCoAtt.FirstOrDefault(y => y.C == x.C);
            return new { name = names.GetValueOrDefault(x.C) ?? "—", people = x.N, present = a?.Present ?? 0, absent = a?.Absent ?? 0, leave = a?.Leave ?? 0 };
        }).ToList();

        // leave
        var since = nowUtc.AddDays(-90);
        var leaveByStatus = (await Leaves().Where(r => r.CreatedAt >= since).GroupBy(r => r.Status)
                .Select(g => new { S = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.S, x => x.N);
        var leavePending = await Leaves().CountAsync(r => r.Status == "pending");

        var ld = _db.LeaveDays.AsNoTracking()
            .Where(d => d.Request.Status == "approved" && d.Date >= today && d.Date <= today.AddDays(6));
        if (sc.Users != null) ld = ld.Where(d => sc.Users!.Contains(d.UserId));
        var leave7 = (await ld.GroupBy(d => d.Date).Select(g => new { D = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.D, x => x.N);

        var latest = await Leaves().Where(r => r.Status == "pending").OrderByDescending(r => r.Id).Take(6)
            .Select(r => new
            {
                r.Id,
                Name = r.User.Name,
                Company = _db.Companies.Where(c => c.Id == r.CompanyId).Select(c => c.Name).FirstOrDefault(),
                Days = r.Days.Count,
                r.CreatedAt
            }).ToListAsync();

        // tasks
        var tq = Tasks();
        var byStatus = (await tq.GroupBy(t => t.Status).Select(g => new { S = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.S, x => x.N);
        var overdue = await tq.CountAsync(t => t.DueAt != null && t.DueAt < nowUtc && t.Status != "done");
        var waiting = await tq.CountAsync(t => t.Approval == "pending");

        var created = (await tq.Where(t => t.CreatedAt >= fromUtc).GroupBy(t => t.CreatedAt.AddMinutes(330).Date)
                .Select(g => new { D = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => DateOnly.FromDateTime(x.D), x => x.N);
        var done = (await tq.Where(t => t.CompletedAt != null && t.CompletedAt >= fromUtc)
                .GroupBy(t => t.CompletedAt!.Value.AddMinutes(330).Date)
                .Select(g => new { D = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => DateOnly.FromDateTime(x.D), x => x.N);

        var overdueList = await tq.Where(t => t.DueAt != null && t.DueAt < nowUtc && t.Status != "done")
            .OrderBy(t => t.DueAt).Take(6)
            .Select(t => new
            {
                t.Id,
                t.Title,
                Name = t.User.Name,
                Company = _db.Companies.Where(c => c.Id == t.CompanyId).Select(c => c.Name).FirstOrDefault(),
                t.DueAt,
                t.Progress
            }).ToListAsync();

        var awaiting = await _db.TaskAssignments.CountAsync(a =>
            a.Status == "awaiting_owner" && (sc.Companies == null || sc.Companies.Contains(a.CompanyId)));

        return Ok(new
        {
            companies = new { total = companiesTotal, active = companiesActive },
            people,
            trend,
            byCompany,
            leave = new
            {
                pending = leavePending,
                byStatus = leaveByStatus,
                next7 = Days(today.AddDays(6), 7).Select(d => new { date = d.ToString("yyyy-MM-dd"), n = leave7.GetValueOrDefault(d) }).ToList(),
                latest
            },
            tasks = new
            {
                byStatus,
                overdue,
                waiting,
                awaitingOwner = awaiting,
                trend = Days(today, n).Select(d => new { date = d.ToString("yyyy-MM-dd"), created = created.GetValueOrDefault(d), done = done.GetValueOrDefault(d) }).ToList(),
                overdueList
            }
        });
    }
}