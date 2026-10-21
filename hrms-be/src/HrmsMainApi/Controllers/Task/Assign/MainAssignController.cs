using System.Globalization;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/assign-tasks")]
public class MainAssignController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly TaskDispatcher _disp;
    private readonly Notifier _notify;

    public MainAssignController(AppDbContext db, IWebHostEnvironment env, TaskDispatcher disp, Notifier notify)
    {
        _db = db;
        _env = env;
        _disp = disp;
        _notify = notify;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // companies this person can send tasks to: super admin = all, otherwise the companies connected to them
    private async Task<List<int>?> CompanyIdsAsync()
    {
        var super = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == CurrentUserId && u.MainRole != null && u.MainRole.IsActive);
        return super ? null : await ReportingScope.VisibleCompanyIdsAsync(_db, CurrentUserId);
    }

    [HttpGet("companies")]
    public async Task<IActionResult> Companies()
    {
        var ids = await CompanyIdsAsync();
        var q = _db.Companies.AsNoTracking().Where(c => c.IsActive);
        if (ids != null) q = q.Where(c => ids.Contains(c.Id));

        return Ok(await q.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.OwnerName }).ToListAsync());
    }

    private static DateTime? Utc(string? s, out bool bad)
    {
        bad = false;
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var o)) return o.UtcDateTime;
        bad = true;
        return null;
    }

    // multipart: title, description, dueAt, sendAt (empty = as soon as the owner picks people), companyIds ("1,4"), files
    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Create(
        [FromForm] string? title, [FromForm] string? description, [FromForm] string? dueAt, [FromForm] string? sendAt,
        [FromForm] string? companyIds, [FromForm] List<IFormFile>? files)
    {
        var name = Regex.Replace((title ?? string.Empty).Trim(), @"\s+", " ");
        if (name.Length == 0) return Bad("Task name is required.");
        if (name.Length > 150) return Bad("Task name: max 150 characters.");
        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 4000) return Bad("Description: max 4000 characters.");

        var due = Utc(dueAt, out var badDue);
        var send = Utc(sendAt, out var badSend);
        if (badDue || badSend) return Bad("Invalid date or time.");
        if (send != null && send < DateTime.UtcNow.AddMinutes(-1)) return Bad("The send time is in the past.");
        if (due != null && due < (send ?? DateTime.UtcNow)) return Bad("The due time is before the send time.");

        var ids = (companyIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
        if (ids.Count == 0) return Bad("Pick at least one company.");

        var allowed = await CompanyIdsAsync();
        if (allowed != null && ids.Any(i => !allowed.Contains(i))) return Bad("You can only send tasks to your own companies.");

        var companies = await _db.Companies.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.IsActive).Select(c => new { c.Id, c.UserId }).ToListAsync();
        if (companies.Count != ids.Count) return Bad("Some companies are not active.");

        var list = files ?? new();
        var fe = TaskFiles.Check(list, 0, 30, long.MaxValue);
        if (fe != null) return Bad(fe);

        // files are stored once and shared by every company's copy of the task
        var saved = new List<string>();
        var stored = new List<(string Name, string Path, long Size)>();
        try
        {
            foreach (var f in list) stored.Add(await TaskFiles.SaveRawAsync(f, _env, 0, saved));

            foreach (var c in companies)
            {
                var a = new TaskAssignment
                {
                    CompanyId = c.Id,
                    CreatedByUserId = CurrentUserId,
                    Source = "main",
                    Title = name,
                    Description = desc.Length == 0 ? null : desc,
                    DueAt = due,
                    SendAt = send,
                    Status = "awaiting_owner"
                };
                foreach (var s in stored) a.Files.Add(new TaskAssignmentFile { FileName = s.Name, FilePath = s.Path, Size = s.Size });
                _db.TaskAssignments.Add(a);
            }
            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        await _notify.PushAsync(companies.Select(c => c.UserId), "task", "Task from the main team",
            $"{name}\nFrom {CurrentUserName}. Open the task inbox and pick who should get it.", "/tasks/inbox");

        return Ok(new { count = companies.Count });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var list = await _db.TaskAssignments.AsNoTracking().Include(a => a.Targets).Include(a => a.Files)
            .Where(a => a.CreatedByUserId == CurrentUserId && a.Source == "main")
            .OrderByDescending(a => a.Id).Take(300).ToListAsync();
        return Ok(await _disp.ShapeAsync(list));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var a = await _db.TaskAssignments.FirstOrDefaultAsync(x => x.Id == id && x.CreatedByUserId == CurrentUserId && x.Source == "main");
        if (a == null) return NotFound(new { message = "Task not found." });
        if (a.Status is not ("awaiting_owner" or "scheduled")) return Bad("It was already sent or closed.");

        a.Status = "cancelled";
        await _db.SaveChangesAsync();
        return Ok();
    }
}