using hrms.Data;
using hrms.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

[ApiController]
[Authorize]
[Route("api/main/notifications")]
public class MainNotificationsController : BaseController
{
    private readonly AppDbContext _db;

    public MainNotificationsController(AppDbContext db) => _db = db;

    [HttpGet("unseen")]
    public async Task<IActionResult> Unseen()
    {
        var uid = CurrentUserId;
        var list = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == uid && n.SeenAt == null)
            .OrderBy(n => n.Id).Take(50)
            .Select(n => new { id = n.Id, kind = n.Kind, title = n.Title, text = n.Text, link = n.Link, createdAt = n.CreatedAt })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("{id:int}/seen")]
    public async Task<IActionResult> Seen(int id)
    {
        var uid = CurrentUserId;
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (n != null && n.SeenAt == null)
        {
            n.SeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok();
    }
    [HttpPost("seen-all")]
    public async Task<IActionResult> SeenAll()
    {
        var uid = CurrentUserId;
        var rows = await _db.Notifications.Where(n => n.UserId == uid && n.SeenAt == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var n in rows) n.SeenAt = now;
        await _db.SaveChangesAsync();
        return Ok();
    }
}