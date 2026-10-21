using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public sealed record Actor(int UserId, int? EmployeeId, int CompanyId);

[Route("api/company/attendance")]
public class AttendanceController : CompanyBaseController
{
    private static readonly string[] PhotoExt = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;
    private readonly IWebHostEnvironment _env;

    public AttendanceController(AppDbContext db, AttendanceRules rules, IWebHostEnvironment env)
    {
        _db = db;
        _rules = rules;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private async Task<Actor?> Me()
    {
        if (IsCompanyOwner)
        {
            var ok = await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == CurrentCompanyId && c.IsActive);
            return ok ? new Actor(CurrentUserId, null, CurrentCompanyId) : null;
        }

        if (CurrentEmployeeId is not { } id) return null;

        return await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == CurrentCompanyId && e.IsActive)
            .Select(e => new Actor(e.UserId, e.Id, e.CompanyId))
            .FirstOrDefaultAsync();
    }

    private static bool Blocked(Actor me, RuleSet rs) => me.EmployeeId == null && !rs.OwnerOn(Ist.Today);

    private IActionResult OwnerOff() =>
        StatusCode(403, new { message = "Attendance is not enabled for the owner." });

    private static bool GoodCoords(double? lat, double? lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    private static string Why(string phase) => phase switch
    {
        "early" => "Attendance has not started yet.",
        "request" => "Grace time is over. Send an attendance request.",
        "closed" => "Attendance is closed for today.",
        "holiday" => "Today is a holiday.",
        "off" => "Today is a weekly off.",
        _ => "Attendance is not set up for your company."
    };

    private async Task<string> SavePhoto(IFormFile f, int companyId, List<string> saved)
    {
        var rel = $"uploads/attendance/{companyId}/photos/{Guid.NewGuid():N}{Path.GetExtension(f.FileName).ToLowerInvariant()}";
        var full = Path.Combine(_env.ContentRootPath, "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using (var fs = System.IO.File.Create(full))
        {
            await f.CopyToAsync(fs);
        }

        saved.Add(full);
        return "/" + rel;
    }

    private async Task<object> Build(Actor me, RuleSet rs)
    {
        var utc = DateTime.UtcNow;
        var nowIst = Ist.Of(utc);
        var date = DateOnly.FromDateTime(nowIst);

        var plan = rs.Plan(date);
        var ph = rs.PhaseAt(date, nowIst);

        var att = await _db.Attendances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "present");
        var req = await _db.AttendanceRequests.AsNoTracking()
            .Where(r => r.UserId == me.UserId && r.Date == date)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();
        var onLeave = await _db.Attendances.AsNoTracking()
            .AnyAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "leave");

        var state = att != null ? "marked" : onLeave ? "leave" : req?.Status == "pending" ? "pending" : ph.Name;

        var slots = new List<object>();
        DateTime? nextSlot = null;
        if (att != null && rs.Periods.Count > 0)
        {
            var done = await _db.AttendanceCheckins.AsNoTracking()
                .Where(c => c.UserId == me.UserId && c.Date == date).ToListAsync();
            var nowMin = (int)Math.Floor(nowIst.TimeOfDay.TotalMinutes);

            foreach (var s in rs.Periods)
            {
                var f = Ist.Mins(s.From);
                var t = Ist.Mins(s.To);
                var hit = done.FirstOrDefault(c => c.SlotFrom == s.From);
                var st = hit != null ? "done" : nowMin > t ? "missed" : nowMin >= f ? "open" : "upcoming";
                slots.Add(new { from = s.From, to = s.To, state = st, at = hit?.At });

                foreach (var edge in new[] { Ist.ToUtc(date, f), Ist.ToUtc(date, t + 1) })
                    if (edge > utc && (nextSlot == null || edge < nextSlot)) nextSlot = edge;
            }
        }

        return new
        {
            date = date.ToString("yyyy-MM-dd"),
            serverNow = utc,
            state,
            label = plan.HolidayName,
            color = plan.HolidayColor,
            timeFrom = plan.From,
            timeTo = plan.To,
            graceUntil = plan.Grace,
            nextChangeAt = state == ph.Name ? ph.NextChangeUtc : nextSlot,
            checkInAt = att?.CheckInAt,
            lateMinutes = att?.LateMinutes,
            source = att?.Source,
            requestStatus = req?.Status,
            decisionNote = req?.DecisionNote,
            requireLocation = rs.RequireLocation,
            requirePhoto = rs.RequirePhoto,
            slots
        };
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Attendance is not available for this account." });

        var rs = await _rules.LoadAsync(me.CompanyId, me.UserId);
        if (Blocked(me, rs)) return OwnerOff();

        return Ok(await Build(me, rs));
    }

    // multipart: lat, lng, accuracy, photo  (location / photo only when the rules ask for them)
    [HttpPost("mark")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Mark(
        [FromForm] double? lat, [FromForm] double? lng, [FromForm] double? accuracy, [FromForm] IFormFile? photo)
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Attendance is not available for this account." });

        var utc = DateTime.UtcNow;
        var nowIst = Ist.Of(utc);
        var date = DateOnly.FromDateTime(nowIst);

        var rs = await _rules.LoadAsync(me.CompanyId, me.UserId);
        if (Blocked(me, rs)) return OwnerOff();

        if (await _db.Attendances.AnyAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "leave"))
            return Bad("You are on leave today.");

        var ph = rs.PhaseAt(date, nowIst);
        if (ph.Name != "open") return Bad(Why(ph.Name));

        if (await _db.AttendanceRequests.AnyAsync(r => r.UserId == me.UserId && r.Date == date && r.Status == "pending"))
            return Bad("Your request is pending approval.");

        var hasLoc = GoodCoords(lat, lng);
        if (rs.RequireLocation && !hasLoc)
            return Bad("Location is required to mark attendance. Turn on location and allow access.");
        if (rs.RequirePhoto && photo == null)
            return Bad("A photo is required to mark attendance. Allow camera access and take your photo.");
        if (photo != null)
        {
            if (!PhotoExt.Contains(Path.GetExtension(photo.FileName).ToLowerInvariant())) return Bad("Photo must be JPG, PNG or WEBP.");
            if (photo.Length > 3_000_000) return Bad("Photo: max 3 MB.");
        }

        var row = await _db.Attendances.FirstOrDefaultAsync(a => a.UserId == me.UserId && a.Date == date);
        if (row?.Status == "present") return Bad("Already marked today.");

        if (row == null)
        {
            row = new Attendance { UserId = me.UserId, EmployeeId = me.EmployeeId, CompanyId = me.CompanyId, Date = date };
            _db.Attendances.Add(row);
        }

        var saved = new List<string>();
        try
        {
            row.Status = "present";
            row.CheckInAt = utc;
            row.Source = "normal";
            row.LateMinutes = RuleSet.Late(rs.Plan(date), date, nowIst);
            row.Note = null;
            row.Lat = hasLoc ? lat : null;
            row.Lng = hasLoc ? lng : null;
            row.Accuracy = hasLoc ? accuracy : null;
            if (photo != null) row.PhotoPath = await SavePhoto(photo, me.CompanyId, saved);

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            AttendanceFiles.Cleanup(saved);
            return Bad("Already marked today.");
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok(await Build(me, rs));
    }

    // multipart: lat, lng, accuracy   (press Check in while a window is open)
    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromForm] double? lat, [FromForm] double? lng, [FromForm] double? accuracy)
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Attendance is not available for this account." });

        var utc = DateTime.UtcNow;
        var nowIst = Ist.Of(utc);
        var date = DateOnly.FromDateTime(nowIst);

        var rs = await _rules.LoadAsync(me.CompanyId, me.UserId);
        if (Blocked(me, rs)) return OwnerOff();

        if (!await _db.Attendances.AnyAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "present"))
            return Bad("Mark your attendance first.");

        var nowMin = (int)Math.Floor(nowIst.TimeOfDay.TotalMinutes);
        var slot = rs.Periods.FirstOrDefault(s => nowMin >= Ist.Mins(s.From) && nowMin <= Ist.Mins(s.To));
        if (slot == null) return Bad("No check-in window is open right now.");

        if (await _db.AttendanceCheckins.AnyAsync(c => c.UserId == me.UserId && c.Date == date && c.SlotFrom == slot.From))
            return Bad("Already checked in for this window.");

        var hasLoc = GoodCoords(lat, lng);
        if (rs.RequireLocation && !hasLoc)
            return Bad("Location is required to check in. Turn on location and allow access.");

        _db.AttendanceCheckins.Add(new AttendanceCheckin
        {
            UserId = me.UserId,
            EmployeeId = me.EmployeeId,
            CompanyId = me.CompanyId,
            Date = date,
            SlotFrom = slot.From,
            SlotTo = slot.To,
            At = utc,
            Lat = hasLoc ? lat : null,
            Lng = hasLoc ? lng : null,
            Accuracy = hasLoc ? accuracy : null
        });

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Bad("Already checked in for this window."); }

        return Ok(await Build(me, rs));
    }

    // multipart: reason + files (0..5)
    [HttpPost("request")]
    [RequestSizeLimit(60_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<IActionResult> SendRequest([FromForm] string? reason, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Attendance is not available for this account." });

        var text = Regex.Replace((reason ?? string.Empty).Trim(), @"[ \t]+", " ");
        if (text.Length < 5) return Bad("Write a reason (min 5 characters).");
        if (text.Length > 1000) return Bad("Reason: max 1000 characters.");

        var list = files ?? new();
        var fe = AttendanceFiles.Check(list);
        if (fe != null) return Bad(fe);

        var utc = DateTime.UtcNow;
        var nowIst = Ist.Of(utc);
        var date = DateOnly.FromDateTime(nowIst);

        var rs = await _rules.LoadAsync(me.CompanyId, me.UserId);
        if (Blocked(me, rs)) return OwnerOff();

        if (await _db.Attendances.AnyAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "leave"))
            return Bad("You are on leave today.");

        var ph = rs.PhaseAt(date, nowIst);
        if (ph.Name != "request")
            return Bad(ph.Name == "open" ? "You can still mark attendance directly." : Why(ph.Name));

        if (await _db.Attendances.AnyAsync(a => a.UserId == me.UserId && a.Date == date && a.Status == "present"))
            return Bad("Already marked today.");
        if (await _db.AttendanceRequests.AnyAsync(r => r.UserId == me.UserId && r.Date == date && r.Status == "pending"))
            return Bad("A request is already pending.");

        var req = new AttendanceRequest
        {
            UserId = me.UserId,
            EmployeeId = me.EmployeeId,
            CompanyId = me.CompanyId,
            Date = date,
            Reason = text,
            Status = "pending",
            LateMinutes = RuleSet.Late(rs.Plan(date), date, nowIst),
            RequestedAt = utc
        };

        var saved = new List<string>();
        try
        {
            foreach (var f in list)
                req.Files.Add(await AttendanceFiles.SaveAsync(f, _env, me.CompanyId, saved));

            _db.AttendanceRequests.Add(req);
            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok(await Build(me, rs));
    }

    // GET my-requests  -> my own attendance requests (for the My Requests page)
    [HttpGet("my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Attendance is not available for this account." });

        var list = await _db.AttendanceRequests.AsNoTracking()
            .Where(r => r.UserId == me.UserId)
            .OrderByDescending(r => r.Id)
            .Take(500)
            .Select(r => new
            {
                r.Id,
                Date = r.Date.ToString("yyyy-MM-dd"),
                r.Reason,
                r.Status,
                r.LateMinutes,
                r.RequestedAt,
                r.DecidedAt,
                r.DecisionNote,
                DecidedBy = _db.Users.Where(u => u.Id == r.DecidedByUserId).Select(u => u.Name).FirstOrDefault(),
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList()
            })
            .ToListAsync();

        return Ok(list);
    }
}