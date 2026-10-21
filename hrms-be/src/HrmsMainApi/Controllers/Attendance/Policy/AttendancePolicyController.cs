using System.Text.Json;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record PolicyInput(
    bool Active, string? TimeFrom, string? TimeTo, string? GraceUntil, List<DayRule>? Rules, List<int>? OwnerDays,
    bool RequireLocation, bool RequirePhoto, bool LoginLocation, List<PeriodSlot>? Periods);

[ApiController]
[Authorize]
[Route("api/main/attendance-policy")]
public class AttendancePolicyController : BaseController
{
    private static readonly Regex TimeRx = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);
    private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;

    public AttendancePolicyController(AppDbContext db) => _db = db;

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private static bool GraceOk(string f, string t, string g) =>
        TimeRx.IsMatch(g) && Ist.Mins(g) > Ist.Mins(f) && Ist.Mins(g) < Ist.Mins(t);

    private async Task<IActionResult?> CheckScope(int? companyId, int? userId)
    {
        if (userId != null && companyId == null) return Bad("Choose a company.");
        if (companyId != null && !await _db.Companies.AnyAsync(c => c.Id == companyId))
            return NotFound(new { message = "Company not found." });
        if (userId != null && !await _db.Employees.AnyAsync(e => e.UserId == userId && e.CompanyId == companyId))
            return NotFound(new { message = "Employee not found." });
        return null;
    }

    // GET ?companyId=&userId=   (none = global, companyId = company, both = one employee)
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? companyId, [FromQuery] int? userId)
    {
        var deny = await CheckScope(companyId, userId);
        if (deny != null) return deny;

        var s = await _db.AttendanceSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId) ?? new AttendanceSetting();

        return Ok(new
        {
            active = s.TimingActive,
            timeFrom = s.TimeFrom,
            timeTo = s.TimeTo,
            graceUntil = s.GraceUntil,
            rules = JsonSerializer.Deserialize<List<DayRule>>(s.DayRules, Json) ?? new(),
            ownerDays = s.OwnerDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x)).ToList(),
            requireLocation = s.RequireLocation,
            requirePhoto = s.RequirePhoto,
            loginLocation = s.LoginLocation,
            periods = JsonSerializer.Deserialize<List<PeriodSlot>>(s.Periods, Json) ?? new(),
            hasOwn = s.Id != 0
        });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromQuery] int? companyId, [FromQuery] int? userId, [FromBody] PolicyInput i)
    {
        var deny = await CheckScope(companyId, userId);
        if (deny != null) return deny;

        var from = (i.TimeFrom ?? string.Empty).Trim();
        var to = (i.TimeTo ?? string.Empty).Trim();
        if (!TimeRx.IsMatch(from) || !TimeRx.IsMatch(to)) return Bad("Enter valid From and To time.");
        if (from == to) return Bad("From and To cannot be same.");

        var grace = string.IsNullOrWhiteSpace(i.GraceUntil) ? null : i.GraceUntil.Trim();
        if (grace != null && !GraceOk(from, to, grace)) return Bad("Grace time must be after From and before To.");

        var rules = new List<DayRule>();
        foreach (var r in i.Rules ?? new())
        {
            if (r.Day is < 0 or > 6 || rules.Any(x => x.Day == r.Day)) return Bad("Invalid day rule.");
            if (r.Off) { rules.Add(new DayRule(r.Day, true, null, null, null)); continue; }
            if (r.From == null && r.To == null) continue;

            if (r.From == null || r.To == null || !TimeRx.IsMatch(r.From) || !TimeRx.IsMatch(r.To) || r.From == r.To)
                return Bad($"{DayNames[r.Day]}: invalid time.");

            var g = string.IsNullOrWhiteSpace(r.Grace) ? null : r.Grace.Trim();
            if (g != null && !GraceOk(r.From, r.To, g)) return Bad($"{DayNames[r.Day]}: grace must be after From and before To.");

            rules.Add(new DayRule(r.Day, false, r.From, r.To, g));
        }

        var periods = new List<PeriodSlot>();
        foreach (var p in i.Periods ?? new())
        {
            if (!TimeRx.IsMatch(p.From ?? "") || !TimeRx.IsMatch(p.To ?? "") || Ist.Mins(p.From!) >= Ist.Mins(p.To!))
                return Bad("Check-in window: To must be after From.");
            if (periods.Any(x => x.From == p.From)) return Bad("Two check-in windows start at the same time.");
            periods.Add(p);
        }
        if (periods.Count > 12) return Bad("Max 12 check-in windows.");

        periods = periods.OrderBy(x => x.From).ToList();
        for (var k = 1; k < periods.Count; k++)
            if (Ist.Mins(periods[k].From) < Ist.Mins(periods[k - 1].To))
                return Bad("Check-in windows cannot overlap. Start the next window at or after the previous one ends.");

        var od = userId == null
            ? (i.OwnerDays ?? new()).Where(x => x is >= 0 and <= 6).Distinct().OrderBy(x => x).ToList()
            : new List<int>();

        var s = await _db.AttendanceSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId);
        if (s == null)
        {
            s = new AttendanceSetting { CompanyId = companyId, UserId = userId };
            _db.AttendanceSettings.Add(s);
        }

        s.TimingActive = i.Active;
        s.TimeFrom = from;
        s.TimeTo = to;
        s.GraceUntil = grace;
        s.DayRules = JsonSerializer.Serialize(rules, Json);
        s.OwnerDays = string.Join(',', od);
        s.OwnerAttends = od.Count > 0;
        s.RequireLocation = i.RequireLocation;
        s.RequirePhoto = i.RequirePhoto;
        s.LoginLocation = i.LoginLocation;
        s.Periods = JsonSerializer.Serialize(periods.OrderBy(x => x.From).ToList(), Json);
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    // DELETE ?companyId=&userId=   (employee goes back to the company / global setting)
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int companyId, [FromQuery] int userId)
    {
        var s = await _db.AttendanceSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == userId);
        if (s != null)
        {
            _db.AttendanceSettings.Remove(s);
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    // GET employees?companyId=&roleId=&search=   (picker for employee-wise settings)
    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] int companyId, [FromQuery] int? roleId, [FromQuery] string? search)
    {
        var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == companyId && e.IsActive);
        if (roleId != null) q = q.Where(e => e.CompanyRoleId == roleId);

        var s = (search ?? string.Empty).Trim().ToLower();
        if (s.Length > 0)
            q = q.Where(e => e.User.Name.ToLower().Contains(s) || e.EmployeeCode.ToLower().Contains(s));

        var list = await q.OrderBy(e => e.User.Name).Take(200)
            .Select(e => new
            {
                e.UserId,
                Code = e.EmployeeCode,
                Name = e.User.Name,
                Role = e.CompanyRole != null ? e.CompanyRole.Name : null,
                HasOverride = _db.AttendanceSettings.Any(x => x.CompanyId == companyId && x.UserId == e.UserId),
                OverrideActive = _db.AttendanceSettings.Any(x => x.CompanyId == companyId && x.UserId == e.UserId && x.TimingActive)
            })
            .ToListAsync();

        return Ok(list);
    }
}