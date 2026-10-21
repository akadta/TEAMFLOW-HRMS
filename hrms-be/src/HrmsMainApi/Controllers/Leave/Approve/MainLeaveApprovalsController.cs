using hrms.Data;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/leave-approvals")]
public class MainLeaveApprovalsController : BaseController
{
    private readonly AppDbContext _db;
    private readonly LeaveFlow _flow;

    public MainLeaveApprovalsController(AppDbContext db, LeaveFlow flow)
    {
        _db = db;
        _flow = flow;
    }

    // GET ?scope=pending|all   (requests where I am an approver)
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope) =>
        Ok(await _flow.ListAsync(CurrentUserId, null, scope != "all"));

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] LeaveDecideInput i)
    {
        var r = await _db.LeaveRequests.Include(x => x.Approvals).Include(x => x.Days)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound(new { message = "Request not found." });

        var note = (i.Note ?? string.Empty).Trim();
        if (note.Length > 500) return BadRequest(new { message = "Note: max 500 characters." });

        var err = await _flow.DecideAsync(r, CurrentUserId, false, i.Approve, note.Length == 0 ? null : note);
        return err == null ? Ok() : BadRequest(new { message = err });
    }
}