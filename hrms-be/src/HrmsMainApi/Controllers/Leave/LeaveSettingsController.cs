using hrms.Data;
using hrms.Models;
using hrms.Shared.Controllers;
using hrms.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms.HrmsMainApi.Controllers;

public record LeaveSettingsInput(bool IsActive, int? MaxDays, LeaveConfig? Config);

[ApiController]
[Authorize]
[Route("api/main/leave-settings")]
public class LeaveSettingsController : BaseController
{
    private readonly AppDbContext _db;

    public LeaveSettingsController(AppDbContext db) => _db = db;

    // GET ?companyId=   (omit = global)
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? companyId)
    {
        if (companyId != null && !await _db.Companies.AnyAsync(c => c.Id == companyId))
            return NotFound(new { message = "Company not found." });

        var s = await _db.LeaveSettings.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId);
        return Ok(new { isActive = s?.IsActive ?? false, maxDays = s?.MaxDays, config = LeaveFlow.Parse(s?.Config) });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromQuery] int? companyId, [FromBody] LeaveSettingsInput i)
    {
        if (companyId != null && !await _db.Companies.AnyAsync(c => c.Id == companyId))
            return NotFound(new { message = "Company not found." });
        if (i.MaxDays is < 1 or > 365) return BadRequest(new { message = "Max days must be between 1 and 365." });

        var cfg = i.Config ?? new LeaveConfig();
        cfg.Mode = cfg.Mode == "all" ? "all" : "any";
        if (!cfg.Managers && !cfg.Owner && !cfg.Main)
            return BadRequest(new { message = "Tick at least one approver." });

        var row = await _db.LeaveSettings.FirstOrDefaultAsync(x => x.CompanyId == companyId);
        if (row == null)
        {
            row = new LeaveSetting { CompanyId = companyId };
            _db.LeaveSettings.Add(row);
        }

        row.IsActive = i.IsActive;
        row.MaxDays = i.MaxDays;
        row.Config = LeaveFlow.Serialize(cfg);
        row.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }
}