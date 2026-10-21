using System.Globalization;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public sealed record AssignPerson(int UserId, int EmployeeId, string Code, string Name, string? Role);

[Route("api/company/tasks/assign")]
public class AssignTasksController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PermissionService _perm;
    private readonly TaskDispatcher _disp;

    public AssignTasksController(AppDbContext db, IWebHostEnvironment env, PermissionService perm, TaskDispatcher disp)
    {
        _db = db;
        _env = env;
        _perm = perm;
        _disp = disp;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });

    // every employee (and the owner) can open the assign page
    private Task<bool> Can() => Task.FromResult(IsCompanyOwner || CurrentEmployeeId != null);

    private async Task<bool> CanAssignPeople() =>
        IsCompanyOwner || (await _perm.SlugsAsync(false, CurrentEmployeeId)).Contains("tasks.assign");

    // everybody can send a task to the company owner.
    // the owner (whole company) and people with the tasks.assign slug (their direct reports) can also assign to employees.
    private async Task<List<AssignPerson>> PeopleAsync()
    {
        var list = new List<AssignPerson>();

        if (!IsCompanyOwner)
        {
            var owner = await _db.Companies.AsNoTracking().Where(c => c.Id == CurrentCompanyId)
                .Select(c => new AssignPerson(c.UserId, 0, "OWNER", c.User.Name, "Owner")).FirstAsync();
            list.Add(owner);
        }

        if (await CanAssignPeople())
        {
            var q = _db.Employees.AsNoTracking().Where(e => e.CompanyId == CurrentCompanyId && e.IsActive);
            if (!IsCompanyOwner)
            {
                var scope = await ReportScope.EmployeeIdsAsync(_db, false, CurrentUserId, CurrentCompanyId, CurrentEmployeeId, false)
                            ?? new List<int>();
                q = q.Where(e => scope.Contains(e.Id));
            }

            list.AddRange(await q.OrderBy(e => e.User.Name)
                .Select(e => new AssignPerson(e.UserId, e.Id, e.EmployeeCode, e.User.Name, e.CompanyRole != null ? e.CompanyRole.Name : null))
                .ToListAsync());
        }

        return list;
    }
    private static DateTime? Utc(string? s, out bool bad)
    {
        bad = false;
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var o)) return o.UtcDateTime;
        bad = true;
        return null;
    }

    [HttpGet("people")]
    public async Task<IActionResult> People()
    {
        if (!await Can()) return Forbid();
        return Ok((await PeopleAsync()).Select(p => new { p.UserId, p.Code, p.Name, p.Role }));
    }

    // multipart: title, description, dueAt, sendAt (empty = now), userIds ("4,9"), files (any size)
    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Create(
        [FromForm] string? title, [FromForm] string? description, [FromForm] string? dueAt, [FromForm] string? sendAt,
        [FromForm] string? userIds, [FromForm] List<IFormFile>? files)
    {
        if (!await Can()) return Forbid();

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

        var ids = (userIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
        if (ids.Count == 0) return Bad("Pick at least one employee.");

        var chosen = (await PeopleAsync()).Where(p => ids.Contains(p.UserId)).ToList();
        if (chosen.Count != ids.Count)
            return Bad("Some employees are not allowed. You can only assign to people of your company who report to you.");

        var list = files ?? new();
        var fe = TaskFiles.Check(list, 0, 30, long.MaxValue);
        if (fe != null) return Bad(fe);

        var a = new TaskAssignment
        {
            CompanyId = CurrentCompanyId,
            CreatedByUserId = CurrentUserId,
            Source = "company",
            Title = name,
            Description = desc.Length == 0 ? null : desc,
            DueAt = due,
            SendAt = send,
            Status = "scheduled"
        };
        foreach (var p in chosen)
            a.Targets.Add(new TaskAssignmentTarget { UserId = p.UserId, EmployeeId = p.EmployeeId == 0 ? null : p.EmployeeId });

        var saved = new List<string>();
        try
        {
            foreach (var f in list)
            {
                var (n, path, size) = await TaskFiles.SaveRawAsync(f, _env, CurrentCompanyId, saved);
                a.Files.Add(new TaskAssignmentFile { FileName = n, FilePath = path, Size = size });
            }

            _db.TaskAssignments.Add(a);
            await _db.SaveChangesAsync();

            if (send == null) await _disp.DispatchAsync(a); // instant
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok(new { id = a.Id, status = a.Status });
    }

    // assignments I created
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        if (!await Can()) return Forbid();

        var list = await _db.TaskAssignments.AsNoTracking().Include(a => a.Targets).Include(a => a.Files)
            .Where(a => a.CompanyId == CurrentCompanyId && a.CreatedByUserId == CurrentUserId)
            .OrderByDescending(a => a.Id).Take(200).ToListAsync();

        return Ok(await _disp.ShapeAsync(list));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!await Can()) return Forbid();

        var a = await _db.TaskAssignments.Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == CurrentCompanyId && x.CreatedByUserId == CurrentUserId);
        if (a == null) return NotFound(new { message = "Task not found." });
        if (a.Status != "scheduled") return Bad("Only a scheduled task can be cancelled.");

        a.Status = "cancelled";
        foreach (var f in a.Files) TaskFiles.Delete(_env, f.FilePath);
        await _db.SaveChangesAsync();
        return Ok();
    }
}