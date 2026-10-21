using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

[Route("api/company/dashboard")]
public class CompanyDashboardController : CompanyBaseController
{
    private readonly AppDbContext _db;

    public CompanyDashboardController(AppDbContext db) => _db = db;

    private static int Clamp(int? d, int def) => Math.Clamp(d ?? def, 7, 31);

    private static DateOnly[] Days(DateOnly today, int n) =>
        Enumerable.Range(0, n).Select(i => today.AddDays(i - n + 1)).ToArray();

    // null = whole company (owner), otherwise the direct reports of this manager
    private async Task<List<int>?> ScopeAsync()
    {
        if (IsCompanyOwner) return null;
        var eids = await ReportScope.EmployeeIdsAsync(_db, false, CurrentUserId, CurrentCompanyId, CurrentEmployeeId, false)
                   ?? new List<int>();
        return await _db.Employees.AsNoTracking().Where(e => eids.Contains(e.Id)).Select(e => e.UserId).ToListAsync();
    }

    private static async Task<List<object>> TaskTrendAsync(IQueryable<WorkTask> q, DateOnly today, int n)
    {
        var from = today.AddDays(-(n - 1));
        var fromUtc = Ist.ToUtc(from, 0);

        var created = await q.Where(t => t.CreatedAt >= fromUtc)
            .GroupBy(t => t.CreatedAt.AddMinutes(330).Date)
            .Select(g => new { D = g.Key, N = g.Count() }).ToListAsync();
        var done = await q.Where(t => t.CompletedAt != null && t.CompletedAt >= fromUtc)
            .GroupBy(t => t.CompletedAt!.Value.AddMinutes(330).Date)
            .Select(g => new { D = g.Key, N = g.Count() }).ToListAsync();

        var c = created.ToDictionary(x => DateOnly.FromDateTime(x.D), x => x.N);
        var d = done.ToDictionary(x => DateOnly.FromDateTime(x.D), x => x.N);

        return Days(today, n).Select(day => (object)new
        {
            date = day.ToString("yyyy-MM-dd"),
            created = c.GetValueOrDefault(day),
            done = d.GetValueOrDefault(day)
        }).ToList();
    }

