using hrms.Data;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public sealed record CheckinRow(
    string Date, int UserId, string Code, string Name, string? Role,
    string From, string To, string Status, DateTime? At, double? Lat, double? Lng);

[Route("api/company/attendance/checkins")]
public class AttendanceCheckinReportController : CompanyBaseController
{
    private sealed record P(int UserId, string Code, string Name, string? Role);

    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;
    private readonly PermissionService _perm;

    public AttendanceCheckinReportController(AppDbContext db, AttendanceRules rules, PermissionService perm)
    {
        _db = db;
        _rules = rules;
        _perm = perm;
    }

    private async Task<bool> Can() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("attendance.report");

    // one row per person, per day they were present, per check-in window (done / missed / open / upcoming)
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        if (!await Can()) return Forbid();

        var nowIst = Ist.Now;
        var today = DateOnly.FromDateTime(nowIst);
        var nowMin = (int)Math.Floor(nowIst.TimeOfDay.TotalMinutes);
        var to = dateTo == null || dateTo > today ? today : dateTo.Value;
        var from = dateFrom ?? new DateOnly(to.Year, to.Month, 1);

        if (from > to) return BadRequest(new { message = "From date is after To date." });
        if (to.DayNumber - from.DayNumber + 1 > 93) return BadRequest(new { message = "Max 93 days at a time." });

        var cid = CurrentCompanyId;
        var forUser = await _rules.BookAsync(cid);

        var scope = await ReportScope.EmployeeIdsAsync(_db, IsCompanyOwner, CurrentUserId, cid, CurrentEmployeeId, true);
        var empQ = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid && e.IsActive);
        if (scope != null) empQ = empQ.Where(e => scope.Contains(e.Id));

        var people = await empQ.OrderBy(e => e.User.Name)
            .Select(e => new P(e.UserId, e.EmployeeCode, e.User.Name, e.CompanyRole != null ? e.CompanyRole.Name : null))
            .ToListAsync();

        if (IsCompanyOwner)
        {
            var owner = await _db.Companies.AsNoTracking().Where(c => c.Id == cid)
                .Select(c => new P(c.UserId, "OWNER", c.User.Name, "Owner")).FirstAsync();
            people.Insert(0, owner);
        }

        var present = (await _db.Attendances.AsNoTracking()
                .Where(a => a.CompanyId == cid && a.Date >= from && a.Date <= to && a.Status == "present")
                .Select(a => new { a.UserId, a.Date })
                .ToListAsync())
            .Select(a => (a.UserId, a.Date)).ToHashSet();

        var checks = (await _db.AttendanceCheckins.AsNoTracking()
                .Where(c => c.CompanyId == cid && c.Date >= from && c.Date <= to)
                .ToListAsync())
            .ToDictionary(c => (c.UserId, c.Date, c.SlotFrom));

        var rows = new List<CheckinRow>();
        for (var d = to; d >= from; d = d.AddDays(-1))
        {
            var ds = d.ToString("yyyy-MM-dd");
            foreach (var p in people)
            {
                if (!present.Contains((p.UserId, d))) continue;

                foreach (var s in forUser(p.UserId).Periods)
                {
                    checks.TryGetValue((p.UserId, d, s.From), out var c);
                    var f = Ist.Mins(s.From);
                    var t = Ist.Mins(s.To);

                    var status = c != null ? "done"
                        : d < today || nowMin > t ? "missed"
                        : nowMin >= f ? "open"
                        : "upcoming";

                    rows.Add(new CheckinRow(ds, p.UserId, p.Code, p.Name, p.Role, s.From, s.To, status, c?.At, c?.Lat, c?.Lng));
                }
            }
        }

        return Ok(new { dateFrom = from, dateTo = to, rows });
    }
}