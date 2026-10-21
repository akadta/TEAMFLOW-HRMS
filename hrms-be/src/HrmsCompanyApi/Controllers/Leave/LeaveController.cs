using System.Globalization;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

[Route("api/company/leave")]
public class LeaveController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly AttendanceRules _rules;
    private readonly LeaveFlow _flow;
    private readonly IWebHostEnvironment _env;

    public LeaveController(AppDbContext db, AttendanceRules rules, LeaveFlow flow, IWebHostEnvironment env)
    {
        _db = db;
        _rules = rules;
        _flow = flow;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // employee, or the company owner
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

    // dates that cannot be chosen: holiday / weekly off, existing leave, already present
    private async Task<Dictionary<DateOnly, (string Kind, string Label)>> BlockedAsync(
        Actor me, RuleSet rs, DateOnly from, DateOnly to)
    {
        var map = new Dictionary<DateOnly, (string Kind, string Label)>();

        var taken = await _db.LeaveDays.AsNoTracking()
            .Where(x => x.UserId == me.UserId && x.Date >= from && x.Date <= to &&
                        (x.Request.Status == "pending" || x.Request.Status == "approved"))
            .Select(x => new { x.Date, x.Request.Status })
            .ToListAsync();
        foreach (var t in taken)
            map[t.Date] = ("leave", t.Status == "approved" ? "Leave already approved" : "Leave request pending");

        var present = await _db.Attendances.AsNoTracking()
            .Where(a => a.UserId == me.UserId && a.Date >= from && a.Date <= to && a.Status == "present")
            .Select(a => a.Date)
            .ToListAsync();
        foreach (var p in present) map[p] = ("present", "Attendance already marked");

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var plan = rs.Plan(day);
            if (plan.HolidayName != null) map[day] = ("holiday", plan.HolidayName);
            else if (plan.Off) map[day] = ("holiday", "Weekly off");
        }

        return map;
    }

    // GET calendar  -> is leave on, max days, and the dates that are blocked
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Leave is not available for this account." });

        var eff = await _flow.EffectiveAsync(me.CompanyId);
        var today = Ist.Today;
        var from = dateFrom == null || dateFrom < today ? today : dateFrom.Value;
        var to = dateTo ?? today.AddDays(365);
        if (to > today.AddDays(365)) to = today.AddDays(365);
        if (to < from) to = from;

        var rs = await _rules.LoadAsync(me.CompanyId);
        var blocked = await BlockedAsync(me, rs, from, to);

        return Ok(new
        {
            active = eff.Active,
            maxDays = eff.MaxDays,
            today = today.ToString("yyyy-MM-dd"),
            days = blocked.OrderBy(b => b.Key)
                .Select(b => new { date = b.Key.ToString("yyyy-MM-dd"), kind = b.Value.Kind, label = b.Value.Label })
        });
    }

    // multipart: reason, dates ("2026-10-12,2026-10-13"), files (optional, up to 5)
    [HttpPost("request")]
    [RequestSizeLimit(60_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<IActionResult> Create(
        [FromForm] string? reason, [FromForm] string? dates, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Leave is not available for this account." });

        var eff = await _flow.EffectiveAsync(me.CompanyId);
        if (!eff.Active) return Bad("Leave requests are not enabled for your company.");

        var text = Regex.Replace((reason ?? string.Empty).Trim(), @"[ \t]+", " ");
        if (text.Length < 5) return Bad("Write a reason (min 5 characters).");
        if (text.Length > 1000) return Bad("Reason: max 1000 characters.");

        var ds = new List<DateOnly>();
        foreach (var s in (dates ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return Bad("Invalid date.");
            ds.Add(d);
        }
        ds = ds.Distinct().OrderBy(x => x).ToList();
        if (ds.Count == 0) return Bad("Select at least one date.");

        var today = Ist.Today;
        if (ds[0] < today || ds[^1] > today.AddDays(365)) return Bad("Dates must be from today up to one year ahead.");
        if (eff.MaxDays != null && ds.Count > eff.MaxDays) return Bad($"Max {eff.MaxDays} day(s) per request.");

        var list = files ?? new();
        var fe = AttendanceFiles.Check(list);
        if (fe != null) return Bad(fe);

        var rs = await _rules.LoadAsync(me.CompanyId);
        var blocked = await BlockedAsync(me, rs, ds[0], ds[^1]);
        foreach (var d in ds)
            if (blocked.TryGetValue(d, out var b)) return Bad($"{d:dd MMM yyyy}: {b.Label}.");

        var approvers = await _flow.BuildApproversAsync(me.CompanyId, me.UserId, eff.Config);
        var utc = DateTime.UtcNow;

        var req = new LeaveRequest
        {
            UserId = me.UserId,
            EmployeeId = me.EmployeeId,
            CompanyId = me.CompanyId,
            Reason = text,
            Status = "pending",
            CreatedAt = utc
        };
        foreach (var d in ds) req.Days.Add(new LeaveDay { UserId = me.UserId, Date = d });
        foreach (var a in approvers) req.Approvals.Add(a);

        if (approvers.Count == 0)
        {
            req.Status = "approved";
            req.DecidedAt = utc;
            req.DecisionNote = "No approver needed";
        }

        var saved = new List<string>();
        try
        {
            foreach (var f in list)
            {
                var (name, path) = await AttendanceFiles.SaveRawAsync(f, _env, me.CompanyId, saved);
                req.Files.Add(new LeaveFile { FileName = name, FilePath = path });
            }

            _db.LeaveRequests.Add(req);
            await _db.SaveChangesAsync();

            if (req.Status == "approved")
            {
                await _flow.WriteLeaveRowsAsync(req);
                await _db.SaveChangesAsync();
            }
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        if (req.Status == "pending") await _flow.NotifyApproversAsync(req);

        return Ok(new { id = req.Id, status = req.Status });
    }

    // GET mine  -> my own requests
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var me = await Me();
        if (me == null) return StatusCode(403, new { message = "Leave is not available for this account." });

        var list = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.UserId == me.UserId)
            .OrderByDescending(r => r.Id)
                      .Take(300)
            .Select(r => new
            {
                r.Id,
                r.Reason,
                r.Status,
                r.CreatedAt,
                r.DecidedAt,
                r.DecisionNote,
                DecidedBy = _db.Users.Where(u => u.Id == r.DecidedByUserId).Select(u => u.Name).FirstOrDefault(),
                Days = r.Days.OrderBy(d => d.Date).Select(d => d.Date).ToList(),
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList(),
                Approvals = r.Approvals.OrderBy(a => a.Id)
                    .Select(a => new
                    {
                        a.Group,
                        a.Mode,
                        a.Decision,
                        Approver = _db.Users.Where(u => u.Id == a.ApproverUserId).Select(u => u.Name).FirstOrDefault()
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("mine/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await _db.LeaveRequests.Include(x => x.Approvals)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
        if (r == null) return NotFound(new { message = "Request not found." });
        if (r.Status != "pending") return Bad("Only pending requests can be cancelled.");

        r.Status = "cancelled";
        r.DecidedAt = DateTime.UtcNow;
        foreach (var a in r.Approvals.Where(a => a.Decision == "pending")) a.Decision = "skipped";

        await _db.SaveChangesAsync();
        return Ok();
    }

    // GET approvals?scope=pending|all   (requests where I am an approver; owner sees the whole company)
    [HttpGet("approvals")]
    public async Task<IActionResult> Approvals([FromQuery] string? scope)
    {
        var list = await _flow.ListAsync(CurrentUserId, IsCompanyOwner ? CurrentCompanyId : null, scope != "all");
        return Ok(list);
    }

    [HttpPost("approvals/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecideInput i)
    {
        var r = await _db.LeaveRequests.Include(x => x.Approvals).Include(x => x.Days)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "Request not found." });

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return Bad("Note: max 500 characters.");

        var force = IsCompanyOwner && r.CompanyId == CurrentCompanyId;
        var err = await _flow.DecideAsync(r, CurrentUserId, force, i.Approve, note.Length == 0 ? null : note);
        return err == null ? Ok() : Bad(err);
    }
}