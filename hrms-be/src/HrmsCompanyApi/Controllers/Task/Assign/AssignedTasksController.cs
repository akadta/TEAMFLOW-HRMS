using hrms.Data;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsCompanyApi.Controllers;

[Route("api/company/tasks/assigned")]
public class AssignedTasksController : CompanyBaseController
{
    private static readonly string[] Statuses = { "todo", "in_progress", "on_hold", "done" };

    private readonly AppDbContext _db;
    private readonly TaskFlow _flow;

    public AssignedTasksController(AppDbContext db, TaskFlow flow)
    {
        _db = db;
        _flow = flow;
    }

    private async Task<int?> Me()
    {
        if (IsCompanyOwner) return CurrentUserId;

        if (CurrentEmployeeId is not { } id) return null;
        return await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id && e.CompanyId == CurrentCompanyId && e.IsActive)
            .Select(e => (int?)e.UserId).FirstOrDefaultAsync();
    }

    private IActionResult EmployeesOnly() => StatusCode(403, new { message = "Assigned tasks are for employees only." });

    private async Task<object?> One(int id, int uid) =>
        (await _flow.ShapeAsync(_db.WorkTasks.Where(t => t.Id == id), uid, null)).FirstOrDefault();

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var uid = await Me();
        if (uid == null) return EmployeesOnly();
        return Ok(await _flow.ShapeAsync(_db.WorkTasks.Where(t => t.UserId == uid && t.Kind == "assigned"), uid.Value, null));
    }

    [HttpPatch("{id:int}/progress")]
    public async Task<IActionResult> SetProgress(int id, [FromBody] DailyProgressInput i)
    {
        var uid = await Me();
        if (uid == null) return EmployeesOnly();

        var t = await _db.WorkTasks.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid && x.Kind == "assigned");
        if (t == null) return NotFound(new { message = "Task not found." });
        if (t.Approval is "pending" or "approved") return BadRequest(new { message = "This task is waiting for approval or already approved." });
        if (i.Progress is < 0 or > 100) return BadRequest(new { message = "Progress must be between 0 and 100." });
        if (i.Status != null && !Statuses.Contains(i.Status)) return BadRequest(new { message = "Invalid status." });

        if (i.Progress != null) t.Progress = i.Progress.Value;

        if (i.Status == "done") await _flow.OnDoneAsync(t);
        else if (i.Status != null)
        {
            if (t.Status == "done") { t.CompletedAt = null; t.Approval = "none"; }
            t.Status = i.Status;
        }

        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (t.Approval == "pending") await _flow.NotifyApproversAsync(t);
        return Ok(await One(id, uid.Value));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var uid = await Me();
        if (uid == null) return EmployeesOnly();

        var t = await _db.WorkTasks.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid && x.Kind == "assigned");
        if (t == null) return NotFound(new { message = "Task not found." });
        if (t.Approval is "pending" or "approved") return BadRequest(new { message = "This task is already completed or waiting for approval." });

        await _flow.OnDoneAsync(t);
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (t.Approval == "pending") await _flow.NotifyApproversAsync(t);
        return Ok(await One(id, uid.Value));
    }
}