using System.Globalization;
using System.Text.RegularExpressions;
using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record DailyProgressInput(string? Status, int? Progress);

[Route("api/company/tasks/daily")]
public class DailyTasksController : CompanyBaseController
{
    private static readonly string[] Statuses = { "todo", "in_progress", "on_hold", "done" };

    private sealed record Form(string Title, string? Desc, DateTime? Start, DateTime? Due, string Status, int Progress);

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly TaskFlow _flow;

    public DailyTasksController(AppDbContext db, IWebHostEnvironment env, TaskFlow flow)
    {
        _db = db;
        _env = env;
        _flow = flow;
    }

    private BadRequestObjectResult Bad(string m) => BadRequest(new { message = m });
    private IActionResult EmployeesOnly() => StatusCode(403, new { message = "Daily tasks are for employees only." });
    private static string Clean(string? v) => Regex.Replace((v ?? string.Empty).Trim(), @"\s+", " ");
    private static bool Locked(WorkTask t) => t.Approval is "pending" or "approved";

    private async Task<(int Uid, int Eid)?> Me()
    {
        if (CurrentEmployeeId is not { } id) return null;
        var uid = await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == CurrentCompanyId && e.IsActive)
            .Select(e => (int?)e.UserId).FirstOrDefaultAsync();
        return uid == null ? null : (uid.Value, id);
    }

    private IQueryable<WorkTask> Mine(int uid) =>
        _db.WorkTasks.Include(t => t.Files).Include(t => t.Approvals).Where(t => t.UserId == uid && t.Kind == "daily");

    private async Task<object?> One(int id, int uid) =>
        (await _flow.ShapeAsync(_db.WorkTasks.Where(t => t.Id == id), uid, null)).FirstOrDefault();

    // startAt / dueAt arrive as ISO with offset, e.g. 2026-10-12T10:30:00+05:30
    private static (string? Err, Form? F) Parse(
        string? title, string? description, string? startAt, string? dueAt, string? status, int? progress, string fallback)
    {
        var name = Clean(title);
        if (name.Length == 0) return ("Task name is required.", null);
        if (name.Length > 150) return ("Task name: max 150 characters.", null);

        var desc = (description ?? string.Empty).Trim();
        if (desc.Length > 4000) return ("Description: max 4000 characters.", null);

        DateTime? s = null, d = null;
        if (!string.IsNullOrWhiteSpace(startAt))
        {
            if (!DateTimeOffset.TryParse(startAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var o)) return ("Invalid start time.", null);
            s = o.UtcDateTime;
        }
        if (!string.IsNullOrWhiteSpace(dueAt))
        {
            if (!DateTimeOffset.TryParse(dueAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var o)) return ("Invalid due time.", null);
            d = o.UtcDateTime;
        }
        if (s != null && d != null && d < s) return ("Due time is before the start time.", null);

        var st = string.IsNullOrWhiteSpace(status) ? fallback : status;
        if (!Statuses.Contains(st)) return ("Invalid status.", null);

        var pr = progress ?? 0;
        if (pr is < 0 or > 100) return ("Progress must be between 0 and 100.", null);

        return (null, new Form(name, desc.Length == 0 ? null : desc, s, d, st, pr));
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();
        return Ok(await _flow.ShapeAsync(_db.WorkTasks.Where(t => t.UserId == me.Value.Uid && t.Kind == "daily"), me.Value.Uid, null));
    }
    [HttpGet("policy")]
    public async Task<IActionResult> Policy()
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();
        return Ok(await _flow.PreviewAsync(CurrentCompanyId, me.Value.Uid));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();
        var own = await _db.WorkTasks.AnyAsync(t => t.Id == id && t.UserId == me.Value.Uid && t.Kind == "daily");
        return own ? Ok(await One(id, me.Value.Uid)) : NotFound(new { message = "Task not found." });
    }

    // multipart: any file size (videos, images, documents)
    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Create(
        [FromForm] string? title, [FromForm] string? description, [FromForm] string? startAt, [FromForm] string? dueAt,
        [FromForm] string? status, [FromForm] int? progress, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var (err, f) = Parse(title, description, startAt, dueAt, status, progress, "todo");
        if (err != null) return Bad(err);

        var list = files ?? new();
        var fe = TaskFiles.Check(list, 0, 30, long.MaxValue);
        if (fe != null) return Bad(fe);

        var task = new WorkTask
        {
            UserId = me.Value.Uid,
            EmployeeId = me.Value.Eid,
            CompanyId = CurrentCompanyId,
            Kind = "daily",
            Title = f!.Title,
            Description = f.Desc,
            StartAt = f.Start,
            DueAt = f.Due,
            Status = f.Status,
            Progress = f.Progress
        };

        var saved = new List<string>();
        try
        {
            foreach (var file in list) task.Files.Add(await TaskFiles.SaveAsync(file, _env, CurrentCompanyId, saved));
            if (task.Status == "done") await _flow.OnDoneAsync(task);
            _db.WorkTasks.Add(task);
            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }
        if (task.Approval == "pending") await _flow.NotifyApproversAsync(task);
        return Ok(await One(task.Id, me.Value.Uid));
    }

    [HttpPut("{id:int}")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Update(
        int id, [FromForm] string? title, [FromForm] string? description, [FromForm] string? startAt, [FromForm] string? dueAt,
        [FromForm] string? status, [FromForm] int? progress, [FromForm] string? removeFiles, [FromForm] List<IFormFile>? files)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });
        if (Locked(task)) return Bad("This task is waiting for approval or already approved, so it cannot be edited.");

        var (err, f) = Parse(title, description, startAt, dueAt, status, progress, task.Status);
        if (err != null) return Bad(err);

        var rm = (removeFiles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).ToHashSet();
        var drop = task.Files.Where(x => rm.Contains(x.Id)).ToList();

        var list = files ?? new();
        var fe = TaskFiles.Check(list, task.Files.Count - drop.Count, 30, long.MaxValue);
        if (fe != null) return Bad(fe);

        var saved = new List<string>();
        try
        {
            foreach (var x in drop)
            {
                TaskFiles.Delete(_env, x.FilePath);
                _db.WorkTaskFiles.Remove(x);
            }
            foreach (var file in list) task.Files.Add(await TaskFiles.SaveAsync(file, _env, CurrentCompanyId, saved));

            task.Title = f!.Title;
            task.Description = f.Desc;
            task.StartAt = f.Start;
            task.DueAt = f.Due;
            task.UpdatedAt = DateTime.UtcNow;

            if (f.Status == "done") await _flow.OnDoneAsync(task);
            else
            {
                if (task.Status == "done") { task.CompletedAt = null; task.Approval = "none"; }
                task.Status = f.Status;
                task.Progress = f.Progress;
            }

            await _db.SaveChangesAsync();
        }
        catch
        {
            AttendanceFiles.Cleanup(saved);
            throw;
        }
        if (task.Approval == "pending") await _flow.NotifyApproversAsync(task);
        return Ok(await One(id, me.Value.Uid));
    }

    // quick change from the list: status and/or progress
    [HttpPatch("{id:int}/progress")]
    public async Task<IActionResult> SetProgress(int id, [FromBody] DailyProgressInput i)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });
        if (Locked(task)) return Bad("This task is waiting for approval or already approved.");
        if (i.Progress is < 0 or > 100) return Bad("Progress must be between 0 and 100.");
        if (i.Status != null && !Statuses.Contains(i.Status)) return Bad("Invalid status.");

        if (i.Progress != null) task.Progress = i.Progress.Value;

        if (i.Status == "done") await _flow.OnDoneAsync(task);
        else if (i.Status != null)
        {
            if (task.Status == "done") { task.CompletedAt = null; task.Approval = "none"; }
            task.Status = i.Status;
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (task.Approval == "pending") await _flow.NotifyApproversAsync(task);
        return Ok(await One(id, me.Value.Uid));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });
        if (Locked(task)) return Bad("This task is already completed or waiting for approval.");

        await _flow.OnDoneAsync(task);
        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (task.Approval == "pending") await _flow.NotifyApproversAsync(task);
        return Ok(await One(id, me.Value.Uid));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var me = await Me();
        if (me == null) return EmployeesOnly();

        var task = await Mine(me.Value.Uid).FirstOrDefaultAsync(x => x.Id == id);
        if (task == null) return NotFound(new { message = "Task not found." });
        if (Locked(task)) return Bad("This task is waiting for approval or already approved.");

        foreach (var x in task.Files) TaskFiles.Delete(_env, x.FilePath);
        _db.WorkTasks.Remove(task);
        await _db.SaveChangesAsync();
        return Ok();
    }
}