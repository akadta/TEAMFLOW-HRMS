using hrms.Data;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public sealed record ReportRow(
    string Date, int UserId, string Code, string Name, string? Role,
    string Status, string? Note, DateTime? CheckInAt, int LateMinutes, string? Source,
    double? Lat, double? Lng, string? PhotoPath);

[Route("api/company/attendance/report")]
public class AttendanceReportController : CompanyBaseController
{
    private sealed record P(int UserId, string Code, string Name, string? Role, DateTime Joined);

    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;
    private readonly PermissionService _perm;

    public AttendanceReportController(AppDbContext db, AttendanceRules rules, PermissionService perm)
    {
        _db = db;
        _rules = rules;
        _perm = perm;
    }

    private async Task<bool> Can() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("attendance.report");

    // stored rows (present / absent / holiday) are used as they are.
    // a day with no stored row yet (older data, server was down) is derived the same way.
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        if (!await Can()) return Forbid();

        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var to = dateTo == null || dateTo > today ? today : dateTo.Value;
        var from = dateFrom ?? new DateOnly(to.Year, to.Month, 1);

        if (from > to) return BadRequest(new { message = "From date is after To date." });
        var days = to.DayNumber - from.DayNumber + 1;
        if (days > 93) return BadRequest(new { message = "Max 93 days at a time." });

        var cid = CurrentCompanyId;
        var rs = await _rules.LoadAsync(cid);

        var scope = await ReportScope.EmployeeIdsAsync(_db, IsCompanyOwner, CurrentUserId, cid, CurrentEmployeeId, true);

        var empQ = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid && e.IsActive);
        if (scope != null) empQ = empQ.Where(e => scope.Contains(e.Id));

        var people = await empQ
            .OrderBy(e => e.User.Name)
            .Select(e => new P(e.UserId, e.EmployeeCode, e.User.Name,
                e.CompanyRole != null ? e.CompanyRole.Name : null, e.CreatedAt))
            .ToListAsync();

        var ownerUid = -1;
        if (rs.OwnerAttends && IsCompanyOwner)
        {
            var owner = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == cid)
                .Select(c => new P(c.UserId, "OWNER", c.User.Name, "Owner", c.CreatedAt))
                .FirstAsync();
            people.Insert(0, owner);
            ownerUid = owner.UserId;
        }

        if ((long)people.Count * days > 40000)
            return BadRequest(new { message = "Too many rows. Choose a shorter date range." });

        var att = (await _db.Attendances.AsNoTracking()
                .Where(a => a.CompanyId == cid && a.Date >= from && a.Date <= to)
                .ToListAsync())
            .ToDictionary(a => (a.UserId, a.Date));

        var reqs = (await _db.AttendanceRequests.AsNoTracking()
                .Where(r => r.CompanyId == cid && r.Date >= from && r.Date <= to && r.Status != "approved")
                .ToListAsync())
            .GroupBy(r => (r.UserId, r.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var todayPhase = rs.PhaseAt(today, nowIst).Name;

        var rows = new List<ReportRow>();
        for (var d = to; d >= from; d = d.AddDays(-1))
        {
            var plan = rs.Plan(d);
            var ds = d.ToString("yyyy-MM-dd");

            foreach (var p in people)
            {
                if (p.UserId == ownerUid && !rs.OwnerOn(d)) continue;
                if (d < DateOnly.FromDateTime(Ist.Of(p.Joined))) continue;

                att.TryGetValue((p.UserId, d), out var a);
                reqs.TryGetValue((p.UserId, d), out var rq);

                string status;
                string? note = null;

                if (a is { Status: "present" }) { status = "present"; note = a.Note; }
                else if (rq?.Status == "pending") status = "pending";
                else if (a != null)
                {
                    status = a.Status;
                    note = a.Note ?? (rq?.Status == "rejected" ? "Request rejected" : null);
                }
                else if (!plan.Configured) status = "na";
                else if (plan.HolidayName != null) { status = "holiday"; note = plan.HolidayName; }
                else if (plan.Off) { status = "holiday"; note = "Weekly off"; }
                else if (d == today && todayPhase is "early" or "open" or "request") status = "notyet";
                else { status = "absent"; if (rq?.Status == "rejected") note = "Request rejected"; }

                var present = a is { Status: "present" };
                rows.Add(new ReportRow(ds, p.UserId, p.Code, p.Name, p.Role, status, note,
                    present ? a!.CheckInAt : null, present ? a!.LateMinutes : 0, present ? a!.Source : null,
                    present ? a!.Lat : null, present ? a!.Lng : null, present ? a!.PhotoPath : null));
            }
        }

        return Ok(new { dateFrom = from, dateTo = to, rows });
    }
}