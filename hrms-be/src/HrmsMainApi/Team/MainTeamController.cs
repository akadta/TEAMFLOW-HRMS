using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/team")]
public class MainTeamController : BaseController
{
    private readonly AppDbContext _db;
    private readonly LeaveFlow _leave;
    private readonly TaskFlow _tasks;

    public MainTeamController(AppDbContext db, LeaveFlow leave, TaskFlow tasks)
    {
        _db = db;
        _leave = leave;
        _tasks = tasks;
    }

    // null = super admin; otherwise the people this main employee is connected to (exclusions applied)
    private async Task<List<int>?> UsersAsync()
    {
        var super = await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == CurrentUserId && u.MainRole != null && u.MainRole.IsActive);
        return super ? null : (await ReportingScope.VisibleEmployeeUserIdsAsync(_db, CurrentUserId)).ToList();
    }

    [HttpGet("leave")]
    public async Task<IActionResult> Leave(
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo,
        [FromQuery] int? companyId)
    {
        var today = Ist.Today;
        var dFrom = dateFrom ?? new DateOnly(today.Year, 1, 1);
        var dTo = dateTo ?? today.AddDays(365);
        if (dFrom > dTo) return BadRequest(new { message = "From date is after To date." });

        var q = _db.LeaveRequests.AsNoTracking().Where(r => r.Days.Any(d => d.Date >= dFrom && d.Date <= dTo));
        var users = await UsersAsync();
        if (users != null) q = q.Where(r => users.Contains(r.UserId));
        if (companyId != null) q = q.Where(r => r.CompanyId == companyId);

        return Ok(await _leave.ReportAsync(q));
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(
        [FromQuery(Name = "from")] DateOnly? dateFrom, [FromQuery(Name = "to")] DateOnly? dateTo,
        [FromQuery] int? companyId)
    {
        var today = Ist.Today;
        var dTo = dateTo ?? today;
        var dFrom = dateFrom ?? today.AddDays(-6);
        if (dFrom > dTo) return BadRequest(new { message = "From date is after To date." });
        if (dTo.DayNumber - dFrom.DayNumber + 1 > 93) return BadRequest(new { message = "Max 93 days at a time." });

        var fromUtc = Ist.ToUtc(dFrom, 0);
        var toUtc = Ist.ToUtc(dTo.AddDays(1), 0);

        var q = _db.WorkTasks.Where(t => t.Kind != "self" &&
                                         (t.StartAt ?? t.CreatedAt) >= fromUtc && (t.StartAt ?? t.CreatedAt) < toUtc);
        var users = await UsersAsync();
        if (users != null) q = q.Where(t => users.Contains(t.UserId));
        if (companyId != null) q = q.Where(t => t.CompanyId == companyId);

        return Ok(await _tasks.ShapeAsync(q, CurrentUserId, null));
    }

    // main employees can approve a task when they are one of its approvers
    [HttpPost("tasks/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecideInput i)
    {
        var t = await _db.WorkTasks.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id && x.Kind != "self");
        if (t == null) return NotFound(new { message = "Task not found." });

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return BadRequest(new { message = "Note: max 500 characters." });

        var err = await _tasks.DecideAsync(t, CurrentUserId, false, i.Approve, note.Length == 0 ? null : note);
        return err == null ? Ok() : BadRequest(new { message = err });
    }
}