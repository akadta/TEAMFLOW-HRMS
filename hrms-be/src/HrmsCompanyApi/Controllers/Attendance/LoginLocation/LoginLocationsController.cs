using hrms.Data;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public sealed record LoginRow(DateTime At, int UserId, string Code, string Name, string? Role, double Lat, double Lng, double? Accuracy);

[Route("api/company/attendance/logins")]
public class LoginLocationsController : CompanyBaseController
{
    private sealed record P(int UserId, string Code, string Name, string? Role);

    private readonly AppDbContext _db;
    private readonly PermissionService _perm;

    public LoginLocationsController(AppDbContext db, PermissionService perm)
    {
        _db = db;
        _perm = perm;
    }

    private async Task<bool> Can() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("attendance.report");

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        if (!await Can()) return Forbid();

        var today = Ist.Today;
        var to = dateTo == null || dateTo > today ? today : dateTo.Value;
        var from = dateFrom ?? to.AddDays(-6);
        if (from > to) return BadRequest(new { message = "From date is after To date." });
        if (to.DayNumber - from.DayNumber + 1 > 93) return BadRequest(new { message = "Max 93 days at a time." });

        var cid = CurrentCompanyId;
        var scope = await ReportScope.EmployeeIdsAsync(_db, IsCompanyOwner, CurrentUserId, cid, CurrentEmployeeId, true);

        var empQ = _db.Employees.AsNoTracking().Where(e => e.CompanyId == cid);
        if (scope != null) empQ = empQ.Where(e => scope.Contains(e.Id));

        var people = await empQ
            .Select(e => new P(e.UserId, e.EmployeeCode, e.User.Name, e.CompanyRole != null ? e.CompanyRole.Name : null))
            .ToListAsync();

        if (IsCompanyOwner)
        {
            var owner = await _db.Companies.AsNoTracking().Where(c => c.Id == cid)
                .Select(c => new P(c.UserId, "OWNER", c.User.Name, "Owner")).FirstAsync();
            people.Add(owner);
        }

        var uids = people.Select(p => p.UserId).ToList();
        var map = people.ToDictionary(p => p.UserId);
        var fromUtc = Ist.ToUtc(from, 0);
        var toUtc = Ist.ToUtc(to.AddDays(1), 0);

        var logs = await _db.LoginLocations.AsNoTracking()
            .Where(l => l.CompanyId == cid && uids.Contains(l.UserId) && l.At >= fromUtc && l.At < toUtc)
            .OrderByDescending(l => l.At).Take(5000)
            .ToListAsync();

        var rows = logs.Select(l =>
        {
            var p = map[l.UserId];
            return new LoginRow(l.At, l.UserId, p.Code, p.Name, p.Role, l.Lat, l.Lng, l.Accuracy);
        }).ToList();

        return Ok(new { dateFrom = from, dateTo = to, rows });
    }
}