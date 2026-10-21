using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record DecideInput(bool Approve, string? Note);

[Route("api/company/attendance/requests")]
public class AttendanceApprovalController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly PermissionService _perm;
    private readonly IWebHostEnvironment _env;

    public AttendanceApprovalController(AppDbContext db, PermissionService perm, IWebHostEnvironment env)
    {
        _db = db;
        _perm = perm;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    private async Task<bool> Can() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("attendance.approval");

    // GET ?status=pending|approved|rejected (omit = all) &from=2026-10-01&to=2026-10-31
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery(Name = "from")] DateOnly? dateFrom,
        [FromQuery(Name = "to")] DateOnly? dateTo)
    {
        if (!await Can()) return Forbid();

        var q = _db.AttendanceRequests.AsNoTracking().Where(r => r.CompanyId == CurrentCompanyId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);
        if (dateFrom != null) q = q.Where(r => r.Date >= dateFrom);
        if (dateTo != null) q = q.Where(r => r.Date <= dateTo);

        var list = await q
            .OrderBy(r => r.Status == "pending" ? 0 : 1)
            .ThenByDescending(r => r.Id)
            .Take(500)
            .Select(r => new
            {
                r.Id,
                r.Date,
                r.Reason,
                r.Status,
                r.LateMinutes,
                r.RequestedAt,
                r.DecidedAt,
                r.DecisionNote,
                r.DecidedByUserId,
                EmployeeId = r.EmployeeId,
                Code = r.Employee != null ? r.Employee.EmployeeCode : "OWNER",
                Name = r.User.Name,
                Role = r.Employee == null ? "Owner" : (r.Employee.CompanyRole != null ? r.Employee.CompanyRole.Name : null),
                Files = r.Files.Select(f => new { f.FileName, f.FilePath }).ToList()
            })
            .ToListAsync();

        var ids = list.Where(x => x.DecidedByUserId != null).Select(x => x.DecidedByUserId!.Value).Distinct().ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        return Ok(list.Select(x => new
        {
            x.Id,
            x.Date,
            x.Reason,
            x.Status,
            x.LateMinutes,
            x.RequestedAt,
            x.DecidedAt,
            x.DecisionNote,
            x.EmployeeId,
            x.Code,
            x.Name,
            x.Role,
            x.Files,
            DecidedBy = x.DecidedByUserId != null && names.TryGetValue(x.DecidedByUserId.Value, out var n) ? n : null
        }));
    }

    // approve at ANY time -> that day becomes present (the absent row is updated)
    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] DecideInput i)
    {
        if (!await Can()) return Forbid();

        var r = await _db.AttendanceRequests.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == CurrentCompanyId);
        if (r == null) return NotFound(new { message = "Request not found." });
        if (r.Status != "pending") return Bad("Already decided.");

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return Bad("Note: max 500 characters.");

        r.Status = i.Approve ? "approved" : "rejected";
        r.DecidedByUserId = CurrentUserId;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = note.Length == 0 ? null : note;

        if (i.Approve)
        {
            var row = await _db.Attendances.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.Date == r.Date);
            if (row == null)
            {
                row = new Attendance { UserId = r.UserId, EmployeeId = r.EmployeeId, CompanyId = r.CompanyId, Date = r.Date };
                _db.Attendances.Add(row);
            }

            row.Status = "present";
            row.CheckInAt = r.RequestedAt;
            row.Source = "request";
            row.LateMinutes = r.LateMinutes;
            row.RequestId = r.Id;
            row.Note = null;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    // manual override by owner / approver. multipart: userId, date, status (present|absent), note, files
    // present needs proof documents
    [HttpPost("override")]
    [RequestSizeLimit(60_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000)]
    public async Task<IActionResult> Override(
        [FromForm] int userId, [FromForm] DateOnly date, [FromForm] string? status,
        [FromForm] string? note, [FromForm] List<IFormFile>? files)
    {
        if (!await Can()) return Forbid();

        var present = status == "present";
        if (!present && status != "absent") return Bad("Invalid status.");

        var text = (note ?? string.Empty).Trim();
        if (text.Length < 5) return Bad("Write a note (min 5 characters).");
        if (text.Length > 400) return Bad("Note: max 400 characters.");

        var list = files ?? new();
        var fe = AttendanceFiles.Check(list);
        if (fe != null) return Bad(fe);
        if (present && list.Count == 0) return Bad("Add at least one proof document.");

        var today = Ist.Today;
        if (date > today || date < today.AddDays(-90)) return Bad("Date must be within the last 90 days.");

        var cid = CurrentCompanyId;
        var ownerId = await _db.Companies.AsNoTracking().Where(c => c.Id == cid).Select(c => c.UserId).FirstAsync();

        int? empId = null;
        if (userId == ownerId)
        {
            if (!IsCompanyOwner) return Forbid(); // only the owner can change the owner's attendance
        }
        else
        {
            empId = await _db.Employees.AsNoTracking()
                .Where(e => e.UserId == userId && e.CompanyId == cid)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();
            if (empId == null) return NotFound(new { message = "Employee not found." });
        }

        var utc = DateTime.UtcNow;
        var saved = new List<string>();

        try
        {
            var row = await _db.Attendances.FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date);
            if (row == null)
            {
                row = new Attendance { UserId = userId, EmployeeId = empId, CompanyId = cid, Date = date };
                _db.Attendances.Add(row);
            }

            row.Status = present ? "present" : "absent";
            row.Source = "override";
            row.CheckInAt = null;
            row.LateMinutes = 0;
            row.RequestId = null;
            row.Note = $"{text} (by {CurrentUserName})";

            // close any pending request for that day
            var pending = await _db.AttendanceRequests
                .Where(r => r.UserId == userId && r.Date == date && r.Status == "pending")
                .ToListAsync();
            foreach (var p in pending)
            {
                p.Status = present ? "approved" : "rejected";
                p.DecidedByUserId = CurrentUserId;
                p.DecidedAt = utc;
                p.DecisionNote = "Overridden";
            }

            if (present)
            {
                // keeps the proof documents + who approved, visible on the requests page
                var req = new AttendanceRequest
                {
                    UserId = userId,
                    EmployeeId = empId,
                    CompanyId = cid,
                    Date = date,
                    Reason = "[Override] " + text,
                    Status = "approved",
                    RequestedAt = utc,
                    DecidedByUserId = CurrentUserId,
                    DecidedAt = utc,
                    DecisionNote = text
                };
                foreach (var f in list)
                    req.Files.Add(await AttendanceFiles.SaveAsync(f, _env, cid, saved));

                _db.AttendanceRequests.Add(req);
                await _db.SaveChangesAsync();
                row.RequestId = req.Id;
            }

            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok();
    }
}