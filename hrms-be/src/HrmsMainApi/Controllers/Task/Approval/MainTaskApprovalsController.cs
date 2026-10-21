using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/task-approvals")]
public class MainTaskApprovalsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly TaskFlow _flow;

    public MainTaskApprovalsController(AppDbContext db, TaskFlow flow)
    {
        _db = db;
        _flow = flow;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope)
    {
        var uid = CurrentUserId;
        var mine = _db.TaskApprovals.Where(a => a.ApproverUserId == uid).Select(a => a.TaskId);
        var q = _db.WorkTasks.Where(t => t.Kind != "self" && mine.Contains(t.Id));
        if (scope != "all") q = q.Where(t => t.Approval == "pending");

        return Ok(await _flow.ShapeAsync(q, uid, null));
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecideInput i)
    {
        var t = await _db.WorkTasks.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id && x.Kind != "self");
        if (t == null) return NotFound(new { message = "Task not found." });

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return BadRequest(new { message = "Note: max 500 characters." });

        var err = await _flow.DecideAsync(t, CurrentUserId, false, i.Approve, note.Length == 0 ? null : note);
        return err == null ? Ok() : BadRequest(new { message = err });
    }
}