    // GET me?days=30   (everyone: employee or owner)
    [HttpGet("me")]
    public async Task<IActionResult> Me([FromQuery] int? days)
    {
        var uid = CurrentUserId;
        var n = Clamp(days, 30);
        var today = Ist.Today;
        var from = today.AddDays(-(n - 1));
        var nowUtc = DateTime.UtcNow;

        var rows = await _db.Attendances.AsNoTracking()
            .Where(a => a.UserId == uid && a.Date >= from && a.Date <= today)
            .Select(a => new { a.Date, a.Status, a.LateMinutes }).ToListAsync();
        var byDate = rows.ToDictionary(r => r.Date);

        var series = Days(today, n).Select(d =>
        {
            byDate.TryGetValue(d, out var r);
            return new { date = d.ToString("yyyy-MM-dd"), status = r?.Status ?? "none", late = r?.LateMinutes ?? 0 };
        }).ToList();

        var present = rows.Count(r => r.Status == "present");
        var absent = rows.Count(r => r.Status == "absent");
        var leave = rows.Count(r => r.Status == "leave");
        var late = rows.Count(r => r.Status == "present" && r.LateMinutes > 0);
        double? rate = present + absent == 0 ? null : Math.Round(100.0 * present / (present + absent), 1);

        var streak = 0;
        foreach (var d in Days(today, n).OrderByDescending(x => x))
        {
            if (!byDate.TryGetValue(d, out var r)) { if (d == today) continue; break; }
            if (r.Status == "present") streak++;
            else if (r.Status is "holiday" or "leave") continue;
            else break;
        }

        var tq = _db.WorkTasks.AsNoTracking().Where(t => t.UserId == uid);
        var byStatus = (await tq.GroupBy(t => t.Status).Select(g => new { S = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.S, x => x.N);
        var overdue = await tq.CountAsync(t => t.DueAt != null && t.DueAt < nowUtc && t.Status != "done");
        var waiting = await tq.CountAsync(t => t.Approval == "pending");

        var pendingAtt = await _db.AttendanceRequests.CountAsync(r => r.UserId == uid && r.Status == "pending");
        var pendingLeave = await _db.LeaveRequests.CountAsync(r => r.UserId == uid && r.Status == "pending");

        var upcoming = await _db.LeaveDays.AsNoTracking()
            .Where(d => d.UserId == uid && d.Date >= today && d.Request.Status == "approved")
            .OrderBy(d => d.Date).Take(10).Select(d => d.Date).ToListAsync();

        return Ok(new
        {
            days = n,
            present,
            absent,
            leave,
            late,
            rate,
            streak,
            series,
            pending = new { attendance = pendingAtt, leave = pendingLeave },
            upcomingLeave = upcoming.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            tasks = new
            {
                byStatus,
                open = byStatus.GetValueOrDefault("todo") + byStatus.GetValueOrDefault("in_progress") + byStatus.GetValueOrDefault("on_hold"),
                overdue,
                waiting,
                trend = await TaskTrendAsync(tq, today, 14)
            }
        });
    }

    // GET team?days=14   (owner = whole company, manager = direct reports)
    [HttpGet("team")]
    public async Task<IActionResult> Team([FromQuery] int? days)
    {
        var cid = CurrentCompanyId;
        var uids = await ScopeAsync();
        if (uids != null && uids.Count == 0) return Ok(new { available = false });

        var n = Clamp(days, 14);
        var today = Ist.Today;
        var from = today.AddDays(-(n - 1));
        var nowUtc = DateTime.UtcNow;

        IQueryable<Employee> Emps()
        {
            var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid && e.IsActive);
            if (uids != null) q = q.Where(e => uids!.Contains(e.UserId));
            return q;
        }

        IQueryable<Attendance> Att()
        {
            var q = _db.Attendances.AsNoTracking()
                .Where(a => a.CompanyId == cid && a.Date >= from && a.Date <= today && a.EmployeeId != null);
            if (uids != null) q = q.Where(a => uids!.Contains(a.UserId));
            return q;
        }

        IQueryable<WorkTask> Tasks()
        {
            var q = _db.WorkTasks.AsNoTracking().Where(t => t.CompanyId == cid && t.Kind != "self");
            if (uids != null) q = q.Where(t => uids!.Contains(t.UserId));
            return q;
        }

        var people = await Emps().CountAsync();

        // attendance per day (one grouped query)
        var grp = await Att().GroupBy(a => new { a.Date, a.Status })
            .Select(g => new { g.Key.Date, g.Key.Status, N = g.Count(), Late = g.Count(x => x.LateMinutes > 0) })
            .ToListAsync();

        var trend = Days(today, n).Select(d =>
        {
            var rs = grp.Where(g => g.Date == d).ToList();
            int Of(string s) => rs.Where(r => r.Status == s).Sum(r => r.N);
            return new
            {
                date = d.ToString("yyyy-MM-dd"),
                present = Of("present"),
                absent = Of("absent"),
                leave = Of("leave"),
                holiday = Of("holiday"),
                late = rs.Where(r => r.Status == "present").Sum(r => r.Late)
            };
        }).ToList();

        // how late people were over the range: on time / 1-15 / 16-30 / 31-60 / 60+
        var bands = await Att().Where(a => a.Status == "present")
            .GroupBy(a => a.LateMinutes == 0 ? 0 : a.LateMinutes <= 15 ? 1 : a.LateMinutes <= 30 ? 2 : a.LateMinutes <= 60 ? 3 : 4)
            .Select(g => new { B = g.Key, N = g.Count() }).ToListAsync();
        var lateBands = Enumerable.Range(0, 5).Select(i => bands.FirstOrDefault(x => x.B == i)?.N ?? 0).ToList();

        // today per role
        var roleAtt = await Att().Where(a => a.Date == today)
            .GroupBy(a => a.Employee!.CompanyRole != null ? a.Employee.CompanyRole.Name : "No role")
            .Select(g => new
            {
                Role = g.Key,
                Present = g.Count(a => a.Status == "present"),
                Absent = g.Count(a => a.Status == "absent"),
                Leave = g.Count(a => a.Status == "leave")
            }).ToListAsync();
        var rolePeople = await Emps()
            .GroupBy(e => e.CompanyRole != null ? e.CompanyRole.Name : "No role")
            .Select(g => new { Role = g.Key, N = g.Count() }).ToListAsync();
        var byRole = rolePeople.OrderByDescending(r => r.N).Take(10).Select(r =>
        {
            var a = roleAtt.FirstOrDefault(x => x.Role == r.Role);
            return new { role = r.Role, people = r.N, present = a?.Present ?? 0, absent = a?.Absent ?? 0, leave = a?.Leave ?? 0 };
        }).ToList();

        // things waiting
        var reqAtt = _db.AttendanceRequests.AsNoTracking().Where(r => r.CompanyId == cid && r.Status == "pending");
        var reqLeave = _db.LeaveRequests.AsNoTracking().Where(r => r.CompanyId == cid && r.Status == "pending");
        if (uids != null)
        {
            reqAtt = reqAtt.Where(r => uids.Contains(r.UserId));
            reqLeave = reqLeave.Where(r => uids.Contains(r.UserId));
        }

        // leave in the next 7 days + who is away today
        var lq = _db.LeaveDays.AsNoTracking()
            .Where(d => d.Request.CompanyId == cid && d.Request.Status == "approved" && d.Date >= today && d.Date <= today.AddDays(6));
        if (uids != null) lq = lq.Where(d => uids.Contains(d.UserId));
        var leave7 = (await lq.GroupBy(d => d.Date).Select(g => new { D = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.D, x => x.N);
        var onLeaveToday = await lq.Where(d => d.Date == today).Select(d => d.Request.User.Name).Take(8).ToListAsync();

        var absentToday = await Att().Where(a => a.Date == today && a.Status == "absent")
            .Select(a => new { name = a.User.Name, code = a.Employee!.EmployeeCode }).Take(8).ToListAsync();

        // tasks
        var tq = Tasks();
        var byStatus = (await tq.GroupBy(t => t.Status).Select(g => new { S = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.S, x => x.N);
        var overdue = await tq.CountAsync(t => t.DueAt != null && t.DueAt < nowUtc && t.Status != "done");
        var waiting = await tq.CountAsync(t => t.Approval == "pending");
        var avg = await tq.Where(t => t.Status != "done").Select(t => (double?)t.Progress).AverageAsync() ?? 0;
        var overdueList = await tq.Where(t => t.DueAt != null && t.DueAt < nowUtc && t.Status != "done")
            .OrderBy(t => t.DueAt).Take(6)
            .Select(t => new { t.Id, t.Title, Name = t.User.Name, t.DueAt, t.Progress }).ToListAsync();

        return Ok(new
        {
            available = true,
            people,
            trend,
            lateBands,
            byRole,
            requests = new
            {
                attendance = await reqAtt.CountAsync(),
                leave = await reqLeave.CountAsync(),
                taskApprovals = waiting
            },
            leave7 = Days(today.AddDays(6), 7).Select(d => new { date = d.ToString("yyyy-MM-dd"), n = leave7.GetValueOrDefault(d) }).ToList(),
            onLeaveToday,
            absentToday,
            tasks = new
            {
                byStatus,
                overdue,
                waiting,
                avgProgress = Math.Round(avg),
                trend = await TaskTrendAsync(tq, today, 14),
                overdueList
            }
        });
    }
}