using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record SelfStatusInput(string? Status);

[Route("api/company/tasks/self")]
public class SelfTasksController : CompanyBaseController
{
    private static readonly string[] Statuses = { "todo", "in_progress", "on_hold", "done" };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SelfTasksController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });
    private IActionResult EmployeesOnly() => StatusCode(403, new { message = "Self tasks are for employees only." });
    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");

    // employees only: (user id, employee id)
    // employee, or the company owner (EmployeeId is null for the owner)
    private async Task<(int Uid, int? Eid)?> Me()
    {
        if (IsCompanyOwner) return (CurrentUserId, null);

        if (CurrentEmployeeId is not { } id) return null;
        var uid = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == CurrentCompanyId && e.IsActive)
            .Select(e => (int?)e.UserId).FirstOrDefaultAsync();
        if (uid == null) return null;
        return (uid.Value, id);
    }
    private static object Dto(WorkTask t) => new
    {
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.CreatedAt,
        t.UpdatedAt,
        t.CompletedAt,
        Files = t.Files.OrderBy(f => f.Id).Select(f => new { f.Id, f.FileName, f.FilePath, f.Size }).ToList()
    };

    private IQueryable<WorkTask> Mine(int uid) =>
        _db.WorkTasks.Include(t => t.Files).Where(t => t.UserId == uid && t.Kind == "self");

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var list = await Mine(me.Value.Uid).AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt).Take(1000).ToListAsync();
        return Ok(list.Select(Dto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var t = await Mine(me.Value.Uid).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? NotFound(new { message = "Task not found." }) : Ok(Dto(t));
    }

    [HttpPost]
    [RequestSizeLimit(300_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]
    public async Task<IActionResult> Create(
        [FromForm] string? title, [FromForm] string? description, [FromForm] string? status, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var name = Clean(title);
        if (name.Length == 0) return Bad("Task name is required.");
        if (name.Length > 150) return Bad("Task name: max 150 characters.");

        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 4000) return Bad("Description: max 4000 characters.");

        var st = string.IsNullOrWhiteSpace(status) ? "todo" : status;
        if (!Statuses.Contains(st)) return Bad("Invalid status.");

        var list = files ?? new();
        var fe = TaskFiles.Check(list);
        if (fe != null) return Bad(fe);

        var task = new WorkTask
        {
            UserId = me.Value.Uid,
            EmployeeId = me.Value.Eid,
            CompanyId = CurrentCompanyId,
            Kind = "self",
            Title = name,
            Description = desc.Length == 0 ? null : desc,
            Status = st,
            CompletedAt = st == "done" ? DateTime.UtcNow : null
        };

        var saved = new List<string>();
        try
        {
            foreach (var f in list) task.Files.Add(await TaskFiles.SaveAsync(f, _env, CurrentCompanyId, saved));
            _db.WorkTasks.Add(task);
            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok(Dto(task));
    }

    // multipart: title, description, status, removeFiles ("3,5"), files (new)
    [HttpPut("{id:int}")]
    [RequestSizeLimit(300_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]
    public async Task<IActionResult> Update(
        int id, [FromForm] string? title, [FromForm] string? description, [FromForm] string? status,
        [FromForm] string? removeFiles, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });

        var name = Clean(title);
        if (name.Length == 0) return Bad("Task name is required.");
        if (name.Length > 150) return Bad("Task name: max 150 characters.");

        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 4000) return Bad("Description: max 4000 characters.");

        var st = string.IsNullOrWhiteSpace(status) ? task.Status : status;
        if (!Statuses.Contains(st)) return Bad("Invalid status.");

        var rm = (removeFiles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).ToHashSet();
        var drop = task.Files.Where(f => rm.Contains(f.Id)).ToList();

        var list = files ?? new();
        var fe = TaskFiles.Check(list, task.Files.Count - drop.Count);
        if (fe != null) return Bad(fe);

        var saved = new List<string>();
        try
        {
            foreach (var f in drop)
            {
                TaskFiles.Delete(_env, f.FilePath);
                _db.WorkTaskFiles.Remove(f);
            }
            foreach (var f in list) task.Files.Add(await TaskFiles.SaveAsync(f, _env, CurrentCompanyId, saved));

            task.Title = name;
            task.Description = desc.Length == 0 ? null : desc;
            if (st != task.Status) task.CompletedAt = st == "done" ? DateTime.UtcNow : null;
            task.Status = st;
            task.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }

        return Ok(Dto(task));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SelfStatusInput i)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await _db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me.Value.Uid && x.Kind == "self");
        if (task == null) return NotFound(new { message = "Task not found." });
        if (i.Status == null || !Statuses.Contains(i.Status)) return Bad("Invalid status.");

        if (i.Status != task.Status) task.CompletedAt = i.Status == "done" ? DateTime.UtcNow : null;
        task.Status = i.Status;
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { status = task.Status, completedAt = task.CompletedAt });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });

        foreach (var f in task.Files) TaskFiles.Delete(_env, f.FilePath);
        _db.WorkTasks.Remove(task);
        await _db.SaveChangesAsync();
        return Ok();
    }
}