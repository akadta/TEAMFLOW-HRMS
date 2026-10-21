using hrms.Data;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

[Route("api/company/my-attendance")]
public class MyAttendanceController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly AttendanceView _view;

    public MyAttendanceController(AppDbContext db, AttendanceView view)
    {
        _db = db;
        _view = view;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        var today = Ist.Today;
        var to = dateTo == null || dateTo > today ? today : dateTo.Value;
        var from = dateFrom ?? new DateOnly(to.Year, to.Month, 1);
        if (from > to) return BadRequest(new { message = "From date is after To date." });
        if (to.DayNumber - from.DayNumber + 1 > 366) return BadRequest(new { message = "Max 366 days at a time." });

        DateTime joined;
        if (IsCompanyOwner)
        {
            joined = await _db.Companies.AsNoTracking()
                .Where(c => c.Id == CurrentCompanyId).Select(c => c.CreatedAt).FirstAsync();
        }
        else
        {
            var eid = CurrentEmployeeId;
            var j = await _db.Employees.AsNoTracking()
                .Where(e => e.Id == eid && e.CompanyId == CurrentCompanyId)
                .Select(e => (DateTime?)e.CreatedAt).FirstOrDefaultAsync();
            if (j == null) return StatusCode(403, new { message = "Not available for this account." });
            joined = j.Value;
        }

        var uid = CurrentUserId;
        var rows = await _view.DaysAsync(CurrentCompanyId, uid, IsCompanyOwner, joined, from, to);
        var slots = await _view.SlotsAsync(CurrentCompanyId, uid, from, to);

        var fromUtc = Ist.ToUtc(from, 0);
        var toUtc = Ist.ToUtc(to.AddDays(1), 0);
        var logins = await _db.LoginLocations.AsNoTracking()
            .Where(l => l.UserId == uid && l.At >= fromUtc && l.At < toUtc)
            .OrderByDescending(l => l.At).Take(1000)
            .Select(l => new { l.At, l.Lat, l.Lng, l.Accuracy })
            .ToListAsync();

        return Ok(new { dateFrom = from, dateTo = to, rows, slots, logins });
    }
}