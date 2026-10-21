using System.Text.Json;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record HolidayInput(int? CompanyId, string? Name, string? Kind, DateOnly? Date, bool Yearly, int? Weekday, string? Nth, string? Color);
public record DayRule(int Day, bool Off, string? From, string? To, string? Grace = null);
public record HolidaysActiveInput(bool Active);
public record TimingInput(bool Active, string? TimeFrom, string? TimeTo, string? GraceUntil, List<DayRule>? Rules, List<int>? OwnerDays = null);

[ApiController]
[Authorize]
[Route("api/main/attendance")]
public class AttendanceSettingsController : BaseController
{
    private readonly AppDbContext _db;

    public AttendanceSettingsController(AppDbContext db) => _db = db;

    private static readonly Regex TimeRx = new(@"^([01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);
    private static readonly Regex ColorRx = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly string[] Kinds = { "date", "weekly", "nth" };
    private static readonly string[] Days = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });
    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");

    private static List<int> ParseNth(string? s) =>
        (s ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0)
            .Where(n => n is >= 1 and <= 5)
            .Distinct().OrderBy(n => n).ToList();
    private static int Mins(string f, string t)
    {
        var a = f.Split(':');
        var b = t.Split(':');
        return (int.Parse(b[0]) * 60 + int.Parse(b[1]) - (int.Parse(a[0]) * 60 + int.Parse(a[1])) + 1440) % 1440;
    }

    private static bool GraceOk(string f, string t, string g) =>
        TimeRx.IsMatch(g) && Mins(f, g) > 0 && Mins(f, g) < Mins(f, t);
    private static object Dto(Holiday h) => new
    {
        id = h.Id,
        companyId = h.CompanyId,
        name = h.Name,
        kind = h.Kind,
        date = h.Date,
        yearly = h.Yearly,
        weekday = h.Weekday,
        nth = h.Nth,
        color = h.Color,
        isActive = h.IsActive
    };

    private async Task<string?> Check(HolidayInput i)
    {
        var name = Clean(i.Name);
        if (name.Length == 0) return "Name is required.";
        if (name.Length > 100) return "Name: max 100 characters.";
        if (i.Kind == null || !Kinds.Contains(i.Kind)) return "Invalid type.";
        if (i.Color == null || !ColorRx.IsMatch(i.Color)) return "Invalid color.";

        if (i.Kind == "date")
        {
            if (i.Date == null || i.Date.Value.Year < 2000 || i.Date.Value.Year > 2100) return "Pick a valid date.";
        }
        else
        {
            if (i.Weekday is null or < 0 or > 6) return "Pick a weekday.";
            if (i.Kind == "nth" && ParseNth(i.Nth).Count == 0) return "Pick at least one week.";
        }

        if (i.CompanyId != null && !await _db.Companies.AnyAsync(c => c.Id == i.CompanyId))
            return "Company not found.";

        return null;
    }

    private static void Fill(Holiday h, HolidayInput i)
    {
        h.Name = Clean(i.Name);
        h.Kind = i.Kind!;
        h.Color = i.Color!.ToLowerInvariant();
        h.Date = i.Kind == "date" ? i.Date : null;
        h.Yearly = i.Kind == "date" && i.Yearly;
        h.Weekday = i.Kind == "date" ? null : i.Weekday;
        h.Nth = i.Kind == "nth" ? string.Join(',', ParseNth(i.Nth)) : null;
    }

    // ---------- holidays ----------

    // GET holidays            -> global
    // GET holidays?companyId=1 -> that company
    [HttpGet("holidays")]
    public async Task<IActionResult> List([FromQuery] int? companyId)
    {
        var list = await _db.Holidays.AsNoTracking()
            .Where(h => h.CompanyId == companyId)
            .OrderBy(h => h.Id)
            .ToListAsync();
        return Ok(list.Select(Dto));
    }

    [HttpPost("holidays")]
    public async Task<IActionResult> Create([FromBody] HolidayInput i)
    {
        var err = await Check(i);
        if (err != null) return Bad(err);

        var h = new Holiday { CompanyId = i.CompanyId };
        Fill(h, i);
        _db.Holidays.Add(h);
        await _db.SaveChangesAsync();
        return Ok(Dto(h));
    }

    [HttpPut("holidays/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] HolidayInput i)
    {
        var h = await _db.Holidays.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound(new { message = "Holiday not found." });

        var err = await Check(i with { CompanyId = h.CompanyId });
        if (err != null) return Bad(err);

        Fill(h, i);
        await _db.SaveChangesAsync();
        return Ok(Dto(h));
    }

    [HttpPatch("holidays/{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var h = await _db.Holidays.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound(new { message = "Holiday not found." });

        h.IsActive = !h.IsActive;
        await _db.SaveChangesAsync();
        return Ok(Dto(h));
    }

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await _db.Holidays.FirstOrDefaultAsync(x => x.Id == id);
        if (h == null) return NotFound(new { message = "Holiday not found." });

        _db.Holidays.Remove(h);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ---------- scope (global / company) ----------

    private async Task<AttendanceSetting> Row(int? cid)
    {
        var s = await _db.AttendanceSettings.FirstOrDefaultAsync(x => x.CompanyId == cid && x.UserId == null);
        if (s == null)
        {
            s = new AttendanceSetting { CompanyId = cid };
            _db.AttendanceSettings.Add(s);
        }
        return s;
    }

    private async Task<bool> CompanyMissing(int? cid) =>
        cid != null && !await _db.Companies.AnyAsync(c => c.Id == cid);

    [HttpGet("scope")]
    public async Task<IActionResult> GetScope([FromQuery] int? companyId)
    {
        var s = await _db.AttendanceSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == null)
                ?? new AttendanceSetting { CompanyId = companyId };

        return Ok(new
        {
            companyId = s.CompanyId,
            holidaysActive = s.HolidaysActive,
            timingActive = s.TimingActive,
            timeFrom = s.TimeFrom,
            timeTo = s.TimeTo,
            graceUntil = s.GraceUntil,
            ownerDays = (s.OwnerDays ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x)).ToList(),
            rules = JsonSerializer.Deserialize<List<DayRule>>(s.DayRules, Json) ?? new()
        });
    }

    [HttpPut("scope/holidays")]
    public async Task<IActionResult> SetHolidaysActive([FromQuery] int? companyId, [FromBody] HolidaysActiveInput i)
    {
        if (await CompanyMissing(companyId)) return NotFound(new { message = "Company not found." });

        var s = await Row(companyId);
        s.HolidaysActive = i.Active;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("scope/timing")]
    public async Task<IActionResult> SetTiming([FromQuery] int? companyId, [FromBody] TimingInput i)
    {
        if (await CompanyMissing(companyId)) return NotFound(new { message = "Company not found." });

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
                return Bad($"{Days[r.Day]}: invalid time.");

            var g = string.IsNullOrWhiteSpace(r.Grace) ? null : r.Grace.Trim();
            if (g != null && !GraceOk(r.From, r.To, g))
                return Bad($"{Days[r.Day]}: grace must be after From and before To.");

            rules.Add(new DayRule(r.Day, false, r.From, r.To, g));
        }

        var s = await Row(companyId);
        s.TimingActive = i.Active;
        s.TimeFrom = from;
        s.TimeTo = to;
        s.GraceUntil = grace;
        var od = (i.OwnerDays ?? new()).Where(x => x is >= 0 and <= 6).Distinct().OrderBy(x => x).ToList();
        s.OwnerDays = string.Join(',', od);
        s.OwnerAttends = od.Count > 0;
        s.DayRules = JsonSerializer.Serialize(rules, Json);
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }
}