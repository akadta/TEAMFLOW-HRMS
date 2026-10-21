using hrms.Data;
using hrms.Models;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

public record InboxAccept(List<int>? UserIds);
public record InboxDecline(string? Note);

[Route("api/company/tasks/inbox")]
public class TaskInboxController : CompanyBaseController
{
    private readonly AppDbContext _db;
    private readonly TaskDispatcher _disp;
    private readonly Notifier _notify;

    public TaskInboxController(AppDbContext db, TaskDispatcher disp, Notifier notify)
    {
        _db = db;
        _disp = disp;
        _notify = notify;
    }

    private IActionResult? OwnerOnly() =>
        IsCompanyOwner ? null : StatusCode(403, new { message = "Only the company owner can use the task inbox." });

    // GET ?scope=pending|all
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var q = _db.TaskAssignments.AsNoTracking().Include(a => a.Targets).Include(a => a.Files)
            .Where(a => a.CompanyId == CurrentCompanyId && a.Source == "main");
        if (scope != "all") q = q.Where(a => a.Status == "awaiting_owner");

        var list = await q.OrderByDescending(a => a.Id).Take(200).ToListAsync();
        return Ok(await _disp.ShapeAsync(list));
    }

    // the owner chooses who gets it. If the main employee scheduled it for later, it waits for that time.
    [HttpPost("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, [FromBody] InboxAccept i)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var a = await _db.TaskAssignments.Include(x => x.Targets).Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == CurrentCompanyId && x.Source == "main");
        if (a == null) return NotFound(new { message = "Task not found." });
        if (a.Status != "awaiting_owner") return BadRequest(new { message = "This task was already handled." });

        var ids = (i.UserIds ?? new()).Distinct().ToList();
        if (ids.Count == 0) return BadRequest(new { message = "Pick at least one employee." });

        var emps = await _db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == CurrentCompanyId && e.IsActive && ids.Contains(e.UserId))
            .Select(e => new { e.UserId, e.Id }).ToListAsync();
        if (emps.Count != ids.Count) return BadRequest(new { message = "Some employees are not valid." });

        foreach (var e in emps) a.Targets.Add(new TaskAssignmentTarget { UserId = e.UserId, EmployeeId = e.Id });

        if (a.SendAt == null || a.SendAt <= DateTime.UtcNow)
        {
            await _db.SaveChangesAsync();
            await _disp.DispatchAsync(a);
        }
        else
        {
            a.Status = "scheduled";
            await _db.SaveChangesAsync();
        }

        await _notify.PushAsync(new[] { a.CreatedByUserId }, "task", "Task accepted",
            $"The company owner sent \"{a.Title}\" to {emps.Count} employee(s).", "/tasks/assign");
        return Ok();
    }

    [HttpPost("{id:int}/decline")]
    public async Task<IActionResult> Decline(int id, [FromBody] InboxDecline i)
    {
        var deny = OwnerOnly();
        if (deny != null) return deny;

        var a = await _db.TaskAssignments.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == CurrentCompanyId && x.Source == "main");
        if (a == null) return NotFound(new { message = "Task not found." });
        if (a.Status != "awaiting_owner") return BadRequest(new { message = "This task was already handled." });

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return BadRequest(new { message = "Note: max 500 characters." });

        a.Status = "declined";
        a.DecisionNote = note.Length == 0 ? null : note;
        await _db.SaveChangesAsync();

        await _notify.PushAsync(new[] { a.CreatedByUserId }, "task", "Task declined",
            $"The company owner declined \"{a.Title}\"." + (a.DecisionNote == null ? "" : $"\nNote: {a.DecisionNote}"), "/tasks/assign");
        return Ok();
    }
}